using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Past Bedtime > Build Pages
///
/// Writes the page definitions (Resources/Pages/PageNN.asset) from the GDD's page-by-page plan
/// (section 10). Each page is 2 tiers of 2 panels; panels are authored in panel space
/// (x from 0 to the panel's width, y from 0 to 11, floors as Rect(x, y, width, height)).
///   1  Rooftop at night      Max moves only in the light; follow assist; the edge freezes  1 Smudge
///   2  Water-tower roofs     Choreography; freeze steps; twist to charge (starts low)     3 Smudges
///   3  Billboard alley       Frozen bullets; overwind and flare; bubbles to stand on   2 Dot-Shots, 2 Smudges
///   4  Factory floor         Bats as steps; the Bruiser; slam                             Bruiser, 4 Splotches
///   5  Press room            Mom's first door opening, mid-fight; Bruiser choreography; Bruiser, 3 Smudges, 2 Dot-Shots
///                            Splash Page
///   6  Blot's office         Boss phase 1: ink waves, summons, a chandelier; he escapes  Baron Blot (+ Smudges)
///                            into invisible ink, and Max picks up the Ghost lens
///   7  Invisible-ink stairs  The Ghost lens: ledges only it shows; Erasers; bats          3 Erasers, 3 Splotches
///   8  The ink vat           The rising flood (ink: it only rises where it's lit);        2 Splotches, an Eraser,
///                            boss phase 2: Blot in invisible ink                         Baron Blot (+ bats)
///   9  The roof              Flood and invisible ink together; boss phase 3 and Mom's     a Dot-Shot, Baron Blot
///                            room-light finale; the ending                               (+ his help)
/// </summary>
public static class PageLibrary
{
    private const string Dir = "Assets/Resources/Pages";

    [MenuItem("Past Bedtime/Build Pages")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources", "Pages");
        Save(Page1());
        Save(Page2());
        // Act 1: one scripted pass on page 3 with a 6 s warning; the door never opens.
        Save(Mom(Page3(), 1, tutorial: true, first: 22f));
        // Act 2: 2 to 3 visits a page, the cat on page 4 (2 visits + the cat), and on page 5 the first
        // door opening lands mid-fight.
        Save(Mom(Page4(), 3, cat: true, first: 18f));
        Save(Mom(Page5(), 3, openChance: 0.5f, first: 9f, firstOpens: true));
        // Page 6: one visit before the boss, then Baron Blot himself.
        Save(Mom(Page6(), 1, first: 12f));
        // Act 3: 2 visits a page, openings common (her wedge lights the flood: it surges).
        Save(Mom(Page7(), 2, openChance: 0.7f, first: 20f));
        Save(Mom(Page8(), 2, openChance: 0.6f, first: 24f));
        // Page 9: one visit on the way up; the last one is the finale's, and it's scripted.
        Save(Mom(Page9(), 1, openChance: 0.5f, first: 16f));
        AssetDatabase.SaveAssets();
        Debug.Log("[Pages] Built pages 1-9");
    }

    // ---- shorthand ----------------------------------------------------------------------------

    private static Rect R(float x, float y, float w, float h) => new(x, y, w, h);
    private static PageDef.Spawn S(EnemyKind k, float x, float y, bool right = false) => new() { kind = k, at = new Vector2(x, y), faceRight = right };
    private static PageDef.Prop P(PropKind k, float x, float y, float size = 1f) => new() { kind = k, at = new Vector2(x, y), size = size };
    private static Vector2 V(float x, float y) => new(x, y);
    /// <summary>A speech bubble you can stand on: centre, width, words, and where its speaker is (from the centre).</summary>
    private static PageDef.Prop Say(float x, float y, float width, string text, float toSpeakerX, float toSpeakerY) =>
        new() { kind = PropKind.Bubble, at = new Vector2(x, y), size = width, text = text, tail = new Vector2(toSpeakerX, toSpeakerY) };

