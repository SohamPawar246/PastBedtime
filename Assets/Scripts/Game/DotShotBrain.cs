using UnityEngine;

/// <summary>
/// Dot-Shot, the gunner: 15 HP. Keeps 6 units away and spits an ink pellet every 2 s
/// (8 units/s, 1 heart). Pellets freeze at the beam's edge; re-light them when it suits you,
/// or Haymaker them back.
/// </summary>
public class DotShotBrain : EnemyBrain
{
    public const float Speed = 2f, Keep = 6f, Every = 2f, Range = 15f;
    public Material PelletMaterial;

    private float _reload = 1.2f;
    private bool _fired;

    protected override void Awake()
    {
        base.Awake();
        Health.maxHp = Health.hp = 15f;
    }

    protected override void Think(float dt)
    {
        if (Mode == State.Hurt)
        {
            if (StateTime >= HurtFor) Enter(State.Idle);
            return;
        }
        if (Mode == State.Attack)
        {
            Velocity.x = 0f;
            if (!_fired && StateTime >= 0.29f)
            {
                _fired = true;
                float s = Model.lossyScale.y;
                Vector2 muzzle = (Vector2)transform.position + new Vector2(Facing * 0.5f * s, 0.66f * s);
                Vector2 target = Hero != null ? (Vector2)Hero.transform.position + Vector2.up * 1.1f : muzzle + Vector2.right * Facing;
                Vector2 dir = target - muzzle;
                if (Mathf.Sign(dir.x) != Facing) dir.x = Facing;               // never shoots backwards
                InkPellet.Fire(muzzle, dir, gameObject, PelletMaterial);
                GameAudio.Play("spit", 0.6f);
                SfxLettering.Spawn("PTOO!", muzzle + Vector2.up * 0.6f, Palette.Paper, 0.6f);
            }
            if (StateTime >= 0.67f) Enter(State.Idle);
            return;
        }

        if (SeekGreen(Speed)) return;
        if (Hero == null || Hero.Dead || !HeroInPanel)
        {
            Velocity.x = 0f;
            Clips?.Play("Idle", 0.2f);
            return;
        }
        FaceHero();
        float d = Mathf.Abs(ToHeroX);
        if (d < Keep - 1f) Velocity.x = -Facing * Speed;                   // back off, still facing Max
        else if (d > Keep + 1.5f) Velocity.x = Facing * Speed;
        else Velocity.x = 0f;
        Clips?.Play(Mathf.Abs(Velocity.x) > 0.1f ? "Move" : "Idle", 0.12f);

        _reload -= dt;
        if (_reload <= 0f && d < Range)
        {
            _reload = Every;
            _fired = false;
            Velocity.x = 0f;
            Enter(State.Attack);
            Clips?.Play("Shoot", 0.05f, 1f, restart: true);
        }
    }
}
