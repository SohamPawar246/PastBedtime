using System;
using UnityEngine;

/// <summary>
/// The torch's lens wheel (GDD section 4). The game uses two of its windows: Clear, and the purple Ghost lens
/// (found at the end of page 6), which shows invisible ink. Lenses never change who is awake. Middle-click (or
/// Tab) twists the wheel to the other lens (0.25 s, the beam stays on); 1 picks Clear, 2 the Ghost lens.
///   Clear  baseline                          drain 1x
///   Ghost  reveals invisible ink             drain 2x
/// (The Green and Red lenses were cut after playtesting: two more colours to juggle made it too much. Their
/// slots stay in <see cref="Lens"/>, the shaders' lens index, but nothing unlocks them.)
/// </summary>
public class LensWheel : MonoBehaviour
{
    public Lens Current = Lens.Clear;
    public readonly bool[] Unlocked = { true, false, false, false };
    public const float SwapSeconds = 0.25f;

    public bool Swapping => _swap > 0f;
    public event Action<Lens> Swapped;

    private float _swap;

    public static float DrainRate(Lens lens) => lens == Lens.Ghost ? 2f : 1f;

    public static string Name(Lens lens) => lens == Lens.Ghost ? "GHOST" : "CLEAR";

    public void Unlock(Lens lens)
    {
        if (lens == Lens.Clear || lens == Lens.Ghost) Unlocked[(int)lens] = true;
    }

    public void Step(int dir)
    {
        if (dir == 0) return;
        int i = (int)Current;
        for (int k = 0; k < 4; k++)
        {
            i = (i + (dir > 0 ? 1 : 3)) % 4;
            if (Unlocked[i]) break;
        }
        Pick(i);
    }

    public void Pick(int index)
    {
        if (index < 0 || index > 3 || !Unlocked[index] || index == (int)Current) return;
        Current = (Lens)index;
        _swap = SwapSeconds;
        Swapped?.Invoke(Current);
    }

    private void Update()
    {
        if (_swap > 0f) _swap = Mathf.Max(0f, _swap - Time.deltaTime);
    }
}
