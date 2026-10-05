using System.Collections;
using UnityEngine;

/// <summary>
/// Runs a page (GDD sections 10 and 13): builds it, spawns its Inkies, saves a checkpoint as Max
/// walks into each panel, does the reading sweep at the end of a tier, restarts the panel on a
/// "TO BE CONTINUED..." card when Max is out of hearts, and turns to the next page when he
/// walks off the last panel.
/// </summary>
public class PageManager : MonoBehaviour
{
    public static PageManager I { get; private set; }
    public const int LastPage = 9;

    public PageDef Def { get; private set; }
    public PageLayout Layout { get; private set; }
    public int TierIndex { get; private set; }
    public PanelLayout Panel { get; private set; }
    public bool Busy { get; private set; }

    private Transform _actors;
    private Vector2 _checkpoint;
    private float _checkpointCharge, _checkpointSplash;

    private void Awake() => I = this;
    private void OnDestroy() { if (I == this) I = null; }

    public static PageDef LoadDef(int number) => Resources.Load<PageDef>($"Pages/Page{number:00}");

    public void Load(int number)
    {
        var def = LoadDef(number);
        if (def == null)
        {
            Debug.LogError($"[PageManager] No page {number}");
            return;
        }
        Unload();
        Def = def;
        var gs = GameState.I;
        if (gs != null)
        {
            gs.Page = number;
            gs.PageStarted();
            gs.Notify();
        }
        Layout = PageBuilder.Build(def, transform);
        _actors = new GameObject("Actors").transform;
        _actors.SetParent(transform, false);
        foreach (var tier in Layout.tiers)
            foreach (var pl in tier.panels)
                PageBuilder.SpawnEnemies(pl, _actors);

        TierIndex = 0;
        Panel = null;
        var hero = HeroController.I;
        if (hero != null)
        {
            hero.Teleport(Layout.start);
            SetHeroBounds();
        }
        var torch = TorchController.I;
        if (torch != null)
        {
            torch.Charge.Charge = torch.Charge.Capacity * def.startCharge;
            torch.PlaceAt(Layout.start + Vector2.up);
            torch.SetOn(true);
            // starting on a later page (continue, or GROUNDED back to an act's start): earlier pages' lenses
            for (int n = 1; n < number; n++)
            {
                var before = LoadDef(n);
                if (before == null) continue;
                if (before.unlockLens >= 0) torch.Lenses.Unlock((Lens)before.unlockLens);
            }
        }
        PageCamera.I?.Frame(Layout.tiers[0], Layout.start.x, snap: true);
        MomDirector.I?.Schedule(def);
        GameHUD.I?.PageStart(def);
        if (number > Settings.HighestPage)
        {
            Settings.HighestPage = number;
            Settings.Save();
        }
    }

    private void Unload()
    {
        if (Layout != null && Layout.root != null) Destroy(Layout.root.gameObject);
        if (_actors != null) Destroy(_actors.gameObject);
        MopUpInk();
        Layout = null;
    }

    /// <summary>Loose ink isn't part of any page: pellets, Blot's waves and geysers. A wave frozen in the dark
    /// never runs out, so a turned page (or a restart) mops them all up.</summary>
    private static void MopUpInk()
    {
        foreach (var p in FindObjectsByType<InkPellet>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var w in FindObjectsByType<InkWave>(FindObjectsSortMode.None)) Destroy(w.gameObject);
        foreach (var g in FindObjectsByType<InkGeyser>(FindObjectsSortMode.None)) Destroy(g.gameObject);
    }

    private void SetHeroBounds()
    {
        var hero = HeroController.I;
        if (hero == null || Layout == null) return;
        var tier = Layout.tiers[TierIndex];
        hero.MinX = tier.minX + 0.35f;
        hero.MaxX = tier.maxX + 1.5f;               // walking off the right edge turns the page
    }

    private void Update()
    {
        var hero = HeroController.I;
        if (Layout == null || hero == null || Busy) return;
        if (Finale.I != null && Finale.I.Ended) return;                // the comic's finished: no restarts under the ending
        var tier = Layout.tiers[TierIndex];
        Vector2 hp = hero.transform.position;

        if (hero.Dead)
        {
            StartCoroutine(Restart("TO BE CONTINUED..."));
            return;
        }
        if (hp.y < tier.y0 - 4f)
        {
            StartCoroutine(FellOff());
            return;
        }
        foreach (var pl in tier.panels)
            if (pl.flood != null && pl.flood.Covers(hp + Vector2.up * 0.4f))
            {
                StartCoroutine(Drowned());
                return;
            }
        foreach (var pl in tier.panels)
        {
            if (pl != Panel && hp.x >= pl.rect.xMin - 0.1f && hp.x <= pl.rect.xMax + 0.1f)
            {
                EnterPanel(pl);
                break;
            }
        }
        if (hp.x > tier.maxX + 0.6f) StartCoroutine(NextTier());
    }