    private static PageDef.Panel Panel(float width, Rect[] floors, string caption = null, string line = null,
        PageDef.Spawn[] spawns = null, PageDef.Prop[] props = null, Vector2[] stars = null, Rect[] erasable = null, int backdrop = 0,
        Rect[] ghost = null, Vector2[] ghostStars = null, float flood = -1f, float floodTop = 8f, float floodRise = 0.55f) => new()
    {
        width = width, floors = floors, caption = caption, heroLine = line,
        spawns = spawns ?? new PageDef.Spawn[0], props = props ?? new PageDef.Prop[0], stars = stars ?? new Vector2[0],
        erasable = erasable ?? new Rect[0], backdrop = backdrop,
        ghost = ghost ?? new Rect[0], ghostStars = ghostStars ?? new Vector2[0], flood = flood, floodTop = floodTop, floodRise = floodRise,
    };

    private static PageDef Page(int n, string title, string act, float charge, int unlock, params PageDef.Panel[][] tiers)
    {
        var p = ScriptableObject.CreateInstance<PageDef>();
        p.number = n;
        p.title = title;
        p.act = act;
        p.startCharge = charge;
        p.unlockLens = unlock;
        p.start = new Vector2(1.5f, 2.6f);
        p.tiers = new PageDef.Tier[tiers.Length];
        for (int i = 0; i < tiers.Length; i++) p.tiers[i] = new PageDef.Tier { panels = tiers[i] };
        return p;
    }

    private static PageDef Mom(PageDef p, int visits, float openChance = 0f, bool cat = false, bool tutorial = false, float first = 0f,
        bool firstOpens = false)
    {
        p.momVisits = visits;
        p.momOpenChance = openChance;
        p.catFakeout = cat;
        p.momTutorial = tutorial;
        p.momFirstVisit = first;
        p.momFirstOpens = firstOpens;
        return p;
    }

    private static void Save(PageDef p)
    {
        string path = $"{Dir}/Page{p.number:00}.asset";
        p.name = $"Page{p.number:00}";
        var existing = AssetDatabase.LoadAssetAtPath<PageDef>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(p, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(p);
        }
        else AssetDatabase.CreateAsset(p, path);
    }

    private const string Act1 = "PART 1: NIGHT OF THE BLOT", Act2 = "PART 2: THE INKWORKS", Act3 = "PART 3: BLOT'S TOWER";

    // ---- Act 1 ----------------------------------------------------------------------------------

    private static PageDef Page1() => Page(1, "Rooftop at night", Act1, 1f, -1,
        new[]
        {
            Panel(12f, new[] { R(0, 0, 12, 2.5f) },
                "2:07 A.M. INKWELL CITY SLEEPS...",
                "Psst. Down here! I can't move unless you're reading me.",
                props: new[] { P(PropKind.Antenna, 3f, 2.5f), P(PropKind.Pigeon, 6.5f, 2.5f), P(PropKind.Chimney, 9.3f, 2.5f),
                               Say(7.9f, 4.55f, 1.8f, "COO?", -1.2f, -1.3f) },     // speech bubbles are solid: stand on it
                stars: new[] { V(7.9f, 7.0f) }),
            Panel(13f, new[] { R(0, 0, 5, 2.5f), R(8, 0, 5, 3.4f) },
                "HOLD {FOLLOW}: THE LIGHT FOLLOWS MAX.",
                "Hold {FOLLOW} and the light'll stick with me!",
                props: new[] { P(PropKind.Flowerpot, 6.5f, 8.2f), P(PropKind.WaterTower, 10.6f, 3.4f, 0.9f) },
                stars: new[] { V(6.5f, 5.6f) }),
        },
        new[]
        {
            Panel(12f, new[] { R(0, 0, 12, 2.5f) },
                "AN INKIE! {PUNCH}, {PUNCH}, {PUNCH} TO PUNCH.",
                "Light him up and I'll knock the blot out of him!",
                spawns: new[] { S(EnemyKind.Smudge, 8.5f, 2.6f) },
                props: new[] { P(PropKind.Chimney, 2.2f, 2.5f) },
                stars: new[] { V(2.2f, 5.6f) }),
            // too far to jump: the crate frozen in the gap is the stepping stone (it's redrawn if it falls)
            Panel(13f, new[] { R(0, 0, 3.5f, 2.5f), R(11, 0, 2, 2.5f) },
                "FROZEN THINGS ARE SOLID. KEEP THE CRATE DARK AND STEP ON IT. (HOLD {FOLLOW}: THE LIGHT RIDES HIGH.)",
                "Read me from above, kid. Keep that crate in the dark!",
                props: new[] { P(PropKind.Crate, 7.25f, 1.5f), P(PropKind.Vent, 12f, 2.5f) },
                stars: new[] { V(7.25f, 5.4f), V(12f, 4.4f) }),
        });

