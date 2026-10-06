using System.Collections.Generic;
using UnityEngine;

public class PanelLayout
{
    public Rect rect;                         // world rect on the lane
    public PageDef.Panel def;
    public int tier, index;
    public Transform root;
    public readonly List<EnemyBrain> enemies = new();
    public bool visited;
    public Vector2 entry;                     // where Max respawns in this panel
    public InkFlood flood;                    // Act 3's rising ink, if the panel has it
    public float floorLine;                   // the lowest top anything can stand on (world y): below it is a pit
}

public class TierLayout
{
    public float minX, maxX, y0;
    public readonly List<PanelLayout> panels = new();
    public float CentreY => y0 + PageDef.PanelHeight / 2f;
}

public class PageLayout
{
    public Transform root;
    public readonly List<TierLayout> tiers = new();
    public Vector2 start;
}

/// <summary>
/// Builds a page from its <see cref="PageDef"/> (GDD section 10, the PageBuilder): the panels
/// with their night-sky backdrops and distant city, the inked borders with doorway gaps where
/// floors meet the gutter, invisible bridges across the gutters for Max, the floors and
/// ledges, the props, the stars and the Inkies (each held inside its own panel).
/// Every actor lives on the z = 0 lane; backdrops sit behind it for a little parallax.
/// </summary>
public static class PageBuilder
{
    private static int _layer;
    private static System.Random _rng;
    private static int _star;                // the next star's number on the page being drawn

    public static PageLayout Build(PageDef def, Transform parent)
    {
        _layer = LayerMask.NameToLayer("Comic");
        _rng = new System.Random(def.number * 7919);
        _star = 0;
        var A = GameAssets.I;
        var layout = new PageLayout { root = new GameObject($"Page {def.number}").transform };
        layout.root.SetParent(parent, false);

        for (int t = 0; t < def.tiers.Length; t++)
        {
            var tier = new TierLayout { y0 = -t * PageDef.TierSpacing };
            float x = 0f;
            var panels = def.tiers[t].panels;
            for (int i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                var pl = new PanelLayout
                {
                    def = p, tier = t, index = i,
                    rect = new Rect(x, tier.y0, p.width, PageDef.PanelHeight),
                };
                pl.root = new GameObject($"Tier {t + 1} Panel {i + 1}").transform;
                pl.root.SetParent(layout.root, false);
                pl.entry = FindEntry(pl);
                pl.floorLine = FloorLine(pl);
                BuildPanel(pl, A, i == 0, i == panels.Length - 1);
                tier.panels.Add(pl);
                x += p.width + PageDef.Gutter;
            }
            tier.minX = 0f;
            tier.maxX = x - PageDef.Gutter;
            layout.tiers.Add(tier);
            BuildBridges(tier, layout.root);
        }
        var first = layout.tiers[0].panels[0];
        layout.start = first.rect.min + def.start;
        SetLayer(layout.root);
        return layout;
    }

    private static float FloorLine(PanelLayout pl)
    {
        float low = float.MaxValue;
        foreach (var f in pl.def.floors) low = Mathf.Min(low, f.yMax);
        foreach (var f in pl.def.erasable) low = Mathf.Min(low, f.yMax);
        foreach (var f in pl.def.ghost) low = Mathf.Min(low, f.yMax);
        return pl.rect.yMin + (low < float.MaxValue ? low : 2.5f);
    }

    /// <summary>Instantiates a panel's Inkies (used again when a panel restarts).</summary>
    public static void SpawnEnemies(PanelLayout pl, Transform parent)
    {
        foreach (var s in pl.def.spawns) Spawn(s.kind, pl.rect.min + s.at, pl, parent, s.faceRight);
    }

