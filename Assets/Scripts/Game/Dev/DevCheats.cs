#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Developer cheats for checking the game by hand (the editor and Development builds only: a release build
/// doesn't have them). They work while a comic page is open; F1 shows the list and what's switched on.
///   F1           this list
///   F2           Splash meter full (the Splash Page itself is page 5 on)
///   F3           god mode: Max can't be hurt or knocked out
///   F4           torch full again (and a flared bulb cools at once)
///   F5           endless torch: the charge never runs down
///   F6           Mom comes now and opens the door        Shift+F6  Mom off / on for this page
///   F7           Mom catches you (BUSTED)
///   F8           every awake Inkie in the light is knocked out (Blot takes 40 a press: his escapes, the finale)
///   F9           finish this page (its card, the Nightstand between acts)
///   PgUp / PgDn  the next / the previous page, straight away
///   F10          the Ghost lens, and 5 stars to spend
///   F11          slow motion (a quarter speed) to look at hits frame by frame
///   F12          the room light on / off: the whole page awake
/// </summary>
public class DevCheats : MonoBehaviour
{
    private bool _show, _god, _endless, _slow, _momOff;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("DevCheats");
        DontDestroyOnLoad(go);
        go.AddComponent<DevCheats>();
    }

    private static bool Pressed(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
    private static bool Shift => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

    private void Update()
    {
        if (Pressed(Key.F1)) _show = !_show;
        var pm = PageManager.I;
        if (pm == null || pm.Def == null) { _momOff = false; return; }    // not reading a comic page
        var gs = GameState.I;
        var hero = HeroController.I;
        var heroHealth = hero != null ? hero.GetComponent<Health>() : null;
        var torch = TorchController.I;
        var mom = MomDirector.I;

        if (Pressed(Key.F2) && gs != null) { gs.AddSplash(100f); gs.Notify(); Say("SPLASH METER FULL"); }
        if (Pressed(Key.F3)) Say((_god = !_god) ? "GOD MODE ON" : "GOD MODE OFF");
        if (Pressed(Key.F4) && torch != null) { torch.Charge.Charge = torch.Charge.Capacity; Say("TORCH FULL"); }
        if (Pressed(Key.F5)) Say((_endless = !_endless) ? "ENDLESS TORCH ON" : "ENDLESS TORCH OFF");
        if (Pressed(Key.F6) && mom != null)
        {
            if (Shift)
            {
                _momOff = !_momOff;
                mom.Schedule(_momOff ? ScriptableObject.CreateInstance<PageDef>() : pm.Def);
                Say(_momOff ? "MOM OFF FOR THIS PAGE" : "MOM BACK ON");
            }
            else { mom.VisitNow(opens: true); Say("MOM'S COMING"); }
        }
        if (Pressed(Key.F7) && mom != null) { mom.VisitNow(opens: true); _bust = true; Say("BUSTED (TORCH STAYS ON)"); }
        if (Pressed(Key.F8)) KnockOut();
        if (Pressed(Key.F9) && !pm.Busy) { pm.Finish("PAGE DONE (CHEAT)"); }
        if (Pressed(Key.PageUp) && !pm.Busy) JumpTo(pm.Def.number + 1);
        if (Pressed(Key.PageDown) && !pm.Busy) JumpTo(pm.Def.number - 1);
        if (Pressed(Key.F10) && torch != null && gs != null)
        {
            torch.Lenses.Unlock(Lens.Ghost);
            gs.StarsTotal += 5;
            gs.Notify();
            Say("GHOST LENS, +5 STARS");
        }
        if (Pressed(Key.F11)) Say((_slow = !_slow) ? "SLOW MOTION ON" : "SLOW MOTION OFF");
        if (Pressed(Key.F12) && LightField.I != null && Finale.I != null && !Finale.I.Running)
        {
            bool on = !LightField.I.RoomLight;
            LightField.I.RoomLight = on;
            if (PageView.I != null) PageView.I.RoomLight = on ? 1f : 0f;
            Say(on ? "ROOM LIGHT ON" : "ROOM LIGHT OFF");
        }

        // the ones that hold
        if (_god && heroHealth != null)
        {
            heroHealth.hp = heroHealth.maxHp;
            heroHealth.Invulnerable = Mathf.Max(heroHealth.Invulnerable, 0.2f);
        }
        if (_endless && torch != null && torch.Charge.Overwind == 0) torch.Charge.Charge = torch.Charge.Capacity;
        if (_bust && mom != null && torch != null)
        {
            torch.SetOn(true);                                              // keep the light on till she sees it
            if (mom.State == MomState.Asleep && gs != null) _bust = false;
        }
    }

    private bool _bust;

    private void LateUpdate()
    {
        // after the hit-stop has had its say: a quarter speed, held while slow motion's on (never over the pause)
        if (_slow && !BookmarkPause.IsPaused && Time.timeScale > 0.25f) Time.timeScale = 0.25f;
        else if (!_slow && !BookmarkPause.IsPaused && Mathf.Approximately(Time.timeScale, 0.25f)) Time.timeScale = 1f;
    }

    private void KnockOut()
    {
        var hero = HeroController.I;
        int n = 0;
        foreach (var h in Health.All.ToArray())
        {
            if (h.team != Team.Inkie || h.Dead || !h.IsAwake) continue;
            if (LightField.I != null && !LightField.I.PageAwake && !LightField.I.IsLit(h.transform.position + Vector3.up)) continue;
            h.Invulnerable = 0f;
            bool boss = h.GetComponent<BlotBrain>() != null;
            h.Apply(new Hit { damage = boss ? 40f : 999f, team = Team.Hero, source = hero != null ? hero.gameObject : null, heavy = true, word = "CHEAT!" });
            n++;
        }
        Say(n > 0 ? $"KNOCKED OUT {n}" : "NOBODY AWAKE IN THE LIGHT");
    }

    private static void JumpTo(int page)
    {
        if (page < 1 || PageManager.LoadDef(page) == null) { Say($"NO PAGE {page}"); return; }
        PageManager.I.Load(page);
        Say($"PAGE {page}");
    }

    private static void Say(string text)
    {
        Debug.Log("[Cheat] " + text);
        GameHUD.I?.Warn(text);
    }

    private void OnGUI()
    {
        if (!_show) return;
        string on(bool b) => b ? "<color=#7CFC00>ON</color>" : "off";
        string text =
            "<b>DEV CHEATS</b>  (F1 hides)\n" +
            "F2  Splash meter full\n" +
            $"F3  god mode: {on(_god)}\n" +
            "F4  torch full\n" +
            $"F5  endless torch: {on(_endless)}\n" +
            $"F6  Mom now (opens)   Shift+F6 Mom off: {on(_momOff)}\n" +
            "F7  Mom catches you (BUSTED)\n" +
            "F8  knock out awake Inkies in the light (Blot -40)\n" +
            "F9  finish this page\n" +
            "PgUp / PgDn  next / previous page\n" +
            "F10 Ghost lens, +5 stars\n" +
            $"F11 slow motion: {on(_slow)}\n" +
            "F12 room light on / off\n";
        var pm = PageManager.I;
        var gs = GameState.I;
        var torch = TorchController.I;
        var mom = MomDirector.I;
        if (pm != null && pm.Def != null && gs != null)
            text += $"\npage {pm.Def.number}, tier {pm.TierIndex + 1}, splash {gs.Splash:0}, stars {gs.StarsTotal}, slippers {gs.Slippers}" +
                    (torch != null ? $"\ntorch {torch.Charge.Charge:0}/{torch.Charge.Capacity:0}, overwind {torch.Charge.Overwind}, lens {torch.Lenses.Current}" : "") +
                    (mom != null ? $"\nMom {mom.State}, suspicion {mom.Suspicion:0}" : "") +
                    $"\n{1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f):0} fps";
        var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true, wordWrap = false };
        style.normal.textColor = Color.white;
        var size = style.CalcSize(new GUIContent(text));
        GUI.Box(new Rect(12, 12, size.x + 16, size.y + 12), text, style);
    }
}
#endif
