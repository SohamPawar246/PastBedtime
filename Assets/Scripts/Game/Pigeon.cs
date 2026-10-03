using TMPro;
using UnityEngine;

/// <summary>A pigeon frozen mid-flap (GDD page 1): light it and it flutters off the page.</summary>
public class Pigeon : MonoBehaviour
{
    private Lightable _light;
    private Transform _wingL, _wingR;
    private float _t;
    private bool _flying;

    public static void Create(Transform parent, Vector2 at, Material m)
    {
        var root = new GameObject("Pigeon");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(at.x, at.y, 0.2f);
        var body = PageBuilder.Box(root.transform, "Body", root.transform.position + new Vector3(0f, 0.22f, 0f), new Vector3(0.5f, 0.3f, 0.3f), m, collider: false);
        PageBuilder.Box(root.transform, "Head", root.transform.position + new Vector3(0.25f, 0.42f, 0f), new Vector3(0.18f, 0.18f, 0.18f), m, collider: false);
        var l = root.AddComponent<Lightable>();
        l.points = new[] { new Vector3(0f, 0.25f, 0f) };
        var p = root.AddComponent<Pigeon>();
        p._wingL = PageBuilder.Box(root.transform, "Wing", root.transform.position + new Vector3(0f, 0.3f, -0.18f), new Vector3(0.4f, 0.04f, 0.3f), m, collider: false).transform;
        p._wingR = PageBuilder.Box(root.transform, "Wing", root.transform.position + new Vector3(0f, 0.3f, 0.18f), new Vector3(0.4f, 0.04f, 0.3f), m, collider: false).transform;
    }

    private void Awake() => _light = GetComponent<Lightable>();

    private void Update()
    {
        float dt = _light.Delta;
        if (dt <= 0f) return;
        _t += dt;
        if (!_flying && _t > 0.4f)
        {
            _flying = true;
            SfxLettering.Spawn("COO!", (Vector2)transform.position + Vector2.up, Palette.Paper, 0.6f);
        }
        if (_flying)
        {
            transform.position += new Vector3(2.5f, 3f, 0f) * dt;
            float flap = Mathf.Sin(_t * 30f) * 50f;
            if (_wingL) _wingL.localRotation = Quaternion.Euler(flap, 0f, 0f);
            if (_wingR) _wingR.localRotation = Quaternion.Euler(-flap, 0f, 0f);
            if (_t > 6f) Destroy(gameObject);
        }
    }
}
