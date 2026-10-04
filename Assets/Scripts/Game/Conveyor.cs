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

    private Lightable _motor;
    private float _x0, _x1, _y, _scroll;
    private readonly List<Transform> _marks = new();
    private Transform _gear;
    private bool _was;

    /// <summary>A belt from x0 to x1 on a floor whose top is at `y`, moving at `speed` (negative = left).</summary>
    public static Conveyor Create(PanelLayout pl, float x0, float x1, float y, float speed)
    {
        var A = GameAssets.I;
        var go = new GameObject("Conveyor");
        go.transform.SetParent(pl.root, false);
        var c = go.AddComponent<Conveyor>();
        c._x0 = x0;
        c._x1 = x1;
        c._y = y;
        c.Speed = speed;
        // the belt: a dark band along the floor's front edge, rollers at the ends, chevrons that move
        PageBuilder.Box(go.transform, "Belt", new Vector3((x0 + x1) * 0.5f, y - 0.2f, -1.06f), new Vector3(x1 - x0, 0.36f, 0.08f), A.border, collider: false);
        foreach (float x in new[] { x0, x1 })
            PageBuilder.Box(go.transform, "Roller", new Vector3(x, y - 0.2f, -1.1f), new Vector3(0.42f, 0.42f, 0.06f), A.metal, collider: false);
        for (float x = x0 + 0.3f; x < x1 - 0.2f; x += 0.6f)
        {
            var m = PageBuilder.Box(go.transform, "Chevron", new Vector3(x, y - 0.2f, -1.12f), new Vector3(0.08f, 0.26f, 0.04f), A.window, collider: false);
            m.transform.rotation = Quaternion.Euler(0f, 0f, speed > 0f ? -28f : 28f);
            c._marks.Add(m.transform);
        }
        // the motor at the downstream end: a gearbox with a gear on its face
        float mx = speed > 0f ? x1 + 0.55f : x0 - 0.55f;
        var box = PageBuilder.Box(go.transform, "Motor", new Vector3(mx, y + 0.45f, 0.9f), new Vector3(0.9f, 0.9f, 0.9f), A.metal, collider: false);
        var l = box.AddComponent<Lightable>();
        l.points = new[] { Vector3.zero };
        c._motor = l;
        var gear = new GameObject("Gear").transform;                  // an unscaled pivot: the cross turns round it
        gear.SetParent(go.transform, false);
        gear.position = new Vector3(mx, y + 0.45f, 0.42f);
        PageBuilder.Box(gear, "Spoke", gear.position, new Vector3(0.62f, 0.12f, 0.05f), A.border, collider: false);
        PageBuilder.Box(gear, "Spoke", gear.position, new Vector3(0.12f, 0.62f, 0.05f), A.border, collider: false);
        c._gear = gear;
        PageBuilder.SetLayer(go.transform);
        return c;
    }

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
