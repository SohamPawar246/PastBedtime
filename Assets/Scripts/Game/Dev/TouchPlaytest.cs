#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Editor-only: plays the on-screen controls (<see cref="TouchControls"/>) with simulated fingers on a simulated
/// touch screen, and logs what the game did ("[TouchTest] ..."; "[TouchTest] done" at the end). Run it on a loaded
/// page with <c>GameInput.ForceTouch</c> on: <c>TouchPlaytest.Run()</c>.
/// </summary>
public class TouchPlaytest : MonoBehaviour
{
    private Touchscreen _screen;

    public static void Run()
    {
        var go = new GameObject("TouchPlaytest");
        go.AddComponent<TouchPlaytest>().StartCoroutine(go.GetComponent<TouchPlaytest>().Play());
    }

    private static void Log(string s) => Debug.Log("[TouchTest] " + s);

    private void Touch(int id, Vector2 at, UnityEngine.InputSystem.TouchPhase phase) =>
        InputSystem.QueueStateEvent(_screen, new TouchState { touchId = id, phase = phase, position = at });

    private IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    private IEnumerator Seconds(float s)
    {
        for (float t = 0f; t < s; t += Time.unscaledDeltaTime) yield return null;
    }

    /// <summary>A finger down at `from`, moved to `to` over `seconds`, then lifted.</summary>
    private IEnumerator Drag(int id, Vector2 from, Vector2 to, float seconds, bool lift = true)
    {
        Touch(id, from, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(2);
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            Touch(id, Vector2.Lerp(from, to, t / seconds), UnityEngine.InputSystem.TouchPhase.Moved);
            yield return null;
        }
        Touch(id, to, UnityEngine.InputSystem.TouchPhase.Moved);
        yield return Frames(1);
        if (!lift) yield break;
        Touch(id, to, UnityEngine.InputSystem.TouchPhase.Ended);
        yield return Frames(2);
    }

    private static Vector2 Centre(string name)
    {
        var tc = FindFirstObjectByType<TouchControls>();
        foreach (var rt in tc.GetComponentsInChildren<RectTransform>(true))
            if (rt.name == name) return RectTransformUtility.WorldToScreenPoint(null, rt.position);
        return Vector2.zero;
    }