    private static PageDef Page2() => Page(2, "Water-tower roofs", Act1, 0.12f, -1,
        new[]
        {
            Panel(11f, new[] { R(0, 0, 11, 2.5f) },
                "THE TORCH IS RUNNING DOWN! TWIST THE CRANK: {CRANK}.",
                "Kid, it's getting dark in here. Give that crank a twist!",
                props: new[] { P(PropKind.WaterTower, 7f, 2.5f) },
                stars: new[] { V(9.6f, 3.8f) }),
            Panel(14f, new[] { R(0, 0, 14, 2.5f) },
                "FREEZE A PUNCH NEXT TO ITS PAL, THEN LIGHT THEM BOTH.",
                "Two of 'em... let one swing at the other!",
                spawns: new[] { S(EnemyKind.Smudge, 6f, 2.6f), S(EnemyKind.Smudge, 10f, 2.6f) },
                props: new[] { P(PropKind.Antenna, 12.5f, 2.5f) },
                stars: new[] { V(12.8f, 4.6f) }),
        },
        new[]
        {
            Panel(12f, new[] { R(0, 0, 4, 2.5f), R(4.6f, 0, 1.4f, 4.2f), R(8, 0, 4, 6f) },
                "BRICKS FALL ONLY IN THE LIGHT. STAND ON THE FROZEN ONES.",
                "Up there! Keep the bricks dark and climb!",
                props: new[] { P(PropKind.Brick, 6.6f, 7.2f), P(PropKind.Brick, 7.2f, 9.2f) },
                stars: new[] { V(2f, 4.6f), V(7f, 9.8f) }),
            Panel(13f, new[] { R(0, 0, 13, 6f) },
                "{UP} + {KICK}: LAUNCHER. FREEZE AN INKIE IN THE AIR AND STAND ON IT.",
                "Pop him up with {UP} and {KICK}!",
                spawns: new[] { S(EnemyKind.Smudge, 9f, 6.1f) },
                stars: new[] { V(11.5f, 10.2f) }),
        });

