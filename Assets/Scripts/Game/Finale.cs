using System.Collections;
using UnityEngine;

/// <summary>
/// The end of the comic (the proposal's Act 3 and its twist ending).
///
///  Begin()     Baron Blot is down to his last 30 HP on the roof: Mom flings the door open and flips the
///              big light on. With the room lit the whole page is awake for 30 seconds, Blot, his ink
///              flood and all, while she watches from the doorway (and, line by line, starts rooting
///              for Max). Then she switches it off ("two more minutes") and it's the torch again.
///  Defeated()  Blot is beaten: THE END. "Lights out, young man": Mom takes the comic and the torch,
///              the light goes off, the door shuts... and a moment later the strip of light under her
///              door starts to flicker with torchlight. She's reading it herself. "...just one more page."
/// </summary>
public class Finale : MonoBehaviour
{
    public static Finale I { get; private set; }
    public const float Seconds = 30f;

    public bool Running { get; private set; }
    public bool Ended { get; private set; }
    public float Left { get; private set; }

    private static readonly string[] Rooting =
    {
        "...wait. Who's the man in the top hat?",
        "Is he the bad guy? He LOOKS like the bad guy.",
        "Hit him, Max!",
        "BEHIND you!",
        "Ooh. That's got to hurt.",
    };

    private void Awake() => I = this;
    private void OnDestroy() { if (I == this) I = null; }

    public void Begin()
    {
        if (Running || Ended) return;
        StartCoroutine(RoomLight());
    }

    public void Defeated()
    {
        if (Ended) return;
        Ended = true;
        StartCoroutine(Ending());
    }

    private static IEnumerator Switch(bool on, float seconds)
    {
        var view = PageView.I;
        var field = LightField.I;
        AudioDirector.I?.Toggle();                       // the click of the switch
        if (field != null) field.RoomLight = on;
        if (view == null) yield break;
        float from = view.RoomLight, to = on ? 1f : 0f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            view.RoomLight = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        view.RoomLight = to;
    }

    private IEnumerator RoomLight()
    {
        Running = true;
        var mom = MomDirector.I;
        mom?.Watch(true);
        GameHUD.I?.MomSays("ROSHAN! It's THREE in the MORNING!");
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Switch(true, 0.2f);
        GameHUD.I?.Warn("MOM TURNED THE ROOM LIGHT ON! EVERYTHING'S AWAKE!");
        Left = Seconds;
        float nextLine = 5f;
        int line = 0;
        while (Left > 0f && !Ended)
        {
            float dt = Time.deltaTime;
            Left -= dt;
            GameHUD.I?.Countdown(Left);
            if ((nextLine -= dt) <= 0f && line < Rooting.Length)
            {
                GameHUD.I?.MomSays(Rooting[line++]);
                nextLine = 5.5f;
            }
            yield return null;
        }
        GameHUD.I?.Countdown(0f);
        if (Ended) yield break;                          // he fell in the light: the ending takes it from here
        GameHUD.I?.MomSays("Okay. TWO more minutes. Then SLEEP.");
        yield return Switch(false, 0.25f);
        mom?.Watch(false);
        Running = false;
    }

    private IEnumerator Ending()
    {
        var hero = HeroController.I;
        var mom = MomDirector.I;
        var view = PageView.I;
        var torch = TorchController.I;
        GameHUD.I?.Countdown(0f);
        if (hero != null) hero.Locked = true;
        yield return new WaitForSecondsRealtime(2f);                       // the Baron melts away
        if (mom != null && !mom.Watching)
        {
            mom.Watch(true);                                                // she heard the cheering
            yield return new WaitForSecondsRealtime(0.5f);
        }
        if (view != null && view.RoomLight < 1f) yield return Switch(true, 0.2f);
        GameHUD.I?.MomSays("...he WON!");
        yield return new WaitForSecondsRealtime(1.2f);
        if (GameHUD.I != null) yield return GameHUD.I.Card("THE END", 2.4f);
        GameHUD.I?.MomSays("And YOU, young man: lights. OUT.");
        yield return new WaitForSecondsRealtime(1.6f);

        // she takes the comic, and the torch with it
        if (view != null) StartCoroutine(view.TakeAway(1.6f));
        torch?.SetOn(false);
        CanvasGroup hand = null;
        if (GameTorch.I != null && !GameTorch.I.TryGetComponent(out hand)) hand = GameTorch.I.gameObject.AddComponent<CanvasGroup>();
        for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
        {
            if (hand != null) hand.alpha = 1f - t / 1.4f;
            yield return null;
        }
        if (hand != null) hand.alpha = 0f;
        yield return new WaitForSecondsRealtime(0.5f);

        // click: dark; the door shuts
        if (MomDoorView.I != null) MomDoorView.I.Ending = true;
        yield return Switch(false, 0.15f);
        mom?.Watch(false);
        GameHUD.I?.MomSays("Goodnight, Roshan.");
        // a slow push in on her door
        if (view != null) StartCoroutine(view.PushIn(MomDoorView.DoorCentre, new Vector2(-120f, 30f), 1.75f, 4.5f));
        yield return new WaitForSecondsRealtime(3.2f);

        // the twist: torchlight, under her door
        var door = MomDoorView.I;
        door?.Caption("MEANWHILE, DOWN THE HALL...");
        for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
        {
            if (door != null) door.TorchUnderDoor = t / 1.5f;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.2f);
        GameHUD.I?.MomSays("...KAPOW. Ha!");
        yield return new WaitForSecondsRealtime(2.4f);
        GameHUD.I?.MomSays("...just one more page.");
        yield return new WaitForSecondsRealtime(3.4f);
        Settings.Save();
        App.I?.Flow.Load(Scenes.Credits);
    }
}
