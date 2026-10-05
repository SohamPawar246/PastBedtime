using UnityEngine;

/// <summary>
/// Smudge, the goon (GDD section 6): 20 HP. Walks at you; a 3-hit punch with a 0.5 s
/// wind-up. Beat it with choreography: freeze it mid-swing next to another Smudge, re-light.
/// </summary>
public class SmudgeBrain : EnemyBrain
{
    public const float Speed = 2.4f, Aggro = 12f, Reach = 1.7f;

    private struct Punch
    {
        public string clip;
        public float start, speed, strike, damage, stun;
        public Vector2 knock, offset, size;
        public string word;
    }

    // The combo on the Smudge's own clock: the first fist lands 0.5 s after the wind-up starts.
    private static readonly Punch[] Combo =
    {
        new() { clip = "Punch1", start = 0f, speed = 0.75f, strike = 0.5f, damage = 8f, stun = 0.3f, knock = new Vector2(3f, 0f), offset = new Vector2(1.25f, 0.9f), size = new Vector2(1.3f, 0.8f), word = "BAM!" },
        new() { clip = "Punch2", start = 0.85f, speed = 1.2f, strike = 1.16f, damage = 8f, stun = 0.3f, knock = new Vector2(3f, 0f), offset = new Vector2(1.25f, 0.9f), size = new Vector2(1.3f, 0.8f), word = "BOP!" },
        new() { clip = "Punch3", start = 1.45f, speed = 1f, strike = 2.07f, damage = 10f, stun = 0.6f, knock = new Vector2(7f, 4f), offset = new Vector2(1.1f, 0.5f), size = new Vector2(1.9f, 1.2f), word = "WHUMP!" },
    };
    private const float ComboLength = 2.6f, Active = 0.13f;
    private int _next;

    protected override void Awake()
    {
        base.Awake();
        Health.maxHp = Health.hp = 20f;
    }

    protected override void Think(float dt)
    {
        switch (Mode)
        {
            case State.Windup:
            case State.Attack:
                Velocity.x = 0f;
                RunCombo();
                return;
            case State.Hurt:
                if (StateTime >= HurtFor) Enter(State.Approach);
                return;
            case State.Recover:
                Velocity.x = 0f;
                Clips?.Play("Idle", 0.15f);
                if (StateTime >= 0.7f) Enter(State.Approach);
                return;
        }

        if (Hero == null || Hero.Dead || !HeroInPanel || HeroDistance > Aggro)
        {
            Velocity.x = 0f;
            Clips?.Play("Idle", 0.2f);
            Mode = State.Idle;
            return;
        }
        FaceHero();
        float dy = Hero.transform.position.y - transform.position.y;
        if (Mathf.Abs(ToHeroX) <= Reach && Mathf.Abs(dy) < 1.6f)
        {
            Enter(State.Windup);
            _next = 0;
            Velocity.x = 0f;
            return;
        }
        Mode = State.Approach;
        Velocity.x = Facing * Speed;
        Clips?.Play("Move", 0.12f);
    }

    private void RunCombo()
    {
        float t = StateTime;
        if (_next < Combo.Length && t >= Combo[_next].start)
        {
            var p = Combo[_next];
            Clips?.Play(p.clip, 0.06f, p.speed, restart: true);
            _next++;
        }
        if (t >= Combo[0].strike) Mode = State.Attack;
        bool any = false;
        foreach (var p in Combo)
        {
            if (t >= p.strike && t <= p.strike + Active)
            {
                any = true;
                if (!Attack.Active || Attack.hit.word != p.word)
                    Attack.Begin(Melee(p.damage, p.knock, p.stun, p.word), p.offset, p.size);
            }
        }
        if (!any && Attack.Active) Attack.End();
        if (t >= ComboLength)
        {
            Attack.End();
            Enter(State.Recover);
        }
    }
}
