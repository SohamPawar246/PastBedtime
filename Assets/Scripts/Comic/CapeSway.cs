using UnityEngine;

/// <summary>
/// Max's cape. The animation clips never touch the cape bones (cape.01..04 under
/// DEF-spine.003), so without this the cape would ride the torso rigidly and stick out
/// whenever Max leans. Each frame, after the Animator, every segment is turned toward a
/// hanging direction (down, a little behind him, more behind the faster he moves), more
/// strongly further down the chain, and eased over time so it swings and settles.
/// </summary>
public class CapeSway : MonoBehaviour
{
    [Tooltip("How much each segment, top to bottom, gives in to gravity (0 = rides the torso).")]
    public float[] hang = { 0.45f, 0.7f, 0.85f, 0.95f };
    [Tooltip("How far behind him the cape hangs at rest.")]
    public float backAtRest = 0.25f;
    [Tooltip("Extra trail per metre per second of movement.")]
    public float trailPerSpeed = 0.35f;
    [Tooltip("How quickly the cape catches up (higher = stiffer).")]
    public float response = 10f;

    private Transform[] _bones;
    private Quaternion[] _restLocal;
    private Vector3[] _localDir;
    private Vector3[] _dir;
    private Vector3 _behindLocal;
    private Vector3 _lastPos;
    private Vector3 _velocity;

    private void Awake() => Init();

    private void Init()
    {
        if (_bones != null) return;
        _bones = new Transform[4];
        foreach (var t in GetComponentsInChildren<Transform>(true))
            for (int i = 0; i < 4; i++)
                if (t.name == $"cape.0{i + 1}") _bones[i] = t;
        if (System.Array.Exists(_bones, b => b == null)) { enabled = false; return; }

        _restLocal = new Quaternion[4];
        _localDir = new Vector3[4];
        _dir = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            _restLocal[i] = _bones[i].localRotation;
            Vector3 along = i < 3 ? _bones[i + 1].position - _bones[i].position
                                  : _bones[i].rotation * (Quaternion.Inverse(_bones[i - 1].rotation) * (_bones[i].position - _bones[i - 1].position));
            _localDir[i] = Quaternion.Inverse(_bones[i].rotation) * along.normalized;
            _dir[i] = along.normalized;
        }
        // "Behind" is wherever the cape hangs in the bind pose, relative to the character.
        Vector3 back = _bones[0].position - _bones[0].parent.position;
        back.y = 0f;
        _behindLocal = transform.InverseTransformDirection(back.sqrMagnitude > 1e-6f ? back.normalized : -transform.forward);
        _lastPos = transform.position;
    }

    private Lightable _light;

    private void LateUpdate()
    {
        if (_light == null) _light = GetComponentInParent<Lightable>();
        float dt = _light != null ? _light.Delta : Time.deltaTime;
        if (dt <= 0f) return;                               // frozen: the cape hangs exactly where it was
        if (dt > 0f)
        {
            Vector3 v = (transform.position - _lastPos) / dt;
            _velocity = Vector3.Lerp(_velocity, v, 1f - Mathf.Exp(-dt * 8f));
            _lastPos = transform.position;
        }
        Step(1f - Mathf.Exp(-dt * response));
    }

    /// <summary>Snaps the cape to where it would hang (for posed stills and the first frame).</summary>
    public void Settle()
    {
        Init();
        if (!enabled) return;
        for (int i = 0; i < 4; i++) _dir[i] = Vector3.zero;
        Step(1f);
    }

    private void Step(float follow)
    {
        Vector3 behind = transform.TransformDirection(_behindLocal);
        Vector3 flat = new Vector3(_velocity.x, 0f, _velocity.z);
        Vector3 want = (Vector3.down + behind * backAtRest - flat * trailPerSpeed).normalized;
        for (int i = 0; i < 4; i++)
        {
            var b = _bones[i];
            b.localRotation = _restLocal[i];                       // the torso's say
            Vector3 cur = b.rotation * _localDir[i];
            Vector3 target = Vector3.Slerp(cur, want, hang[Mathf.Min(i, hang.Length - 1)]);
            if (_dir[i] == Vector3.zero) _dir[i] = target;
            _dir[i] = Vector3.Slerp(_dir[i], target, follow).normalized;
            b.rotation = Quaternion.FromToRotation(cur, _dir[i]) * b.rotation;
        }
    }
}
