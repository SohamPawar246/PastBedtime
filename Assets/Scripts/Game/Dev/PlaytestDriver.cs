#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>
/// Editor-only: plays scripted runs through <see cref="GameInput.Virtual"/> and logs what
/// happened, so the rules can be checked without a human at the keyboard.
/// Start one from code: PlaytestDriver.Run("basics").
/// </summary>
public class PlaytestDriver : MonoBehaviour
{
    public static string Last = "";
    /// <summary>Set while a scenario holds the game still for a screenshot; clear it to go on.</summary>
    public static string Held = "";
    private GameInput.Pad _pad;
    private bool _followHero = true;
    private int[] _save;                     // the developer's progress, put back after a run
    private readonly int[] _saveStars = new int[13];
    private bool _atNightstand;

    public static PlaytestDriver Run(string scenario)
    {
        // one driver at a time: an old one would keep steering the torch toward Max
        foreach (var old in FindObjectsByType<PlaytestDriver>(FindObjectsSortMode.None)) Destroy(old.gameObject);
        if (TorchController.I != null) TorchController.I.AimOverride = null;
        var d = new GameObject("Playtest").AddComponent<PlaytestDriver>();
        d._save = new[] { Settings.HighestPage, Settings.Stars, Settings.Spring, Settings.Gear, Settings.Ratchet, Settings.MomTaught ? 1 : 0, Settings.GhostTaught ? 1 : 0 };
        for (int p = 1; p < d._saveStars.Length; p++) d._saveStars[p] = Settings.StarsFound(p);
        Settings.MomTaught = true;               // Mom's visits are ordinary ones in tests (the tutorial has its own)
        Settings.GhostTaught = true;             // and so is page 7 (the Ghost lens lesson has its own test)
        d.StartCoroutine(d.Play(scenario));
        return d;
    }

    private void Log(string s)
    {
        Last += s + "\n";
        Debug.Log("[Playtest] " + s);
    }

    private void Update()
    {
        // between acts the Nightstand waits for KEEP READING: scripted runs read straight on (its own test aside)
        if (Nightstand.Open && !_atNightstand) Nightstand.I?.KeepReading();
        var hero = HeroController.I;
        if (_followHero && hero != null && TorchController.I != null)
            TorchController.I.AimOverride = (Vector2)hero.transform.position + Vector2.up;
    }

    private IEnumerator Wait(float s)
    {
        for (float t = 0f; t < s; t += Time.deltaTime) yield return null;
    }

    private IEnumerator Hold(string tag)
    {
        Held = tag;
        Log($"HOLD {tag}");
        Time.timeScale = 0f;
        while (Held != "") yield return null;
        Time.timeScale = 1f;
    }

    private IEnumerator Play(string scenario)
    {
        Last = "";
        _pad = new GameInput.Pad();
        GameInput.Virtual = _pad;
        yield return Wait(0.6f);
        while (HeroController.I != null && HeroController.I.Locked) yield return null;   // the page's title card
        // "a,b,c": a suite, one after another (each restores the torch and heals Max first)
        foreach (var one in scenario.Split(','))
        {
            Log($"== {one}");
            _pad = new GameInput.Pad();
            GameInput.Virtual = _pad;
            _followHero = true;
            _atNightstand = false;                       // only the Nightstand's own test stops at it
            if (TorchController.I != null)
            {
                TorchController.I.AimOverride = null;
                TorchController.I.SetOn(true);
                TorchController.I.Lenses.Pick((int)Lens.Clear);
                TorchController.I.Charge.Charge = TorchController.I.Charge.Capacity;
            }
            var h = HeroController.I != null ? HeroController.I.GetComponent<Health>() : null;
            if (h != null) { h.hp = h.maxHp; h.Invulnerable = 0f; }
            if (GameState.I != null) GameState.I.Slippers = 3;
            // a flare at the end of the last one (basics ends on one) leaves the bulb cooling: start in the light
            for (float t = 0f; t < 4f && TorchController.I != null && TorchController.I.Charge.Dead; t += Time.unscaledDeltaTime) yield return null;
            yield return Scenario(one.Trim());
        }
        GameInput.Virtual = null;
        if (TorchController.I != null) TorchController.I.AimOverride = null;
        Log("done");
        Destroy(gameObject);
    }

    private IEnumerator Scenario(string scenario)
    {
        switch (scenario)
        {
            case "basics": yield return Basics(); break;
            case "smudge": yield return SmudgeFight(); break;
            case "dotshot": yield return DotShotTest(); break;
            case "bruiser": yield return BruiserTest(); break;
            case "splotch": yield return SplotchTest(); break;
            case "eraser": yield return EraserTest(); break;
            case "mom": yield return MomTest(); break;
            case "blot": yield return BlotTest(); break;
            case "open": yield return OpenFromTitleTest(); break;
            case "crank": yield return CrankTest(); break;
            case "anim": yield return AnimTest(); break;
            case "act3": yield return Act3Test(); break;
            case "finale": yield return FinaleTest(); break;
            case "nightstand": yield return NightstandTest(); break;
            case "bodies": yield return BodiesTest(); break;
            case "look": yield return LookTest(); break;
            case "bruiserko": yield return BruiserKnockout(); break;
            case "machines": yield return MachinesTest(); break;
            case "speed": yield return SpeedTest(); break;
            case "fixes": yield return FixesTest(); break;
            case "momlight": yield return MomLightTest(); break;
            case "blotmoves": yield return BlotMovesTest(); break;
            case "controls": yield return ControlsTest(); break;
            case "death": yield return DeathTest(); break;
            case "combo": yield return ComboTrace(); break;
            case "lettering": yield return LetteringTest(); break;
            case "bugs": yield return BugsTest(); break;
            case "endingpause": yield return EndingPauseTest(); break;
            case "momtutorial": yield return MomTutorialTest(); break;
            case "ghostlesson": yield return GhostLessonTest(); break;
            case "starcaptions": yield return StarsCaptionTest(); break;
            case "polish": yield return PolishTest(); break;
            case "blotdark": yield return BlotDarkTest(); break;
            case "falls": yield return FallsTest(); break;
        }
    }

    // the playtest notes of 5 Oct: the Haymaker that knocks a Smudge out printed two words over each other
    // (KAPOW! and the death's SPLOOSH!). J, J, J on a fresh Smudge (6 + 6 + 14 for its 20 HP), every frame:
    // which words come up when, and how far any two that are up together overlap
    private IEnumerator LetteringTest()
    {
        var hero = HeroController.I;
        PageManager.I.Load(1);
        hero.GetComponent<Health>().Invulnerable = 300f;
        yield return Wait(1.6f);
        while (hero.Locked) yield return null;
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        foreach (var e in pl.enemies.ToArray()) if (e != null) { PageManager.I.EnemyDown(e); Destroy(e.gameObject); }
        float floor = pl.rect.yMin + 2.5f;
        hero.Teleport(new Vector2(pl.rect.xMin + 2f, floor + 0.1f));
        hero.Face(1f);
        var smudge = PageBuilder.Spawn(EnemyKind.Smudge, new Vector2(pl.rect.xMin + 3.5f, floor + 0.1f), pl, PageManager.I.transform);
        _followHero = false;
        TorchController.I.AimOverride = new Vector2(pl.rect.xMin + 3f, floor + 1.2f);           // both of them lit
        yield return Wait(0.8f);

        var seen = new System.Collections.Generic.List<string>();
        float worst = 0f;
        string worstPair = "none", order = "";
        bool shot = false;
        for (float t = 0f; t < 3.4f; t += Time.unscaledDeltaTime)
        {
            if (t < 0.7f && Mathf.Repeat(t, 0.15f) < Time.unscaledDeltaTime) _pad.punch = true;   // mash: the chain queues
            yield return null;
            var up = LetteringUp();
            foreach (var w in up)
                if (!seen.Contains(w.word)) { seen.Add(w.word); order += $"{w.word}@{t:0.00} "; }
            for (int i = 0; i < up.Count; i++)
                for (int j = i + 1; j < up.Count; j++)
                {
                    float o = Mathf.Max(Overlap(up[i].text, up[j].text), Mathf.Max(Overlap(up[i].text, up[j].body), Overlap(up[j].text, up[i].body)));
                    if (o > worst) { worst = o; worstPair = up[i].word + " / " + up[j].word; }
                }
            if (!shot && seen.Contains("KAPOW!")) { shot = true; DevCapture.Shot("lettering_kapow"); }
        }
        Log($"knockout lettering: {order}; smudge down {smudge == null || smudge.Health.Dead} (expect POW! BIFF! KAPOW!, then SPLOOSH! at the melt; True)");
        Log($"worst overlap of two words up together: {worst:0.00} ({worstPair}) (expect 0.00)");
        _followHero = true;
    }