    private static PageDef Page3() => Page(3, "Billboard alley", Act1, 1f, -1,
        new[]
        {
            Panel(13f, new[] { R(0, 0, 13, 2.5f) },
                "PELLETS FREEZE AT THE EDGE OF THE LIGHT. DARK GAPS ARE SAFE LANES.",
                "Ink pellets! They only fly where you're reading.",
                spawns: new[] { S(EnemyKind.DotShot, 10.5f, 2.6f) },
                props: new[] { P(PropKind.Billboard, 6.5f, 2.5f) },
                stars: new[] { V(3f, 4.6f) }),
            Panel(13f, new[] { R(0, 0, 7, 2.5f), R(7, 0, 6, 4.2f) },
                "THE THIRD PUNCH, THE HAYMAKER, KNOCKS PELLETS BACK!",
                "Batter up!",
                spawns: new[] { S(EnemyKind.Smudge, 4f, 2.6f), S(EnemyKind.DotShot, 11f, 4.3f) },
                stars: new[] { V(12f, 7f) }),
        },
        new[]
        {
            Panel(12f, new[] { R(0, 0, 12, 2.5f) },
                "TWIST PAST FULL AND THE BULB FLARES: THE WHOLE PAGE WAKES!",
                "Easy on the crank... too much and, SPRONG!",
                spawns: new[] { S(EnemyKind.Smudge, 8f, 2.6f) },
                props: new[] { P(PropKind.Vent, 5.5f, 2.5f), P(PropKind.Antenna, 2f, 2.5f) },
                stars: new[] { V(5.5f, 5.4f) }),
            Panel(12f, new[] { R(0, 0, 12, 2.5f) },
                "WORDS ARE SOLID IN A COMIC: STAND ON A SPEECH BUBBLE TO REACH HIGHER.",
                "Hop on the bubble, kid. Comic words hold you up!",
                props: new[] { P(PropKind.Billboard, 8f, 2.5f), Say(3.9f, 4.9f, 3.2f, "INK-O! INKREDIBLE!", 2.6f, 0.1f) },
                stars: new[] { V(1.4f, 4.6f), V(3.9f, 7.6f) }),
        });

    // ---- Act 2 ----------------------------------------------------------------------------------

    private static PageDef Page4() => Page(4, "Factory floor", Act2, 1f, -1,
        new[]
        {
            Panel(13f, new[] { R(0, 0, 13, 2.5f) },
                "THE INKWORKS. INK BATS! FROZEN IN MID-AIR, THEY'RE STEPS.",
                "Bats! Freeze one under me and I'll hop on it.",
                spawns: new[] { S(EnemyKind.Splotch, 5.5f, 7f), S(EnemyKind.Splotch, 9.5f, 6.5f) },
                props: new[] { P(PropKind.Vent, 2f, 2.5f) },
                stars: new[] { V(7.5f, 8.9f) }, backdrop: 1),
            Panel(12f, new[] { R(0, 0, 4.5f, 2.5f), R(8, 0, 4, 4.2f) },
                "STANDING ON A FROZEN BAT? KEEP IT DARK: AIM ABOVE MAX, OR HOLD {FOLLOW} AND THE LIGHT RIDES HIGH.",
                spawns: new[] { S(EnemyKind.Splotch, 6.3f, 7.4f) },
                stars: new[] { V(6.2f, 5.6f) }, backdrop: 1),
        },
        new[]
        {
            Panel(15f, new[] { R(0, 0, 15, 2.5f) },
                "THE BRUISER. ARMOURED IN FRONT: HIT HIM FROM BEHIND. {DOWN} + {KICK} IN THE AIR: GROUND SLAM!",
                "Big fella. Freeze him at the top of his swing!",
                spawns: new[] { S(EnemyKind.Bruiser, 11f, 2.6f) },
                props: new[] { P(PropKind.Crate, 4f, 2.5f) },
                stars: new[] { V(2f, 5f) }, backdrop: 1),
            Panel(12f, new[] { R(0, 0, 12, 2.5f) },
                "A CONVEYOR. IT ONLY RUNS WHILE ITS MOTOR IS LIT: KEEP THE MOTOR DARK.",
                "This belt's pulling me backwards! Find the motor...",
                spawns: new[] { S(EnemyKind.Splotch, 7f, 7f) },
                props: new[] { P(PropKind.Conveyor, 2.6f, 2.5f, -7f) },
                stars: new[] { V(6f, 4.6f), V(10.5f, 6.2f) }, backdrop: 1),
        });

