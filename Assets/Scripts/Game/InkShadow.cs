using UnityEngine;

/// <summary>
/// A pool of ink under a figure's feet, on whatever is below (a roof, a ledge, a frozen Inkie), the
/// way a manga artist grounds a figure with a solid black shadow. It lies on the top of the surface,
/// which the page camera sees from a hair above, and shrinks as its owner rises off the ground.
/// </summary>
public class InkShadow : MonoBehaviour
{
    public float width = 1f;

    private Transform _shadow;
    private Collider[] _own;
    private static Mesh _disc;
    private static readonly RaycastHit[] _hits = new RaycastHit[8];

    public static InkShadow Add(GameObject who, float width)
    {
        var s = who.AddComponent<InkShadow>();
        s.width = width;
        return s;
    }

    private void Start()
    {
        _own = GetComponentsInChildren<Collider>(true);
        var go = new GameObject("InkShadow");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = Disc;
        go.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.I != null ? GameAssets.I.border : null;
        go.layer = gameObject.layer;
        _shadow = go.transform;
    }

    private void LateUpdate()
    {
        if (_shadow == null) return;
        Vector3 from = transform.position + Vector3.up * 0.2f;
        int n = Physics.RaycastNonAlloc(from, Vector3.down, _hits, 10f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = _hits[i].collider;
            if (System.Array.IndexOf(_own, c) >= 0) continue;
            if (_hits[i].distance < best) best = _hits[i].distance;
        }
        bool show = best < float.MaxValue;
        if (_shadow.gameObject.activeSelf != show) _shadow.gameObject.SetActive(show);
        if (!show) return;
        float height = Mathf.Max(0f, best - 0.2f);
        float k = Mathf.Clamp01(1f - height / 5f);
        _shadow.position = new Vector3(transform.position.x, from.y - best + 0.012f, 0.45f);
        _shadow.rotation = Quaternion.identity;
        _shadow.localScale = new Vector3(width * Mathf.Lerp(0.3f, 1f, k * k), 1f, 2.6f * Mathf.Lerp(0.5f, 1f, k));
    }

    /// <summary>A flat disc lying in the ground plane, radius 0.5, facing up.</summary>
    private static Mesh Disc
    {
        get
        {
            if (_disc != null) return _disc;
            const int n = 28;
            var v = new Vector3[n + 1];
            var nm = new Vector3[n + 1];
            var t = new int[n * 3];
            v[0] = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
                t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % n; t[i * 3 + 2] = 1 + i;
            }
            for (int i = 0; i < nm.Length; i++) nm[i] = Vector3.up;
            _disc = new Mesh { name = "InkShadow", vertices = v, normals = nm, triangles = t };
            _disc.RecalculateBounds();
            return _disc;
        }
    }
}