    private void EnterPanel(PanelLayout pl)
    {
        Panel = pl;
        if (pl.visited) return;
        pl.visited = true;
        _checkpoint = pl.entry;
        var torch = TorchController.I;
        _checkpointCharge = torch != null ? torch.Charge.Charge : 0f;
        _checkpointSplash = GameState.I != null ? GameState.I.Splash : 0f;
        if (!string.IsNullOrEmpty(pl.def.heroLine)) GameHUD.I?.HeroSays(pl.def.heroLine, 4f);
    }

    public void EnemyDown(EnemyBrain e)
    {
        if (Layout == null) return;
        foreach (var tier in Layout.tiers)
            foreach (var pl in tier.panels)
                pl.enemies.Remove(e);
        // bats are stepping stones: one that's lost is drawn back in at its roost (Blot's summons aren't)
        if (e is SplotchBrain && !e.Summoned && e.Panel != null) StartCoroutine(Redraw(EnemyKind.Splotch, e.Home, e.Panel, Layout));
    }

    private IEnumerator Redraw(EnemyKind kind, Vector2 home, PanelLayout pl, PageLayout layout)
    {
        yield return new WaitForSeconds(4f);
        if (Layout != layout || pl.root == null) yield break;           // the page has turned
        int want = 0, have = 0;
        foreach (var s in pl.def.spawns) if (s.kind == kind) want++;
        foreach (var e in pl.enemies) if (e != null && e.GetType() == typeof(SplotchBrain) && !e.Summoned) have++;
        if (have >= want) yield break;
        var bat = PageBuilder.Spawn(kind, home, pl, _actors);
        if (bat != null) SfxLettering.Spawn("FLAP FLAP", home + Vector2.up * 1.2f, Palette.Paper, 0.7f);
    }

    private IEnumerator NextTier()
    {
        Busy = true;
        var hero = HeroController.I;
        if (TierIndex + 1 >= Layout.tiers.Count)
        {
            yield return PageDone();
            Busy = false;
            yield break;
        }
        hero.Locked = true;
        TierIndex++;
        var tier = Layout.tiers[TierIndex];
        var first = tier.panels[0];
        hero.Teleport(first.entry);
        SetHeroBounds();
        TorchController.I?.PlaceAt(first.entry + Vector2.up);
        Panel = null;
        yield return PageCamera.I.SweepTo(tier, first.entry.x);
        hero.Locked = false;
        Busy = false;
    }

#if UNITY_EDITOR
    /// <summary>Tests: straight to a tier, Max on its first panel's entry.</summary>
    public void GoToTier(int index)
    {
        TierIndex = Mathf.Clamp(index, 0, Layout.tiers.Count - 1);
        var first = Layout.tiers[TierIndex].panels[0];
        HeroController.I.Teleport(first.entry);
        SetHeroBounds();
        Panel = null;
        PageCamera.I?.Frame(Layout.tiers[TierIndex], first.entry.x, snap: true);
    }
#endif

    private IEnumerator PageDone()
    {
        var gs = GameState.I;
        var hero = HeroController.I;
        hero.Locked = true;
        if (Def.unlockLens >= 0) TorchController.I?.Lenses.Unlock((Lens)Def.unlockLens);
        var health = hero.GetComponent<Health>();
        health.hp = Mathf.Min(health.maxHp, health.hp + 1f);         // one heart back per page
        gs?.Notify();
        yield return GameHUD.I?.PageCleared(Def);
        gs?.BankStars();                                               // this page's stars, kept for good
        // between acts: the Nightstand, where the stars buy torch upgrades
        if (Def.number == 3 || Def.number == 6) yield return Nightstand.Visit();
        int next = Def.number + 1;
        while (next <= LastPage && LoadDef(next) == null) next++;   // pages still being drawn are skipped
        hero.Locked = false;
        if (next > LastPage)
        {
            yield return GameHUD.I?.Card("TO BE CONTINUED IN\nMAX VOLTAGE #2!", 3f);
            App.I?.Flow.Load(Scenes.Title);
            yield break;
        }
        Load(next);
    }

    /// <summary>Ends the page on a card instead of the right-hand edge (Baron Blot getting away).</summary>
    public void Finish(string card)
    {
        if (Def != null) StartCoroutine(FinishRoutine(card));
    }

    private IEnumerator FinishRoutine(string card)
    {
        while (Busy) yield return null;                 // after any card already up (BUSTED, a restart)
        Busy = true;
        if (!string.IsNullOrEmpty(card)) yield return GameHUD.I?.Card(card, 2.2f);
        yield return PageDone();
        Busy = false;
    }