    private static PageDef Page5() => Page(5, "Press room", Act2, 1f, -1,
        new[]
        {
            Panel(14f, new[] { R(0, 0, 14, 2.5f) },
                "FREEZE THE BRUISER AT THE TOP OF HIS SLAM, LIGHT A CROWD IN FRONT, RE-LIGHT.",
                "One slam could clear the room... for us!",
                spawns: new[] { S(EnemyKind.Smudge, 6f, 2.6f), S(EnemyKind.Smudge, 8.5f, 2.6f), S(EnemyKind.Bruiser, 12f, 2.6f) },
                stars: new[] { V(13f, 6f) }, backdrop: 1),
            Panel(12f, new[] { R(0, 0, 5, 2.5f), R(5, 0, 7, 4f) },
                "THE PRESS STAMPS ANYTHING UNDER IT... INKIES TOO.",
                spawns: new[] { S(EnemyKind.Smudge, 3.4f, 2.6f), S(EnemyKind.DotShot, 10f, 4.1f) },
                props: new[] { P(PropKind.Press, 2.4f, 2.5f, 1.6f) },
                stars: new[] { V(11f, 7f) }, backdrop: 1),
        },
        new[]
        {
            Panel(13f, new[] { R(0, 0, 13, 2.5f) },
                "SPLASH METER FULL? PRESS {SPLASH}: EVERY INKIE IN THE BEAM GETS IT!",
                "Time for a full-page spread!",
                spawns: new[] { S(EnemyKind.DotShot, 10f, 2.6f) },
                props: new[] { P(PropKind.Crate, 6f, 2.5f) },
                stars: new[] { V(6f, 5f), V(2f, 4.6f) }, backdrop: 1),
            Panel(11f, new[] { R(0, 0, 11, 2.5f) },
                "THE PRESSES ONLY STAMP IN THE LIGHT. FREEZE ONE UP HIGH, THEN WALK UNDER IT.",
                "Blot's office is just past these. Mind your head!",
                props: new[] { P(PropKind.Press, 3.6f, 2.5f, 1.8f), P(PropKind.Press, 7f, 2.5f, 1.8f) },
                stars: new[] { V(9.2f, 4.6f) }, backdrop: 1),
        });

    private static PageDef Page6() => Page(6, "Blot's office", Act2, 1f, (int)Lens.Ghost,
        new[]
        {
            Panel(12f, new[] { R(0, 0, 12, 2.5f) },
                "THE TOP FLOOR OF THE INKWORKS. BARON BLOT'S OFFICE IS DEAD AHEAD.",
                "This is it, kid. Keep that light steady.",
                props: new[] { P(PropKind.Vent, 3f, 2.5f), P(PropKind.Crate, 8.5f, 2.5f) },
                stars: new[] { V(8.5f, 4.6f) }, backdrop: 1),
            Panel(13f, new[] { R(0, 0, 5, 2.5f), R(5, 0, 4, 3.6f), R(9, 0, 4, 4.7f) },
                "A DOOR WITH A GOLD PLATE: \"B. BLOT, ESQ. KNOCK AND PERISH.\"",
                "Shh... I can hear him monologuing.",
                props: new[] { Say(6.9f, 6.7f, 3.6f, "...AND THEN THE CITY IS MINE! MWAHAHA!", 5.8f, -1.2f) },
                stars: new[] { V(2.5f, 4.8f), V(11f, 7.6f), V(6.9f, 8.9f) }, backdrop: 1),
        },
        new[]
        {
            Panel(22f, new[] { R(0, 0, 22, 2.5f) },
                "BARON BLOT! HIS CHANDELIER HANGS BY A THREAD... ONE FLARE WOULD SNAP IT.",
                "Lights on, Baron. You're going down!",
                spawns: new[] { S(EnemyKind.Blot, 16.5f, 2.6f) },
                props: new[] { P(PropKind.Chandelier, 11f, 8.4f), P(PropKind.Crate, 3f, 2.5f) },
                stars: new[] { V(3f, 4.6f) }, backdrop: 1),
        });

    // ---- Act 3 ----------------------------------------------------------------------------------