    // the bug list of 5 Oct, one check each: CLANK! on the Bruiser's front, captions for sound, the ground-only dodge,
    // the music following the light, the wheel read by turns, Max's line on page 2, stars counted once, START OVER
    private IEnumerator BugsTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        var gs = GameState.I;
        var c = torch.Charge;
        hero.GetComponent<Health>().Invulnerable = 600f;
        PageManager.I.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());       // no visits unless a check asks for one
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        foreach (var e in pl.enemies.ToArray()) if (e != null) { PageManager.I.EnemyDown(e); Destroy(e.gameObject); }
        float floor = pl.rect.yMin + 2.5f;
        _followHero = false;

        // 1. a jab on the Bruiser's armoured front: CLANK!, a quarter of the damage
        hero.Teleport(new Vector2(pl.rect.xMin + 2f, floor + 0.1f));
        hero.Face(1f);
        var bruiser = PageBuilder.Spawn(EnemyKind.Bruiser, new Vector2(pl.rect.xMin + 3.6f, floor + 0.1f), pl, PageManager.I.transform);
        bruiser.Facing = -1f;
        torch.AimOverride = new Vector2(pl.rect.xMin + 2.8f, floor + 1.2f);
        yield return Wait(0.6f);
        float bruiserHp = bruiser.Health.hp;
        _pad.punch = true;
        bool clank = false;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            yield return null;
            foreach (var w in LetteringUp()) if (w.word == "CLANK!") clank = true;
        }
        Log($"jab on the Bruiser's front: CLANK! {clank}, damage {bruiserHp - bruiser.Health.hp:0.0} (expect True, 1.5: a quarter of 6)");
        PageManager.I.EnemyDown(bruiser);
        Destroy(bruiser.gameObject);

        // 2. captions for sound: the door's captions follow the setting
        bool keepCaptions = Settings.SoundCaptions;
        var caption = GameObject.Find("DoorCaption")?.GetComponent<TMPro.TextMeshProUGUI>();
        torch.SetOn(false);                                                        // (nothing for her to see)
        Settings.SoundCaptions = false;
        MomDirector.I.VisitNow();
        yield return Wait(2.2f);                                                   // the hall light, then footsteps
        string off = caption != null ? caption.text : "?";
        Settings.SoundCaptions = true;
        yield return null;
        string on = caption != null ? caption.text : "?";
        Log($"door caption with captions for sound off / on: \"{off}\" / \"{on}\" (expect \"\" / \"FOOTSTEPS!\")");
        Settings.SoundCaptions = keepCaptions;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        torch.SetOn(true);

        // 3. the dodge is on the ground only
        hero.Teleport(new Vector2(pl.rect.xMin + 2f, floor + 0.1f));
        torch.AimOverride = new Vector2(pl.rect.xMin + 2.5f, floor + 2f);
        yield return Wait(1f);
        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(0.2f);
        bool inAir = !hero.Grounded;
        _pad.dodge = true;
        yield return null;
        yield return null;
        bool airDodge = hero.Dodging;
        _pad.jumpHeld = false;
        yield return Wait(1.2f);
        _pad.dodge = true;
        yield return null;
        yield return null;
        Log($"dodge pressed in mid-air (in the air {inAir}): dodged {airDodge}; on the ground: dodged {hero.Dodging} (expect True: False; True)");
        yield return Wait(0.6f);

        // 4. the music follows the light: run dry, back with a twist, through a flare, silent while the bulb cools
        c.Charge = c.Capacity;
        yield return Wait(0.6f);
        bool lit = !AudioDirector.I.MusicStopped;
        c.Charge = 0f;
        yield return Wait(0.6f);
        bool dry = AudioDirector.I.MusicStopped;
        _pad.twists = 3;
        yield return Wait(0.6f);
        bool back = !AudioDirector.I.MusicStopped;
        c.Charge = c.Capacity;
        _pad.twists = c.OverwindBuffer + 1;                                        // straight to a flare
        yield return Wait(0.6f);
        bool flaring = c.Dead && !AudioDirector.I.MusicStopped;
        yield return Wait(1.5f);
        bool cooling = c.Dead && AudioDirector.I.MusicStopped;
        yield return Wait(2f);
        bool after = !c.Dead && !AudioDirector.I.MusicStopped;
        Log($"music: lit {lit}, run dry stopped {dry}, twisted back {back}, flaring {flaring}, cooling stopped {cooling}, after {after} (expect True x6)");

        // 5. the wheel is read by turns: at full, a trackpad's 1 s of little scrolls overwinds one notch at most;
        //    separate notches still wind it to a flare
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && Bindings.UsesWheel(Bindings.Act.Crank))
        {
            c.Charge = c.Capacity;
            bool flared = false;
            System.Action onFlare = () => flared = true;
            c.Flared += onFlare;
            GameInput.Virtual = null;
            yield return WaitReal(0.4f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
            {
                Scroll(mouse, 8f);
                yield return null;
            }
            int swipe = c.Overwind;
            bool swipeFlared = flared;
            int notches = 0;
            string climb = "";
            for (int i = 0; i < c.OverwindBuffer + 4 && !flared; i++)                // (the first may only top it up)
            {
                yield return WaitReal(0.3f);
                Scroll(mouse, 120f);
                yield return null;
                yield return null;
                notches++;
                climb += flared ? "F" : c.Overwind.ToString();
            }
            GameInput.Virtual = _pad;
            c.Flared -= onFlare;
            Log($"the wheel at full: a 1 s trackpad swipe overwound {swipe} notch(es), flared {swipeFlared}; then separate notches, overwind {climb}: flared {flared} after {notches} (expect <= 1, False; climbing a notch each, True)");
            yield return Wait(4f);                                                 // the bulb cools
        }

        // 6. Max's line on page 2 names the player's own keys
        var page2 = PageManager.LoadDef(2);
        Log($"page 2's launcher line: \"{Bindings.Format(page2.tiers[1].panels[1].heroLine)}\" (expect the up and kick keys: W and Q by default)");

        // 7. stars count once: one found and banked isn't drawn again; GROUNDED puts the stars in hand back
        Settings.SetStarsFound(1, 0);
        Settings.SetStarsFound(2, 0);
        PageManager.I.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        var stars = PageManager.I.Layout.root.GetComponentsInChildren<StarPickup>(true);
        int drawn = stars.Length, total0 = gs.StarsTotal;
        yield return PickUp(stars[0]);
        bool picked = stars[0] == null;
        PageManager.I.Finish(null);                                                // page done: banked; on to page 2
        yield return Until(() => gs.Page == 2, 12f);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        int banked = gs.StarsTotal - total0;
        yield return PickUp(PageManager.I.Layout.root.GetComponentInChildren<StarPickup>());
        int inHand = gs.StarsTotal - total0;
        gs.Slippers = 1;
        PageManager.I.Busted();                                                    // the last slipper: GROUNDED, back to page 1
        yield return Until(() => gs.Page == 1, 12f);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        int redrawn = PageManager.I.Layout.root.GetComponentsInChildren<StarPickup>(true).Length;
        Log($"stars: page 1 drew {drawn}, picked one {picked}, banked {banked} (expect 1); one more in hand on page 2 ({inHand}, expect 2); " +
            $"after GROUNDED: {gs.StarsTotal - total0} counted (expect 1), page 1 draws {redrawn} (expect {drawn - 1}), shows {gs.StarsThisPage}/5 (expect 1)");

        // 8. START OVER: back to nothing (the driver puts the real save back afterwards)
        Settings.SetStarsFound(3, 5);
        Settings.ResetProgress();
        Log($"START OVER: page {Settings.HighestPage}, stars {Settings.Stars}, upgrades {Settings.Spring}{Settings.Gear}{Settings.Ratchet}, page 3's found stars {Settings.StarsFound(3)} (expect 0, 0, 000, 0)");
        _followHero = true;
    }

    // Mom's first visit is a tutorial, once per save: the page says what to do, she waits in the hall while the torch is
    // on, it walks the player through her visit, asks for the torch back on, and the next visit is an ordinary one
    private IEnumerator MomTutorialTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        var mom = MomDirector.I;
        hero.GetComponent<Health>().Invulnerable = 300f;
        PageManager.I.Load(3);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        mom.Schedule(ScriptableObject.CreateInstance<PageDef>());                 // only the visits this test asks for
        Settings.MomTaught = false;
        torch.SetOn(true);
        var lesson = GameObject.Find("Teach");
        TMPro.TMP_Text Text() => (lesson = lesson != null ? lesson : GameObject.Find("Teach")) != null ? lesson.GetComponentInChildren<TMPro.TMP_Text>() : null;
        mom.VisitNow();
        yield return Wait(0.5f);
        string coming = Text()?.text ?? "(none)";
        Log($"first visit: teaching {mom.Teaching}, torch says '{mom.TorchHint}', page says '{coming.Replace("\n", " / ")}'");
        DevCapture.Shot("momtut_coming");
        yield return Wait(8f);                                                     // well past the usual 1.5 + 6 s to the door
        Log($"torch left on for 8.5 s: still {mom.State} at {mom.Feet:0.00} of the way, suspicion {mom.Suspicion:0} (expect Approaching, 0.70, 0)");
        torch.SetOn(false);
        yield return Wait(0.5f);
        Log($"torch off: page says '{Text()?.text.Replace("\n", " / ")}', torch says '{mom.TorchHint}' (expect GOOD..., none)");
        yield return Until(() => mom.State == MomState.Leaving, 12f);
        yield return Wait(0.3f);
        Log($"she leaves: suspicion {mom.Suspicion:0}, slippers {GameState.I.Slippers}, page says '{Text()?.text.Replace("\n", " / ")}', torch says '{mom.TorchHint}' (expect 0, 3, PHEW... TORCH BACK ON, ON!)");
        DevCapture.Shot("momtut_gone");
        torch.SetOn(true);
        yield return Wait(0.5f);
        var group = lesson != null ? lesson.GetComponent<CanvasGroup>() : null;
        Log($"torch back on: teaching {mom.Teaching}, taught {Settings.MomTaught}, lesson box fading {(group != null ? group.alpha.ToString("0.00") : "?")} (expect False, True, < 1)");
        yield return Until(() => mom.State == MomState.Asleep, 5f);
        mom.VisitNow();
        yield return Wait(0.5f);
        Log($"her next visit: teaching {mom.Teaching}, torch says '{mom.TorchHint}' (expect False, none)");
        mom.Schedule(ScriptableObject.CreateInstance<PageDef>());
        torch.SetOn(true);
    }

    // the Ghost lens is taught once, on page 7: the page asks for purple light (the torch says PURPLE!), then says what
    // it does; back to Clear (or a few seconds on), the lesson's learned and it never comes back
    private IEnumerator GhostLessonTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        hero.GetComponent<Health>().Invulnerable = 300f;
        Settings.GhostTaught = false;
        PageManager.I.Load(7);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());       // no visits: her lesson would come first
        torch.Lenses.Pick((int)Lens.Clear);
        torch.SetOn(true);
        yield return Wait(0.6f);
        var lesson = GameObject.Find("Teach");
        string Says() => lesson != null ? lesson.GetComponentInChildren<TMPro.TMP_Text>().text.Replace("\n", " / ") : "(none)";
        Log($"page 7: ghost unlocked {torch.Lenses.Unlocked[(int)Lens.Ghost]}, page says '{Says()}', torch says '{torch.LessonHint}' (expect True, THE GHOST LENS!..., PURPLE!)");
        DevCapture.Shot("ghost_intro");
        _pad.lensPick = (int)Lens.Ghost;
        yield return Wait(0.6f);
        Log($"ghost lens on: lens {torch.Lenses.Current}, page says '{Says()}', torch says '{torch.LessonHint}' (expect Ghost, PURPLE LIGHT..., none)");
        DevCapture.Shot("ghost_on");
        _pad.lensPick = (int)Lens.Clear;
        yield return Wait(0.6f);
        var group = lesson != null ? lesson.GetComponent<CanvasGroup>() : null;
        Log($"back to clear: taught {Settings.GhostTaught}, lesson box fading {(group != null ? group.alpha.ToString("0.00") : "?")} (expect True, < 1)");
        PageManager.I.Load(8);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        yield return Wait(0.6f);
        Log($"page 8: torch says '{torch.LessonHint}', box showing {(group != null && group.alpha > 0.5f)} (expect none, False)");
    }

    // every star clear of its panel's caption box where it's printed (5 Oct: page 6's star sat behind "A DOOR WITH A
    // GOLD PLATE"), in front of it when the box slides over one as the camera pans, and none inside the scenery
    private IEnumerator StarsCaptionTest()
    {
        var hero = HeroController.I;
        hero.GetComponent<Health>().Invulnerable = 300f;
        for (int n = 1; n <= 9; n++) Settings.SetStarsFound(n, 0);              // every star drawn (OnDestroy puts them back)
        int stars = 0, under = 0, behind = 0, buried = 0;
        string notes = "";
        for (int n = 1; n <= 9; n++)
        {
            PageManager.I.Load(n);
            yield return Wait(0.3f);
            foreach (var tier in PageManager.I.Layout.tiers)
            foreach (var pl in tier.panels)
            {
                var words = pl.root.GetComponentInChildren<CaptionWords>(true);
                if (words == null) continue;
                var box = new Rect(pl.rect.xMin + 0.35f, pl.rect.yMax - 0.35f - words.Height, words.Width, words.Height);
                foreach (var star in pl.root.GetComponentsInChildren<StarPickup>(true))
                {
                    stars++;
                    Vector3 p = star.transform.position;
                    if (box.Overlaps(new Rect(p.x - 0.45f, p.y - 0.45f, 0.9f, 0.9f)))
                    {
                        under++;
                        notes += $" p{n} star {star.Index} at ({p.x - pl.rect.xMin:0.0}, {p.y - pl.rect.yMin:0.0});";
                    }
                    if (p.z >= words.transform.position.z) behind++;
                    foreach (var c in Physics.OverlapSphere(new Vector3(p.x, p.y, 0f), 0.2f, ~0, QueryTriggerInteraction.Ignore))
                        if (c.GetComponentInParent<HeroController>() == null && c.GetComponentInParent<EnemyBrain>() == null)
                        {
                            buried++;
                            notes += $" p{n} star {star.Index} inside {c.name};";
                            break;
                        }
                }
            }
        }
        Log($"stars in captioned panels {stars}: under a caption {under}, behind one {behind}, inside the scenery {buried} (expect > 0, 0, 0, 0){notes}");
        PageManager.I.Load(6);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var door = PageManager.I.Layout.tiers[0].panels[1];
        hero.Teleport(door.rect.min + new Vector2(2f, 2.6f));
        _followHero = false;
        TorchController.I.AimOverride = door.rect.min + new Vector2(6.9f, 7.8f);
        yield return Wait(1.2f);
        DevCapture.Shot("stars_page6_door");
        _followHero = true;
    }

    // the polish of 5 Oct: the hang-and-drop jump (same reach), squash and dust, impact frames, a knockout flung
    // off the panel, leaning lettering, combo pitch, the hurt flash and blink with the hearts popping, the marks of
    // waking and freezing, the crank's tick and pulse, Mom's vignette, the star fly-in, and the Splash Page
    private IEnumerator PolishTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        var pm = PageManager.I;
        var gs = GameState.I;
        var hh = hero.GetComponent<Health>();
        bool flashing = Settings.ReduceFlashing, shake = Settings.ScreenShake;
        Settings.ReduceFlashing = false;
        Settings.ScreenShake = true;
        var pitched = typeof(AudioDirector).GetField("_pitched", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        string Pitches()
        {
            var set = pitched?.GetValue(AudioDirector.I) as AudioSource[];
            if (set == null) return "none";
            string p = "";
            foreach (var a in set) p += $"{a.pitch:0.00} ";
            return p.Trim();
        }

        // 1. a running jump: the same reach and time in the air as the plain arc, the same apex, squashed on landing
        // (page 1, tier 2's first panel: one long roof, nothing overhead)
        Settings.SetStarsFound(1, 0);                                    // its star drawn (OnDestroy puts the save back)
        pm.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        pm.GoToTier(1);
        var pl = pm.Layout.tiers[1].panels[0];
        foreach (var e in pl.enemies.ToArray()) if (e != null) { pm.EnemyDown(e); Destroy(e.gameObject); }
        float floor = pl.rect.yMin + 2.5f;
        hero.Teleport(new Vector2(pl.rect.xMin + 1f, floor + 0.1f));
        hh.Invulnerable = 0f;
        yield return Wait(0.4f);
        var model = hero.transform.GetChild(0);
        float baseY = model.localScale.y;
        _pad.moveX = 1f;
        yield return Wait(0.35f);
        _pad.jump = true;
        _pad.jumpHeld = true;
        float x0 = hero.transform.position.x, top = 0f, air = 0f, stretch = 1f;
        bool off = false;
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            yield return null;
            if (!off && !hero.Grounded) { off = true; x0 = hero.transform.position.x; air = 0f; }
            if (off) air += Time.deltaTime;
            stretch = Mathf.Max(stretch, model.localScale.y / baseY);
            top = Mathf.Max(top, hero.transform.position.y - floor);
            if (off && hero.Grounded) break;
        }
        float reach = hero.transform.position.x - x0, squash = 1f;
        for (int i = 0; i < 4; i++) { squash = Mathf.Min(squash, model.localScale.y / baseY); yield return null; }
        _pad.moveX = 0f;
        _pad.jumpHeld = false;
        Log($"running jump: reach {reach:0.00} in {air:0.00} s, apex {top:0.00}, stretched to {stretch:0.00}, squashed to {squash:0.00} (expect about 6.4, about 0.92, 3.2, > 1.05, < 0.92)");
        yield return Wait(0.4f);

        // 2. a long drop kicks up dust
        hero.Teleport(new Vector2(pl.rect.xMin + 5f, floor + 5f));
        bool dust = false;
        for (float t = 0f; t < 1.5f && !dust; t += Time.deltaTime)
        {
            yield return null;
            dust = GameObject.Find("DustPuff") != null;
        }
        for (int i = 0; i < 4; i++) yield return null;
        DevCapture.Shot("polish_dust");
        Log($"a 5-unit drop: dust puff {dust} (expect True)");
        yield return Wait(0.5f);

        // 3. J, J, J on a Smudge near the panel's right edge: an impact frame on the haymaker that knocks it out,
        //    the words leaning right, the combo climbing, and the body flung off over the border toward the reader
        hero.Teleport(new Vector2(pl.rect.xMax - 3.3f, floor + 0.1f));
        hero.Face(1f);
        hh.Invulnerable = 300f;                                          // this is about his blows, not its
        var smudge = PageBuilder.Spawn(EnemyKind.Smudge, new Vector2(pl.rect.xMax - 2f, floor + 0.1f), pl, pm.transform);
        _followHero = false;
        torch.AimOverride = new Vector2(pl.rect.xMax - 2.5f, floor + 1.2f);
        yield return Wait(0.8f);
        int impacts0 = ComicFx.ImpactFrames;
        bool sawNegative = false, sawFlying = false, crossed = false;
        float minZ = 0f, leanSum = 0f;
        int leaned = 0;
        string pitchAfterCross = "";
        for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
        {
            if (t < 0.7f && Mathf.Repeat(t, 0.15f) < Time.unscaledDeltaTime) _pad.punch = true;
            yield return null;
            if (!sawNegative && Shader.GetGlobalFloat("_PB_Impact") > 0.5f) { sawNegative = true; yield return null; DevCapture.Shot("polish_impact"); }
            foreach (var w in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
                if ((w.name == "SFX POW!" || w.name == "SFX BIFF!") && w.alpha > 0.5f) { leanSum += Mathf.DeltaAngle(0f, w.transform.eulerAngles.z); leaned++; }
            if (pitchAfterCross == "" && gs.Combo >= 2) pitchAfterCross = Pitches();
            if (smudge != null && smudge.Health.Dead)
            {
                minZ = Mathf.Min(minZ, smudge.transform.position.z);
                if (smudge.transform.position.z < -2.6f && !sawFlying) { sawFlying = true; yield return null; DevCapture.Shot("polish_fling"); }
                if (smudge.transform.position.x > pl.rect.xMax) crossed = true;
            }
        }
        Log($"haymaker knockout: impact frames {ComicFx.ImpactFrames - impacts0} (seen {sawNegative}), words leaning {(leaned > 0 ? leanSum / leaned : 0f):0} deg, pitches after the cross {pitchAfterCross} (expect 1, True, < 0 (leaning right), one above 1.0)");
        Log($"the body: flew toward the reader to z {minZ:0.0}, over the panel's border {crossed}, gone {smudge == null} (expect about -3.3, True, True)");
        _followHero = true;

        // 4. hurt: a red flash, a blink through the i-frames, a heart popping out of the box
        foreach (var e in pl.enemies.ToArray()) if (e != null) { pm.EnemyDown(e); Destroy(e.gameObject); }
        hh.hp = hh.maxHp;
        hh.Invulnerable = 0f;
        hero.Teleport(new Vector2(pl.rect.xMin + 6f, floor + 0.1f));
        yield return Wait(0.4f);
        var skin = model.GetComponentsInChildren<Renderer>(true);
        hh.Apply(new Hit { hearts = 1, knockback = new Vector2(-3f, 0f), stun = 0.3f, team = Team.Inkie });
        yield return null;
        bool flashed = skin.Length > 0 && skin[0].HasPropertyBlock();
        var lostHeart = GameObject.Find("Heart4");
        float heartScale = lostHeart != null ? lostHeart.transform.localScale.x : 0f;
        yield return null;                                               // (the page shows the frame before)
        DevCapture.Shot("polish_hurt");
        int hiddenFrames = 0, frames = 0;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            yield return null;
            frames++;
            if (skin.Length > 0 && !skin[0].enabled) hiddenFrames++;
        }
        Log($"hurt: flashed {flashed}, blinked {hiddenFrames}/{frames} frames, visible after {(skin.Length > 0 && skin[0].enabled)}, the lost heart popped to {heartScale:0.00} (expect True, about a third, True, > 1.2)");

        // 5. waking and freezing: a Smudge in the dark, lit (drops, a stretch, a snap), then left (a flatten, a tick);
        //    three switched off at once by the torch: no sound of their own (the torch clicks)
        foreach (var e in pl.enemies.ToArray()) if (e != null) { pm.EnemyDown(e); Destroy(e.gameObject); }
        hero.Teleport(new Vector2(pl.rect.xMin + 1.5f, floor + 0.1f));
        _followHero = false;
        torch.AimOverride = new Vector2(pl.rect.xMin + 1.5f, floor + 1f);
        var dark = PageBuilder.Spawn(EnemyKind.Smudge, new Vector2(pl.rect.xMin + 8f, floor + 0.1f), pl, pm.transform);
        yield return Wait(1.2f);
        int cues0 = ComicFx.Cues;
        torch.AimOverride = new Vector2(pl.rect.xMin + 8f, floor + 1f);
        bool drops = false;
        float grown = 1f;
        var darkModel = dark.transform.GetChild(0);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            yield return null;
            drops |= GameObject.Find("InkDrops") != null;
            grown = Mathf.Max(grown, darkModel.localScale.y);
            if (drops && t > 0.08f && !_shotWake) { _shotWake = true; DevCapture.Shot("polish_wake"); }
        }
        int wakeCues = ComicFx.Cues - cues0;
        torch.AimOverride = new Vector2(pl.rect.xMin + 1.5f, floor + 1f);
        float flat = 1f;
        bool greyed = false;
        var darkSkin = darkModel.GetComponentsInChildren<Renderer>(true);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            yield return null;
            flat = Mathf.Min(flat, darkModel.localScale.y);
            greyed |= darkSkin.Length > 0 && darkSkin[0].HasPropertyBlock();
        }
        int freezeCues = ComicFx.Cues - cues0 - wakeCues;
        Log($"a Smudge lit: ink drops {drops}, stretched to {grown:0.00}, cues {wakeCues}; left in the dark: flattened to {flat:0.00}, greyed {greyed}, cues {freezeCues}, scale after {darkModel.localScale.y:0.00} (expect True, > 1.03, 1, < 0.95, True, 1, 1.00)");
        var crowd = new System.Collections.Generic.List<EnemyBrain>();
        for (int i = 0; i < 3; i++) crowd.Add(PageBuilder.Spawn(EnemyKind.Smudge, new Vector2(pl.rect.xMin + 3f + i * 1.3f, floor + 0.1f), pl, pm.transform));
        torch.AimOverride = new Vector2(pl.rect.xMin + 4.3f, floor + 1f);
        hh.Invulnerable = 5f;
        yield return Wait(1.2f);
        int cues1 = ComicFx.Cues;
        torch.SetOn(false);
        yield return Wait(0.4f);
        int offCues = ComicFx.Cues - cues1;
        torch.SetOn(true);
        yield return Wait(0.4f);
        Log($"three Smudges, the torch switched off and on: their own cues {offCues} then {ComicFx.Cues - cues1 - offCues} (expect 0, 0)");
        foreach (var e in pl.enemies.ToArray()) if (e != null) { pm.EnemyDown(e); Destroy(e.gameObject); }
        _followHero = true;

        // 6. the crank: each notch pulses the light; past full the ratchet climbs
        var c = torch.Charge;
        c.ApplyUpgrades(0, 0, 0);
        c.Charge = c.Capacity;
        yield return Wait(0.3f);
        _pad.twists = 1;
        yield return null;
        yield return null;
        float pulse = (float)(typeof(TorchController).GetField("_pulse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(torch) ?? -1f);
        string p1 = Pitches();
        yield return Wait(0.25f);
        _pad.twists = 1;
        yield return null;
        yield return null;
        string p2 = Pitches();
        Log($"overwinding: the notch's light pulse {pulse:0.00}; ratchet pitches after notch 1: {p1}, after notch 2: {p2} (expect > 0.5, the newest climbing above 1.1)");
        yield return Wait(2.5f);
        c.ApplyUpgrades(gs.Spring, gs.Gear, gs.Ratchet);

        // 7. Mom on her way: the edges close in (never over her door), and lift as she goes
        var mom = MomDirector.I;
        var vignette = MomVignette.I;
        torch.SetOn(false);
        mom.VisitNow();
        float peak = 0f, atFeet = 0f;
        for (float t = 0f; t < 14f && mom.State != MomState.AtDoor; t += Time.deltaTime)
        {
            yield return null;
            if (vignette != null) { peak = Mathf.Max(peak, vignette.Strength); if (mom.Feet > 0.5f && atFeet == 0f) atFeet = vignette.Strength; }
        }
        yield return Wait(0.6f);
        DevCapture.Shot("polish_vignette");
        float holeAlpha = -1f, cornerAlpha = -1f;
        var tex = vignette != null ? vignette.GetComponent<UnityEngine.UI.RawImage>().texture as Texture2D : null;
        if (tex != null && MomDoorView.I != null)
        {
            var door = MomDoorView.I.DoorOnScreen();
            holeAlpha = tex.GetPixelBilinear(door.center.x / Screen.width, door.center.y / Screen.height).a;
            cornerAlpha = tex.GetPixelBilinear(0.99f, 0.01f).a;
        }
        float atDoor = vignette != null ? vignette.Strength : -1f;
        for (float t = 0f; t < 12f && mom.State != MomState.Asleep; t += Time.deltaTime) yield return null;
        yield return Wait(1.2f);
        Log($"Mom coming: vignette {atFeet:0.00} half way, {atDoor:0.00} at the door; texture at her door {holeAlpha:0.00}, in a corner {cornerAlpha:0.00}; after she's gone {(vignette != null ? vignette.Strength : -1f):0.00} (expect > 0.2, about 0.55, 0.00, > 0.8, 0.00)");
        torch.SetOn(true);

        // 8. a star flies up into the counter, which only counts it as it lands
        StarPickup star = null;
        foreach (var s in pl.root.GetComponentsInChildren<StarPickup>()) { star = s; break; }
        var stars = GameObject.Find("Stars")?.GetComponent<TMPro.TMP_Text>();
        if (star != null && stars != null)
        {
            string before = stars.text;
            hero.Teleport((Vector2)star.transform.position - new Vector2(0f, 0.9f));
            torch.AimOverride = star.transform.position;
            bool flying = false;
            string during = "";
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                yield return null;
                if (!flying && GameObject.Find("FlyingStar") != null) { flying = true; during = stars.text; }
                if (flying && t > 0.2f && !_shotStar) { _shotStar = true; DevCapture.Shot("polish_starfly"); }
            }
            yield return WaitReal(0.5f);
            Log($"a star picked up: flying {flying}; counter '{Plain(before)}' -> while flying '{Plain(during)}' -> landed '{Plain(stars.text)}' (expect True, unchanged while flying, then one more)");
            torch.AimOverride = null;
        }
        else Log("no star on page 1's first panel");

        // 9. the Splash Page: the borders blow off, the camera punches in, a giant KA-POW!, and it all comes back
        pm.Load(5);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        var arena = pm.Layout.tiers[0].panels[0];
        EnemyBrain target = null;
        foreach (var e in arena.enemies) if (e != null && e is SmudgeBrain) { target = e; break; }
        if (target == null) { Log("no Smudge on page 5's first panel"); }
        else
        {
            hero.Teleport((Vector2)target.transform.position - new Vector2(1.6f, 0f));
            hh.Invulnerable = 10f;
            yield return Wait(0.6f);
            Transform border = null;
            foreach (Transform b in arena.root) if (b.name == "BorderTop") { border = b; break; }
            Vector3 home = border != null ? border.position : Vector3.zero;
            float baseSize = PageCamera.ViewWidth * PageCamera.TextureHeight / PageCamera.TextureWidth / 2f;
            gs.AddSplash(100f);
            int impacts1 = ComicFx.ImpactFrames;
            _pad.splash = true;
            yield return WaitReal(0.12f);
            float moved = border != null ? Vector3.Distance(border.position, home) : -1f;
            bool splashing = ComicFx.Splashing;
            yield return WaitReal(0.2f);
            float zoom = PageCamera.I.Cam.orthographicSize / baseSize;
            bool title = false;
            foreach (var w in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None)) title |= w.name == "SFX KA-POW!" && w.transform.localScale.x > 0.9f;
            DevCapture.Shot("polish_splash");
            yield return WaitReal(1.2f);
            float back = border != null ? Vector3.Distance(border.position, home) : -1f;
            Log($"splash page: splashing {splashing}, a border blown {moved:0.0} off, the view at {zoom:0.00} of its size, title {title}, impact frames {ComicFx.ImpactFrames - impacts1}; after: border {back:0.00} from home, view {PageCamera.I.Cam.orthographicSize / baseSize:0.00}, splashing {ComicFx.Splashing} (expect True, > 1, about 0.8, True, 1; 0.00, 1.00, False)");
        }

        Settings.ReduceFlashing = flashing;
        Settings.ScreenShake = shake;
        torch.AimOverride = null;
        _followHero = true;
    }

    private bool _shotWake, _shotStar;

    private static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<.*?>", "");

    // Blot's exits never wait on the torch (5 Oct: the page stuck on 6 when the beam left him as he dove into his
    // pool; gone under, there was nothing to light and the door stayed locked). Beaten into a blink, the beam
    // swung away: he still pops up across the room. Beaten into his getaway, the beam swung away: the page turns.
    private IEnumerator BlotDarkTest()
    {
        var pm = PageManager.I;
        var hero = HeroController.I;
        pm.Load(6);
        yield return Wait(0.3f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        hero.GetComponent<Health>().Invulnerable = 600f;
        pm.GoToTier(1);
        var office = pm.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in office.enemies) if (e is BlotBrain b) blot = b;
        if (blot == null) { Log("no Blot in the office"); yield break; }
        hero.Teleport(new Vector2(blot.transform.position.x - 4f, office.rect.yMin + 2.6f));
        _followHero = false;
        var torch = TorchController.I;
        Vector2 corner = new Vector2(office.rect.xMin + 1f, office.rect.yMax - 0.8f);   // a dark corner of the room
        torch.AimOverride = (Vector2)blot.transform.position + Vector2.up * 1.2f;
        yield return Wait(1.6f);
        Log($"the office: blot hp {blot.Health.hp:0} of {BlotBrain.MaxHp:0} (expect {BlotBrain.MaxHp:0})");

        // 1. a blink, and the beam away the moment he melts
        var skin = blot.GetComponentInChildren<Renderer>(true);
        for (int i = 0; i < 5 && skin.enabled; i++)
        {
            torch.AimOverride = (Vector2)blot.transform.position + Vector2.up * 1.2f;
            blot.Health.Invulnerable = 0f;
            blot.Health.Apply(new Hit { damage = 12f, team = Team.Hero, source = hero.gameObject });
            yield return null;
            yield return null;
        }
        bool melted = !skin.enabled;
        Vector3 from = blot.transform.position;
        torch.AimOverride = corner;
        yield return Wait(1.4f);
        Log($"blinked {melted}, the beam away: shown again {skin.enabled}, moved {Vector3.Distance(blot.transform.position, from):0.0}, lit {blot.Light.IsAwake} (expect True, True, > 2, False)");

        // 2. the getaway, and the beam away the moment he starts to dive
        for (float t = 0f; t < 12f && !blot.Escaping; t += 0.3f)
        {
            torch.AimOverride = (Vector2)blot.transform.position + Vector2.up * 1.2f;
            blot.Health.Invulnerable = 0f;
            blot.Health.Apply(new Hit { damage = 30f, team = Team.Hero, source = hero.gameObject, heavy = true });
            yield return Wait(0.3f);
        }
        torch.AimOverride = corner;
        float started = Time.time;
        bool unlit = true;
        for (float t = 0f; t < 16f && pm.Def != null && pm.Def.number == 6; t += Time.deltaTime)
        {
            yield return null;
            if (Time.time - started > 0.3f && blot != null && blot.Light.IsAwake) unlit = false;   // (once the beam's swung off)
        }
        Log($"escaping at hp {(blot != null ? blot.Health.hp : -1f):0}, the beam off him all the while {unlit}: page {(pm.Def != null ? pm.Def.number : -1)} after {Time.time - started:0.0} s (expect True, 7)");
        torch.AimOverride = null;
        _followHero = true;
    }

    // Falls never wait on the torch (6 Oct: Max ran off into a pit, out of the beam, and hung there frozen at the
    // panel's bottom edge until the beam was dragged all the way down to him; crates and Inkies the same). Below the
    // ledge they left with nothing under them, the dark doesn't hold them: they drop out of the panel. Up in the air
    // (a jump, a launched Inkie, a crate at the pit's lip) they still hang in the dark as steps.
    private IEnumerator FallsTest()
    {
        var pm = PageManager.I;
        var hero = HeroController.I;
        var torch = TorchController.I;
        var h = hero.GetComponent<Health>();
        pm.Load(1);
        yield return Wait(0.3f);
        while (hero.Locked) yield return null;
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        pm.GoToTier(1);
        var flat = pm.Layout.tiers[1].panels[0];
        var pit = pm.Layout.tiers[1].panels[1];                     // a pit from 3.5 to 11, the crate in it
        Log($"pit panel floor line {pit.floorLine - pit.rect.yMin:0.0} above its bottom (expect 2.5)");

        // 1. Max runs off the ledge and the torch goes off as he drops: he falls on out, and the page takes a heart
        hero.Teleport(pit.rect.min + new Vector2(1.5f, 2.6f));
        yield return Wait(0.6f);
        h.hp = h.maxHp;
        float hp0 = h.hp;
        _pad.moveX = 1f;
        for (float t = 0f; t < 2f && (hero.Grounded || hero.transform.position.x < pit.rect.xMin + 3.6f); t += Time.deltaTime) yield return null;
        _followHero = false;
        torch.SetOn(false);
        _pad.moveX = 0f;
        float dark = Time.time;
        yield return Wait(0.2f);
        DevCapture.Shot("falls_max");
        Log($"in the dark over the pit: dropping {hero.Light.Dropping}, awake {hero.Light.IsAwake}, lit {hero.Light.Lit}, feet {hero.transform.position.y - pit.rect.yMin:0.00} above the panel's bottom (expect True, True, False)");
        for (float t = 0f; t < 3f && h.hp >= hp0; t += Time.deltaTime) yield return null;
        Log($"fell out of the panel in the dark: heart lost {h.hp < hp0} after {Time.time - dark:0.00} s (expect True, under 0.8)");
        yield return Wait(1.0f);
        torch.SetOn(true);
        _followHero = true;
        yield return Wait(0.6f);

        // 2. a jump over a floor, the torch off on the way up: he hangs in the dark (as ever)
        hero.Teleport(flat.rect.min + new Vector2(4f, 2.6f));
        yield return Wait(0.5f);
        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(0.15f);
        torch.SetOn(false);
        _followHero = false;
        yield return null;
        Vector3 hang = hero.transform.position;
        yield return Wait(0.6f);
        _pad.jumpHeld = false;
        Log($"jump over a floor, torch off going up: hangs {Vector3.Distance(hero.transform.position, hang) < 0.05f}, in the air {hang.y - flat.rect.yMin - 2.5f:0.00} up (expect True, > 0.5)");

        // 3. a jump out over the pit, the torch off on the way up: he hangs too (still above the ledge he left)
        torch.SetOn(true);
        _followHero = true;
        yield return Wait(1.2f);
        hero.Teleport(pit.rect.min + new Vector2(2.6f, 2.6f));
        yield return Wait(0.5f);
        _pad.moveX = 1f;
        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(0.15f);
        torch.SetOn(false);
        _followHero = false;
        yield return null;
        hang = hero.transform.position;
        yield return Wait(0.6f);
        _pad.moveX = 0f;
        _pad.jumpHeld = false;
        Log($"jump out over the pit, torch off going up: hangs {Vector3.Distance(hero.transform.position, hang) < 0.05f}, dropping {hero.Light.Dropping} (expect True, False)");
        hero.Teleport(pit.rect.min + new Vector2(1f, 2.6f));
        torch.SetOn(true);
        yield return Wait(0.8f);

        // 4. the crate: lit, it drops into the pit; the torch off with it down there, it falls on out and is drawn back
        var crate = pit.root.GetComponentInChildren<FallingProp>();
        Vector3 home = crate.transform.position;
        torch.AimOverride = (Vector2)home;
        for (float t = 0f; t < 3f && crate.transform.position.y > home.y - 0.6f; t += Time.deltaTime) yield return null;
        torch.SetOn(false);
        float off = Time.time;
        Vector3 sank = crate.transform.position;
        bool redrawn = false;
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            yield return null;
            if ((crate.transform.position - home).magnitude < 0.05f) { redrawn = true; break; }
        }
        Log($"crate sank {home.y - sank.y:0.00} into the pit, torch off: drawn back in {redrawn} after {Time.time - off:0.00} s (expect True, under 1.6)");
        yield return Wait(1.2f);
        Log($"back at its lip in the dark: still there {(crate.transform.position - home).magnitude < 0.05f}, frozen {!crate.GetComponent<Lightable>().IsAwake} (expect True, True)");
        torch.SetOn(true);

        // 5. a Smudge knocked off the ledge, the torch off as it goes over: it drops out of the panel
        var smudge = PageBuilder.Spawn(EnemyKind.Smudge, pit.rect.min + new Vector2(2.6f, 2.6f), pit, pm.transform);
        torch.AimOverride = pit.rect.min + new Vector2(2f, 3.6f);
        yield return Wait(1.0f);
        h.Invulnerable = 600f;
        smudge.Health.Apply(new Hit { damage = 1f, team = Team.Hero, source = hero.gameObject, knockback = new Vector2(9f, 4f), stun = 0.8f });
        for (float t = 0f; t < 1.5f && smudge != null && (smudge.Grounded || smudge.transform.position.y > pit.rect.yMin + 2.4f); t += Time.deltaTime) yield return null;
        torch.SetOn(false);
        off = Time.time;
        for (float t = 0f; t < 3f && smudge != null; t += Time.deltaTime) yield return null;
        Log($"smudge knocked into the pit, torch off: gone {smudge == null} after {Time.time - off:0.00} s, off the panel's list {!pit.enemies.Contains(smudge)} (expect True, under 0.8, True)");
        torch.SetOn(true);

        // 6. a Smudge launched up at the pit's edge, the torch off on its way up: it hangs as a step
        smudge = PageBuilder.Spawn(EnemyKind.Smudge, pit.rect.min + new Vector2(3.0f, 2.6f), pit, pm.transform);
        yield return Wait(1.0f);
        smudge.Health.Apply(new Hit { damage = 1f, team = Team.Hero, source = hero.gameObject, knockback = new Vector2(2f, 11f), stun = 0.8f });
        yield return Wait(0.12f);
        torch.SetOn(false);
        yield return null;
        hang = smudge.transform.position;
        yield return Wait(0.8f);
        Log($"smudge launched at the edge, torch off going up: hangs {smudge != null && Vector3.Distance(smudge.transform.position, hang) < 0.05f} (expect True)");
        torch.SetOn(true);
        if (smudge != null) { pm.EnemyDown(smudge); Destroy(smudge.gameObject); }

        // 7. a Smudge knocked out over the pit, the torch off as its body comes down: it drops out too
        smudge = PageBuilder.Spawn(EnemyKind.Smudge, pit.rect.min + new Vector2(3.0f, 2.6f), pit, pm.transform);
        yield return Wait(1.0f);
        float died = smudge.transform.position.y;
        smudge.Health.Apply(new Hit { damage = 999f, team = Team.Hero, source = hero.gameObject, knockback = new Vector2(6f, 2f), heavy = true });
        for (float t = 0f; t < 2f && smudge != null && smudge.transform.position.y > died - 0.3f; t += Time.deltaTime)
        {
            torch.AimOverride = (Vector2)smudge.transform.position;            // lit through its flight, up and over
            yield return null;
        }
        torch.SetOn(false);
        off = Time.time;
        for (float t = 0f; t < 3f && smudge != null; t += Time.deltaTime) yield return null;
        Log($"knocked-out body over the pit, torch off: gone {smudge == null} after {Time.time - off:0.00} s (expect True, under 0.8)");

        torch.SetOn(true);
        torch.AimOverride = null;
        h.Invulnerable = 0f;
        _followHero = true;
    }

    /// <summary>Waits until something's true, or gives up after a while (a failing check mustn't hang the suite).</summary>
    private static IEnumerator Until(System.Func<bool> done, float timeout)
    {
        for (float t = 0f; t < timeout && !done(); t += Time.unscaledDeltaTime) yield return null;
    }

    /// <summary>Puts Max right on a star (lit), so he picks it up.</summary>
    private IEnumerator PickUp(StarPickup star)
    {
        if (star == null) yield break;
        Vector2 at = star.transform.position;
        TorchController.I.AimOverride = at;
        yield return Wait(0.3f);
        for (float t = 0f; t < 1f && star != null; t += Time.deltaTime)
        {
            HeroController.I.Teleport(at + Vector2.down * 0.9f);
            yield return null;
        }
    }

    /// <summary>One frame's scroll on the real mouse, as if the wheel or a trackpad sent it.</summary>
    private static void Scroll(UnityEngine.InputSystem.Mouse mouse, float y)
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
        {
            position = mouse.position.ReadValue(),
            scroll = new Vector2(0f, y),
        });
    }

    /// <summary>The SFX words up right now: each word's glyphs and, for a burst, its star's solid middle.</summary>
    private static System.Collections.Generic.List<(string word, Rect text, Rect body)> LetteringUp()
    {
        var list = new System.Collections.Generic.List<(string, Rect, Rect)>();
        foreach (var t in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("SFX ") || t.alpha < 0.5f) continue;
            var b = t.textBounds;                                          // the glyphs, in the word's own space
            Vector3 c = t.transform.TransformPoint(b.center), e = Vector3.Scale(b.extents, t.transform.lossyScale);
            Rect text = new Rect(c.x - e.x, c.y - e.y, e.x * 2f, e.y * 2f);
            Rect body = text;
            var burst = t.transform.Find("Burst");
            if (burst != null)
            {
                var sb = burst.GetComponent<Renderer>().bounds;
                body = new Rect(sb.center.x - sb.extents.x * 0.66f, sb.center.y - sb.extents.y * 0.66f, sb.size.x * 0.66f, sb.size.y * 0.66f);
            }
            list.Add((t.name.Substring(4), text, body));
        }
        return list;
    }

    /// <summary>How much two boxes overlap, as a share of the smaller one.</summary>
    private static float Overlap(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        if (w <= 0f || h <= 0f) return 0f;
        return w * h / Mathf.Max(1e-4f, Mathf.Min(a.width * a.height, b.width * b.height));
    }

    // tests jump between pages and spend stars: none of that is the developer's own progress
    private void OnDestroy()
    {
        if (_save == null) return;
        Settings.HighestPage = _save[0];
        Settings.Stars = _save[1];
        Settings.Spring = _save[2];
        Settings.Gear = _save[3];
        Settings.Ratchet = _save[4];
        Settings.MomTaught = _save[5] == 1;
        Settings.GhostTaught = _save[6] == 1;
        for (int p = 1; p < _saveStars.Length; p++) Settings.SetStarsFound(p, _saveStars[p]);
        Settings.Save();
    }

    private IEnumerator Basics()
    {
        var hero = HeroController.I;
        PageManager.I.Load(1);                                         // page 1's open rooftop, whatever ran before
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        float x0 = hero.transform.position.x;
        _pad.moveX = 1f;
        yield return Wait(1f);
        _pad.moveX = 0f;
        Log($"run 1 s: dx {hero.transform.position.x - x0:0.00} (expect about 6.5)");

        float y0 = hero.transform.position.y, top = y0;
        _pad.jump = true;
        _pad.jumpHeld = true;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { top = Mathf.Max(top, hero.transform.position.y); yield return null; }
        _pad.jumpHeld = false;
        Log($"held jump apex {top - y0:0.00} (expect about 3.2)");

        yield return Wait(0.4f);
        y0 = hero.transform.position.y; top = y0;
        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(0.06f);
        _pad.jumpHeld = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime) { top = Mathf.Max(top, hero.transform.position.y); yield return null; }
        Log($"tapped jump apex {top - y0:0.00} (expect about 1.8)");

        // the core rule: torch off, Max freezes
        yield return Wait(0.3f);
        TorchController.I.SetOn(false);
        yield return Wait(0.1f);
        x0 = hero.transform.position.x;
        _pad.moveX = 1f;
        yield return Wait(0.6f);
        Log($"torch off: awake {hero.Light.IsAwake}, dx {hero.transform.position.x - x0:0.00} (expect False, 0)");
        TorchController.I.SetOn(true);
        yield return Wait(0.6f);
        _pad.moveX = 0f;
        Log($"torch on again: awake {hero.Light.IsAwake}, dx {hero.transform.position.x - x0:0.00} (expect True, >0)");

        // the beam's edge is a wall of time: aim away and walk toward it
        _followHero = false;
        TorchController.I.AimOverride = (Vector2)hero.transform.position + new Vector2(-3f, 1f);
        _pad.moveX = 1f;
        yield return Wait(1.2f);
        _pad.moveX = 0f;
        Log($"walked toward the edge: x {hero.transform.position.x:0.00}, beam x {LightField.I.BeamCentre.x:0.00} r {LightField.I.BeamRadius:0.0}, awake {hero.Light.IsAwake}");
        _followHero = true;

        // twist: drain then crank (on a stock torch: the save's Nightstand upgrades change the numbers)
        var charge = TorchController.I.Charge;
        charge.ApplyUpgrades(0, 0, 0);
        charge.Charge = 10f;
        _pad.twists = 5;
        yield return null;
        yield return null;
        Log($"5 twists from 10 s: {charge.Charge:0.0} (expect about 20)");
        charge.Charge = charge.Capacity;
        _pad.twists = 3;
        yield return null;
        yield return null;
        Log($"3 notches past full: overwind {charge.Overwind} (expect 3)");
        _pad.twists = 1;
        yield return null;
        yield return null;
        Log($"one more: flare {LightField.I.FlareTime:0.00}, dead {charge.Dead} (expect 1.5, True)");
        if (GameState.I != null) charge.ApplyUpgrades(GameState.I.Spring, GameState.I.Gear, GameState.I.Ratchet);
    }

    private IEnumerator DotShotTest()
    {
        PageManager.I.Load(3);
        yield return Wait(1.6f);
        var hero = HeroController.I;
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        var ds = pl.enemies[0];
        hero.Teleport(new Vector2(pl.rect.xMin + 3.5f, pl.rect.yMin + 2.6f));
        // light the whole lane between them so it fires
        _followHero = false;
        TorchController.I.AimOverride = new Vector2(pl.rect.xMin + 7f, pl.rect.yMin + 3.5f);
        InkPellet pellet = null;
        for (float t = 0f; t < 5f && pellet == null; t += Time.deltaTime)
        {
            pellet = FindFirstObjectByType<InkPellet>();
            yield return null;
        }
        Log($"pellet fired: {pellet != null}, dot-shot at {ds.transform.position.x:0.0}, max at {hero.transform.position.x:0.0}");
        if (pellet == null) yield break;
        yield return Wait(0.15f);
        // pull the light back to Max only: the pellet should hang at the beam's edge
        TorchController.I.AimOverride = (Vector2)hero.transform.position + new Vector2(-2.5f, 1f);
        yield return Wait(0.6f);
        if (pellet == null) { Log("pellet gone before the check"); yield break; }
        Vector3 p0 = pellet.transform.position;
        bool awake0 = pellet.GetComponent<Lightable>().IsAwake;
        yield return Wait(0.6f);
        Log($"pellet frozen: awake {awake0}, moved {(pellet != null ? Vector3.Distance(p0, pellet.transform.position) : -1f):0.000} (expect False, 0), beam edge x {LightField.I.BeamCentre.x + LightField.I.BeamRadius:0.0}, pellet x {p0.x:0.0}");
        // re-light it: it resumes toward Max
        float hp0 = hero.GetComponent<Health>().hp;
        TorchController.I.AimOverride = new Vector2(pl.rect.xMin + 6f, pl.rect.yMin + 3.5f);
        yield return Wait(1.2f);
        Log($"re-lit: pellet alive {pellet != null}, max hearts {hero.GetComponent<Health>().hp:0} (was {hp0:0})");
        _followHero = true;
    }

    private IEnumerator BruiserTest()
    {
        PageManager.I.Load(5);
        yield return Wait(1.6f);
        var hero = HeroController.I;
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        BruiserBrain bruiser = null;
        foreach (var e in pl.enemies) if (e is BruiserBrain b) bruiser = b;
        Log($"panel 1: {pl.enemies.Count} inkies, bruiser {bruiser != null}");
        if (bruiser == null) yield break;
        // stand Max just in front of the Bruiser with the two Smudges between: the slam should catch them
        hero.Teleport(new Vector2(bruiser.transform.position.x - 2.0f, pl.rect.yMin + 2.6f));
        _followHero = false;
        TorchController.I.AimOverride = new Vector2(bruiser.transform.position.x - 3f, pl.rect.yMin + 3f);
        int choreo0 = GameState.I.Choreography;
        float heroHp0 = hero.GetComponent<Health>().hp;
        for (float t = 0f; t < 5f && bruiser.Mode != EnemyBrain.State.Windup; t += Time.deltaTime) yield return null;
        Log($"bruiser winding up: {bruiser.Mode == EnemyBrain.State.Windup}");
        yield return Wait(0.6f);
        // freeze him at the top of the wind-up
        TorchController.I.AimOverride = new Vector2(bruiser.transform.position.x - 7f, pl.rect.yMin + 3f);
        hero.Teleport(new Vector2(bruiser.transform.position.x - 6f, pl.rect.yMin + 2.6f));
        yield return Wait(1.0f);
        Log($"frozen mid wind-up: awake {bruiser.Light.IsAwake}, mode {bruiser.Mode}");
        var smudges = new System.Collections.Generic.List<Health>();
        float slot = 1.4f;
        foreach (var e in pl.enemies)
        {
            if (!(e is SmudgeBrain)) continue;
            smudges.Add(e.Health);
            // the light trick: line the crowd up in front of the frozen Bruiser
            var cc = e.GetComponent<CharacterController>();
            cc.enabled = false;
            e.transform.position = new Vector3(bruiser.transform.position.x - slot, e.transform.position.y, 0f);
            cc.enabled = true;
            slot += 1.1f;
        }
        string before = string.Join(",", smudges.ConvertAll(h => h.hp.ToString("0")));
        // re-light the whole crowd
        TorchController.I.AimOverride = new Vector2(bruiser.transform.position.x - 2.5f, pl.rect.yMin + 3f);
        yield return Wait(1.4f);
        string after = string.Join(",", smudges.ConvertAll(h => h == null ? "dead" : h.hp.ToString("0")));
        Log($"smudges hp {before} -> {after}, choreography {choreo0} -> {GameState.I.Choreography}, max hearts {hero.GetComponent<Health>().hp:0} (was {heroHp0:0})");
        _followHero = true;
    }

    private IEnumerator SplotchTest()
    {
        PageManager.I.Load(4);
        yield return Wait(1.6f);
        var hero = HeroController.I;
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        var bat = pl.enemies[0] as SplotchBrain;
        hero.Teleport(new Vector2(bat.transform.position.x + 1f, pl.rect.yMin + 2.6f));
        float hp0 = hero.GetComponent<Health>().hp;
        bool dived = false;
        for (float t = 0f; t < 6f; t += Time.deltaTime)
        {
            if (bat.Mode == EnemyBrain.State.Attack) dived = true;
            yield return null;
        }
        Log($"bat dived: {dived}, max hearts {hero.GetComponent<Health>().hp:0} (was {hp0:0})");
        // freeze the bat (light only Max, below it) and stand on it
        yield return Wait(1.5f);
        TorchController.I.Charge.Charge = TorchController.I.Charge.Capacity;
        _followHero = false;
        Vector2 batPos = bat.transform.position;
        TorchController.I.AimOverride = new Vector2(batPos.x + 5f, batPos.y - 4f);
        for (int i = 0; i < 20; i++)
        {
            yield return null;
            if (i % 5 == 4) Log($"  frame {i}: beam {LightField.I.BeamCentre} r {LightField.I.BeamRadius:0.0}, bat {bat.transform.position} awake {bat.Light.IsAwake}");
        }
        batPos = bat.transform.position;
        Log($"bat frozen: awake {bat.Light.IsAwake} at {batPos}");
        hero.Teleport(batPos + new Vector2(0f, 0.6f));
        TorchController.I.AimOverride = batPos + new Vector2(0f, 4.3f);      // Max at the beam's bottom rim
        yield return Wait(0.8f);
        Log($"standing on the frozen bat: hero y {hero.transform.position.y:0.00}, bat top {batPos.y + 0.23f:0.00}, grounded {hero.Grounded}, bat awake {bat.Light.IsAwake}");
        _followHero = true;
    }

    private IEnumerator EraserTest()
    {
        PageManager.I.Load(7);
        yield return Wait(1.6f);
        var pl = PageManager.I.Layout.tiers[0].panels[1];               // the Eraser on its bridge
        var eraser = pl.enemies[0];
        int Blocks() { int n = 0; foreach (var b in pl.root.GetComponentsInChildren<ErasableBlock>(false)) n++; return n; }
        int b0 = Blocks();
        float y0 = eraser.transform.position.y;
        _followHero = false;
        TorchController.I.AimOverride = new Vector2(eraser.transform.position.x, pl.rect.yMin + 3f);
        yield return Wait(4f);
        Log($"lit eraser for 4 s: blocks {b0} -> {Blocks()}, eraser at x {eraser.transform.position.x - pl.rect.xMin:0.0}, still on the bridge {Mathf.Abs(eraser.transform.position.y - y0) < 0.1f}");
        DevCapture.Shot("eraser_lit");
        TorchController.I.AimOverride = new Vector2(pl.rect.xMin - 6f, pl.rect.yMin + 3f);
        int b1 = Blocks();
        yield return Wait(2f);
        Log($"dark eraser for 2 s: blocks {b1} -> {Blocks()} (expect no change)");
        _followHero = true;

        // the panel restarts (Max out of hearts): the eaten bridge is drawn back in
        HeroController.I.Teleport(pl.entry);                             // in the Eraser's panel, so it's the one that restarts
        yield return Wait(0.3f);
        var h = HeroController.I.GetComponent<Health>();
        h.Invulnerable = 0f;
        h.hp = 0f;
        yield return Wait(5f);
        Log($"after the panel restart: blocks {Blocks()} (expect {pl.root.GetComponentsInChildren<ErasableBlock>(true).Length}: the whole bridge)");
    }

    /// <summary>The torch in Roshan's hand: the crank turning, the dial and the lens wheel.</summary>
    // Max's moves play to their ends and hand over to the right stance: checked by the state that's
    // playing, with pictures at the poses that matter (DevCapture.Folder must be set)
    private IEnumerator AnimTest()
    {
        var hero = HeroController.I;
        var clips = hero.Clips;
        // on a flat, empty panel with Mom asleep (her open door would hold him still mid-combo)
        PageManager.I.Load(1);
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var flat = PageManager.I.Layout.tiers[0].panels[0];
        hero.Teleport(flat.rect.min + new Vector2(2.5f, 2.6f));
        hero.Face(1f);
        yield return Wait(0.5f);
        int page = GameState.I.Page;
        GameState.I.Page = Mathf.Max(page, 4);          // the slam is a page-4 move

        // E, E, E: jab, cross, haymaker; then the guard stays up a while before he relaxes
        _pad.punch = true;
        yield return Wait(0.1f);
        DevCapture.Shot("anim_jab");
        _pad.punch = true;
        yield return Wait(0.34f);
        Log($"combo at 0.44 s: {clips.Current} (expect Cross)");
        DevCapture.Shot("anim_cross");
        _pad.punch = true;
        yield return Wait(0.49f);
        Log($"combo at 0.93 s: {clips.Current} (expect Haymaker)");
        DevCapture.Shot("anim_haymaker");
        yield return Wait(0.6f);
        Log($"after the combo: {clips.Current} (expect Guard)");
        yield return Wait(1.6f);
        Log($"a while later: {clips.Current} (expect Idle)");

        _pad.kick = true;
        yield return Wait(0.17f);
        Log($"kick: {clips.Current} {clips.Normalized:0.00}");
        DevCapture.Shot("anim_kick");
        yield return Wait(0.8f);

        // the dodge: dash, then the skid plays out instead of idling while he slides
        _pad.dodge = true;
        yield return Wait(0.1f);
        DevCapture.Shot("anim_dodge_dash");
        yield return Wait(0.18f);
        Log($"dodge skid: {clips.Current} vx {hero.Velocity.x:0.0} (expect Dodge, still sliding)");
        DevCapture.Shot("anim_dodge_skid");
        yield return Wait(0.45f);
        Log($"after the dodge: {clips.Current} (expect Guard)");
        yield return Wait(0.4f);

        _pad.up = true;
        _pad.kick = true;
        yield return Wait(0.2f);
        _pad.up = false;
        Log($"launcher: {clips.Current}");
        DevCapture.Shot("anim_launcher");
        yield return Wait(0.8f);

        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(0.35f);
        _pad.jumpHeld = false;
        _pad.kick = true;
        yield return Wait(0.12f);
        Log($"dive kick: {clips.Current}");
        DevCapture.Shot("anim_divekick");
        while (!hero.Grounded) yield return null;
        yield return null;
        Log($"dive kick landed: {clips.Current} (expect Land)");
        yield return Wait(1f);

        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(0.4f);
        _pad.jumpHeld = false;
        _pad.down = true;
        _pad.kick = true;
        yield return Wait(0.1f);
        _pad.down = false;
        Log($"slam dive: {clips.Current} (expect SlamDive)");
        DevCapture.Shot("anim_slamdive");
        while (!hero.Grounded) yield return null;
        yield return Wait(0.06f);
        Log($"slam landed: {clips.Current} (expect GroundSlam)");
        DevCapture.Shot("anim_slam_land");
        yield return Wait(0.9f);
        Log($"after the slam: {clips.Current} (expect Guard)");
        GameState.I.Page = page;
    }

    // Act 3: invisible ink only real in Ghost light; the flood only rises where it's lit; drowning;
    // Blot hiding in invisible ink in phase 2
    private IEnumerator Act3Test()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        var gs = GameState.I;
        PageManager.I.Load(7);
        hero.GetComponent<Health>().Invulnerable = 120f;
        yield return Wait(1.6f);
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        var inks = pl.root.GetComponentsInChildren<InvisibleInk>(true);
        Log($"page 7 panel 1: {inks.Length} invisible-ink pieces, ghost unlocked {torch.Lenses.Unlocked[(int)Lens.Ghost]} (expect 3, True)");
        _followHero = false;
        var bridge = pl.rect.min + new Vector2(5.5f, 3f);
        TorchController.I.AimOverride = bridge;
        torch.Lenses.Pick((int)Lens.Clear);
        yield return Wait(0.6f);
        Log($"clear lens on the bridge: real {inks[0].Real} (expect False)");
        DevCapture.Shot("act3_ghost_hidden");
        torch.Lenses.Pick((int)Lens.Ghost);
        yield return Wait(0.6f);
        Log($"ghost lens on the bridge: real {inks[0].Real} (expect True)");
        DevCapture.Shot("act3_ghost_shown");
        // walk Max over the gap with the Ghost lens following him
        _followHero = true;
        hero.Teleport(pl.rect.min + new Vector2(2.5f, 2.7f));
        yield return Wait(0.3f);
        _pad.moveX = 1f;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            var hx = hero.transform.position.x - pl.rect.xMin;
            if ((hx > 2.5f && hx < 2.85f) || (hx > 5.0f && hx < 5.3f) || (hx > 8.1f && hx < 8.4f)) { _pad.jump = true; _pad.jumpHeld = true; }
            else _pad.jumpHeld = false;
            yield return null;
        }
        _pad.moveX = 0f;
        _pad.jumpHeld = false;
        Log($"crossed the invisible bridge: x {hero.transform.position.x - pl.rect.xMin:0.0} y {hero.transform.position.y - pl.rect.yMin:0.0} (expect x > 10.5, y about 2.5)");
        torch.Lenses.Pick((int)Lens.Clear);

        // page 8: the flood
        PageManager.I.Load(8);
        hero.GetComponent<Health>().Invulnerable = 120f;
        yield return Wait(1.6f);
        var p8 = PageManager.I.Layout.tiers[0].panels[0];
        var flood = p8.flood;
        _followHero = false;
        hero.Teleport(p8.rect.min + new Vector2(11.5f, 5.5f));             // up on the pillar, out of harm
        TorchController.I.AimOverride = p8.rect.min + new Vector2(6f, 9.5f);   // the beam up high
        float l0 = flood.Level;
        yield return Wait(1.5f);
        Log($"beam high: flood {l0 - p8.rect.yMin:0.00} -> {flood.Level - p8.rect.yMin:0.00} (expect no change)");
        TorchController.I.AimOverride = p8.rect.min + new Vector2(6f, 2.5f);   // the beam on the ink
        yield return Wait(2f);
        Log($"beam on the ink 2 s: flood {flood.Level - p8.rect.yMin:0.00} (expect about 2.3)");
        DevCapture.Shot("act3_flood");
        torch.SetOn(false);
        float l1 = flood.Level;
        yield return Wait(1f);
        Log($"torch off: flood held {Mathf.Abs(flood.Level - l1) < 0.001f} (expect True)");
        torch.SetOn(true);
        // drown
        int hearts0 = Mathf.RoundToInt(hero.GetComponent<Health>().hp);
        hero.GetComponent<Health>().Invulnerable = 0f;
        hero.Teleport(new Vector2(p8.rect.xMin + 6f, flood.Level - 0.6f));
        // read it the moment Max is back on his feet (the beam's still on the ink, so it starts rising again)
        yield return Until(() => PageManager.I.Busy, 2f);
        yield return Until(() => !PageManager.I.Busy, 4f);
        Log($"drowned: hearts {hearts0} -> {hero.GetComponent<Health>().hp:0}, flood drained {Mathf.Abs(flood.Level - flood.Start) < 0.05f} (expect -1, True)");
        hero.GetComponent<Health>().Invulnerable = 120f;

        // page 8's vat: Blot in invisible ink
        var vat = PageManager.I.Layout.tiers[1].panels[1];
        BlotBrain blot = null;
        foreach (var e in vat.enemies) if (e is BlotBrain b) blot = b;
        Log($"the vat: blot {blot != null}, phase {(blot != null ? blot.Phase : 0)}, hp {(blot != null ? blot.Health.hp : 0):0} (expect True, 2, {BlotBrain.VatHp:0})");
        if (blot == null) yield break;
        PageManager.I.GoToTier(1);
        yield return Wait(0.5f);
        hero.Teleport(new Vector2(blot.transform.position.x - 4f, vat.rect.yMin + 2.6f));
        TorchController.I.AimOverride = (Vector2)blot.transform.position + new Vector2(-2f, 1.5f);
        torch.Lenses.Pick((int)Lens.Clear);
        for (float t = 0f; t < 9f && !blot.Hidden; t += Time.deltaTime) yield return null;
        Log($"he slipped into invisible ink: hidden {blot.Hidden} (expect True)");
        DevCapture.Shot("act3_blot_hidden");
        float hp0 = blot.Health.hp;
        bool landed = blot.Health.Apply(new Hit { damage = 6f, team = Team.Hero, source = hero.gameObject });
        Log($"a punch while hidden: landed {landed}, hp {hp0:0} -> {blot.Health.hp:0} (expect False)");
        torch.Lenses.Pick((int)Lens.Ghost);
        yield return Wait(0.4f);
        blot.Health.Invulnerable = 0f;
        landed = blot.Health.Apply(new Hit { damage = 6f, team = Team.Hero, source = hero.gameObject });
        Log($"in Ghost light: hidden {blot.Hidden}, a punch landed {landed} (expect False, True)");
        DevCapture.Shot("act3_blot_found");
        torch.Lenses.Pick((int)Lens.Clear);
        _followHero = true;
    }

    // Page 9: Blot's last 60 HP brings Mom and the room light; beating him plays the ending
    // (the ending rolls the credits, which unloads the game: run this one last in a suite)
    private IEnumerator FinaleTest()
    {
        var hero = HeroController.I;
        PageManager.I.Load(9);
        hero.GetComponent<Health>().Invulnerable = 300f;
        yield return Wait(1.6f);
        var arena = PageManager.I.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in arena.enemies) if (e is BlotBrain b) blot = b;
        Log($"the roof: blot {blot != null}, phase {(blot != null ? blot.Phase : 0)}, hp {(blot != null ? blot.Health.hp : 0):0} (expect True, 3, {BlotBrain.RoofHp:0})");
        if (blot == null) yield break;
        // straight to the second tier, in front of him
        _pad.moveX = 0f;
        PageManager.I.GoToTier(1);
        hero.Teleport(new Vector2(arena.rect.xMin + 10f, arena.rect.yMin + 2.6f));
        _followHero = false;
        TorchController.I.AimOverride = (Vector2)blot.transform.position + new Vector2(-3f, 1.5f);
        TorchController.I.Lenses.Pick((int)Lens.Ghost);
        yield return Wait(1.5f);
        blot.Health.hp = 34f;
        blot.Health.Invulnerable = 0f;
        blot.Health.Apply(new Hit { damage = 6f, team = Team.Hero, source = hero.gameObject });
        yield return Wait(1.2f);
        Log($"finale: running {Finale.I.Running}, room light {LightField.I.RoomLight}, mom watching {MomDirector.I.Watching} (expect True x3)");
        DevCapture.Shot("finale_roomlight");
        TorchController.I.SetOn(false);
        yield return Wait(0.5f);
        Log($"torch off in the room light: max awake {hero.Light.IsAwake}, blot awake {blot.Light.IsAwake} (expect True, True)");
        TorchController.I.SetOn(true);
        yield return Wait(3f);
        DevCapture.Shot("finale_rooting");
        // the ending can't be broken: Max down to his last heart, one of Blot's helpers right beside him, swinging
        var heroHealth = hero.GetComponent<Health>();
        var helper = PageBuilder.Spawn(EnemyKind.Smudge, (Vector2)hero.transform.position + new Vector2(1.2f, 0.1f), arena, PageManager.I.transform);
        helper.Summoned = true;
        var summoned = typeof(BlotBrain).GetField("_summoned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(blot) as System.Collections.Generic.List<EnemyBrain>;
        summoned?.Add(helper);
        heroHealth.hp = 1f;
        heroHealth.Invulnerable = 0f;
        blot.Health.Invulnerable = 0f;
        blot.Health.Apply(new Hit { damage = 40f, team = Team.Hero, source = hero.gameObject });
        Log($"blot beaten: dead {blot.Health.Dead}, ending {Finale.I.Ended} (expect True, True)");
        // the twist, wordless: dark, the torch clicks on under her door, the crank, the music, the light running down
        // and wound straight back up, the iris
        float[] at = { 2.5f, 5f, 8.5f, 9.5f, 11f, 14f, 15.2f, 17f, 19.5f, 22.2f, 23.8f, 26f, 28.4f };   // (9.5: the comic's gone, the light's still on)
        float clock = 0f;
        foreach (float t in at)
        {
            yield return WaitReal(t - clock);
            clock = t;
            DevCapture.Shot($"ending_{t:00.0}");
            if (Mathf.Approximately(t, 5f))
                Log($"the ending, 5 s on: helper melted {helper == null}, max hearts {heroHealth.hp:0}, dead {hero.Dead}, panel restarting {PageManager.I.Busy} (expect True, 1, False, False)");
        }
        _followHero = true;
    }

    // the ending waits while the game is bookmarked (run alone, or last: the ending rolls the credits)
    private IEnumerator EndingPauseTest()
    {
        var hero = HeroController.I;
        PageManager.I.Load(9);
        hero.GetComponent<Health>().Invulnerable = 300f;
        yield return Wait(1.6f);
        while (hero.Locked) yield return null;
        var arena = PageManager.I.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in arena.enemies) if (e is BlotBrain b) blot = b;
        if (blot == null) { Log("no Blot on the roof"); yield break; }
        _pad.moveX = 0f;
        PageManager.I.GoToTier(1);
        hero.Teleport(new Vector2(arena.rect.xMin + 10f, arena.rect.yMin + 2.6f));
        _followHero = false;
        TorchController.I.AimOverride = (Vector2)blot.transform.position + new Vector2(-3f, 1.5f);
        yield return Wait(1.5f);
        blot.Health.hp = 34f;
        blot.Health.Invulnerable = 0f;
        blot.Health.Apply(new Hit { damage = 6f, team = Team.Hero, source = hero.gameObject });
        yield return Wait(1.2f);
        blot.Health.Invulnerable = 0f;
        blot.Health.Apply(new Hit { damage = 40f, team = Team.Hero, source = hero.gameObject });
        float blow = Time.unscaledTime;
        yield return WaitReal(1f);
        var bookmark = FindFirstObjectByType<BookmarkPause>();
        bookmark?.Pause();
        yield return WaitReal(4f);
        bool held = !Finale.I.ReadingAgain && MomDoorView.I != null && !MomDoorView.I.Ending;   // still at "...he WON!"
        bookmark?.Resume();
        while (Finale.I != null && !Finale.I.ReadingAgain && Time.unscaledTime - blow < 40f) yield return null;
        Log($"bookmarked 4 s during the ending: held {held}; the torch came on under her door {Time.unscaledTime - blow:0.0} s after the blow (expect True, about 21.5: 17.5 plus the 4 s)");
        _followHero = true;
    }

    // the end of Act 1: the Nightstand, a Spring upgrade, then on to page 4
    private IEnumerator NightstandTest()
    {
        _atNightstand = true;
        var gs = GameState.I;
        var torch = TorchController.I;
        PageManager.I.Load(3);
        yield return Wait(1.6f);
        gs.StarsTotal = 12;
        gs.Spring = gs.Gear = gs.Ratchet = 0;
        torch.Charge.ApplyUpgrades(0, 0, 0);
        PageManager.I.Finish(null);
        for (float t = 0f; t < 8f && !Nightstand.Open; t += Time.unscaledDeltaTime) yield return null;
        yield return WaitReal(0.5f);
        Log($"nightstand open {Nightstand.Open} (expect True)");
        DevCapture.Shot("nightstand");
        var buttons = FindObjectsByType<CaptionButton>(FindObjectsSortMode.None);
        CaptionButton spring = null, keep = null;
        foreach (var b in buttons)
        {
            if (b.transform.parent != null && b.transform.parent.parent != null && b.transform.parent.parent.name == "SPRING") spring = b;
            if (b.name == "KeepReading") keep = b;
        }
        spring?.Button.onClick.Invoke();
        yield return WaitReal(0.3f);
        Log($"bought spring: level {gs.Spring}, capacity {torch.Charge.Capacity:0}, stars left {gs.StarsTotal} (expect 1, 80, 8)");
        DevCapture.Shot("nightstand_bought");
        keep?.Button.onClick.Invoke();
        for (float t = 0f; t < 5f && gs.Page != 4; t += Time.unscaledDeltaTime) yield return null;
        Log($"kept reading: page {gs.Page}, capacity {torch.Charge.Capacity:0} (expect 4, 80)");
    }

    // bodies: awake Inkies don't block Max, frozen ones are solid and standable, the dodge goes through
    // everything, the beam sweeping over an Inkie doesn't nudge it, and the slam comes down through the Bruiser
    private IEnumerator BodiesTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        PageManager.I.Load(1);
        hero.GetComponent<Health>().Invulnerable = 300f;
        yield return Wait(1.6f);
        while (hero.Locked) yield return null;                         // a new act's splash card holds him longer
        var pl = PageManager.I.Layout.tiers[0].panels[0];
        foreach (var e in pl.enemies.ToArray()) if (e != null) { PageManager.I.EnemyDown(e); Destroy(e.gameObject); }
        float floor = pl.rect.yMin + 2.5f;
        hero.Teleport(new Vector2(pl.rect.xMin + 2f, floor + 0.1f));
        var smudge = PageBuilder.Spawn(EnemyKind.Smudge, new Vector2(pl.rect.xMin + 5f, floor + 0.1f), pl, PageManager.I.transform);
        yield return Wait(0.8f);

        // 1. walk through an awake Smudge
        _pad.moveX = 1f;
        float sx = smudge.transform.position.x;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime) yield return null;
        _pad.moveX = 0f;
        Log($"walked through the awake Smudge: max x {hero.transform.position.x - sx:+0.0;-0.0} from it (expect > +0.8)");

        // 2. a frozen Smudge is a wall: the beam trails behind Max so the Smudge ahead stays dark
        hero.Teleport(new Vector2(pl.rect.xMin + 2f, floor + 0.1f));
        var cc = smudge.GetComponent<CharacterController>();
        cc.enabled = false;
        smudge.transform.position = new Vector3(pl.rect.xMin + 5f, floor + 0.05f, 0f);
        cc.enabled = true;
        _followHero = false;
        _pad.moveX = 1f;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            torch.AimOverride = (Vector2)hero.transform.position + new Vector2(-3.3f, 1f);
            yield return null;
        }
        _pad.moveX = 0f;
        Log($"walked into the frozen Smudge: awake {smudge.Light.IsAwake}, max stopped {smudge.transform.position.x - hero.transform.position.x:0.00} short of it (expect False, about 0.7)");
        DevCapture.Shot("bodies_wall");

        // 3. stand on it, then light it: he drops through
        hero.Teleport(new Vector2(smudge.transform.position.x, floor + 2.6f));
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            torch.AimOverride = (Vector2)hero.transform.position + new Vector2(-3.4f, 2.4f);
            yield return null;
        }
        Log($"on top of the frozen Smudge: max {hero.transform.position.y - floor:0.00} above the roof (expect about 1.3)");
        DevCapture.Shot("bodies_standing");
        torch.AimOverride = (Vector2)smudge.transform.position + Vector2.up;
        yield return Wait(0.6f);
        Log($"lit it: awake {smudge.Light.IsAwake}, max {hero.transform.position.y - floor:0.00} above the roof (expect True, about 0)");

        // 4. dodge through a frozen Smudge
        hero.Teleport(new Vector2(smudge.transform.position.x - 1.2f, floor + 0.1f));
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            torch.AimOverride = (Vector2)hero.transform.position + new Vector2(-3.3f, 1f);
            yield return null;
        }
        bool frozen = !smudge.Light.IsAwake;
        _pad.moveX = 1f;
        _pad.dodge = true;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            torch.AimOverride = (Vector2)hero.transform.position + new Vector2(-3.3f, 1f);
            yield return null;
        }
        _pad.moveX = 0f;
        Log($"dodged through the frozen Smudge (frozen {frozen}): max {hero.transform.position.x - smudge.transform.position.x:+0.0;-0.0} past it (expect True, > +1)");

        // 5. sweeping the beam on and off a Smudge frozen mid-walk doesn't nudge it
        hero.Teleport(new Vector2(pl.rect.xMin + 1.5f, floor + 0.1f));
        torch.AimOverride = (Vector2)smudge.transform.position + Vector2.up;
        yield return Wait(0.6f);                                         // it walks toward Max
        torch.AimOverride = new Vector2(pl.rect.xMax + 20f, floor);       // dark, mid-walk
        yield return Wait(0.3f);
        float x0 = smudge.transform.position.x;
        for (int i = 0; i < 6; i++)
        {
            torch.AimOverride = (Vector2)smudge.transform.position + Vector2.up;
            yield return Wait(0.12f);
            torch.AimOverride = new Vector2(pl.rect.xMax + 20f, floor);
            yield return Wait(0.12f);
        }
        Log($"six quick sweeps of the beam: the Smudge moved {Mathf.Abs(smudge.transform.position.x - x0):0.000} (expect 0)");
        _followHero = true;

        // 6. the Bruiser: walk through him, and slam down through him onto the roof
        PageManager.I.Load(4);
        hero.GetComponent<Health>().Invulnerable = 300f;
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;                           // the act and page cards
        PageManager.I.GoToTier(1);
        var arena = PageManager.I.Layout.tiers[1].panels[0];
        BruiserBrain br = null;
        foreach (var e in arena.enemies) if (e is BruiserBrain b) br = b;
        float roof = arena.rect.yMin + 2.5f;
        hero.Teleport(new Vector2(br.transform.position.x - 2.2f, roof + 0.1f));
        yield return Wait(0.2f);
        _pad.moveX = 1f;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime) yield return null;
        _pad.moveX = 0f;
        Log($"walked through the Bruiser: max {hero.transform.position.x - br.transform.position.x:+0.0;-0.0} from him (expect > +0.5)");
        yield return Wait(1.6f);                                         // let his swing play out
        _followHero = false;
        torch.AimOverride = new Vector2(br.transform.position.x, roof + 2.8f);   // the beam on him first
        yield return Wait(0.5f);
        float hp0 = br.Health.hp;
        hero.Teleport(new Vector2(br.transform.position.x + 0.1f, roof + 3.6f));
        yield return null;
        _pad.down = true;
        _pad.kick = true;
        for (float t = 0f; t < 1.2f && !hero.Grounded; t += Time.deltaTime) yield return null;
        _pad.down = false;
        yield return Wait(0.1f);
        Log($"slammed onto the Bruiser: max {hero.transform.position.y - roof:0.00} above the roof, bruiser hp {hp0:0} -> {br.Health.hp:0} (expect about 0, -10)");
        DevCapture.Shot("bodies_slam");
        _followHero = true;
    }

    // pictures of the printing: a page from each act, the beam on Max
    private IEnumerator LookTest()
    {
        var hero = HeroController.I;
        hero.GetComponent<Health>().Invulnerable = 300f;
        foreach (int page in new[] { 1, 5, 8 })
        {
            PageManager.I.Load(page);
            yield return Wait(0.5f);
            while (hero.Locked) yield return null;
            yield return Wait(0.8f);
            DevCapture.Shot($"look_page{page}");
        }
    }

    // the Bruiser can be beaten with nothing but the real controls: bait his slam, dodge through him
    // while he winds up, and combo his unarmoured back while he's committed
    private IEnumerator BruiserKnockout()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        PageManager.I.Load(4);
        hero.GetComponent<Health>().Invulnerable = 600f;
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        PageManager.I.GoToTier(1);
        var arena = PageManager.I.Layout.tiers[1].panels[0];
        BruiserBrain br = null;
        foreach (var e in arena.enemies) if (e is BruiserBrain b) br = b;
        float roof = arena.rect.yMin + 2.5f;
        hero.Teleport(new Vector2(br.transform.position.x - 4f, roof + 0.1f));
        _followHero = false;
        int dodges = 0, combos = 0, clanks = 0;
        float hp0 = br.Health.hp, t = 0f, punchGap = 0f;
        br.Health.Hurt += h => { if (h.word == "CLANK!") clanks++; };
        while (t < 40f && !br.Health.Dead)
        {
            t += Time.deltaTime;
            punchGap -= Time.deltaTime;
            Vector2 me = hero.transform.position, him = br.transform.position;
            torch.AimOverride = (me + him) * 0.5f + Vector2.up * 1.2f;        // both in the light
            float toHim = Mathf.Sign(him.x - me.x);
            bool behind = Mathf.Sign(me.x - him.x) == -br.Facing;           // he's looking the other way
            float dist = Mathf.Abs(him.x - me.x);
            _pad.moveX = 0f;
            if (behind && br.Mode != EnemyBrain.State.Hurt)
            {
                if (dist > 1.3f) _pad.moveX = toHim;
                else if (punchGap <= 0f)
                {
                    if (hero.Facing != toHim) _pad.moveX = toHim * 0.2f;
                    _pad.punch = true;
                    punchGap = 0.3f;
                    combos++;
                }
            }
            else if (br.Mode == EnemyBrain.State.Windup && dist < 3.2f && !hero.Dodging)
            {
                _pad.moveX = toHim;                                          // through him, to his back
                _pad.dodge = true;
                dodges++;
            }
            else if (br.Mode != EnemyBrain.State.Windup && br.Mode != EnemyBrain.State.Attack)
            {
                // bait: step into his reach so he commits to a slam
                _pad.moveX = dist > 2f ? toHim : 0f;
            }
            yield return null;
        }
        _pad.moveX = 0f;
        Log($"bruiser knockout: dead {br.Health.Dead} after {t:0.0} s, hp {hp0:0} -> {br.Health.hp:0}, dodges {dodges}, punches {combos}, clanks {clanks} (expect True)");
        DevCapture.Shot("bruiser_ko");
        _followHero = true;
    }

    // speech bubbles hold Max up; presses stamp only when lit and flatten Inkies; the belt only runs
    // while its motor is lit
    private IEnumerator MachinesTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        hero.GetComponent<Health>().Invulnerable = 300f;

        // 1. the pigeon's COO? on page 1
        PageManager.I.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var p1 = PageManager.I.Layout.tiers[0].panels[0];
        hero.Teleport(p1.rect.min + new Vector2(7.9f, 6.6f));
        yield return Wait(0.8f);
        Log($"on the COO? bubble: max at {hero.transform.position.y - p1.rect.yMin:0.00} (expect about 5.2, not 2.5)");
        DevCapture.Shot("machines_bubble");

        // 2. the presses on page 5
        PageManager.I.Load(5);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        PageManager.I.GoToTier(1);
        var room = PageManager.I.Layout.tiers[1].panels[1];
        var presses = room.root.GetComponentsInChildren<Press>();
        Log($"press room: {presses.Length} presses (expect 2)");
        var press = presses[0];
        _followHero = false;
        hero.Teleport(room.rect.min + new Vector2(1f, 2.6f));
        var smudge = PageBuilder.Spawn(EnemyKind.Smudge, new Vector2(press.transform.position.x, room.rect.yMin + 2.6f), room, PageManager.I.transform);
        smudge.enabled = false;                                                   // it stays put under the press (it'd walk off after Max)
        torch.AimOverride = new Vector2(press.transform.position.x, room.rect.yMin + 4.5f);
        float low = float.MaxValue;
        for (float t = 0f; t < 3.2f; t += Time.deltaTime)
        {
            low = Mathf.Min(low, press.transform.position.y - room.rect.yMin);
            yield return null;
        }
        Log($"a lit press: lowest {low:0.00} above the panel floor (expect about 2.95), the Smudge under it {(smudge == null || smudge.Health.Dead ? "flattened" : "still standing, hp " + smudge.Health.hp)} (expect flattened)");
        DevCapture.Shot("machines_press");
        torch.AimOverride = new Vector2(room.rect.xMax + 30f, room.rect.yMin);
        yield return Wait(0.3f);
        float y0 = press.transform.position.y;
        yield return Wait(1.2f);
        Log($"a dark press: moved {Mathf.Abs(press.transform.position.y - y0):0.000} in 1.2 s (expect 0)");

        // 3. the conveyor on page 4
        PageManager.I.Load(4);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        PageManager.I.GoToTier(1);
        var belt = PageManager.I.Layout.tiers[1].panels[1];
        hero.Teleport(belt.rect.min + new Vector2(7f, 2.6f));
        torch.AimOverride = belt.rect.min + new Vector2(10.5f, 4.5f);       // Max lit, the motor (at x 2) dark
        yield return Wait(0.4f);
        float x0 = hero.transform.position.x;
        yield return Wait(1f);
        Log($"motor dark: the belt moved max {hero.transform.position.x - x0:+0.00;-0.00} (expect 0)");
        torch.AimOverride = belt.rect.min + new Vector2(4.5f, 3.5f);        // both lit
        yield return Wait(0.4f);
        x0 = hero.transform.position.x;
        yield return Wait(1f);
        Log($"motor lit: the belt moved max {hero.transform.position.x - x0:+0.00;-0.00} in 1 s (expect < -1: carried back to its start)");
        DevCapture.Shot("machines_belt");
        _followHero = true;
    }

    // how fast Max really runs, on a plain roof (page 1) and in the Inkworks (page 4)
    private IEnumerator SpeedTest()
    {
        var hero = HeroController.I;
        foreach (int page in new[] { 1, 4 })
        {
            PageManager.I.Load(page);
            yield return Wait(0.5f);
            while (hero.Locked) yield return null;
            yield return Wait(0.3f);
            float x0 = hero.transform.position.x, t = 0f;
            string samples = "";
            _pad.moveX = 1f;
            int frame = 0;
            while (t < 1f)
            {
                t += Time.deltaTime;
                if (frame++ % 15 == 0) samples += $" {hero.Velocity.x:0.0}@{t:0.00}";
                yield return null;
            }
            _pad.moveX = 0f;
            var torch = TorchController.I;
            Log($"page {page}: ran {hero.transform.position.x - x0:0.00} in {t:0.00} s; velocity{samples} (awake {hero.Light.IsAwake}, locked {hero.Locked}, hushed {hero.Hushed}, torch on {torch.On} charge {torch.Charge.Charge:0} dead {torch.Charge.Dead}, beam live {LightField.I.BeamLive} at {LightField.I.BeamCentre} r {LightField.I.BeamRadius:0.0}, max at {(Vector2)hero.transform.position})");
            DevCapture.Shot($"speed_page{page}");
            yield return Wait(0.5f);
        }
    }

    // the playtest notes of 4 Oct: dialogue, lettering, the belt, bars, BUSTED, ledges, bats, props, perches, captions
    private IEnumerator FixesTest()
    {
        var hero = HeroController.I;
        var torch = TorchController.I;
        var hud = GameHUD.I;
        hero.GetComponent<Health>().Invulnerable = 600f;

        // 1. a long line lasts long enough, and lettering keeps clear of it
        PageManager.I.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        string line = "This is a long line of dialogue, long enough to need some time to read it all.";
        hud.HeroSays(line, 4f);
        yield return Wait(0.2f);
        bool has = hud.BubbleLane(out var balloon);
        Vector2 inside = balloon.center;
        SfxLettering.Spawn("KAPOW!", inside, Palette.Yellow, 1.25f, burst: true);
        yield return null;
        var pop = GameObject.Find("SFX KAPOW!");
        bool clear = pop != null && !balloon.Contains((Vector2)pop.transform.position);
        Log($"long line: balloon showing {has}; KAPOW printed clear of it {clear} (expect True, True)");
        DevCapture.Shot("fixes_balloon");
        yield return Wait(4.8f);
        Log($"after 5 s the long line is still up: {hud.BubbleLane(out _)} (expect True: it gets {2.4f + line.Length * 0.07f:0.0} s)");

        // 2. the conveyor beats a run
        PageManager.I.Load(4);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        PageManager.I.GoToTier(1);
        var beltPanel = PageManager.I.Layout.tiers[1].panels[1];
        _followHero = false;
        hero.Teleport(beltPanel.rect.min + new Vector2(5f, 2.6f));
        torch.AimOverride = beltPanel.rect.min + new Vector2(3f, 3.5f);          // the motor and Max both lit
        yield return Wait(1.0f);                                                  // the beam settles; the belt has him
        float x0 = hero.transform.position.x;
        _pad.moveX = 1f;
        yield return Wait(1.2f);
        _pad.moveX = 0f;
        Log($"running against the lit belt for 1.2 s: moved {hero.transform.position.x - x0:+0.00;-0.00} (expect about 0: the belt is faster than he runs)");
        torch.AimOverride = beltPanel.rect.min + new Vector2(8.5f, 3.5f);        // motor dark, Max lit
        hero.Teleport(beltPanel.rect.min + new Vector2(6f, 2.6f));
        yield return Wait(0.4f);
        x0 = hero.transform.position.x;
        _pad.moveX = 1f;
        yield return Wait(0.8f);
        _pad.moveX = 0f;
        Log($"motor dark: ran {hero.transform.position.x - x0:+0.00;-0.00} in 0.8 s (expect about +5)");

        // 3. the page 1 crate: lit, it falls out of its panel; it is drawn back in
        PageManager.I.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        PageManager.I.GoToTier(1);
        var cratePanel = PageManager.I.Layout.tiers[1].panels[1];
        var crate = cratePanel.root.GetComponentInChildren<FallingProp>();
        Vector3 home = crate.transform.position;
        hero.Teleport(cratePanel.rect.min + new Vector2(1f, 2.6f));
        torch.AimOverride = (Vector2)home;
        float pit = cratePanel.rect.yMin + 1.0f;                                  // its panel's floor line: below it, it's lost
        for (float t = 0f; t < 3f && crate.transform.position.y > pit - 0.3f; t += Time.deltaTime)
        {
            torch.AimOverride = (Vector2)crate.transform.position;              // lit all the way down the gap
            yield return null;
        }
        Log($"lit crate fell: {crate.transform.position.y < home.y - 1f} (expect True)");
        torch.AimOverride = cratePanel.rect.min + new Vector2(1f, 4f);
        yield return Wait(2.5f);
        Log($"crate drawn back in: {(crate.transform.position - home).magnitude < 0.05f} (expect True)");

        // 4. with the follow assist the crate stays dark under him
        torch.AimOverride = null;
        hero.Teleport(new Vector2(home.x, home.y + 1.0f));
        _pad.followHeld = true;
        yield return Wait(1.2f);
        bool crateDark = !crate.GetComponent<Lightable>().IsAwake;
        Log($"standing on the frozen crate, follow held: crate dark {crateDark}, max lit {hero.Light.IsAwake}, max y {hero.transform.position.y - cratePanel.rect.yMin:0.00} (expect True, True, about 2.5)");
        DevCapture.Shot("fixes_perch");
        _pad.followHeld = false;

        // 5. a bat over the ink comes back after it dies
        PageManager.I.Load(8);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var batPanel = PageManager.I.Layout.tiers[0].panels[1];
        SplotchBrain bat = null;
        int bats = 0;
        foreach (var e in batPanel.enemies) if (e is SplotchBrain b) { bats++; if (bat == null) bat = b; }
        hero.Teleport(batPanel.rect.min + new Vector2(1.5f, 5.4f));
        torch.AimOverride = (Vector2)bat.transform.position + Vector2.up;
        yield return Wait(0.5f);
        bat.Health.Apply(new Hit { damage = 50f, team = Team.Hero, source = hero.gameObject });
        yield return Wait(5f);
        int after = 0;
        foreach (var e in batPanel.enemies) if (e is SplotchBrain && e != null && !e.Health.Dead) after++;
        Log($"a bat over the ink died: bats {bats} -> {after} after 5 s (expect {bats})");

        // 6. the Dot-Shot on page 9 stays on its floor when it backs away
        PageManager.I.Load(9);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var shooterPanel = PageManager.I.Layout.tiers[0].panels[0];
        EnemyBrain shooter = null;
        foreach (var e in shooterPanel.enemies) if (e is DotShotBrain) shooter = e;
        float sy = shooter.transform.position.y;
        torch.AimOverride = null;
        _followHero = true;
        hero.Teleport(shooterPanel.rect.min + new Vector2(6f, 3.7f));
        yield return Wait(6f);
        bool standing = shooter != null && Mathf.Abs(shooter.transform.position.y - sy) < 0.3f;
        Log($"the Dot-Shot backed away for 6 s: still on its floor {standing} (expect True)");

        // 7. the boss bar falls with his health, and only shows on his tier
        PageManager.I.Load(6);
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var barBox = GameObject.Find("BossBox");
        Log($"tier 1 of page 6 (Blot is on tier 2): boss bar showing {barBox != null && barBox.activeInHierarchy} (expect False)");
        PageManager.I.GoToTier(1);
        var office = PageManager.I.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in office.enemies) if (e is BlotBrain bb) blot = bb;
        hero.Teleport(new Vector2(blot.transform.position.x - 5f, office.rect.yMin + 2.6f));
        _followHero = false;
        torch.AimOverride = (Vector2)blot.transform.position + new Vector2(-2f, 1.5f);
        yield return Wait(1f);
        blot.Health.hp = BlotBrain.MaxHp * 2f / 3f;
        yield return Wait(0.8f);
        UnityEngine.UI.Image fill = null;
        foreach (var img in GameHUD.I.GetComponentsInChildren<UnityEngine.UI.Image>(true)) if (img.name == "Fill" && img.transform.parent.name == "Back") fill = img;
        Log($"blot at 2/3 of {BlotBrain.MaxHp:0}: fill {(fill != null ? fill.fillAmount : -1f):0.00} with a sprite {fill != null && fill.sprite != null}, bar showing {fill != null && fill.gameObject.activeInHierarchy} (expect 0.67, True, True)");

        // 8. BUSTED doesn't heal
        PageManager.I.Load(3);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        _followHero = true;
        torch.AimOverride = null;
        var h = hero.GetComponent<Health>();
        h.Invulnerable = 0f;
        h.hp = 2f;
        PageManager.I.Busted();
        yield return Wait(0.5f);
        for (float t = 0f; t < 6f && PageManager.I.Busy; t += Time.deltaTime) yield return null;
        Log($"busted at 2 hearts: hearts after the restart {h.hp:0} (expect 2)");
        h.Invulnerable = 600f;

        // 9. the first panel's caption stays on the page when the camera has moved on
        PageManager.I.Load(5);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var p5 = PageManager.I.Layout.tiers[0];
        hero.Teleport(new Vector2(p5.maxX - 3f, p5.y0 + 4.2f));
        yield return Wait(1.5f);
        var cap = p5.panels[0].root.GetComponentInChildren<CaptionInView>();
        float viewL = PageCamera.I.ViewportToLane(new Vector2(0f, 0.5f)).x;
        Log($"panel 1's caption with the camera panned right: left edge {cap.transform.position.x - viewL:+0.00;-0.00} inside the page edge (expect >= 0)");
        DevCapture.Shot("fixes_caption");
        _followHero = true;
    }

    // Mom's light falls on the trouble, and while her door is open Max holds still (and can't be hurt)
    private IEnumerator MomLightTest()
    {
        var hero = HeroController.I;
        var mom = MomDirector.I;
        var torch = TorchController.I;
        var pm = PageManager.I;
        pm.Load(5);
        yield return Wait(0.5f);
        mom.Schedule(ScriptableObject.CreateInstance<PageDef>());       // no visits but ours
        while (hero.Locked) yield return null;
        var panel = pm.Layout.tiers[0].panels[0];
        hero.Teleport(panel.rect.min + new Vector2(1.5f, 2.6f));
        yield return Wait(0.5f);

        // 1. torch off: her wedge alone, and it lands on an Inkie
        torch.SetOn(false);
        mom.VisitNow(opens: true);
        for (float t = 0f; t < 14f && !(mom.State == MomState.Opening && mom.Open >= 1f); t += Time.deltaTime) yield return null;
        yield return Wait(0.3f);
        int lit = 0, all = 0;
        string who = "";
        foreach (var e in panel.enemies)
        {
            if (e == null) continue;
            all++;
            if (e.Light.IsAwake) { lit++; who += e.GetType().Name.Replace("Brain", "") + $"@{e.transform.position.x - panel.rect.xMin:0.0} "; }
        }
        var w = LightField.I.Wedges.Count > 0 ? LightField.I.Wedges[0] : null;
        string spot = w != null ? $"{(w[2].x + w[3].x) * 0.5f - panel.rect.xMin:0.0}" : "none";
        Log($"door open, torch off: inkies lit by her light {lit}/{all} [{who}], wedge foot at {spot} (expect >= 1 lit)");
        DevCapture.Shot("momlight_wedge");
        for (float t = 0f; t < 8f && mom.State != MomState.Asleep; t += Time.deltaTime) yield return null;

        // 2. torch off, Max stood in her light (awake in it, like the Inkies): he holds still while the door is open
        hero.Teleport(panel.rect.min + new Vector2(1.5f, 2.6f));
        yield return Wait(0.4f);
        mom.VisitNow(opens: true);
        for (float t = 0f; t < 14f && !(mom.State == MomState.Opening && mom.Open >= 1f); t += Time.deltaTime) yield return null;
        w = LightField.I.Wedges.Count > 0 ? LightField.I.Wedges[0] : null;
        if (w != null)
        {
            float feet = panel.rect.yMin + 2.6f, k = (w[0].y - feet) / (w[0].y - w[3].y);
            hero.Teleport(new Vector2(Mathf.Lerp((w[0].x + w[1].x) * 0.5f, (w[2].x + w[3].x) * 0.5f, k) - 1.5f, feet));
        }
        yield return Wait(0.3f);
        Vector2 p0 = hero.transform.position;
        _pad.moveX = 1f;
        _pad.jump = true;
        _pad.jumpHeld = true;
        yield return Wait(1f);
        _pad.moveX = 0f;
        _pad.jumpHeld = false;
        _pad.jump = false;                                  // never read while he held still: don't let it fire later
        Vector2 moved = (Vector2)hero.transform.position - p0;
        var hh = hero.GetComponent<Health>();
        bool says = GameHUD.I.BubbleLane(out _);
        Log($"door open, Max in her light: awake {hero.Light.IsAwake}, hushed {hero.Hushed}, moved {moved.x:+0.00;-0.00} / {moved.y:+0.00;-0.00}, can't be hurt {hh.Invulnerable > 0f}, 'shh' balloon {says} (expect True, True, ~0 / ~0, True, True)");
        DevCapture.Shot("momlight_hush");
        for (float t = 0f; t < 8f && mom.State != MomState.Asleep; t += Time.deltaTime) yield return null;
        torch.SetOn(true);
        yield return Wait(0.2f);
        float x0 = hero.transform.position.x;
        _pad.moveX = 1f;
        yield return Wait(0.5f);
        _pad.moveX = 0f;
        Log($"she's gone: hushed {hero.Hushed}, ran {hero.transform.position.x - x0:+0.00;-0.00}, suspicion {mom.Suspicion:0}, slippers {GameState.I.Slippers} (expect False, about +3, < 100, 3)");
    }

    // Baron Blot's new tricks: geysers through the floor, the lunge, the blink when beaten on, the vat's second wave
    private IEnumerator BlotMovesTest()
    {
        var hero = HeroController.I;
        var pm = PageManager.I;
        var torch = TorchController.I;
        pm.Load(6);
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        hero.GetComponent<Health>().Invulnerable = 600f;
        pm.GoToTier(1);
        var office = pm.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in office.enemies) if (e is BlotBrain b) blot = b;
        hero.Teleport(new Vector2(blot.transform.position.x - 5f, office.rect.yMin + 2.6f));
        _followHero = false;
        var geysers = new System.Collections.Generic.HashSet<int>();
        var waves = new System.Collections.Generic.HashSet<int>();
        int lunges = 0;
        bool lunging = false, shotGeyser = false, shotLunge = false, shotPuddle = false;
        float geyserY = float.NaN;
        for (float t = 0f; t < 32f; t += Time.deltaTime)
        {
            torch.AimOverride = ((Vector2)blot.transform.position + (Vector2)hero.transform.position) * 0.5f + Vector2.up * 1.4f;
            foreach (var g in FindObjectsByType<InkGeyser>(FindObjectsSortMode.None))
                if (geysers.Add(g.GetInstanceID()) && float.IsNaN(geyserY)) geyserY = g.transform.position.y;
            foreach (var wv in FindObjectsByType<InkWave>(FindObjectsSortMode.None)) waves.Add(wv.GetInstanceID());
            bool now = Mathf.Abs(blot.Velocity.x) > 7f;
            if (now && !lunging) lunges++;
            lunging = now;
            foreach (var live in FindObjectsByType<InkGeyser>(FindObjectsSortMode.None))
            {
                if (!shotGeyser && live.transform.Find("Column").localScale.y > 1.2f) { shotGeyser = true; DevCapture.Shot("blot_geysers"); }
                if (!shotPuddle && !live.transform.Find("Column").gameObject.activeSelf && live.transform.Find("Puddle").localScale.x > 1.4f) { shotPuddle = true; DevCapture.Shot("blot_puddles"); }
            }
            if (!shotLunge && now) { shotLunge = true; DevCapture.Shot("blot_lunge"); }
            yield return null;
        }
        Log($"32 s in the office: geysers {geysers.Count}, waves {waves.Count}, lunges {lunges} (expect all > 0); first geyser {geyserY - office.rect.yMin:0.00} above the panel floor (expect 2.50)");

        // the blink: 36 damage in quick succession and he melts away, to pop up across the room
        for (float t = 0f; t < 3f && Mathf.Abs(blot.Velocity.x) > 7f; t += Time.deltaTime) yield return null;   // not mid-lunge
        Vector3 at = blot.transform.position;
        float hp0 = blot.Health.hp;
        for (int i = 0; i < 3; i++)
        {
            blot.Health.Invulnerable = 0f;
            blot.Health.Apply(new Hit { damage = 12f, team = Team.Hero, source = hero.gameObject, knockback = new Vector2(2f, 0f) });
            yield return Wait(0.15f);
        }
        DevCapture.Shot("blot_blink");
        yield return Wait(1.2f);
        bool shown = true;
        foreach (var r in blot.GetComponentsInChildren<SkinnedMeshRenderer>()) shown &= r.enabled;
        Log($"blink after 36 damage: moved {Mathf.Abs(blot.transform.position.x - at.x):0.0} (expect > 4), hp {hp0:0} -> {blot.Health.hp:0}, shown again {shown}, still in the panel {blot.transform.position.x > office.rect.xMin && blot.transform.position.x < office.rect.xMax} (expect True, True)");

        // the vat: every wave has a twin a beat behind it
        pm.Load(8);
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        hero.GetComponent<Health>().Invulnerable = 600f;
        pm.GoToTier(1);
        var vat = pm.Layout.tiers[1].panels[1];
        blot = null;
        foreach (var e in vat.enemies) if (e is BlotBrain b) blot = b;
        hero.Teleport(new Vector2(blot.transform.position.x - 6.5f, vat.rect.yMin + 2.6f));
        var times = new System.Collections.Generic.List<float>();
        waves.Clear();
        int stale = FindObjectsByType<InkWave>(FindObjectsSortMode.None).Length;
        Log($"waves left over from the office after the page turned: {stale} (expect 0)");
        for (float t = 0f; t < 30f && times.Count < 2; t += Time.deltaTime)
        {
            torch.AimOverride = ((Vector2)blot.transform.position + (Vector2)hero.transform.position) * 0.5f + Vector2.up * 1.4f;
            foreach (var wv in FindObjectsByType<InkWave>(FindObjectsSortMode.None))
                if (waves.Add(wv.GetInstanceID())) times.Add(t);
            yield return null;
        }
        Log($"the vat: waves {times.Count}, the twin {(times.Count >= 2 ? times[1] - times[0] : -1f):0.00} s behind (expect 2, about 0.5)");
        _followHero = true;
    }

    // controls: rebinding swaps, names follow the bindings into captions, DEFAULTS, and the Controls page in the Bookmark
    private IEnumerator ControlsTest()
    {
        var mine = Bindings.Snapshot();
        Bindings.ResetDefaults();
        Bindings.Set(Bindings.Act.Jump, 0, new Bindings.Control(Bindings.Kind.Key, (int)UnityEngine.InputSystem.Key.K));
        Log($"jump on K: '{Bindings.Name(Bindings.Act.Jump)}', '{Bindings.Format("PRESS {JUMP}")}' (expect 'K', 'PRESS K')");
        Bindings.Set(Bindings.Act.Punch, 0, new Bindings.Control(Bindings.Kind.Key, (int)UnityEngine.InputSystem.Key.K));
        Log($"punch onto K: punch '{Bindings.Name(Bindings.Act.Punch)}', jump '{Bindings.Name(Bindings.Act.Jump)}' (expect 'K', 'E': they swap)");
        Bindings.Set(Bindings.Act.Crank, 0, new Bindings.Control(Bindings.Kind.Wheel));
        Bindings.Set(Bindings.Act.Lens, 0, new Bindings.Control(Bindings.Kind.Wheel));
        Log($"wheel to the lens: lens '{Bindings.SlotName(Bindings.Act.Lens, 0)}', crank '{Bindings.SlotName(Bindings.Act.Crank, 0)}' (expect 'SCROLL', 'MIDDLE CLICK')");
        Bindings.ResetDefaults();
        Log($"defaults: jump '{Bindings.Name(Bindings.Act.Jump)}', punch '{Bindings.Name(Bindings.Act.Punch)}', follow '{Bindings.Name(Bindings.Act.Follow)}', torch '{Bindings.Name(Bindings.Act.Torch)}' (expect SPACE, E, LEFT MOUSE, RIGHT CLICK)");

        // a caption on the page re-letters itself
        PageManager.I.Load(1);
        yield return Wait(0.5f);
        while (HeroController.I.Locked) yield return null;
        TMPro.TextMeshPro cap = null;
        foreach (var t in PageManager.I.Layout.tiers[0].panels[1].root.GetComponentsInChildren<TMPro.TextMeshPro>())
            if (t.gameObject.name == "Caption") cap = t;
        string before = cap != null ? cap.text : "(none)";
        float w0 = cap != null ? cap.rectTransform.sizeDelta.x : 0f;
        Bindings.Set(Bindings.Act.Follow, 0, new Bindings.Control(Bindings.Kind.Key, (int)UnityEngine.InputSystem.Key.H));
        yield return null;
        Log($"caption '{before}' -> '{(cap != null ? cap.text : "(none)")}', box {w0:0.00} -> {(cap != null ? cap.rectTransform.sizeDelta.x : 0f):0.00} (expect LEFT MOUSE -> H)");
        Bindings.ResetDefaults();

        // the Controls page, from the Bookmark
        var pause = FindFirstObjectByType<BookmarkPause>();
        pause.Pause();
        yield return WaitReal(0.3f);
        OptionRow row = null;
        foreach (var r in FindObjectsByType<OptionRow>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (r.name == "Row Controls" && r.gameObject.activeInHierarchy) row = r;
        row?.OnSubmit(null);
        yield return WaitReal(0.4f);
        var modal = GameObject.Find("ControlsModal");
        Log($"bookmark > controls: row {row != null}, page open {modal != null && modal.activeInHierarchy} (expect True, True)");
        DevCapture.Shot("controls_page");
        Bindings.Set(Bindings.Act.Kick, 1, new Bindings.Control(Bindings.Kind.Mouse, 4));
        yield return WaitReal(0.1f);
        CaptionButton defaults = null, back = null;
        if (modal != null)
            foreach (var b in modal.GetComponentsInChildren<CaptionButton>()) { if (b.name == "DefaultsButton") defaults = b; if (b.name == "BackButton") back = b; }
        defaults?.Button.onClick.Invoke();
        yield return WaitReal(0.2f);
        Log($"DEFAULTS: kick spare '{Bindings.SlotName(Bindings.Act.Kick, 1)}' (expect '--')");
        back?.Button.onClick.Invoke();
        yield return WaitReal(0.2f);
        Log($"BACK: page open {modal != null && modal.activeInHierarchy}, still paused {BookmarkPause.IsPaused} (expect False, True)");
        pause.Resume();
        Bindings.Restore(mine);
    }

    // after Max goes down, nothing he "does" lands: not a punch during his fall, not a slam that was on its way
    private IEnumerator DeathTest()
    {
        var hero = HeroController.I;
        var pm = PageManager.I;
        var h = hero.GetComponent<Health>();

        // 1. knocked out next to a Smudge, then mashing punch and kick through the death animation
        pm.Load(1);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        pm.GoToTier(1);
        var panel = pm.Layout.tiers[1].panels[0];
        var smudge = panel.enemies[0];
        hero.Teleport(new Vector2(smudge.transform.position.x - 1.2f, panel.rect.yMin + 2.6f));
        hero.Face(1f);
        yield return Wait(0.6f);
        GameState.I.Splash = 0f;                            // a full meter can't show a hit
        float s0 = smudge.Health.hp, splash0 = GameState.I.Splash;
        h.Invulnerable = 0f;
        h.hp = 1f;
        h.Apply(new Hit { hearts = 1, team = Team.Inkie, source = smudge.gameObject, knockback = new Vector2(-2f, 3f), stun = 0.3f, word = "BAM!" });
        for (float t = 0f; t < 0.9f; t += 0.1f)
        {
            _pad.punch = true;
            _pad.kick = true;
            yield return Wait(0.1f);
        }
        Log($"punching through his own knockout: max dead {hero.Dead}, smudge hp {s0:0} -> {smudge.Health.hp:0}, splash {splash0:0} -> {GameState.I.Splash:0} (expect True, unchanged, unchanged)");
        for (float t = 0f; t < 6f && (hero.Dead || hero.Locked); t += Time.deltaTime) yield return null;

        // 2. a Smudge that's down: its melting body takes no more hits
        pm.GoToTier(1);
        panel = pm.Layout.tiers[1].panels[0];
        smudge = panel.enemies[0];
        hero.Teleport(new Vector2(smudge.transform.position.x - 1.2f, panel.rect.yMin + 2.6f));
        hero.Face(1f);
        h.Invulnerable = 600f;
        yield return Wait(0.6f);
        smudge.Health.Apply(new Hit { damage = 999f, team = Team.Hero, source = hero.gameObject });
        GameState.I.Splash = 0f;
        splash0 = GameState.I.Splash;
        for (int i = 0; i < 3; i++)
        {
            _pad.punch = true;
            yield return Wait(0.3f);
        }
        Log($"punching a melting Smudge: splash {splash0:0} -> {GameState.I.Splash:0} (expect unchanged: no hit lands)");

        // 3. killed (by the ink, not a blow) in the middle of a ground slam: the body landing is no slam
        pm.Load(4);
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        EnemyBrain bruiser = null;
        int tierOf = 0;
        for (int ti = 0; ti < pm.Layout.tiers.Count; ti++)
            foreach (var pl in pm.Layout.tiers[ti].panels)
                foreach (var e in pl.enemies)
                    if (e is BruiserBrain) { bruiser = e; tierOf = ti; }
        if (bruiser == null) { Log("no Bruiser on page 4"); yield break; }
        if (tierOf > 0) pm.GoToTier(tierOf);
        hero.Teleport(new Vector2(bruiser.transform.position.x - 0.6f, bruiser.transform.position.y + 4.5f));
        _followHero = false;
        TorchController.I.AimOverride = (Vector2)bruiser.transform.position + Vector2.up * 2f;
        yield return Wait(0.1f);
        float b0 = bruiser.Health.hp;
        _pad.down = true;
        _pad.kick = true;
        yield return Wait(0.05f);
        h.Invulnerable = 0f;
        h.hp = 0f;                                         // gone without a blow (drowned, fallen)
        yield return Wait(1.2f);
        _pad.down = false;
        Log($"slam cut short by a knockout: bruiser hp {b0:0} -> {bruiser.Health.hp:0} (expect unchanged)");
        _followHero = true;
        for (float t = 0f; t < 6f && (hero.Dead || hero.Locked); t += Time.deltaTime) yield return null;
    }

    // the punch chain, frame by frame: what's playing, whether a move is on, and the frame time
    private IEnumerator ComboTrace()
    {
        var hero = HeroController.I;
        var combat = hero.GetComponent<HeroCombat>();
        PageManager.I.Load(1);
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        var flat = PageManager.I.Layout.tiers[0].panels[0];
        hero.Teleport(flat.rect.min + new Vector2(2.5f, 2.6f));
        hero.Face(1f);
        yield return Wait(1.6f);
        string trace = "";
        float t = 0f;
        _pad.punch = true;
        for (int f = 0; t < 1.2f; f++)
        {
            yield return null;
            t += Time.deltaTime;
            if (f == 6 || f == 30) _pad.punch = true;               // the 2nd and 3rd presses, mid-swing
            trace += $"{t:0.00}:{hero.Clips.Current}{(combat.Busy ? "*" : "")}{(hero.Locked ? "L" : "")}{(hero.Hushed ? "H" : "")}{(hero.Hurting ? "h" : "")} ";
        }
        Log("combo trace: " + trace);
    }

    private IEnumerator WaitReal(float s)
    {
        for (float t = 0f; t < s; t += Time.unscaledDeltaTime) yield return null;
    }

    private IEnumerator CrankTest()
    {
        var torch = TorchController.I;
        var c = torch.Charge;
        c.Charge = c.Capacity * 0.18f;
        yield return Wait(0.6f);
        DevCapture.Shot("crank_low");
        float before = c.Charge;
        for (int i = 0; i < 6; i++)
        {
            _pad.twists = 1;
            yield return Wait(0.06f);
            DevCapture.Shot($"crank_{i}");
        }
        Log($"6 twists: charge {before:0.0} -> {c.Charge:0.0}, beam brightness {LightField.I.Brightness:0.00} while cranking");
        yield return Wait(0.8f);
        Log($"settled: brightness {LightField.I.Brightness:0.00}");
        DevCapture.Shot("crank_settled");
        c.Charge = c.Capacity;
        _pad.twists = 2;
        yield return Wait(0.15f);
        DevCapture.Shot("crank_overwind");
        Log($"overwind {c.Overwind}");
        torch.Lenses.Unlock(Lens.Ghost);
        _pad.lensStep = 1;
        yield return Wait(0.35f);
        DevCapture.Shot("crank_ghost");
        Log($"lens now {torch.Lenses.Current} (expect Ghost)");
        torch.Lenses.Pick((int)Lens.Clear);
    }

    /// <summary>From the title: press "start reading", follow the cut into the game and the comic
    /// opening, and shoot frames along the way.</summary>
    private IEnumerator OpenFromTitleTest()
    {
        DontDestroyOnLoad(gameObject);
        CaptionButton start = null;
        for (float t = 0f; t < 40f && start == null; t += Time.unscaledDeltaTime)
        {
            foreach (var b in FindObjectsByType<CaptionButton>(FindObjectsSortMode.None))
                if (b.name.Contains("READING") || b.name.Contains("CONTINUE")) start = b;
            yield return null;
        }
        if (start == null) { Log("no start button"); yield break; }
        for (float t = 0f; t < 40f && !start.Button.IsInteractable(); t += Time.unscaledDeltaTime) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
        DevCapture.Shot("open_00_title");
        Log("pressing " + start.name);
        start.Button.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.3f);
        DevCapture.Shot("open_01_fading");
        while (PageView.I == null) yield return null;
        while (App.I != null && App.I.Flow.IsLoading) yield return null;      // the held title frame
        float t0 = Time.unscaledTime;
        foreach (float at in new[] { 0.05f, 0.3f, 0.5f, 0.7f, 0.9f, 1.2f, 1.6f, 2.2f, 5f })
        {
            while (Time.unscaledTime - t0 < at) yield return null;
            DevCapture.Shot($"open_t{at:0.00}");
        }
        Log("opening done: framing " + PageView.I.Framing.ToString("0.00") + ", opening " + PageView.I.Opening);
        Destroy(gameObject);
    }

    private IEnumerator BlotTest()
    {
        var pm = PageManager.I;
        pm.Load(6);
        MomDirector.I.Schedule(ScriptableObject.CreateInstance<PageDef>());     // no Mom in this run
        yield return Wait(0.2f);
        while (HeroController.I.Locked) yield return null;
        var hero = HeroController.I;
        var hh = hero.GetComponent<Health>();
        hh.Invulnerable = 120f;
        // into the office: off the first tier's right edge, then the reading sweep
        hero.Teleport(new Vector2(pm.Layout.tiers[0].maxX + 0.8f, pm.Layout.tiers[0].y0 + 5f));
        for (float t = 0f; t < 4f && pm.TierIndex == 0; t += Time.deltaTime) yield return null;
        yield return Wait(1.2f);
        var office = pm.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in office.enemies) if (e is BlotBrain b) blot = b;
        Log($"office: tier {pm.TierIndex + 1}, blot {blot != null}, hp {(blot != null ? blot.Health.hp : 0):0}");
        if (blot == null) yield break;
        DevCapture.Shot("blot_intro");

        // light him up (Max stays in the dark, safe) and he throws ink waves along the floor
        _followHero = false;
        hero.Teleport(new Vector2(office.rect.xMin + 6f, office.rect.yMin + 2.6f));
        TorchController.I.AimOverride = (Vector2)blot.transform.position + new Vector2(-1f, 1.2f);
        InkWave wave = null;
        for (float t = 0f; t < 12f && wave == null; t += Time.deltaTime) { wave = FindFirstObjectByType<InkWave>(); yield return null; }
        Log($"ink wave thrown: {wave != null}");
        if (wave != null)
        {
            yield return Wait(0.3f);
            DevCapture.Shot("blot_wave");
            // it rolls out of the beam and stops dead at its edge, a frozen wall of ink
            yield return Wait(1.2f);
            if (wave != null)
            {
                float x0 = wave.transform.position.x;
                yield return Wait(0.6f);
                Log($"wave past the beam's edge: moved {(wave != null ? Mathf.Abs(wave.transform.position.x - x0) : -1f):0.00} (expect 0), awake {(wave != null && wave.GetComponent<Lightable>().IsAwake)}");
                DevCapture.Shot("blot_wave_frozen");
            }
        }

        // he summons Smudges, two at a time
        int smudges = 0;
        for (float t = 0f; t < 12f && smudges < 2; t += Time.deltaTime)
        {
            TorchController.I.AimOverride = (Vector2)blot.transform.position + new Vector2(-1f, 1.2f);
            smudges = 0;
            foreach (var e in office.enemies) if (e is SmudgeBrain s && !s.Health.Dead) smudges++;
            yield return null;
        }
        Log($"smudges summoned: {smudges} (expect 2)");
        yield return Wait(0.6f);
        DevCapture.Shot("blot_summon");

        // the chandelier: lure him under it, then a flare snaps the thread
        FallingProp hanging = null;
        foreach (var f in office.root.GetComponentsInChildren<FallingProp>()) if (f.held) hanging = f;
        if (hanging != null)
        {
            var cc = blot.GetComponent<CharacterController>();
            cc.enabled = false;
            blot.transform.position = new Vector3(hanging.transform.position.x, blot.transform.position.y, 0f);
            cc.enabled = true;
            float hp0 = blot.Health.hp;
            var charge = TorchController.I.Charge;
            charge.Charge = charge.Capacity;
            _pad.twists = charge.OverwindBuffer + 1;
            yield return null;
            yield return Wait(0.35f);
            DevCapture.Shot("blot_chandelier");
            yield return Wait(1.4f);
            Log($"flare snapped the chandelier: held {hanging.held}, blot hp {hp0:0} -> {blot.Health.hp:0} (expect -30)");
        }
        else Log("no chandelier found");

        // knock him to half (in the light): he dives into his pool and the page ends
        yield return Wait(2.2f);                                       // the torch comes back after the flare
        _followHero = false;
        for (float t = 0f; t < 10f && !blot.Escaping; t += 0.3f)
        {
            TorchController.I.AimOverride = (Vector2)blot.transform.position + Vector2.up * 1.2f;
            blot.Health.Apply(new Hit { damage = 20f, team = Team.Hero, source = hero.gameObject, knockback = new Vector2(2f, 0f), stun = 0.3f, heavy = true });
            yield return Wait(0.3f);
        }
        Log($"blot escaping at hp {blot.Health.hp:0}");
        yield return Wait(1.0f);
        DevCapture.Shot("blot_dive");
        for (float t = 0f; t < 12f && (pm.Def == null || pm.Def.number == 6); t += Time.deltaTime)
        {
            if (blot != null) TorchController.I.AimOverride = (Vector2)blot.transform.position + Vector2.up * 1.2f;
            yield return null;
        }
        _followHero = true;
        Log($"page after the escape: {(pm.Def != null ? pm.Def.number : -1)} (expect 7)");
    }

    private IEnumerator MomTest()
    {
        var mom = MomDirector.I;
        var gs = GameState.I;
        var torch = TorchController.I;

        // page 3: the scripted pass, a 6 s warning; with the torch off she finds nothing
        PageManager.I.Load(3);
        HeroController.I.GetComponent<Health>().Invulnerable = 60f;   // this run is about Mom, not the fights
        yield return Wait(1.6f);
        mom.VisitNow();
        float warn = 0f;
        while (mom.State != MomState.AtDoor && warn < 10f) { warn += Time.deltaTime; yield return null; }
        Log($"page 3 warning window {warn:0.0} s (expect 7.5: 1.5 stirring + 6 approaching)");
        DevCapture.Shot("mom_atdoor");
        torch.SetOn(false);
        for (float t = 0f; t < 8f && mom.State != MomState.Asleep; t += Time.deltaTime) yield return null;
        Log($"torch off through the visit: suspicion {mom.Suspicion:0}, slippers {gs.Slippers}, lights-out {gs.LightsOut} (expect 0, 3, 1)");
        torch.SetOn(true);

        // the torch left on at the door: caught
        mom.VisitNow();
        float peak = 0f;
        for (float t = 0f; t < 16f && gs.Slippers == 3; t += Time.deltaTime)
        {
            if (peak < 55f && mom.Suspicion >= 55f) DevCapture.Shot("mom_suspicion");
            peak = Mathf.Max(peak, mom.Suspicion);
            yield return null;
        }
        Log($"torch on at the door: slippers {gs.Slippers} (expect 2), peak suspicion {peak:0}");
        yield return Wait(0.3f);
        DevCapture.Shot("mom_busted");
        yield return Wait(0.9f);
        DevCapture.Shot("mom_busted2");
        yield return Wait(3f);

        // page 4: the cat pads up; no need to hide
        PageManager.I.Load(4);
        HeroController.I.GetComponent<Health>().Invulnerable = 60f;
        yield return Wait(1f);
        mom.VisitNow(cat: true);
        for (float t = 0f; t < 6f && mom.State != MomState.AtDoor; t += Time.deltaTime) yield return null;
        yield return Wait(0.3f);
        DevCapture.Shot("mom_cat");
        for (float t = 0f; t < 6f && mom.State != MomState.Asleep; t += Time.deltaTime) yield return null;
        Log($"the cat, torch on: suspicion {mom.Suspicion:0}, slippers {gs.Slippers} (expect 0, 2)");

        // page 5: the first visit comes 9 s in and opens the door mid-fight (the torch stays on till she's in the doorway)
        PageManager.I.Load(5);
        HeroController.I.GetComponent<Health>().Invulnerable = 60f;
        yield return Wait(0.3f);
        float since = 0.3f;
        while (mom.State == MomState.Asleep && since < 14f) { since += Time.deltaTime; yield return null; }
        Log($"first visit after {since:0.0} s (expect 9), state {mom.State}");
        yield return Wait(0.8f);
        DevCapture.Shot("mom_firstvisit");
        for (float t = 0f; t < 12f && mom.State != MomState.Opening; t += Time.deltaTime) yield return null;
        Log($"door opening: {mom.State == MomState.Opening}, suspicion with the torch on {mom.Suspicion:0} (expect < 100)");
        torch.SetOn(false);
        yield return Wait(1.3f);
        var hero = HeroController.I;
        int awakeInkies = 0, inkies = 0;
        foreach (var e in PageManager.I.Layout.tiers[0].panels[0].enemies) { inkies++; if (e.Light.IsAwake) awakeInkies++; }
        Log($"door open, torch off: wedges {LightField.I.Wedges.Count}, inkies awake in the wedge {awakeInkies}/{inkies}, max awake {hero.Light.IsAwake}, suspicion {mom.Suspicion:0}");
        DevCapture.Shot("mom_opening");
        for (float t = 0f; t < 8f && mom.State != MomState.Asleep; t += Time.deltaTime) yield return null;
        Log($"she left: wedges {LightField.I.Wedges.Count}, suspicion {mom.Suspicion:0}, slippers {gs.Slippers}");
        torch.SetOn(true);
    }

    private IEnumerator SmudgeFight()
    {
        var hero = HeroController.I;
        var pm = PageManager.I;
        pm.Load(1);                                                       // page 1's Smudge
        yield return Wait(0.5f);
        while (hero.Locked) yield return null;
        // go to tier 2, panel 1 (the Smudge)
        var tier1 = pm.Layout.tiers[0];
        hero.Teleport(new Vector2(tier1.maxX - 0.5f, 3f));
        _pad.moveX = 1f;
        yield return Wait(1.6f);
        _pad.moveX = 0f;
        Log($"tier now {pm.TierIndex + 1}, hero {hero.transform.position}");
        var panel = pm.Layout.tiers[1].panels[0];
        Log($"enemies in panel: {panel.enemies.Count}");
        if (panel.enemies.Count == 0) yield break;
        var smudge = panel.enemies[0];
        var sh = smudge.Health;
        // walk up to it
        for (float t = 0f; t < 3f && Mathf.Abs(smudge.transform.position.x - hero.transform.position.x) > 1.6f; t += Time.deltaTime)
        {
            _pad.moveX = Mathf.Sign(smudge.transform.position.x - hero.transform.position.x);
            yield return null;
        }
        _pad.moveX = 0f;
        Log($"close: gap {smudge.transform.position.x - hero.transform.position.x:0.00}, smudge state {smudge.Mode}");
        float heroHp0 = hero.GetComponent<Health>().hp;
        for (int i = 0; i < 3; i++)
        {
            _pad.punch = true;
            yield return Wait(0.3f);
        }
        yield return Wait(0.6f);
        Log($"after J,J,J: smudge hp {sh.hp:0} / {sh.maxHp:0}, splash {GameState.I.Splash:0}, state {smudge.Mode}");
        yield return Wait(2.5f);
        Log($"after its combo window: max hearts {hero.GetComponent<Health>().hp:0} (was {heroHp0:0})");
        for (int i = 0; i < 6 && !sh.Dead; i++)
        {
            _pad.punch = true;
            yield return Wait(0.35f);
        }
        yield return Wait(0.5f);
        Log($"finish: smudge dead {sh.Dead}, hp {sh.hp:0}");
    }
}
#endif
