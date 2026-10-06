using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The comic's own punctuation for its biggest moments. Like the lettering, it's the artist's, not the world's,
/// so it runs on real time (held by the Bookmark, not by a hit-stop).
///  ImpactFrame()  the lit page flips to a negative for a few frames: a haymaker landing, a knockout, the ground
///                 slam, the Splash Page. Never more than one every 0.55 s, and none at all with Reduce flashing.
///  SplashPage()   the super becomes a real splash page: the panel borders blow off, the page camera punches in
///                 on Max, a giant diagonal KA-POW! holds the frame, then the panels snap back.
///  InkCue()       an Inkie waking in the light, or freezing out of it, says so with a soft snap or a paper tick;
///                 a crowd changing at once (the torch switched, a flare) is left to the torch's own click.
/// </summary>
public class ComicFx : MonoBehaviour
{
    private static ComicFx _i;
    private static readonly int ImpactId = Shader.PropertyToID("_PB_Impact");

    /// <summary>The shortest gap between two impact frames (a splash page may follow a blow a little sooner).</summary>
    public const float ImpactGap = 0.55f, SplashGap = 0.34f;
    public const float BlowOff = 0.16f, Hold = 0.42f, SnapBack = 0.18f;

    /// <summary>For tests: impact frames printed, and wake/freeze sounds played, since the game scene opened.</summary>
    public static int ImpactFrames { get; private set; }
    public static int Cues { get; private set; }
    /// <summary>The Splash Page's sequence is on (the torch's aim holds still under the camera's punch-in).</summary>
    public static bool Splashing => _i != null && _i._splashing;

    private float _lastImpact = -10f;
    private bool _splashing;
    private int _wakes, _freezes;
    private float _lastCue = -10f;

    private static ComicFx I
    {
        get
        {
            if (_i == null) _i = new GameObject("ComicFx").AddComponent<ComicFx>();
            return _i;
        }
    }

    private void Awake() => ImpactFrames = Cues = 0;

    private void OnDestroy()
    {
        if (_i != this) return;
        _i = null;
        Shader.SetGlobalFloat(ImpactId, 0f);
    }

    // ---- the impact frame ---------------------------------------------------------------------------------------

    public static void ImpactFrame() => I.Impact(ImpactGap);

    private void Impact(float gap)
    {
        if (Settings.ReduceFlashing || BookmarkPause.IsPaused) return;
        if (Time.unscaledTime - _lastImpact < gap) return;
        _lastImpact = Time.unscaledTime;
        ImpactFrames++;
        StartCoroutine(Negative());
    }

    private IEnumerator Negative()
    {
        Shader.SetGlobalFloat(ImpactId, 1f);
        // three frames at 60 fps; at least two whatever the frame rate
        int frames = 0;
        for (float t = 0f; frames < 2 || t < 0.05f; frames++)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        Shader.SetGlobalFloat(ImpactId, 0f);
    }

    // ---- the splash page --------------------------------------------------------------------------------------

    private struct Border
    {
        public Transform t;
        public Vector3 pos, scale;
        public Quaternion rot;
        public Vector3 away;
        public float turn;
    }

    private readonly List<Border> _borders = new();

    /// <param name="at">Max (the camera punches in on him; the title slashes across above him).</param>
    public static void SplashPage(Vector2 at)
    {
        var fx = I;
        if (fx._splashing) return;
        fx.StartCoroutine(fx.Splash(at));
    }

