using System;
using UnityEngine;

/// <summary>
/// Twist to charge (GDD section 4). Charge is seconds of light. One twist of the crank is
/// +2 s (Gear upgrades: +3, +4). At full the spring takes 3 more notches of overwind
/// (Ratchet: 5, 7), bleeding one off every 0.6 s once you stop; one notch past that is a
/// FLARE: the whole page wakes for 1.5 s, then the bulb cools and the torch is dead for 2 s.
/// </summary>
public class ChargeSystem : MonoBehaviour
{
    public float Capacity = 60f;
    public float Charge = 60f;
    public float PerTwist = 2f;
    public int OverwindBuffer = 3;

    public const float FlareSeconds = 1.5f;
    public const float CoolSeconds = 2f;
    public const float BleedEvery = 0.6f;

    public int Overwind { get; private set; }
    /// <summary>True from a flare until the bulb has cooled.</summary>
    public bool Dead => _deadFor > 0f;
    public float SinceTwist { get; private set; } = 99f;
    public float Share => Capacity > 0f ? Charge / Capacity : 0f;
    // "Full" includes the half-second of drain since the last notch, and any wound overwind
    public bool Full => Overwind > 0 || Charge >= Capacity - 0.5f;

    public event Action<int> Twisted;      // overwind after the twist (0 if not at full)
    public event Action Flared;

    private float _deadFor;
    private float _bleed;

    public void Drain(float seconds, float rate)
    {
        Charge = Mathf.Max(0f, Charge - seconds * rate);
    }

    public void Twist(int notches)
    {
        for (int i = 0; i < notches; i++)
        {
            SinceTwist = 0f;
            _bleed = 0f;
            if (Dead) continue;                       // the cooling bulb ignores the crank
            if (!Full)
            {
                Charge = Mathf.Min(Capacity, Charge + PerTwist);
                Twisted?.Invoke(0);
                continue;
            }
            Charge = Capacity;
            Overwind++;
            if (Overwind > OverwindBuffer)
            {
                Flare();
                return;
            }
            Twisted?.Invoke(Overwind);
        }
    }

    public void Flare()
    {
        Overwind = 0;
        _deadFor = FlareSeconds + CoolSeconds;
        if (LightField.I != null) LightField.I.FlareTime = FlareSeconds;
        Flared?.Invoke();
    }

    public void ApplyUpgrades(int spring, int gear, int ratchet)
    {
        Capacity = spring switch { 1 => 80f, 2 => 100f, _ => 60f };
        PerTwist = gear switch { 1 => 3f, 2 => 4f, _ => 2f };
        OverwindBuffer = ratchet switch { 1 => 5, 2 => 7, _ => 3 };
        Charge = Mathf.Min(Charge, Capacity);
    }

    private void Update()
    {
        float dt = Time.deltaTime;                    // the reader's hand runs on real time
        SinceTwist += dt;
        if (_deadFor > 0f) _deadFor = Mathf.Max(0f, _deadFor - dt);
        if (Overwind > 0 && SinceTwist > BleedEvery)
        {
            _bleed += dt;
            if (_bleed >= BleedEvery)
            {
                _bleed = 0f;
                Overwind--;
            }
        }
    }
}
