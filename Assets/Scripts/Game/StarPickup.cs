using TMPro;
using UnityEngine;

/// <summary>A gold star (5 per page): Max picks it up when he touches it, awake.</summary>
public class StarPickup : MonoBehaviour
{
    private Lightable _light;
    private float _spin;
    /// <summary>Which of its page's stars this is (stars found once aren't drawn again).</summary>
    public int Index;

    public static GameObject Create(Transform parent, Vector2 at, Material m, int index)
    {
        var go = new GameObject("Star");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(at.x, at.y, -2.4f);     // in front of the captions (-2.2), behind the borders (-2.5)
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = StarMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        var l = go.AddComponent<Lightable>();
        l.points = new[] { Vector3.zero };
        go.AddComponent<StarPickup>().Index = index;
        return go;
    }

    private void Awake() => _light = GetComponent<Lightable>();

    private void Update()
    {
        float dt = _light.Delta;
        _spin += dt * 120f;
        transform.rotation = Quaternion.Euler(0f, _spin, 0f);
        var hero = HeroController.I;
        if (hero == null || !hero.Light.IsAwake) return;
        Vector2 d = (Vector2)hero.transform.position + Vector2.up * 0.9f - (Vector2)transform.position;
        if (Mathf.Abs(d.x) < 0.7f && Mathf.Abs(d.y) < 1.1f)
        {
            var gs = GameState.I;
            gs?.FindStar(Index);
            GameHUD.I?.StarFound(transform.position);                   // it flies up into the counter
            SfxLettering.Spawn("TWINKLE!", (Vector2)transform.position + Vector2.up, Palette.Yellow, 0.8f);
            GameAudio.Play("star", 0.7f);
            Destroy(gameObject);
        }
    }

    private static Mesh _star;
    /// <summary>A chunky 5-point star, extruded so the outline pass has an edge to draw.</summary>
    private static Mesh StarMesh() => _star != null ? _star : _star = Resources.Load<Mesh>("Game/Meshes/Star");
}
