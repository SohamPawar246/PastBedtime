using UnityEngine;

/// <summary>
/// The Bruiser, the brute: 60 HP. Slow; armoured from the front; an overhead slam with a
/// 1.2 s wind-up hits everything awake within 2.5 units, Inkies included (2 hearts to Max).
/// Freeze him at the top of the wind-up, light a crowd in front of him, re-light: one slam
/// clears the room.
/// </summary>
public class BruiserBrain : EnemyBrain
{
    public const float Speed = 1.3f, Aggro = 14f, Reach = 2.2f, Windup = 1.2f, SlamRadius = 2.5f;

    protected override bool SuperArmour => Mode == State.Windup || Mode == State.Attack;
    protected override float TurnYaw => 60f;
    private bool _slammed;

    protected override void Awake()
    {
        base.Awake();
        Health.maxHp = Health.hp = 60f;
        Health.frontArmour = true;
    }

    protected override void Think(float dt)
    {
        switch (Mode)
        {
            case State.Hurt:
                if (StateTime >= HurtFor) Enter(State.Approach);
                return;
            case State.Windup:
                Velocity.x = 0f;
                if (StateTime >= Windup) { Slam(); Enter(State.Attack); }
                return;
            case State.Attack:
                Velocity.x = 0f;
                if (StateTime >= 0.5f) { Attack.End(); Enter(State.Recover); }
                return;
            case State.Recover:
                Velocity.x = 0f;
                Clips?.Play("Idle", 0.25f);
                if (StateTime >= 1.0f) Enter(State.Approach);
                return;
        }

        if (SeekGreen(Speed)) return;
        if (Hero == null || Hero.Dead || !HeroInPanel || HeroDistance > Aggro)
        {
            Velocity.x = 0f;
            Clips?.Play("Idle", 0.25f);
            return;
        }
        FaceHero();
        if (Mathf.Abs(ToHeroX) <= Reach && Mathf.Abs(Hero.transform.position.y - transform.position.y) < 2f)
        {
            Enter(State.Windup);
            _slammed = false;
            Velocity.x = 0f;
            // the clip's impact is at 60% of its length: stretch it so that lands at 1.2 s
            Clips?.Play("Slam", 0.08f, SlamSpeed(), restart: true);
            SfxLettering.Spawn("HNNGH...", (Vector2)transform.position + new Vector2(0f, 3.2f * Model.lossyScale.y), Palette.Paper, 0.8f);
            return;
        }
        Velocity.x = Facing * Speed;
        Clips?.Play("Move", 0.15f, 1f);         // the library walk covers 0.96 m/s; at 1.35x scale that is his 1.3
    }

    private float SlamSpeed()
    {
        float length = 2f;
        if (Clips != null && Clips.Animator != null)
            foreach (var c in Clips.Animator.runtimeAnimatorController.animationClips)
                if (c.name.EndsWith("Slam")) length = c.length;
        return Mathf.Max(0.1f, length * 0.6f / Windup);
    }

    private void Slam()
    {
        if (_slammed) return;
        _slammed = true;
        float s = Model.lossyScale.y;
        Attack.Begin(Melee(25f, new Vector2(9f, 7f), 1.0f, "KRAK!", hearts: 2, heavy: true),
                     new Vector2(1.0f * s, 0.6f), new Vector2(SlamRadius * 2f, 2.4f));
        Attack.Tick(Facing);
        Vector2 at = (Vector2)transform.position + new Vector2(Facing * 1.2f * s, 0.4f);
        SfxLettering.Spawn("KRAK!", at + Vector2.up * 1.2f, Palette.Yellow, 1.5f, burst: true);
        MangaFx.Focus(at + Vector2.up * 0.6f, 1.2f);
        GameEvents.Impact(0.9f);
        GameAudio.Play("slam", 1f);
    }
}
