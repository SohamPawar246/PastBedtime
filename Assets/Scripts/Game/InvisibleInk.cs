using UnityEngine;

/// <summary>
/// Invisible ink (GDD section 4, the Ghost lens): ledges and secret stars drawn in ink only the
/// violet beam shows. While the Ghost lens is on them they ink in and are real: solid underfoot,
/// a star you can take. Out of it they fade back to a faint dotted hint (a secret star shows
/// nothing at all) and Max falls through, after a quarter second's grace so a flick of the lens
/// wheel isn't a fall. Flares and Mom's light don't show it: only the Ghost lens does.
/// </summary>
public class InvisibleInk : MonoBehaviour
{
    public const float Grace = 0.25f, InkIn = 0.12f;

    /// <summary>Inked in and solid right now.</summary>
    public bool Real => _shown > 0.5f;

    private float _shown, _since = 99f;
    private Vector3 _scale;
    private Bounds _area;                     // the whole ledge, measured before it's faded
    private Renderer[] _ink;
    private Collider[] _solid;
    private Behaviour[] _live;

    private void Awake()
    {
        _scale = transform.localScale;
        _ink = GetComponentsInChildren<Renderer>(true);
        _area = _ink.Length > 0 ? _ink[0].bounds : new Bounds(transform.position, Vector3.one);
        _solid = GetComponentsInChildren<Collider>(true);
        _live = GetComponents<StarPickup>();
        Apply();
    }

    private void Update()
    {
        var lf = LightField.I;
        bool shown = lf != null && lf.Lens == Lens.Ghost && lf.BeamLive && Touches(lf.BeamCentre, lf.BeamRadius);
        _since = shown ? 0f : _since + Time.deltaTime;
        _shown = Mathf.MoveTowards(_shown, _since <= Grace ? 1f : 0f, Time.deltaTime / InkIn);
        Apply();
    }

    private bool Touches(Vector2 centre, float radius)
    {
        var b = _area;
        Vector2 nearest = new(Mathf.Clamp(centre.x, b.min.x, b.max.x), Mathf.Clamp(centre.y, b.min.y, b.max.y));
        return (nearest - centre).sqrMagnitude <= radius * radius;
    }

    private void Apply()
    {
        bool real = Real;
        foreach (var r in _ink) r.enabled = _shown > 0.02f;
        foreach (var c in _solid) c.enabled = real;
        foreach (var l in _live) l.enabled = real;
        // inks in from the middle out, like a stroke being drawn
        transform.localScale = new Vector3(_scale.x * Mathf.Lerp(0.2f, 1f, _shown), _scale.y, _scale.z);
    }
}
