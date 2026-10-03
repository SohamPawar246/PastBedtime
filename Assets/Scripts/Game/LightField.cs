using System.Collections.Generic;
using UnityEngine;

public enum Lens { Clear, Green, Red, Ghost }

/// <summary>
/// The one rule (GDD section 2): only what's lit is alive.
///
///     Awake = inTorch OR inDoorWedge OR flare OR roomLight
///
/// Holds the torch beam (centre and radius in comic-world units, on the z = 0 lane), the
/// lens, Mom's door wedges, the flare and the room light; answers "is this lit?" for
/// every <see cref="Lightable"/>; and pushes the beam to global shader values so the page
/// is printed bright inside the light and read-in-the-dark outside it.
/// </summary>
[DefaultExecutionOrder(-100)]
public class LightField : MonoBehaviour
{
    public static LightField I { get; private set; }

    /// <summary>Max is lit 0.3 units past the beam's edge, so the edge never feels unfair.</summary>
    public const float HeroGrace = 0.3f;
    /// <summary>The visible soft edge of the beam.</summary>
    public const float Feather = 0.4f;

    // ---- the torch (set by TorchController) -------------------------------------------------
    public Vector2 BeamCentre;
    public float BeamRadius = 4f;
    public bool BeamOn = true;
    /// <summary>A low-charge flicker: the beam drops out and the page freezes.</summary>
    public bool Dropout;
    public Lens Lens = Lens.Clear;
    /// <summary>Visual brightness of the beam (dims while twisting, flares on overwind).</summary>
    public float Brightness = 1f;

    // ---- the other wake-lights ---------------------------------------------------------------
    /// <summary>Seconds of page-wide flare left.</summary>
    public float FlareTime;
    /// <summary>Mom's room light (finale): the whole page is awake.</summary>
    public bool RoomLight;
    /// <summary>Wedges of hallway light on the page (polygons in comic-world units).</summary>
    public readonly List<Vector2[]> Wedges = new();

    public bool BeamLive => BeamOn && !Dropout && BeamRadius > 0.01f;
    public bool PageAwake => FlareTime > 0f || RoomLight;

    private static readonly int BeamId = Shader.PropertyToID("_PB_Beam");
    private static readonly int LensId = Shader.PropertyToID("_PB_Lens");
    private static readonly int WakeId = Shader.PropertyToID("_PB_Wake");
    private static readonly int GameId = Shader.PropertyToID("_PB_GameLight");
    private static readonly int WedgeAId = Shader.PropertyToID("_PB_WedgeA");
    private static readonly int WedgeBId = Shader.PropertyToID("_PB_WedgeB");
    private static readonly int WedgeOnId = Shader.PropertyToID("_PB_WedgeOn");

    private void Awake()
    {
        I = this;
        Shader.SetGlobalFloat(GameId, 1f);
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
        Shader.SetGlobalFloat(GameId, 0f);         // the title's cover is printed without the grade
        Shader.SetGlobalFloat(WakeId, 0f);
        Shader.SetGlobalFloat(WedgeOnId, 0f);
    }

    private void Update()
    {
        if (FlareTime > 0f) FlareTime = Mathf.Max(0f, FlareTime - Time.deltaTime);
        Push();
    }

    /// <summary>Is a point on the lane lit by any wake-light? `margin` widens the beam (Max's grace).</summary>
    public bool IsLit(Vector2 p, float margin = 0f)
    {
        if (PageAwake) return true;
        if (BeamLive && (p - BeamCentre).sqrMagnitude <= (BeamRadius + margin) * (BeamRadius + margin)) return true;
        foreach (var w in Wedges)
            if (InPolygon(p, w)) return true;
        return false;
    }

    /// <summary>Awake if any sample point is lit (GDD: big enemies half in the light).</summary>
    public bool IsAwake(IReadOnlyList<Vector3> points, bool isHero)
    {
        float margin = isHero ? HeroGrace : 0f;
        for (int i = 0; i < points.Count; i++)
            if (IsLit(points[i], margin)) return true;
        return false;
    }

    /// <summary>0 outside, 1 inside the beam proper, smooth over the feather (for effects).</summary>
    public float BeamAmount(Vector2 p)
    {
        if (PageAwake) return 1f;
        if (!BeamLive) return 0f;
        float d = (p - BeamCentre).magnitude;
        return 1f - Mathf.SmoothStep(0f, 1f, (d - (BeamRadius - Feather)) / Feather);
    }

    private void Push()
    {
        float r = BeamLive ? BeamRadius : 0f;
        Shader.SetGlobalVector(BeamId, new Vector4(BeamCentre.x, BeamCentre.y, r, Feather));
        Color lens = Lens switch
        {
            Lens.Green => Palette.LensGreen,
            Lens.Red => Palette.LensRed,
            Lens.Ghost => Palette.LensGhost,
            _ => Palette.LensClear,
        };
        float strength = Lens == Lens.Clear ? 0.18f : 0.5f;
        Shader.SetGlobalVector(LensId, new Vector4(lens.r, lens.g, lens.b, strength * Brightness));
        float wake = RoomLight ? 1f : Mathf.Clamp01(FlareTime / 0.25f);
        Shader.SetGlobalFloat(WakeId, wake);
        if (Wedges.Count > 0 && Wedges[0].Length == 4)
        {
            var w = Wedges[0];
            Shader.SetGlobalVector(WedgeAId, new Vector4(w[0].x, w[0].y, w[1].x, w[1].y));
            Shader.SetGlobalVector(WedgeBId, new Vector4(w[2].x, w[2].y, w[3].x, w[3].y));
            Shader.SetGlobalFloat(WedgeOnId, 1f);
        }
        else Shader.SetGlobalFloat(WedgeOnId, 0f);
    }

    private static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }
}
