using System;
using UnityEngine;

public enum EnemyKind { Smudge, DotShot, Bruiser, Splotch, Eraser, Blot }
public enum PropKind { Flowerpot, Crate, Chimney, WaterTower, Antenna, Billboard, Pigeon, Vent, Brick, Chandelier, Bubble, Press, Conveyor }

/// <summary>
/// One comic page (GDD section 10): 2 tiers (rows), each 2 or 3 panels read left to right.
/// A panel is one arena or platforming room about 8 to 14 units wide and 11 tall; gutters are
/// 0.6 units of paper. Everything inside a panel is authored in panel space: x from 0 to its
/// width, y from 0 (the panel's bottom border) to 11.
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
    public Vector2 start = new(1.5f, 3f);           // Max's start, in the first panel's space

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
    [Tooltip("Lens handed to Max as her first visit begins (-1 for none). Page 5: Red.")]
    public int momLens = -1;
    public Tier[] tiers = Array.Empty<Tier>();

    [Serializable]
    public class Tier
    {
        public Panel[] panels = Array.Empty<Panel>();
    }

    [Serializable]
    public class Panel
    {
        public float width = 12f;
        [Tooltip("A yellow caption box in the panel's top-left corner.")]
        public string caption;
        [Tooltip("What Max says to the reader when he walks into this panel.")]
        public string heroLine;
        public Rect[] floors = Array.Empty<Rect>();
        [Tooltip("Floors the Eraser can rub out (built from 1-unit blocks).")]
        public Rect[] erasable = Array.Empty<Rect>();
        [Tooltip("Ledges in invisible ink: only real while the Ghost lens is on them.")]
        public Rect[] ghost = Array.Empty<Rect>();
        [Tooltip("Secret stars in invisible ink (Ghost lens only).")]
        public Vector2[] ghostStars = Array.Empty<Vector2>();
        [Tooltip("Blot's rising ink: where its surface starts (panel space; below 0 = no flood), where it stops, and how fast it rises while lit.")]
        public float flood = -1f;
        public float floodTop = 8f, floodRise = 0.55f;
        public Spawn[] spawns = Array.Empty<Spawn>();
        public Prop[] props = Array.Empty<Prop>();
        public Vector2[] stars = Array.Empty<Vector2>();
        [Tooltip("0 night rooftops, 1 factory, 2 tower.")]
        public int backdrop;
    }

    [Serializable]
    public struct Spawn
    {
        public EnemyKind kind;
        public Vector2 at;
        public bool faceRight;
    }

    [Serializable]
    public struct Prop
    {
        public PropKind kind;
        public Vector2 at;
        [Tooltip("Scale; a bubble's or a press's width; a conveyor's length (negative: it runs left).")]
        public float size;
        [Tooltip("A speech bubble's words.")]
        public string text;
        [Tooltip("A speech bubble's tail: where the speaker is, from the bubble's centre.")]
        public Vector2 tail;
    }
}
