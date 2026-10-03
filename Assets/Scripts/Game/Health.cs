using System;
using System.Collections.Generic;
using UnityEngine;

public enum Team { Hero, Inkie, Hazard }

/// <summary>One landed blow.</summary>
public struct Hit
{
    public float damage;          // HP, for Inkies
    public int hearts;            // for Max (1 per Inkie hit, 2 for Bruiser slams and the boss)
    public Vector2 knockback;     // lane units per second, applied on top of the target's motion
    public float stun;            // seconds of hitstun
    public Team team;             // who threw it
    public GameObject source;
    public bool heavy;            // bigger shake and lettering
    public bool reflects;         // the Haymaker sends ink pellets back
    public bool pierce;           // breaks front armour (the ground slam comes down from above)
    public string word;           // SFX lettering, or null for the default
}

/// <summary>
/// HP, team and the hurtbox (this object's colliders). Rules (GDD section 2):
/// damage and knockback only happen between two awake things, and Inkies hurt Inkies.
/// </summary>
public class Health : MonoBehaviour
{
    public Team team = Team.Inkie;
    public float maxHp = 20f;
    public float hp = 20f;
    [Tooltip("Front armour: hits from the side this faces do a quarter damage (the Bruiser).")]
    public bool frontArmour;

    public Lightable Light { get; private set; }
    public bool Dead => hp <= 0f;
    public bool IsAwake => Light == null || Light.IsAwake;
    public float Invulnerable;               // seconds of i-frames left (on the owner's clock)

    /// <summary>Which way this character faces along the lane (+1 right, -1 left).</summary>
    public Func<float> Facing;

    public event Action<Hit> Hurt;
    public event Action<Hit> Died;

    private static readonly List<Health> _all = new();
    public static IReadOnlyList<Health> All => _all;

    private void Awake() => Light = GetComponent<Lightable>();
    private void OnEnable() => _all.Add(this);
    private void OnDisable() => _all.Remove(this);

    private void Update()
    {
        if (Invulnerable > 0f && Light != null) Invulnerable = Mathf.Max(0f, Invulnerable - Light.Delta);
        else if (Invulnerable > 0f) Invulnerable = Mathf.Max(0f, Invulnerable - Time.deltaTime);
    }

    /// <summary>Who may hurt whom: Max hurts Inkies; Inkies hurt Max and each other; hazards hurt everyone.</summary>
    public static bool CanHurt(Team attacker, Team target) => attacker switch
    {
        Team.Hero => target == Team.Inkie,
        Team.Inkie => target == Team.Hero || target == Team.Inkie,
        _ => true,
    };

    public bool Apply(Hit hit)
    {
        if (Dead || Invulnerable > 0f || !IsAwake) return false;
        if (team == Team.Hero)
        {
            hp -= Mathf.Max(1, hit.hearts);
        }
        else
        {
            float dmg = hit.damage;
            if (frontArmour && !hit.pierce && hit.source != null && Facing != null)
            {
                float side = Mathf.Sign(hit.source.transform.position.x - transform.position.x);
                if (Mathf.Approximately(side, Facing()))
                {
                    dmg *= 0.25f;
                    hit.knockback *= 0.2f;
                    hit.stun = 0f;
                    hit.word = "CLANK!";
                }
            }
            hp -= dmg;
        }
        hp = Mathf.Max(0f, hp);
        Hurt?.Invoke(hit);
        if (hp <= 0f) Died?.Invoke(hit);
        return true;
    }

    public void Heal(float amount)
    {
        if (Dead) return;
        hp = Mathf.Min(maxHp, hp + amount);
    }
}
