using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An Inkworks conveyor belt (the proposal's moving machinery), laid along the top of a floor. It runs
/// only while its motor, the gearbox at one end, is lit, and then it carries whatever stands on it,
/// Max and Inkies alike. Keep the motor in the dark and it's just a floor.
/// </summary>
public class Conveyor : MonoBehaviour
{
    public static readonly List<Conveyor> All = new();

    /// <summary>Faster than Max can run (7): while it runs, the only way along it is to stop its motor.</summary>
    public const float BeltSpeed = 8.5f;
    public float Speed = BeltSpeed;              // lane units a second; the sign is the direction
    public bool Running => _motor != null && _motor.IsAwake;

    [SerializeField] private Lightable _motor;
    [Tooltip("The belt's ends and its top (world).")]
    [SerializeField] private float _x0, _x1, _y;
    [SerializeField] private List<Transform> _marks = new();
    [SerializeField] private Transform _gear;
    private float _scroll;
    private bool _was;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>How fast the belt under a pair of feet carries them (0 if it's stopped, or none).</summary>
    public static float Carry(Vector2 feet)
    {
        foreach (var c in All)
            if (c.Running && feet.x >= c._x0 - 0.1f && feet.x <= c._x1 + 0.1f && Mathf.Abs(feet.y - c._y) < 0.25f)
                return c.Speed;
        return 0f;
    }

    private void Update()
    {
        bool run = Running;
        if (run && !_was) SfxLettering.Spawn("WHIRR", new Vector2(_motor.transform.position.x, _y + 1.4f), Palette.Paper, 0.6f);
        _was = run;
        if (!run) return;
        float dt = Time.deltaTime;
        _scroll = Mathf.Repeat(_scroll + Speed * dt, 0.6f);
        for (int i = 0; i < _marks.Count; i++)
        {
            var p = _marks[i].position;
            float x = _x0 + 0.3f + Mathf.Repeat(i * 0.6f + _scroll, Mathf.Max(0.6f, _x1 - _x0 - 0.5f));
            _marks[i].position = new Vector3(x, p.y, p.z);
        }
        _gear.Rotate(0f, 0f, -Speed * 120f * dt);
    }
}
