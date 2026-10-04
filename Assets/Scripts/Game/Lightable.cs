using System.Collections.Generic;
using UnityEngine;

/// <summary>Anything that wants to hear when its owner freezes or wakes.</summary>
public interface IFreezable
{
    void OnFreeze();
    void OnWake();
}

/// <summary>
/// On every dynamic thing in the comic (GDD section 14). Asks the <see cref="LightField"/>
/// whether any of its sample points is lit and freezes or wakes the object to match.
///
/// Gameplay code never reads Time.deltaTime directly: it reads <see cref="Delta"/>, which
/// is 0 while frozen. Freezing therefore pauses AI, timers, attacks and movement for free.
/// Frozen things keep their velocity (rigidbodies store and restore it), keep their colliders
/// (solid scenery you can stand on), stop animating, pause particles and fall silent.
/// </summary>
[DefaultExecutionOrder(-50)]
public class Lightable : MonoBehaviour
{
    [Tooltip("Sample points, local to this transform (feet, body, head). Awake if any is lit.")]
    public Vector3[] points = { new(0f, 0.2f, 0f), new(0f, 0.9f, 0f), new(0f, 1.6f, 0f) };
    [Tooltip("Max uses the 0.3 unit grace margin at the beam's edge.")]
    public bool isHero;

    public bool IsAwake { get; private set; } = true;
    public float Delta => IsAwake ? Time.deltaTime : 0f;
    public float FixedDelta => IsAwake ? Time.fixedDeltaTime : 0f;
    /// <summary>Seconds since this thing last woke (Inkies "reorient" for 0.3 s).</summary>
    public float AwakeFor { get; private set; }

    public event System.Action<bool> Changed;

    private readonly List<Vector3> _world = new(3);
    private Rigidbody _rb;
    private Vector3 _storedVelocity, _storedAngular;
    private bool _wasKinematic;
    private IFreezable[] _listeners;
    private ParticleSystem[] _particles;
    private AudioSource[] _audio;
    private float[] _audioVolume;
    private bool _started;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _listeners = GetComponents<IFreezable>();
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _audio = GetComponentsInChildren<AudioSource>(true);
        _audioVolume = new float[_audio.Length];
        for (int i = 0; i < _audio.Length; i++) _audioVolume[i] = _audio[i].volume;
    }

    private void Start()
    {
        _started = true;
        Evaluate(force: true);
    }

    private void Update()
    {
        Evaluate(false);
        if (IsAwake) AwakeFor += Time.deltaTime;
        // Frozen sources fade out over 0.1 s (GDD section 12).
        for (int i = 0; i < _audio.Length; i++)
        {
            if (_audio[i] == null) continue;
            float target = IsAwake ? _audioVolume[i] : 0f;
            _audio[i].volume = Mathf.MoveTowards(_audio[i].volume, target, Time.deltaTime * 10f * Mathf.Max(0.01f, _audioVolume[i]));
        }
    }

    private void FixedUpdate() => Evaluate(false);

    /// <summary>Re-reads the listeners (call after adding components at runtime).</summary>
    public void Refresh()
    {
        _listeners = GetComponents<IFreezable>();
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    public IReadOnlyList<Vector3> WorldPoints()
    {
        _world.Clear();
        foreach (var p in points) _world.Add(transform.TransformPoint(p));
        return _world;
    }

    /// <summary>Forget the motion a frozen rigidbody would wake with (a prop drawn back in starts still).</summary>
    public void ClearStoredMotion()
    {
        _storedVelocity = Vector3.zero;
        _storedAngular = Vector3.zero;
    }

    private void Evaluate(bool force)
    {
        if (!_started || LightField.I == null) return;
        bool awake = LightField.I.IsAwake(WorldPoints(), isHero);
        if (!force && awake == IsAwake) return;
        if (awake) Wake(); else Freeze();
    }

    private void Freeze()
    {
        bool was = IsAwake;
        IsAwake = false;
        if (_rb != null && !_rb.isKinematic)
        {
            _storedVelocity = _rb.linearVelocity;
            _storedAngular = _rb.angularVelocity;
            _wasKinematic = false;
            _rb.isKinematic = true;
        }
        else if (_rb != null) _wasKinematic = true;
        foreach (var p in _particles) if (p != null) p.Pause(true);
        foreach (var l in _listeners) l.OnFreeze();
        if (was) Changed?.Invoke(false);
    }

    private void Wake()
    {
        bool was = IsAwake;
        IsAwake = true;
        AwakeFor = 0f;
        if (_rb != null && !_wasKinematic && _rb.isKinematic)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = _storedVelocity;
            _rb.angularVelocity = _storedAngular;
        }
        foreach (var p in _particles) if (p != null) p.Play(true);
        foreach (var l in _listeners) l.OnWake();
        if (!was) Changed?.Invoke(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (var p in points) Gizmos.DrawWireSphere(transform.TransformPoint(p), 0.08f);
    }
}
