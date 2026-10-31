using TMPro;
using UnityEngine;

/// <summary>A pigeon frozen mid-flap (GDD page 1): light it and it flutters off the page.</summary>
public class Pigeon : MonoBehaviour
{
    private Lightable _light;
    [SerializeField] private Transform _wingL, _wingR;
    private float _t;
    private bool _flying;

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
