using System;
using UnityEngine;

/// <summary>
/// The torch's four-colour lens wheel (GDD section 4). Lenses never change who is awake:
/// each only adds one side effect and one catch. Middle-click (or Tab) twists the wheel a step (0.25 s, the
/// beam stays on); 1 to 4 jump straight to a lens.
///   Clear  baseline                          drain 1x
///   Green  heals everything awake in it      drain 1.5x  (Inkies heal too)
///   Red    Mom barely sees it                drain 1x    (beam shrinks to 3.0)
///   Ghost  reveals invisible ink             drain 2x
/// </summary>
public class LensWheel : MonoBehaviour
{
    public Lens Current = Lens.Clear;
    public readonly bool[] Unlocked = { true, false, false, false };
    public const float SwapSeconds = 0.25f;

    public bool Swapping => _swap > 0f;
    public event Action<Lens> Swapped;

    private float _swap;

    public static float DrainRate(Lens lens) => lens switch
    {
        Lens.Green => 1.5f,
        Lens.Ghost => 2f,
        _ => 1f,
    };

    public static string Name(Lens lens) => lens switch
    {
        Lens.Green => "MEND",
        Lens.Red => "HUSH",
        Lens.Ghost => "GHOST",
        _ => "CLEAR",
    };

    public void Unlock(Lens lens) => Unlocked[(int)lens] = true;

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