    /// <summary>Mom caught the torch on (GDD section 8): BUSTED, a slipper lost, the panel restarts.
    /// Losing all three slippers is GROUNDED: back to the first page of the act.</summary>
    public void Busted()
    {
        if (Busy) return;
        StartCoroutine(BustedRoutine());
    }

    private IEnumerator BustedRoutine()
    {
        Busy = true;
        var gs = GameState.I;
        var torch = TorchController.I;
        torch?.SetOn(false);
        AudioDirector.I?.DoorClose();
        gs.Slippers = Mathf.Max(0, gs.Slippers - 1);
        gs.Notify();
        if (gs.Slippers <= 0)
        {
            yield return GameHUD.I?.Card("GROUNDED!\n<size=45%>(FOR A WEEK)</size>", 2.6f);
            gs.Slippers = 3;
            gs.StarsTotal = Settings.Stars;                            // stars from the unfinished page go back on it
            int first = Def.number <= 3 ? 1 : Def.number <= 6 ? 4 : 7;
            Busy = false;
            Load(first);
            yield break;
        }
        yield return GameHUD.I?.Card("BUSTED!\n<size=40%>\"ROSHAN. LIGHTS. OUT.\"</size>", 2.2f);
        Busy = false;
        yield return Restart(null, refill: false);                     // getting caught is no free heal
    }

    private IEnumerator FellOff()
    {
        Busy = true;
        var hero = HeroController.I;
        var h = hero.GetComponent<Health>();
        h.hp = Mathf.Max(0f, h.hp - 1f);
        GameState.I?.Notify();
        if (h.hp <= 0f)
        {
            Busy = false;
            yield return Restart("TO BE CONTINUED...");
            yield break;
        }
        yield return GameHUD.I?.Card("WHOOPS!", 0.8f);
        hero.Teleport(Panel != null ? Panel.entry : _checkpoint);
        TorchController.I?.PlaceAt(hero.transform.position + Vector3.up);
        Busy = false;
    }

    /// <summary>Max went under Blot's ink: a heart, and the tier's ink drains back down.</summary>
    private IEnumerator Drowned()
    {
        Busy = true;
        var hero = HeroController.I;
        var h = hero.GetComponent<Health>();
        h.hp = Mathf.Max(0f, h.hp - 1f);
        GameState.I?.Notify();
        SfxLettering.Spawn("GLUB!", (Vector2)hero.transform.position + Vector2.up * 1.5f, Palette.Paper, 1.1f, burst: true);
        GameAudio.Play("splat", 0.8f);
        if (h.hp <= 0f)
        {
            Busy = false;
            yield return Restart("TO BE CONTINUED...");
            yield break;
        }
        hero.Locked = true;
        yield return GameHUD.I?.Card("GLUB GLUB!", 0.8f);
        foreach (var pl in Layout.tiers[TierIndex].panels) pl.flood?.Drain();
        hero.Teleport(Panel != null ? Panel.entry : _checkpoint);
        TorchController.I?.PlaceAt(hero.transform.position + Vector3.up);
        hero.Locked = false;
        Busy = false;
    }

    /// <summary>The panel restarts with the charge and splash meter Max had when he walked in (and full
    /// hearts, unless it was Mom who caught him).</summary>
    private IEnumerator Restart(string card, bool refill = true)
    {
        Busy = true;
        var hero = HeroController.I;
        hero.Locked = true;
        if (card != null)
        {
            yield return new WaitForSeconds(0.9f);
            yield return GameHUD.I?.Card(card, 2f);
        }
        var pl = Panel ?? Layout.tiers[TierIndex].panels[0];
        foreach (var b in pl.root.GetComponentsInChildren<ErasableBlock>(true)) b.Restore();   // an eaten bridge is drawn back in
        foreach (var other in Layout.tiers[TierIndex].panels) other.flood?.Drain();           // and the ink goes back down
        foreach (var prop in pl.root.GetComponentsInChildren<FallingProp>(true)) prop.Redraw();  // crates and bricks back up
        foreach (var e in pl.enemies.ToArray()) if (e != null) Destroy(e.gameObject);
        pl.enemies.Clear();
        PageBuilder.SpawnEnemies(pl, _actors);
        MopUpInk();
        var health = hero.GetComponent<Health>();
        health.hp = refill ? health.maxHp : Mathf.Max(1f, health.hp);
        hero.Teleport(pl.visited ? _checkpoint : pl.entry);
        hero.Clips?.Play("Idle", 0f, 1f, restart: true);
        var torch = TorchController.I;
        if (torch != null)
        {
            torch.Charge.Charge = Mathf.Max(_checkpointCharge, torch.Charge.Capacity * 0.25f);
            torch.PlaceAt(hero.transform.position + Vector3.up);
            torch.SetOn(true);
        }
        if (GameState.I != null)
        {
            GameState.I.Splash = _checkpointSplash;
            GameState.I.Notify();
        }
        hero.Locked = false;
        Busy = false;
    }
}