    private IEnumerator Play()
    {
        _screen = Touchscreen.current ?? InputSystem.AddDevice<Touchscreen>();
        var hero = HeroController.I;
        var combat = hero.GetComponent<HeroCombat>();
        var torch = TorchController.I;
        float mm = Form.MmToPixels(1f);
        yield return Seconds(0.5f);
        while (hero.Locked) yield return null;
        torch.Charge.Charge = torch.Charge.Capacity;
        torch.SetOn(true);
        Log($"screen {UnityEngine.Device.Screen.width}x{UnityEngine.Device.Screen.height}, 1 mm = {mm:F1} px, touch {GameInput.TouchMode}");

        // the stick: a thumb in the left of the screen pushed 8 mm right runs him right
        float x0 = hero.transform.position.x;
        var stick = new Vector2(UnityEngine.Device.Screen.width * 0.15f, UnityEngine.Device.Screen.height * 0.3f);
        Touch(1, stick, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(2);
        Touch(1, stick + new Vector2(8f * mm, 0f), UnityEngine.InputSystem.TouchPhase.Moved);
        yield return Seconds(0.6f);
        Log($"stick right 0.6 s: moved {hero.transform.position.x - x0:+0.00;-0.00} (expect > 2), MoveX {GameInput.MoveX}");
        Touch(1, stick + new Vector2(8f * mm, 0f), UnityEngine.InputSystem.TouchPhase.Ended);
        yield return Seconds(0.3f);

        // JUMP: a tap lifts him off the ground
        float y0 = hero.transform.position.y;
        float peak = y0;
        var jump = Centre("Jump");
        Touch(2, jump, UnityEngine.InputSystem.TouchPhase.Began);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { peak = Mathf.Max(peak, hero.transform.position.y); yield return null; }
        Touch(2, jump, UnityEngine.InputSystem.TouchPhase.Ended);
        Log($"JUMP held 0.5 s: rose {peak - y0:F2} (expect about 3)");
        yield return Seconds(1.2f);

        // KICK: a plain tap is a kick; a swipe up on it is the launcher
        var kick = Centre("Kick");
        bool kickShown = FindFirstObjectByType<TouchControls>().transform.Find("SafeArea/Kick").gameObject.activeInHierarchy;
        if (kickShown)
        {
            Touch(3, kick, UnityEngine.InputSystem.TouchPhase.Began);
            yield return Seconds(0.15f);
            string plain = combat.MoveName;
            Touch(3, kick, UnityEngine.InputSystem.TouchPhase.Ended);
            yield return Seconds(0.9f);
            yield return Drag(3, kick, kick + new Vector2(0f, 6f * mm), 0.05f, lift: false);
            yield return Frames(3);
            string swiped = combat.MoveName;
            Touch(3, kick + new Vector2(0f, 6f * mm), UnityEngine.InputSystem.TouchPhase.Ended);
            Log($"KICK tap: {plain} (expect Kick); swipe up: {swiped} (expect Launcher)");
            yield return Seconds(1f);
        }
        else Log("KICK isn't shown on this page (it's taught on page 2)");

        // POW held keeps punching: the chain climbs to the haymaker
        var pow = Centre("Punch");
        Touch(4, pow, UnityEngine.InputSystem.TouchPhase.Began);
        string seen = "";
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            string m = combat.MoveName;
            if (m != "None" && !seen.EndsWith(m)) seen += (seen == "" ? "" : ">") + m;
            yield return null;
        }
        Touch(4, pow, UnityEngine.InputSystem.TouchPhase.Ended);
        Log($"POW held 1.4 s: {seen} (expect Jab>Cross>Haymaker)");
        yield return Seconds(1f);

        // rolling a thumb from JUMP onto POW punches (in the air): no lifting the thumb between them
        Touch(5, jump, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(3);
        bool rolledAirborne = !hero.Grounded;
        string rolled = "None";
        for (int i = 1; i <= 6; i++)
        {
            Touch(5, Vector2.Lerp(jump, pow, i / 6f), UnityEngine.InputSystem.TouchPhase.Moved);
            yield return null;
        }
        for (float t = 0f; t < 0.3f; t += Time.deltaTime) { if (combat.MoveName != "None") rolled = combat.MoveName; yield return null; }
        Touch(5, pow, UnityEngine.InputSystem.TouchPhase.Ended);
        Log($"rolled from JUMP onto POW: jumped {rolledAirborne || !hero.Grounded}, then {rolled} (expect True, Jab)");
        yield return Seconds(1.2f);

        // the beam: dragged ahead of him it stays ahead while he runs, then glides back; a double tap brings it back
        Vector2 aimAt = PageView.I.LaneToScreen((Vector2)hero.transform.position + new Vector2(5f, 1f));
        yield return Drag(6, aimAt + new Vector2(0f, -20f), aimAt, 0.2f);
        yield return Seconds(0.3f);
        float ahead = torch.Centre.x - hero.transform.position.x;
        yield return Seconds(1.2f);
        float stillAhead = torch.Centre.x - hero.transform.position.x;
        yield return Seconds(2.5f);
        float back = torch.Centre.x - hero.transform.position.x;
        Log($"beam dragged 5 ahead: {ahead:F1}, 1.2 s later {stillAhead:F1} (expect still ~5), after the park {back:F1} (expect ~0)");
        yield return Drag(7, aimAt, aimAt, 0.05f);
        Touch(8, aimAt, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(2);
        Touch(8, aimAt, UnityEngine.InputSystem.TouchPhase.Ended);
        yield return Frames(2);
        Touch(9, aimAt, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(2);
        Touch(9, aimAt, UnityEngine.InputSystem.TouchPhase.Ended);
        yield return Seconds(0.6f);
        Log($"double tap: beam {torch.Centre.x - hero.transform.position.x:F1} from Max (expect ~0, well before 3 s)");

        // the torch: a tap on it (on the hand, away from the lens) switches it off and on
        var hand = GameTorch.I.CrankOnScreen + new Vector2(9f * mm, 2f * mm);
        Log($"tapping the hand at {hand}: on the hand {GameTorch.I.OnHand(hand)}, on the lens {GameTorch.I.OnLens(hand, 55f)}");
        bool before = torch.On;
        Touch(10, hand, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(3);
        Touch(10, hand, UnityEngine.InputSystem.TouchPhase.Ended);
        yield return Frames(3);
        bool after = torch.On;
        Touch(11, hand, UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(3);
        Touch(11, hand, UnityEngine.InputSystem.TouchPhase.Ended);
        yield return Frames(3);
        Log($"torch tapped: {before} -> {after} -> {torch.On} (expect True -> False -> True)");

        Log("done");
        Destroy(gameObject);
    }
}
#endif