    private static PageDef Page7() => Page(7, "Invisible-ink stairwell", Act3, 1f, -1,
        new[]
        {
            // a 7.5-unit gap: too far to jump (a running jump makes 6.5), so the Ghost lens is the way over
            Panel(13f, new[] { R(0, 0, 3f, 2.5f), R(10.5f, 0, 2.5f, 2.5f) },
                "BLOT ESCAPED INTO INVISIBLE INK. ONLY THE PURPLE GHOST LENS ({LENS}) SHOWS IT.",
                "The floor's gone... but I can FEEL it's there. Try the purple lens!",
                stars: new[] { V(11.8f, 4.6f) },
                ghost: new[] { R(3.8f, 2.1f, 1.6f, 0.4f), R(6.9f, 2.1f, 1.6f, 0.4f) },
                ghostStars: new[] { V(5.6f, 5.4f) }, backdrop: 2),
            Panel(12f, new[] { R(0, 0, 2.5f, 2.5f), R(9.5f, 0, 2.5f, 2.5f) },
                "AN ERASER! IT RUBS OUT THE FLOOR IT SLIDES OVER. FREEZE IT... OR TAKE THE INVISIBLE WAY ROUND.",
                "Uh oh. That thing's eating the bridge!",
                spawns: new[] { S(EnemyKind.Eraser, 6f, 2.5f) },
                stars: new[] { V(6f, 7.6f) },
                erasable: new[] { R(2.5f, 1.5f, 7, 1f) },
                ghost: new[] { R(3.2f, 4.6f, 2.4f, 0.4f), R(6.4f, 5.2f, 2.4f, 0.4f) }, backdrop: 2),
        },
        new[]
        {
            Panel(14f, new[] { R(0, 0, 2.5f, 2.5f), R(11.5f, 0, 2.5f, 2.5f) },
                "TWO ERASERS. LURE ONE UNDER A FALLING BRICK!",
                spawns: new[] { S(EnemyKind.Eraser, 5f, 2.5f), S(EnemyKind.Eraser, 9f, 2.5f, true), S(EnemyKind.Splotch, 7f, 7.5f) },
                props: new[] { P(PropKind.Brick, 7f, 9.5f) },
                stars: new[] { V(7f, 6f) },
                erasable: new[] { R(2.5f, 1.5f, 9, 1f) }, backdrop: 2),
            Panel(11f, new[] { R(0, 0, 11, 2.5f) },
                "THE STAIRS GO UP INTO THE DARK. SOME OF THEM ARE INVISIBLE.",
                spawns: new[] { S(EnemyKind.Splotch, 7f, 7f), S(EnemyKind.Splotch, 3.5f, 8f) },
                ghost: new[] { R(2.5f, 4.4f, 2f, 0.4f), R(5.5f, 6.2f, 2f, 0.4f) },
                ghostStars: new[] { V(6.5f, 8.6f) }, backdrop: 2),
        });

