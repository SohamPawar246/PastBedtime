using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A texture drawn on any four-cornered shape with true perspective: a page lying open on a bed,
/// a comic's cover swinging open. The quad is split into a grid whose vertices follow the
/// projective map from the unit square to the corners, so lines stay straight and spacing
/// foreshortens like a real tilted page. Also maps a point on the shape back to its UV (the torch
/// aiming at the comic).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class QuadImage : MaskableGraphic
{
    [SerializeField] private Texture _texture;
    /// <summary>Corners in local units: bottom-left, top-left, top-right, bottom-right.</summary>
    public Vector2 bl = new(-100f, -100f), tl = new(-100f, 100f), tr = new(100f, 100f), br = new(100f, -100f);
    public Rect uvRect = new(0f, 0f, 1f, 1f);
    [Range(1, 32)] public int grid = 16;

    private Matrix4x4 _h, _inv;          // unit square -> quad (projective), and back

    public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

    public Texture texture
    {
        get => _texture;
        set { if (_texture == value) return; _texture = value; SetMaterialDirty(); }
    }

    public void SetCorners(Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
    {
        bl = bottomLeft; tl = topLeft; tr = topRight; br = bottomRight;
        SetVerticesDirty();
    }

    public Vector2[] Corners => new[] { bl, tl, tr, br };

    /// <summary>The point on the quad at (u, v) in [0,1]^2 (u right, v up).</summary>
    public Vector2 UVToLocal(Vector2 uv)
    {
        Solve();
        Vector3 p = _h * new Vector4(uv.x, uv.y, 1f, 0f);
        return new Vector2(p.x / p.z, p.y / p.z);
    }

    /// <summary>The (u, v) under a local point; false if it's outside the quad.</summary>
    public bool LocalToUV(Vector2 local, out Vector2 uv)
    {
        Solve();
        Vector3 q = _inv * new Vector4(local.x, local.y, 1f, 0f);
        uv = Mathf.Abs(q.z) > 1e-8f ? new Vector2(q.x / q.z, q.y / q.z) : Vector2.zero;
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    /// <summary>The projective map taking (0,0),(1,0),(1,1),(0,1) to bl, br, tr, tl (Heckbert's square-to-quad).</summary>
    private void Solve()
    {
        Vector2 p0 = bl, p1 = br, p2 = tr, p3 = tl;
        float sx = p0.x - p1.x + p2.x - p3.x, sy = p0.y - p1.y + p2.y - p3.y;
        float a, b, c, d, e, f, g, h;
        if (Mathf.Abs(sx) < 1e-6f && Mathf.Abs(sy) < 1e-6f)
        {
            a = p1.x - p0.x; b = p3.x - p0.x; c = p0.x;
            d = p1.y - p0.y; e = p3.y - p0.y; f = p0.y;
            g = 0f; h = 0f;
        }
        else
        {
            float dx1 = p1.x - p2.x, dx2 = p3.x - p2.x, dy1 = p1.y - p2.y, dy2 = p3.y - p2.y;
            float det = dx1 * dy2 - dx2 * dy1;
            if (Mathf.Abs(det) < 1e-9f) det = 1e-9f;
            g = (sx * dy2 - dx2 * sy) / det;
            h = (dx1 * sy - sx * dy1) / det;
            a = p1.x - p0.x + g * p1.x; b = p3.x - p0.x + h * p3.x; c = p0.x;
            d = p1.y - p0.y + g * p1.y; e = p3.y - p0.y + h * p3.y; f = p0.y;
        }
        _h = new Matrix4x4(new Vector4(a, d, g, 0f), new Vector4(b, e, h, 0f), new Vector4(c, f, 1f, 0f), new Vector4(0f, 0f, 0f, 1f));
        var m3 = _h;
        m3.m33 = 1f;
        _inv = m3.inverse;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Solve();
        int n = Mathf.Max(1, grid);
        Color32 col = color;
        for (int j = 0; j <= n; j++)
        {
            for (int i = 0; i <= n; i++)
            {
                var uv = new Vector2((float)i / n, (float)j / n);
                Vector2 pos = UVToLocal(uv);
                vh.AddVert(pos, col, new Vector2(uvRect.x + uv.x * uvRect.width, uvRect.y + uv.y * uvRect.height));
            }
        }
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 2, d = a + n + 1;
                vh.AddTriangle(a, d, c);
                vh.AddTriangle(a, c, b);
            }
        }
    }

    public override bool Raycast(Vector2 sp, Camera eventCamera) => false;   // decoration, never blocks clicks
}
