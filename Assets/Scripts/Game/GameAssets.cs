using UnityEngine;

/// <summary>
/// Everything the game scene builds from, in one asset (Resources/Game/GameAssets), filled by
/// Past Bedtime > Build Game Assets: the character prefabs and the comic-world materials.
/// </summary>
public class GameAssets : ScriptableObject
{
    [Header("Characters")]
    public GameObject max;
    public GameObject smudge, dotShot, bruiser, splotch, eraser;
    public GameObject blot;

    [Header("Comic world (all PastBedtime/Comic/Lit, tone only)")]
    public Material sky;          // night sky behind the panel: heavy dots
    public Material moon;         // flat paper
    public Material far;          // the near city behind the lane: solid ink
    public Material skylineMid;   // the city's middle distance: tone, a fine line
    public Material skylineFar;   // the far skyline: pale, finest line, hazy at its foot
    public Material window;       // lit windows: flat paper
    public Material roof;         // rooftops and ledges: light dots
    public Material wall;         // building fronts: mid dots
    public Material border;       // panel borders: flat ink
    public Material ink;          // wet ink (pellets, props)
    public Material metal;        // pipes, tanks, antennas
    public Material wood;         // crates, water towers
    public Material paper;        // speech bubbles, captions
    public Material star;         // gold stars: flat paper with an ink outline
    public Material caption;      // caption boxes: flat yellow (lettering is the one colour in the comic)
    public Material ghostHint;    // the dotted hint of invisible ink: flat violet
    public Material nothing;      // draws nothing (Baron Blot in invisible ink)

    private static GameAssets _i;
    public static GameAssets I => _i != null ? _i : _i = Resources.Load<GameAssets>("Game/GameAssets");

    public GameObject Enemy(EnemyKind kind) => kind switch
    {
        EnemyKind.Smudge => smudge,
        EnemyKind.DotShot => dotShot,
        EnemyKind.Bruiser => bruiser,
        EnemyKind.Splotch => splotch,
        EnemyKind.Blot => blot,
        _ => eraser,
    };
}