    private static PageDef Page8() => Page(8, "The ink vat", Act3, 1f, -1,
        new[]
        {
            Panel(13f, new[] { R(0, 0, 3, 2.5f), R(10.5f, 0, 2.5f, 5.3f) },
                "THE INK IS RISING! IT'S INK: IT ONLY MOVES WHERE THE LIGHT TOUCHES IT.",
                "Keep the light up high, kid. Don't let it touch the ink!",
                props: new[] { P(PropKind.Vent, 11.7f, 5.3f) },
                stars: new[] { V(6f, 7.4f) },
                ghost: new[] { R(4.4f, 3.4f, 2.2f, 0.4f), R(7.6f, 4.4f, 2.2f, 0.4f) },
                flood: 1.2f, floodTop: 9f, floodRise: 0.55f, backdrop: 2),
            Panel(12f, new[] { R(0, 0, 2.5f, 5.3f), R(9.5f, 0, 2.5f, 6.4f) },
                "BATS OVER THE INK. FREEZE ONE AND STAND ON IT... BUT DON'T LIGHT THE INK BELOW.",
                spawns: new[] { S(EnemyKind.Splotch, 4.4f, 4.6f), S(EnemyKind.Splotch, 7.0f, 5.2f) },   // just below the ledge: step down onto them
                stars: new[] { V(6f, 8.6f) },
                flood: 1.0f, floodTop: 9f, floodRise: 0.55f, backdrop: 2),
        },
        new[]
        {
            Panel(13f, new[] { R(0, 0, 2.5f, 4f), R(10.5f, 0, 2.5f, 4f) },
                "AN ERASER ON THE CATWALK, AND THE INK COMING UP UNDER IT.",
                "Light the Eraser, not the ink. Easy, right?",
                spawns: new[] { S(EnemyKind.Eraser, 6.5f, 4f) },
                stars: new[] { V(6.5f, 7.2f) },
                erasable: new[] { R(2.5f, 3f, 8, 1f) },
                flood: 1.0f, floodTop: 8f, floodRise: 0.5f, backdrop: 2),
            Panel(22f, new[] { R(0, 0, 22, 2.5f), R(3, 4.6f, 3, 0.5f), R(15.5f, 4.6f, 3, 0.5f) },
                "THE INK VAT. BARON BLOT, IN HIS ELEMENT... NOW YOU SEE HIM, NOW YOU DON'T.",
                "He's hiding in invisible ink! The purple lens'll find him!",
                spawns: new[] { S(EnemyKind.Blot, 16.5f, 2.6f) },
                props: new[] { P(PropKind.Crate, 9f, 2.5f) },
                stars: new[] { V(4.5f, 6.6f) },
                ghostStars: new[] { V(17f, 6.8f) }, backdrop: 2),
        });

    private static PageDef Page9() => Page(9, "The roof", Act3, 1f, -1,
        new[]
        {
            Panel(13f, new[] { R(0, 0, 3, 2.5f), R(4.5f, 0, 3.5f, 3.6f), R(10, 0, 3, 4.7f) },
                "THE TOP OF THE TOWER. THE INK FOLLOWS YOU UP.",
                "Almost there. Keep that light off the ink!",
                spawns: new[] { S(EnemyKind.DotShot, 11.5f, 4.8f) },
                props: new[] { Say(9.0f, 7.1f, 2.2f, "PEW! PEW!", 2.5f, -1.5f) },
                stars: new[] { V(6.2f, 6.4f), V(9.0f, 9.4f) },
                flood: 1.0f, floodTop: 6.5f, floodRise: 0.6f, backdrop: 0),
            Panel(12f, new[] { R(0, 0, 2.5f, 4.7f), R(9.5f, 0, 2.5f, 5.6f) },
                "THE LAST STRETCH IS DRAWN IN INVISIBLE INK... OVER THE FLOOD. EVERY LENS YOU'VE GOT!",
                stars: new[] { V(1.2f, 7f) },
                ghost: new[] { R(3.2f, 4.5f, 1.9f, 0.4f), R(6.2f, 5f, 1.9f, 0.4f) },
                ghostStars: new[] { V(7.1f, 7.8f) },
                flood: 1.0f, floodTop: 7f, floodRise: 0.5f, backdrop: 0),
        },
        new[]
        {
            Panel(24f, new[] { R(0, 0, 24, 2.5f), R(4, 5f, 3, 0.5f), R(17, 5f, 3, 0.5f) },
                "THE ROOF. BARON BLOT'S LAST STAND.",
                "This ends tonight, Baron!",
                spawns: new[] { S(EnemyKind.Blot, 18f, 2.6f) },
                props: new[] { P(PropKind.Antenna, 2f, 2.5f), P(PropKind.Chimney, 12f, 2.5f), P(PropKind.WaterTower, 22f, 2.5f, 0.9f) },
                stars: new[] { V(5.5f, 7f) }, backdrop: 0),
        });
}
