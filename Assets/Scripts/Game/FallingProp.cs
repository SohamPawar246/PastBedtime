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
    private bool _spent;

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
    }

    private void Start()
    {
        if (held && TorchController.I != null) TorchController.I.Charge.Flared += Release;
    }

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