    private IEnumerator Splash(Vector2 at)
    {
        _splashing = true;
        HitStopper.Stop(BlowOff + Hold);                                       // the world holds its breath
        Impact(SplashGap);
        PageCamera.Punch(at + Vector2.up * 1.2f, 0.8f, BlowOff, Hold, SnapBack + 0.1f);
        SfxLettering.Title("KA-POW!", TitleAt(at), 2.6f, BlowOff + Hold + SnapBack);
        GatherBorders(at);

        // the borders blow off the page...
        for (float t = 0f; t < BlowOff; t += BookmarkPause.UnpausedDelta)
        {
            float k = 1f - Mathf.Pow(1f - t / BlowOff, 3f);
            PoseBorders(k);
            yield return null;
        }
        PoseBorders(1f);
        // ...the frame holds...
        for (float t = 0f; t < Hold; t += BookmarkPause.UnpausedDelta) yield return null;
        // ...and the panels snap back into their ink, with a little overshoot
        for (float t = 0f; t < SnapBack; t += BookmarkPause.UnpausedDelta)
        {
            float k = t / SnapBack;
            float back = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f);      // ease out, overshooting
            PoseBorders(1f - back);
            yield return null;
        }
        RestoreBorders();
        _splashing = false;
    }

    /// <summary>Where the title goes: above Max, kept inside his panel.</summary>
    private static Vector2 TitleAt(Vector2 at)
    {
        Vector2 p = at + new Vector2(0f, 3.4f);
        var pm = PageManager.I;
        if (pm == null || pm.Layout == null || pm.TierIndex >= pm.Layout.tiers.Count) return p;
        var tier = pm.Layout.tiers[pm.TierIndex];
        float top = tier.y0 + PageDef.PanelHeight - 2.3f, bottom = tier.y0 + 3.2f;
        p.y = Mathf.Clamp(p.y, bottom, top);
        if (PageCamera.I != null)
        {
            float l = PageCamera.I.ViewportToLane(new Vector2(0.24f, 0.5f)).x, r = PageCamera.I.ViewportToLane(new Vector2(0.76f, 0.5f)).x;
            p.x = Mathf.Clamp(p.x, l, r);                                        // all of it on the page in view
        }
        return p;
    }

    private void GatherBorders(Vector2 at)
    {
        RestoreBorders();
        var pm = PageManager.I;
        if (pm == null || pm.Layout == null || pm.TierIndex >= pm.Layout.tiers.Count) return;
        foreach (var pl in pm.Layout.tiers[pm.TierIndex].panels)
        {
            if (pl.root == null) continue;
            Vector2 centre = pl.rect.center;
            foreach (Transform c in pl.root)
            {
                if (!c.name.StartsWith("Border")) continue;
                Vector2 p = c.position;
                // each stroke flies straight out from its panel, and a little more away from the blow
                Vector2 dir = c.name == "BorderTop" ? Vector2.up : c.name == "BorderBottom" ? Vector2.down
                            : p.x < centre.x ? Vector2.left : Vector2.right;
                dir = (dir + (p - at).normalized * 0.45f).normalized;
                float length = Mathf.Max(c.lossyScale.x, c.lossyScale.y);
                _borders.Add(new Border
                {
                    t = c, pos = c.localPosition, rot = c.localRotation, scale = c.localScale,
                    away = (Vector3)(dir * Random.Range(1.5f, 2.6f)),
                    turn = Random.Range(-1f, 1f) * Mathf.Lerp(16f, 4f, Mathf.InverseLerp(3f, 14f, length)),
                });
            }
        }
    }

    private void PoseBorders(float k)
    {
        foreach (var b in _borders)
        {
            if (b.t == null) continue;
            b.t.localPosition = b.pos + b.t.parent.InverseTransformVector(b.away) * k;
            b.t.localRotation = b.rot * Quaternion.Euler(0f, 0f, b.turn * k);
        }
    }

    private void RestoreBorders()
    {
        foreach (var b in _borders)
        {
            if (b.t == null) continue;
            b.t.localPosition = b.pos;
            b.t.localRotation = b.rot;
            b.t.localScale = b.scale;
        }
        _borders.Clear();
    }

    // ---- an Inkie waking or freezing ---------------------------------------------------------------------------

    /// <summary>An Inkie woke in the light (true) or froze out of it (false) this frame.</summary>
    public static void InkCue(bool woke)
    {
        if (woke) I._wakes++; else I._freezes++;
    }

    private void LateUpdate()
    {
        // one Inkie (or two) changing on its own is worth a sound; a crowd at once is the torch's switch or a
        // flare, and the switch has its own click
        int changed = _wakes + _freezes;
        if (changed > 0 && changed <= 2 && Time.unscaledTime - _lastCue > 0.08f && !BookmarkPause.IsPaused)
        {
            _lastCue = Time.unscaledTime;
            Cues++;
            if (_wakes > 0) GameAudio.Snap(0.32f);
            else GameAudio.Tick(0.26f);
        }
        _wakes = _freezes = 0;
    }
}
