using UnityEngine;

/// <summary>
/// Plays named animator states on a character's model and stops on the current frame when
/// the character freezes (GDD: animation stops, then resumes exactly where it was).
/// Every controller is a flat list of states named after the moves; code cross-fades.
/// </summary>
public class ClipPlayer : MonoBehaviour, IFreezable
{
    public Animator Animator { get; private set; }
    public string Current { get; private set; }

    private Lightable _light;
    private float _speed = 1f;

    private void Awake()
    {
        Animator = GetComponentInChildren<Animator>();
        _light = GetComponent<Lightable>();
        if (Animator != null)
        {
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    public bool Has(string state) => Animator != null && Animator.HasState(0, Animator.StringToHash(state));

    /// <summary>Cross-fades to a state (no-op if already playing it, unless restart).</summary>
    public void Play(string state, float fade = 0.08f, float speed = 1f, bool restart = false)
    {
        if (Animator == null || string.IsNullOrEmpty(state)) return;
        if (!Has(state)) return;
        SetSpeed(speed);
        if (!restart && state == Current) return;
        Current = state;
        Animator.CrossFadeInFixedTime(state, fade, 0, 0f);
    }

    public void SetSpeed(float speed)
    {
        _speed = speed;
        if (Animator != null && (_light == null || _light.IsAwake)) Animator.speed = speed;
    }

    /// <summary>0..1 through the current state (loops wrap).</summary>
    public float Normalized
    {
        get
        {
            if (Animator == null) return 0f;
            var info = Animator.IsInTransition(0) ? Animator.GetNextAnimatorStateInfo(0) : Animator.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime;
        }
    }

    public void OnFreeze()
    {
        if (Animator != null) Animator.speed = 0f;
    }

    public void OnWake()
    {
        if (Animator != null) Animator.speed = _speed;
    }
}