    /// <summary>One Inkie into a panel, its arena (Baron Blot summons Smudges this way too).</summary>
    public static EnemyBrain Spawn(EnemyKind kind, Vector2 at, PanelLayout pl, Transform parent, bool faceRight = false)
    {
        var A = GameAssets.I;
        var prefab = A != null ? A.Enemy(kind) : null;
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, new Vector3(at.x, at.y, 0f), Quaternion.identity, parent);
        go.name = kind.ToString();
        SetLayer(go.transform);
        var brain = go.GetComponent<EnemyBrain>();
        if (brain == null) return null;
        brain.MinX = pl.rect.xMin;
        brain.MaxX = pl.rect.xMax;
        brain.Facing = faceRight ? 1f : -1f;
        brain.Panel = pl;
        brain.Home = at;
        if (brain is DotShotBrain ds && A != null) ds.PelletMaterial = A.ink;
        var body = go.GetComponent<CharacterController>();
        InkShadow.Add(go, body != null ? body.radius * 2.7f : 1f);
        pl.enemies.Add(brain);
        return brain;
    }

    // ---- one panel ------------------------------------------------------------------------------

    private static void BuildPanel(PanelLayout pl, GameAssets A, bool firstInTier, bool lastInTier)
    {
        var r = pl.rect;
        var p = pl.def;

        // the night sky in gradation tone (dark overhead, the city's glow low down, pricked with stars),
        // and the city behind the lane in three depths, the way a manga background recedes: a pale
        // skyline in fine line, a middle layer in tone, the near buildings in solid ink
        Quad(pl.root, "Sky", new Vector3(r.center.x, r.center.y, 8f), new Vector2(r.width, r.height), A.sky);
        if (_rng.NextDouble() < 0.6)
        {
            float mx = r.xMin + (float)(_rng.NextDouble() * 0.6 + 0.2) * r.width;
            Disc(pl.root, "Moon", new Vector3(mx, r.yMax - 2.2f, 7.8f), 0.9f + (float)_rng.NextDouble() * 0.5f, A.moon);
        }
        Skyline(pl, A.skylineFar != null ? A.skylineFar : A.far, A.window, 7.2f, 2.2f, 4.8f, 1.0f, 2.2f, 0.05f, 0.5f, 0f, p.backdrop);
        Skyline(pl, A.skylineMid != null ? A.skylineMid : A.far, A.window, 6.6f, 3.0f, 6.6f, 1.2f, 2.6f, 0.3f, 1.4f, 0.07f, p.backdrop);
        Skyline(pl, A.far, A.window, 6.0f, 3.6f, 8.6f, 1.4f, 3.4f, 1.1f, 3.4f, 0.22f, p.backdrop);

        // floors: rooftops you can stand on (the building front runs down out of the panel)
        foreach (var f in p.floors)
        {
            var w = new Rect(r.xMin + f.x, r.yMin + f.y, f.width, f.height);
            float bottom = Mathf.Min(w.yMin, r.yMin - 0.3f);
            if (w.yMin <= r.yMin + 0.05f) w.yMin = bottom;
            Box(pl.root, "Roof", new Vector3(w.center.x, w.center.y, 0.5f), new Vector3(w.width, w.height, 3f), A.roof);
            if (w.height > 1.5f)                                  // a darker band under the parapet
                Box(pl.root, "Wall", new Vector3(w.center.x, w.center.y - 0.35f, -0.02f), new Vector3(w.width - 0.2f, w.height - 1f, 3f), A.wall, collider: false);
        }
        foreach (var f in p.erasable)
        {
            for (float ex = 0f; ex < f.width - 0.01f; ex += 1f)
            {
                float bw = Mathf.Min(1f, f.width - ex);
                var b = Box(pl.root, "Erasable", new Vector3(r.xMin + f.x + ex + bw / 2f, r.yMin + f.y + f.height / 2f, 0.5f),
                            new Vector3(bw - 0.04f, f.height, 2.4f), A.wood);
                if (b.TryGetComponent(out BoxCollider solid))                 // drawn with a hairline gap, solid edge to edge
                    solid.size = new Vector3(bw / (bw - 0.04f), 1f, 1f);
                b.AddComponent<ErasableBlock>();
            }
        }
        foreach (var f in p.ghost)
            InvisibleInk.Ledge(Box(pl.root, "InvisibleInk", new Vector3(r.xMin + f.center.x, r.yMin + f.center.y, 0.5f),
                                   new Vector3(f.width, f.height, 2.4f), A.ink));

        foreach (var prop in p.props) BuildProp(pl, prop, A);
        // each of the page's stars has its number (in drawing order); one already found isn't drawn again. They're
        // placed clear of the caption's box, and printed in front of it when it slides over them as the camera pans
        var gs = GameState.I;
        foreach (var s in p.stars)
        {
            int n = _star++;
            if (gs == null || !gs.StarFound(gs.Page, n)) StarPickup.Create(pl.root, r.min + s, A.star, n);
        }
        foreach (var s in p.ghostStars)
        {
            int n = _star++;
            if (gs == null || !gs.StarFound(gs.Page, n)) InvisibleInk.Secret(StarPickup.Create(pl.root, r.min + s, A.star, n));
        }
        if (p.flood >= 0f) pl.flood = InkFlood.Create(pl, p.flood, p.floodTop, p.floodRise, A.ink);
        if (!string.IsNullOrEmpty(p.caption)) Caption.Create(pl.root, new Vector2(r.xMin + 0.35f, r.yMax - 0.35f), p.caption, r);

        BuildBorder(pl, A, firstInTier, lastInTier);
    }

    /// <summary>One depth of the city behind a panel: silhouettes left to right, with gaps between them
    /// (wider the nearer the layer, so the layers behind show through) and a few lit windows. The
    /// Inkworks (backdrop 1) adds smokestacks; Blot's tower (backdrop 2) adds spires.</summary>
    private static void Skyline(PanelLayout pl, Material m, Material lit, float z, float minH, float maxH, float minW, float maxW,
        float minGap, float maxGap, float windows, int backdrop)
    {
        var r = pl.rect;
        float R() => (float)_rng.NextDouble();
        float near = Mathf.InverseLerp(7.2f, 6.0f, z);                 // 0 the far skyline .. 1 the near buildings
        float x = r.xMin - R() * 1.2f;
        while (x < r.xMax - 0.3f)
        {
            float w = Mathf.Lerp(minW, maxW, R());
            float h = Mathf.Lerp(minH, maxH, R());
            bool stack = backdrop == 1 && R() < 0.35f;
            bool spire = backdrop == 2 && R() < 0.3f;
            if (stack) { w = Mathf.Lerp(0.45f, 0.8f, R()); h = maxH * Mathf.Lerp(0.95f, 1.2f, R()); }
            float x0 = Mathf.Max(x, r.xMin + 0.05f), x1 = Mathf.Min(x + w, r.xMax - 0.05f);
            if (x1 - x0 > 0.25f)
            {
                float cx = (x0 + x1) / 2f, cw = x1 - x0;
                h = Mathf.Min(h, r.height - (spire ? 2.6f : 0.8f));
                Box(pl.root, "Skyline", new Vector3(cx, r.yMin + h / 2f, z), new Vector3(cw, h, 1f), m, collider: false);
                if (stack) Box(pl.root, "StackLip", new Vector3(cx, r.yMin + h - 0.12f, z - 0.02f), new Vector3(cw + 0.22f, 0.24f, 1.05f), m, collider: false);
                if (spire) Cone(pl.root, "Spire", new Vector3(cx, r.yMin + h + 0.9f, z), cw * 0.5f, 1.8f, m);
                Vector2 pane = new Vector2(0.28f, 0.42f) * Mathf.Lerp(0.6f, 1f, near);
                for (float wy = r.yMin + 1.2f; wy < r.yMin + h - 0.8f; wy += 1.1f)
                    for (float wx = x0 + 0.4f; wx < x1 - 0.3f; wx += 0.7f)
                        if (R() < windows)
                            Quad(pl.root, "Window", new Vector3(wx, wy, z - 0.55f), pane, lit);
            }
            x += w + Mathf.Lerp(minGap, maxGap, R());
        }
    }

    /// <summary>Where Max respawns: on the leftmost floor of the panel.</summary>
    private static Vector2 FindEntry(PanelLayout pl)
    {
        float bestX = float.MaxValue;
        Vector2 at = pl.rect.min + new Vector2(1.2f, 3f);
        foreach (var f in pl.def.floors)
            if (f.x < bestX)
            {
                bestX = f.x;
                at = pl.rect.min + new Vector2(Mathf.Max(f.x, 0f) + 1.0f, f.y + f.height + 0.1f);
            }
        return at;
    }

    // ---- borders and gutters ---------------------------------------------------------------------

    private static void BuildBorder(PanelLayout pl, GameAssets A, bool firstInTier, bool lastInTier)
    {
        const float T = 0.22f, Z = -2.5f, Door = 3.2f;
        var r = pl.rect;
        // a slight hand-inked wobble: each stroke is a touch off true
        float Wob() => (float)(_rng.NextDouble() - 0.5) * 0.06f;
        Box(pl.root, "BorderTop", new Vector3(r.center.x, r.yMax, Z), new Vector3(r.width + T, T + Wob(), 0.1f), A.border, collider: false);
        Box(pl.root, "BorderBottom", new Vector3(r.center.x, r.yMin, Z), new Vector3(r.width + T, T + Wob(), 0.1f), A.border, collider: false);

        foreach (bool left in new[] { true, false })
        {
            float x = left ? r.xMin : r.xMax;
            // doorways where a floor meets this edge (Max walks through the gap in the ink)
            var gaps = new List<Vector2>();
            foreach (var f in pl.def.floors)
            {
                bool touches = left ? f.x <= 0.05f : f.x + f.width >= pl.def.width - 0.05f;
                if (touches) gaps.Add(new Vector2(r.yMin + f.y + f.height, r.yMin + f.y + f.height + Door));
            }
            if ((left && firstInTier && pl.tier == 0) || (!left && lastInTier)) { }   // page edges: still a doorway out
            gaps.Sort((a, b) => a.x.CompareTo(b.x));
            float y = r.yMin;
            foreach (var g in gaps)
            {
                if (g.x > y) VBar(pl.root, x, y, g.x, Z, T, A.border);
                y = Mathf.Max(y, g.y);
            }
            if (y < r.yMax) VBar(pl.root, x, y, r.yMax, Z, T, A.border);
        }
    }

    private static void VBar(Transform parent, float x, float y0, float y1, float z, float t, Material m)
    {
        Box(parent, "BorderSide", new Vector3(x, (y0 + y1) / 2f, z), new Vector3(t, y1 - y0 + t, 0.1f), m, collider: false);
    }

    /// <summary>Invisible floor across each gutter where two panels' floors meet at the same height.</summary>
    private static void BuildBridges(TierLayout tier, Transform root)
    {
        for (int i = 0; i + 1 < tier.panels.Count; i++)
        {
            var a = tier.panels[i];
            var b = tier.panels[i + 1];
            foreach (var fa in a.def.floors)
            {
                if (fa.x + fa.width < a.def.width - 0.05f) continue;
                foreach (var fb in b.def.floors)
                {
                    if (fb.x > 0.05f) continue;
                    float top = Mathf.Min(fa.y + fa.height, fb.y + fb.height);
                    if (Mathf.Abs((fa.y + fa.height) - (fb.y + fb.height)) > 0.6f) continue;
                    var go = new GameObject("GutterBridge");
                    go.transform.SetParent(root, false);
                    var c = go.AddComponent<BoxCollider>();
                    go.transform.position = new Vector3(a.rect.xMax + PageDef.Gutter / 2f, a.rect.yMin + top - 0.5f, 0.5f);
                    c.size = new Vector3(PageDef.Gutter + 0.4f, 1f, 3f);
                }
            }
        }
    }

    // ---- props ---------------------------------------------------------------------------------

    private static void BuildProp(PanelLayout pl, PageDef.Prop prop, GameAssets A)
    {
        Vector2 at = pl.rect.min + prop.at;
        float s = prop.size > 0f ? prop.size : 1f;
        switch (prop.kind)
        {
            case PropKind.Chimney:
                Box(pl.root, "Chimney", new Vector3(at.x, at.y + 0.9f * s, 1.6f), new Vector3(0.9f, 1.8f, 0.9f) * s, A.wall, collider: false);
                Box(pl.root, "ChimneyCap", new Vector3(at.x, at.y + 1.85f * s, 1.6f), new Vector3(1.15f, 0.25f, 1.1f) * s, A.roof, collider: false);
                break;
            case PropKind.WaterTower:
                for (int k = -1; k <= 1; k += 2)
                    Box(pl.root, "Leg", new Vector3(at.x + k * 0.8f * s, at.y + 1.2f * s, 2.2f), new Vector3(0.12f, 2.4f, 0.12f) * s, A.metal, collider: false);
                Cylinder(pl.root, "Tank", new Vector3(at.x, at.y + 3.1f * s, 2.2f), 1.1f * s, 1.8f * s, A.wood);
                Cone(pl.root, "TankRoof", new Vector3(at.x, at.y + 4.3f * s, 2.2f), 1.25f * s, 0.7f * s, A.roof);
                break;
            case PropKind.Antenna:
                Box(pl.root, "Mast", new Vector3(at.x, at.y + 1.4f * s, 1.2f), new Vector3(0.07f, 2.8f, 0.07f) * s, A.metal, collider: false);
                for (int k = 0; k < 3; k++)
                    Box(pl.root, "Rung", new Vector3(at.x, at.y + (1.6f + k * 0.45f) * s, 1.2f), new Vector3((1.1f - k * 0.25f), 0.06f, 0.06f) * s, A.metal, collider: false);
                break;
            case PropKind.Billboard:
                Box(pl.root, "Board", new Vector3(at.x, at.y + 2.6f * s, 2.5f), new Vector3(4.2f, 2.2f, 0.2f) * s, A.paper, collider: false);
                for (int k = -1; k <= 1; k += 2)
                    Box(pl.root, "Post", new Vector3(at.x + k * 1.4f * s, at.y + 0.8f * s, 2.6f), new Vector3(0.14f, 1.6f, 0.14f) * s, A.metal, collider: false);
                break;
            case PropKind.Vent:
                Box(pl.root, "Vent", new Vector3(at.x, at.y + 0.4f * s, 1.4f), new Vector3(0.8f, 0.8f, 0.8f) * s, A.metal, collider: false);
                break;
            case PropKind.Crate:
                FallingProp.Create(pl.root, at + Vector2.up * 0.5f * s, new Vector3(1f, 1f, 1f) * s, A.wood, hazard: false, "Crate");
                break;
            case PropKind.Flowerpot:
                FallingProp.Create(pl.root, at, new Vector3(0.55f, 0.6f, 0.55f) * s, A.wall, hazard: true, "Flowerpot", fallSpeed: 3f);
                break;
            case PropKind.Brick:
                FallingProp.Create(pl.root, at, new Vector3(0.8f, 0.4f, 0.5f) * s, A.wall, hazard: true, "Brick", fallSpeed: 4f);
                break;
            case PropKind.Pigeon:
                Pigeon.Create(pl.root, at, A.ink);
                break;
            case PropKind.Chandelier:
                Chandelier(pl, at, s, A);
                break;
            case PropKind.Bubble:
                SpeechBubble.Create(pl.root, at, prop.text ?? "...", s, at + prop.tail);
                break;
            case PropKind.Press:
                Press.Create(pl, at, s, Mathf.Repeat(prop.at.x * 0.37f, 1f));
                break;
            case PropKind.Conveyor:
                Conveyor.Create(pl, at.x, at.x + Mathf.Abs(prop.size), at.y, Conveyor.BeltSpeed * Mathf.Sign(prop.size));
                break;
        }
    }

    /// <summary>Blot's chandelier: hangs by a thread from the panel's top until a flare snaps it,
    /// then drops on whoever is underneath (30 damage: lure the Baron under it).</summary>
    private static void Chandelier(PanelLayout pl, Vector2 at, float s, GameAssets A)
    {
        float top = pl.rect.yMax;
        Box(pl.root, "Thread", new Vector3(at.x, (at.y + 0.3f * s + top) / 2f, 0.2f), new Vector3(0.05f, top - at.y - 0.3f * s, 0.05f), A.metal, collider: false);
        var go = new GameObject("Chandelier");
        go.transform.SetParent(pl.root, false);
        go.transform.position = new Vector3(at.x, at.y, 0f);
        go.layer = _layer;
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(2.4f, 0.4f, 1f) * s;
        Box(go.transform, "Ring", new Vector3(at.x, at.y, 0f), new Vector3(2.4f, 0.22f, 0.9f) * s, A.metal, collider: false);
        Box(go.transform, "Hub", new Vector3(at.x, at.y + 0.25f * s, 0f), new Vector3(0.35f, 0.5f, 0.35f) * s, A.metal, collider: false);
        for (int k = -1; k <= 1; k++)
        {
            float x = at.x + k * 0.85f * s;
            Box(go.transform, "Candle", new Vector3(x, at.y + 0.33f * s, 0f), new Vector3(0.15f, 0.42f, 0.15f) * s, A.paper, collider: false);
            Box(go.transform, "Flame", new Vector3(x, at.y + 0.64f * s, 0f), new Vector3(0.09f, 0.16f, 0.09f) * s, A.paper, collider: false);
        }
        for (int k = 0; k < 4; k++)                                   // crystal drops
            Box(go.transform, "Drop", new Vector3(at.x + (-1.05f + k * 0.7f) * s, at.y - 0.25f * s, 0f), new Vector3(0.08f, 0.26f, 0.08f) * s, A.paper, collider: false);
        FallingProp.Attach(go, hazard: true, damage: 30f, held: true);
    }

    // ---- primitives ------------------------------------------------------------------------------

    public static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        go.layer = _layer;
        return go;
    }

    public static GameObject Quad(Transform parent, string name, Vector3 centre, Vector2 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        Object.Destroy(go.GetComponent<Collider>());
        go.layer = _layer;
        return go;
    }

    private static GameObject Disc(Transform parent, string name, Vector3 centre, float radius, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        Object.Destroy(go.GetComponent<Collider>());
        go.layer = _layer;
        return go;
    }

    private static GameObject Cylinder(Transform parent, string name, Vector3 centre, float radius, float height, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        Object.Destroy(go.GetComponent<Collider>());
        go.layer = _layer;
        return go;
    }

    private static void Cone(Transform parent, string name, Vector3 centre, float radius, float height, Material m)
    {
        var mesh = new Mesh { name = "Cone" };
        const int n = 20;
        var v = new List<Vector3> { new(0f, height / 2f, 0f) };
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            v.Add(new Vector3(Mathf.Cos(a) * radius, -height / 2f, Mathf.Sin(a) * radius));
        }
        var tris = new List<int>();
        for (int i = 0; i < n; i++) tris.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
        mesh.SetVertices(v);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.layer = _layer;
    }

    public static void SetLayer(Transform t)
    {
        int layer = LayerMask.NameToLayer("Comic");
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
    }
}
