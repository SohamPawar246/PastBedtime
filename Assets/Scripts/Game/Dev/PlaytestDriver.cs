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
    private bool _atNightstand;

    public static PlaytestDriver Run(string scenario)
    {
        // one driver at a time: an old one would keep steering the torch toward Max
        foreach (var old in FindObjectsByType<PlaytestDriver>(FindObjectsSortMode.None)) Destroy(old.gameObject);
        if (TorchController.I != null) TorchController.I.AimOverride = null;
        var d = new GameObject("Playtest").AddComponent<PlaytestDriver>();
        d._save = new[] { Settings.HighestPage, Settings.Stars, Settings.Spring, Settings.Gear, Settings.Ratchet };
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
            case "green": yield return GreenTest(); break;
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
        }
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
        Settings.Save();
    }

    private IEnumerator Basics()
    {
        var hero = HeroController.I;
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

        // twist: drain then crank
        var charge = TorchController.I.Charge;
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
        Log($"after the panel restart: blocks {Blocks()} (expect {b0})");
    }

    /// <summary>The torch in Roshan's hand: the crank turning, the dial and the lens wheel.</summary>
    // Max's moves play to their ends and hand over to the right stance: checked by the state that's
    // playing, with pictures at the poses that matter (DevCapture.Folder must be set)
    private IEnumerator AnimTest()
    {
        var hero = HeroController.I;
        var clips = hero.Clips;
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
        yield return Wait(2f);
        Log($"drowned: hearts {hearts0} -> {hero.GetComponent<Health>().hp:0}, flood drained {Mathf.Abs(flood.Level - flood.Start) < 0.01f} (expect -1, True)");
        hero.GetComponent<Health>().Invulnerable = 120f;

        // page 8's vat: Blot in invisible ink
        var vat = PageManager.I.Layout.tiers[1].panels[1];
        BlotBrain blot = null;
        foreach (var e in vat.enemies) if (e is BlotBrain b) blot = b;
        Log($"the vat: blot {blot != null}, phase {(blot != null ? blot.Phase : 0)}, hp {(blot != null ? blot.Health.hp : 0):0} (expect True, 2, 75)");
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

    // Page 9: Blot's last 30 HP brings Mom and the room light; beating him plays the ending
    private IEnumerator FinaleTest()
    {
        var hero = HeroController.I;
        PageManager.I.Load(9);
        hero.GetComponent<Health>().Invulnerable = 300f;
        yield return Wait(1.6f);
        var arena = PageManager.I.Layout.tiers[1].panels[0];
        BlotBrain blot = null;
        foreach (var e in arena.enemies) if (e is BlotBrain b) blot = b;
        Log($"the roof: blot {blot != null}, phase {(blot != null ? blot.Phase : 0)}, hp {(blot != null ? blot.Health.hp : 0):0} (expect True, 3, 70)");
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
        blot.Health.Invulnerable = 0f;
        blot.Health.Apply(new Hit { damage = 40f, team = Team.Hero, source = hero.gameObject });
        Log($"blot beaten: dead {blot.Health.Dead}, ending {Finale.I.Ended} (expect True, True)");
        float[] at = { 2.5f, 4.5f, 7f, 9.5f, 13.5f, 16.5f, 19f };
        float clock = 0f;
        foreach (float t in at)
        {
            yield return WaitReal(t - clock);
            clock = t;
            DevCapture.Shot($"ending_{t:00.0}");
        }
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
        Log($"motor lit: the belt moved max {hero.transform.position.x - x0:+0.00;-0.00} in 1 s (expect about -2.4)");
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
            Log($"page {page}: ran {hero.transform.position.x - x0:0.00} in {t:0.00} s; velocity{samples}");
            yield return Wait(0.5f);
        }
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
        torch.Lenses.Unlock(Lens.Green);
        _pad.lensStep = 1;
        yield return Wait(0.35f);
        DevCapture.Shot("crank_green");
        Log($"lens now {torch.Lenses.Current}");
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

    private IEnumerator GreenTest()
    {
        PageManager.I.Load(4);
        yield return Wait(0.2f);
        while (HeroController.I.Locked) yield return null;
        var h = HeroController.I.GetComponent<Health>();
        h.hp = 3f;
        TorchController.I.Lenses.Pick((int)Lens.Green);
        yield return Wait(6.6f);
        Log($"6.6 s in the green beam: hearts 3 -> {h.hp:0} (expect 5)");
        TorchController.I.Lenses.Pick((int)Lens.Clear);
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

        // page 5: the first visit comes 9 s in, opens the door mid-fight, and the Red lens turns up
        PageManager.I.Load(5);
        HeroController.I.GetComponent<Health>().Invulnerable = 60f;
        yield return Wait(0.3f);
        Log($"page 5 starts: red unlocked {torch.Lenses.Unlocked[(int)Lens.Red]} (expect False)");
        float since = 0.3f;
        while (mom.State == MomState.Asleep && since < 14f) { since += Time.deltaTime; yield return null; }
        Log($"first visit after {since:0.0} s (expect 9), state {mom.State}");
        yield return Wait(0.8f);
        DevCapture.Shot("mom_redlens");
        Log($"red unlocked {torch.Lenses.Unlocked[(int)Lens.Red]} (expect True)");
        torch.Lenses.Pick((int)Lens.Red);
        for (float t = 0f; t < 12f && mom.State != MomState.Opening; t += Time.deltaTime) yield return null;
        Log($"door opening: {mom.State == MomState.Opening}, suspicion with red on {mom.Suspicion:0}");
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
