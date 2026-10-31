using UnityEngine;

public enum EnemyKind { Smudge, DotShot, Bruiser, Splotch, Eraser, Blot }

/// <summary>
/// One comic page's settings (GDD section 10): its title, the charge it starts with, the lens it
/// unlocks, and Mom's visits. The page itself is drawn in <see cref="art"/>: 2 tiers (rows), each
/// 2 or 3 panels read left to right, a panel being one arena or platforming room about 8 to 14
/// units wide and 11 tall, with gutters of 0.6 units of paper between them.
/// </summary>
[CreateAssetMenu(menuName = "Past Bedtime/Page", fileName = "Page")]
public class PageDef : ScriptableObject
{
    public const float PanelHeight = 11f, Gutter = 0.6f, TierSpacing = 16f;

    public int number = 1;
    public string title = "Rooftop at night";
    public string act = "PART 1: NIGHT OF THE BLOT";
    [Tooltip("Charge share at the start of the page (page 2 starts nearly flat to teach twisting).")]
    [Range(0f, 1f)] public float startCharge = 1f;
    [Tooltip("Lens unlocked when the page is finished (-1 for none).")]
    public int unlockLens = -1;
    [Tooltip("The drawn page (Art/Pages/PageNN): its panels, everything in them, and where Max starts.")]
    public PageLayout art;

    [Header("Mom (GDD section 8)")]
    [Tooltip("How many times she comes to the door on this page.")]
    public int momVisits;
    [Tooltip("Chance each visit ends with the door opening (0 = never).")]
    [Range(0f, 1f)] public float momOpenChance;
    [Tooltip("One of the visits is the cat instead.")]
    public bool catFakeout;
    [Tooltip("Act 1's scripted first pass: a 6 s warning and a tutorial caption.")]
    public bool momTutorial;
    [Tooltip("Seconds into the page before her first visit (0 = random 25 to 55).")]
    public float momFirstVisit;
    [Tooltip("Her first visit opens the door (page 5: the first opening, mid-fight).")]
    public bool momFirstOpens;
}
