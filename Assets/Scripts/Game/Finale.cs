using System.Collections;
using UnityEngine;

/// <summary>
/// The end of the comic (the proposal's Act 3 and its twist ending).
///
///  Begin()     Baron Blot is down to his last 60 HP on the roof: Mom flings the door open and flips the
///              big light on. With the room lit the whole page is awake for 30 seconds, Blot, his ink
///              flood and all, while she watches from the doorway (and, line by line, starts rooting
///              for Max). Then she switches it off ("two more minutes") and it's the torch again.
///  Defeated()  Blot is beaten: THE END. "Lights out, young man": Mom takes the comic and the torch,
///              the light goes off, the door shuts, "Goodnight, Roshan." A long quiet moment... then, out in
///              the hall, the sounds only a reader of this comic knows: the click of the torch, a dim light
///              under the door, the crank wound notch by notch with the light growing at every one, and the
///              comic's own music spinning back up. Then the light fades as the torch runs down, and she
///              winds it straight back up. Nobody says a word: the iris closes on the light under the door.
/// </summary>
public class Finale : MonoBehaviour
{
    public static Finale I { get; private set; }
    public const float Seconds = 30f;

    public bool Running { get; private set; }
    public bool Ended { get; private set; }
    public float Left { get; private set; }
    /// <summary>The ending: someone down the hall has the comic open again (its music plays).</summary>
    public bool ReadingAgain { get; private set; }

    private static readonly string[] Rooting =
    {
        "...wait. Who's the man in the top hat?",
        "Is he the bad guy? He LOOKS like the bad guy.",
        "Hit him, Max!",
        "BEHIND you!",
        "Ooh. That's got to hurt.",
    };

    private void Awake() => I = this;
    private void OnDestroy()
    {
        if (I == this) I = null;
        if (ReadingAgain) AudioDirector.I?.Duck(1f);                   // left mid-ending: never leave the music turned down
    }

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
        for (float t = 0f; t < seconds; t += BookmarkPause.UnpausedDelta)
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
        yield return BookmarkPause.Wait(0.6f);
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
        if (hero != null)
        {
            hero.Locked = true;
            hero.GetComponent<Health>().Invulnerable = 1e4f;              // it's over: nothing on the page can hurt him now
        }
        yield return BookmarkPause.Wait(2f);                              // the Baron melts away
        if (mom != null && !mom.Watching)
        {
            mom.Watch(true);                                                // she heard the cheering
            yield return BookmarkPause.Wait(0.5f);
        }
        if (view != null && view.RoomLight < 1f) yield return Switch(true, 0.2f);
        GameHUD.I?.MomSays("...he WON!");
        yield return BookmarkPause.Wait(1.2f);
        if (GameHUD.I != null) yield return GameHUD.I.Card("THE END", 2.4f);
        GameHUD.I?.MomSays("And YOU, young man: lights. OUT.");
        yield return BookmarkPause.Wait(1.6f);

        // she takes the comic, and the torch with it
        if (view != null) StartCoroutine(view.TakeAway(1.6f));
        torch?.SetOn(false);
        CanvasGroup hand = null;
        if (GameTorch.I != null && !GameTorch.I.TryGetComponent(out hand)) hand = GameTorch.I.gameObject.AddComponent<CanvasGroup>();
        for (float t = 0f; t < 1.4f; t += BookmarkPause.UnpausedDelta)
        {
            if (hand != null) hand.alpha = 1f - t / 1.4f;
            yield return null;
        }
        if (hand != null) hand.alpha = 0f;
        yield return BookmarkPause.Wait(0.5f);

        // click: dark; the door shuts, and the room's music winds down with the light
        if (MomDoorView.I != null) MomDoorView.I.Ending = true;
        AudioDirector.I?.TapeStop(true, 0.6f);
        yield return Switch(false, 0.15f);
        mom?.Watch(false);
        GameHUD.I?.MomSays("Goodnight, Roshan.");
        // a slow push in on her door, and a long quiet moment in the dark
        if (view != null) StartCoroutine(view.PushIn(MomDoorView.DoorCentre, new Vector2(-120f, 30f), 1.75f, 4.5f));
        yield return BookmarkPause.Wait(4.8f);

        // the twist, told only in the comic's own language: out in the hall, the click of the torch and a dim
        // light under the door; the crank, notch by notch, the light growing with every one; the comic's music
        // spinning back up (it only plays while the comic's being read)
        var door = MomDoorView.I;
        if (view != null) StartCoroutine(view.PushIn(MomDoorView.AboveGap, new Vector2(-120f, -60f), 1.95f, 7.5f));   // settled before the iris
        AudioDirector.I?.TorchOn(0.35f);
        yield return TorchUnderDoor(door, 0.3f, 0.12f);
        yield return BookmarkPause.Wait(0.9f);
        for (int notch = 1; notch <= 5; notch++)
        {
            GameAudio.Play("twist", 0.3f, 0.03f);
            yield return TorchUnderDoor(door, 0.3f + 0.14f * notch, 0.08f);
            yield return BookmarkPause.Wait(0.2f);
        }
        yield return BookmarkPause.Wait(0.6f);
        AudioDirector.I?.Duck(0.5f);                                      // through a door, down the hall
        ReadingAgain = true;
        yield return BookmarkPause.Wait(3f);

        // the light under her door fades as the torch runs down... and she winds it straight back up: she isn't stopping
        yield return TorchUnderDoor(door, 0.45f, 1.4f);
        yield return BookmarkPause.Wait(0.5f);
        for (int notch = 1; notch <= 4; notch++)
        {
            GameAudio.Play("twist", 0.3f, 0.03f);
            yield return TorchUnderDoor(door, 0.45f + 0.14f * notch, 0.08f);
            yield return BookmarkPause.Wait(0.18f);
        }
        yield return BookmarkPause.Wait(1.4f);

        // the iris closes on the light under her door, holds there a moment, and shuts; the music swells as it goes
        Settings.Save();
        const float close = 2.6f, hold = 1.6f;
        if (App.I == null || door == null)
        {
            AudioDirector.I?.Duck(1f);
            App.I?.Flow.Load(Scenes.Credits);
            yield break;
        }
        App.I.Flow.IrisOut(Scenes.Credits, door.LightOnScreen(), 0.2f, close, hold);
        for (float t = 0f; t < close + hold; t += BookmarkPause.UnpausedDelta)
        {
            AudioDirector.I?.Duck(Mathf.Lerp(0.5f, 1f, t / (close + hold)));
            yield return null;
        }
        AudioDirector.I?.Duck(1f);
    }

    private static IEnumerator TorchUnderDoor(MomDoorView door, float to, float seconds)
    {
        if (door == null) yield break;
        float from = door.TorchUnderDoor;
        for (float t = 0f; t < seconds; t += BookmarkPause.UnpausedDelta)
        {
            door.TorchUnderDoor = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        door.TorchUnderDoor = to;
    }
}
