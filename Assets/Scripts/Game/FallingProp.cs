using TMPro;
using UnityEngine;

/// <summary>
/// Things that fall (GDD section 7): a flowerpot, a brick, a crate. Lit, they fall; frozen, they
/// hang in the air and are solid, so they make steps. Hazards hurt whatever awake thing they
/// land on, Inkies included (a choreography hit).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Lightable))]
public class FallingProp : MonoBehaviour
{
    public bool hazard;
    public float damage = 12f;
    /// <summary>Hanging by a thread until a flare's SPRONG shakes it loose (the chandelier).</summary>
    public bool held;
    private Rigidbody _rb;
    private Lightable _light;
    private bool _spent, _startHeld;
    private Vector3 _home;
    private Quaternion _homeRot;
    private float _gone, _pop = 1f;
    private RigidbodyConstraints _homeConstraints;

    private const RigidbodyConstraints Falling = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

    public static FallingProp Create(Transform parent, Vector2 at, Vector3 size, Material m, bool hazard, string name, float fallSpeed = 0f)
    {
        var go = PageBuilder.Box(parent, name, new Vector3(at.x, at.y, 0f), size, m);
        var p = Attach(go, hazard);
        go.GetComponent<Rigidbody>().linearVelocity = new Vector3(0f, -fallSpeed, 0f);   // already falling when the page is drawn
        return p;
    }

    /// <summary>Makes any solid thing a falling prop (the chandelier is built from parts).</summary>
    public static FallingProp Attach(GameObject go, bool hazard, float damage = 12f, bool held = false)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.constraints = held ? RigidbodyConstraints.FreezeAll : Falling;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var l = go.AddComponent<Lightable>();
        l.points = new[] { Vector3.zero };
        var p = go.AddComponent<FallingProp>();
        p.hazard = hazard;
        p.damage = damage;
        p.held = held;
        return p;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _light = GetComponent<Lightable>();
        _scale = transform.localScale;
    }

    private void Start()
    {
        if (held && TorchController.I != null) TorchController.I.Charge.Flared += Release;
        _home = transform.position;
        _homeRot = transform.rotation;
        _startHeld = held;
        _homeConstraints = _rb.constraints;
        // the panel's floor line: below it a prop has dropped into a pit (lit or frozen, it's no use there)
        _pit = _home.y - 10f;
        var layout = PageManager.I != null ? PageManager.I.Layout : null;
        if (layout != null)
            foreach (var tier in layout.tiers)
                foreach (var pl in tier.panels)
                    if (pl.rect.Contains((Vector2)_home)) _pit = pl.rect.yMin + 1.0f;
    }

    private float _pit;

    /// <summary>Back where the page drew it, as it was (a panel restart; or it fell out of its panel).</summary>
    public void Redraw()
    {
        if (_rb == null) return;
        bool kin = _rb.isKinematic;
        _rb.isKinematic = true;
        transform.SetPositionAndRotation(_home, _homeRot);
        _rb.position = _home;
        _rb.rotation = _homeRot;
        if (!kin) { _rb.isKinematic = false; _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
        else _rb.isKinematic = kin;
        held = _startHeld;
        _rb.constraints = _homeConstraints;
        _light.ClearStoredMotion();
        _spent = false;
        _gone = 0f;
        _pop = 0f;                                         // inks back in with a little pop
    }

    private void Update()
    {
        if (_pop < 1f)
        {
            _pop = Mathf.Min(1f, _pop + Time.deltaTime / 0.3f);
            float k = 1f - (1f - _pop) * (1f - _pop);
            transform.localScale = _scale * Mathf.LerpUnclamped(0.2f, 1f, k);
        }
        // down a gap, below its panel's floor line (falling, or frozen down there): the page draws it
        // back in where it was a moment later
        if (transform.position.y < _pit)
        {
            _gone += Time.deltaTime;
            if (_gone > 1.5f) Redraw();
        }
        else _gone = 0f;
    }

    private Vector3 _scale;

    private void OnDestroy()
    {
        if (TorchController.I != null) TorchController.I.Charge.Flared -= Release;
    }

    /// <summary>The flare's SPRONG snaps the thread: down it comes (while the page is lit).</summary>
    public void Release()
    {
        if (!held || this == null) return;
        held = false;
        _rb.constraints = Falling;
        SfxLettering.Spawn("SNAP!", (Vector2)transform.position + Vector2.up * 1.2f, Palette.Paper, 1f, burst: true);
        GameAudio.Play("ping", 0.8f);
    }

    private void FixedUpdate()
    {
        if (held || !_light.IsAwake || _rb.isKinematic) return;
        if (_rb.IsSleeping()) _rb.WakeUp();               // whatever it rested on may vanish (Blot diving away)
        // comic gravity: a touch heavier than real so falls read quickly
        _rb.AddForce(Vector3.down * 12f, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!hazard || _spent || !_light.IsAwake) return;
        var h = c.collider.GetComponentInParent<Health>();
        if (h == null || !h.IsAwake || c.relativeVelocity.magnitude < 3f) return;
        if (h.Apply(new Hit { damage = damage, hearts = 1, knockback = new Vector2(0f, 3f), stun = 0.8f, team = Team.Hazard, source = gameObject, word = "CRASH!", heavy = damage >= 20f }))
        {
            _spent = true;
            SfxLettering.Spawn("CRASH!", (Vector2)transform.position + Vector2.up, Palette.Yellow, 1.1f, burst: true);
            GameAudio.Play("crash", 0.8f);
        }
    }
}
