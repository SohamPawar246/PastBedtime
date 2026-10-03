using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Past Bedtime > Build Game Assets
///
/// Makes everything the Game scene builds from (Resources/Game):
///  - the comic-world materials (PastBedtime/Comic/Lit, tone only; captions flat yellow);
///  - an animator controller per character: a flat list of states named after the moves,
///    which code cross-fades (Max and the Bruiser use the Universal Animation Library clips
///    plus our own moves; the other Inkies use their own clips);
///  - the character prefabs: model, controller, collider, light sample points, health, brain;
///  - GameAssets, which ties them together.
/// Run Build Comic Cast first so the models are imported.
/// </summary>
public static class GameAssetsSetup
{
    private const string Root = "Assets/Resources/Game";
    private const string MatDir = Root + "/Materials";
    private const string AnimDir = Root + "/Animators";
    private const string PrefabDir = Root + "/Characters";
    private const string UAL = "Assets/Art/Characters/Quaternius/UAL1_Standard.fbx";
    private const string Moves = "Assets/Art/Characters/MaxVoltage/ComicMoves.fbx";

    [MenuItem("Past Bedtime/Build Game Assets")]
    public static void Build()
    {
        foreach (var d in new[] { Root, MatDir, AnimDir, PrefabDir }) EnsureFolder(d);
        LoopLibraryClips();
        ImportMoves();

        var A = AssetDatabase.LoadAssetAtPath<GameAssets>(Root + "/GameAssets.asset");
        if (A == null)
        {
            A = ScriptableObject.CreateInstance<GameAssets>();
            AssetDatabase.CreateAsset(A, Root + "/GameAssets.asset");
        }

        // a manga night: the sky in gradation tone, dark overhead to the city's glow at the horizon, with
        // stars; the city recedes in three depths (each paler and more finely lined than the one before)
        A.sky = Mat("Sky", 0.12f, flat: true, outline: 0f, toneBottom: 0.46f, stars: 1f);
        A.skylineFar = Mat("SkylineFar", 0.36f, flat: true, outline: 1f, toneBottom: 0.52f);
        A.skylineMid = Mat("SkylineMid", 0.2f, flat: true, outline: 1.4f, toneBottom: 0.3f);
        A.moon = Mat("Moon", 1f, flat: true, outline: 1.5f);
        A.far = Mat("FarBuilding", 0.07f, flat: true, outline: 0f);
        A.window = Mat("Window", 1f, flat: true, outline: 0f);
        // the scenery gets the inker's scuffs (short thick strokes on the lit side, like the reference art)
        A.roof = Mat("Roof", 0.6f, rim: 0.25f, scuffs: 1f);
        A.wall = Mat("Wall", 0.4f, rim: 0.2f, scuffs: 0.85f);
        A.border = Mat("Border", 0f, flat: true, outline: 0f);
        A.ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Comic/Materials/EN_Ink.mat");
        if (A.ink == null) A.ink = Mat("Ink", 0f);
        A.metal = Mat("Metal", 0.55f, rim: 0.4f, scuffs: 0.8f);
        A.wood = Mat("Wood", 0.5f, rim: 0.2f, scuffs: 1f);
        A.paper = Mat("Paper", 0.97f, flat: true, outline: 2f);
        A.star = Mat("Star", 1f, flat: true, outline: 3f);
        A.caption = Unlit("Caption", Palette.Yellow);
        A.ghostHint = Unlit("GhostHint", Palette.LensGhost);
        A.nothing = Nothing();

        A.max = Character("Max", "Assets/Art/Characters/MaxVoltage/MaxVoltage.fbx", MaxStates(), go =>
        {
            Controller(go, 0.32f, 1.8f);
            Points(go, true, 0.2f, 0.9f, 1.6f);
            Health(go, Team.Hero, 5f);
            go.AddComponent<ClipPlayer>();
            go.AddComponent<HeroController>();
            go.AddComponent<HeroCombat>();
            go.transform.GetChild(0).gameObject.AddComponent<CapeSway>();
        });
        A.smudge = Character("Smudge", "Assets/Art/Characters/Inkies/Smudge.fbx", Own("Idle", "Move", "Punch1", "Punch2", "Punch3", "Hit", "Death"), go =>
        {
            Controller(go, 0.42f, 1.3f);
            Points(go, false, 0.25f, 0.75f, 1.2f);
            Health(go, Team.Inkie, 20f);
            go.AddComponent<ClipPlayer>();
            go.AddComponent<SmudgeBrain>();
        });
        A.dotShot = Character("DotShot", "Assets/Art/Characters/Inkies/DotShot.fbx", Own("Idle", "Move", "Shoot", "Hit", "Death"), go =>
        {
            go.transform.GetChild(0).localScale = Vector3.one * 1.15f;
            Controller(go, 0.36f, 0.95f);
            Points(go, false, 0.25f, 0.75f);
            Health(go, Team.Inkie, 15f);
            go.AddComponent<ClipPlayer>();
            go.AddComponent<DotShotBrain>();
        });
        A.bruiser = Character("Bruiser", "Assets/Art/Characters/Inkies/Bruiser.fbx", BruiserStates(), go =>
        {
            go.transform.GetChild(0).localScale = Vector3.one * 1.35f;
            Controller(go, 0.62f, 2.45f);
            Points(go, false, 0.3f, 1.25f, 2.2f);
            var h = Health(go, Team.Inkie, 60f);
            h.frontArmour = true;
            go.AddComponent<ClipPlayer>();
            go.AddComponent<BruiserBrain>();
        });
        A.splotch = Character("Splotch", "Assets/Art/Characters/Inkies/Splotch.fbx", Own("Fly", "Dive", "Hit", "Death"), go =>
        {
            go.transform.GetChild(0).localScale = Vector3.one * 1.2f;
            var box = go.AddComponent<BoxCollider>();            // frozen in mid-air, a stepping stone
            box.center = new Vector3(0f, 0.02f, 0f);
            box.size = new Vector3(1.5f, 0.42f, 1f);
            var l = go.AddComponent<Lightable>();
            l.points = new[] { Vector3.zero, new Vector3(-0.6f, 0.1f, 0f), new Vector3(0.6f, 0.1f, 0f) };
            Health(go, Team.Inkie, 8f);
            go.AddComponent<ClipPlayer>();
            go.AddComponent<SplotchBrain>();
        });
        A.eraser = Character("Eraser", "Assets/Art/Characters/Inkies/Eraser.fbx", Own("Idle", "Move", "Attack", "Hit", "Death"), go =>
        {
            var box = go.AddComponent<BoxCollider>();            // solid rubber: stand on it when frozen
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(1.2f, 0.5f, 0.6f);
            var l = go.AddComponent<Lightable>();
            l.points = new[] { new Vector3(0f, 0.25f, 0f), new Vector3(-0.5f, 0.25f, 0f), new Vector3(0.5f, 0.25f, 0f) };
            Health(go, Team.Inkie, 25f);
            go.AddComponent<ClipPlayer>();
            go.AddComponent<EraserBrain>();
        });
        A.blot = Character("Blot", "Assets/Art/Characters/Inkies/Blot.fbx", Own("Idle", "Move", "Throw", "Summon", "Laugh", "Hit", "Dive", "Death"), go =>
        {
            go.transform.GetChild(0).localScale = Vector3.one * 1.15f;     // the Baron stands a head over Max
            Controller(go, 0.75f, 2.5f);
            Points(go, false, 0.3f, 1.2f, 2.2f);
            Health(go, Team.Inkie, BlotBrain.MaxHp);
            go.AddComponent<ClipPlayer>();
            go.AddComponent<BlotBrain>();
        });

        EditorUtility.SetDirty(A);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameAssets] Built materials, controllers and prefabs in " + Root);
    }

    // ---- states ------------------------------------------------------------------------------

    /// <summary>Max: our state names → clip names (library first; our own moves when they exist).</summary>
    private static (string state, string[] clips)[] MaxStates() => new[]
    {
        ("Idle", new[] { "Idle_Loop" }),
        ("Guard", new[] { "Guard", "Idle_Loop" }),
        ("Run", new[] { "Sprint_Loop" }),
        ("JumpStart", new[] { "JumpStart", "Jump_Start" }),
        ("JumpLoop", new[] { "Jump_Loop" }),
        ("Land", new[] { "Jump_Land" }),
        ("Jab", new[] { "Jab", "Punch_Jab" }),
        ("Cross", new[] { "Cross", "Punch_Cross" }),
        ("Haymaker", new[] { "Haymaker", "Sword_Attack" }),
        ("Kick", new[] { "Kick", "Punch_Cross" }),
        ("Launcher", new[] { "Launcher", "Punch_Cross" }),
        ("DiveKick", new[] { "DiveKick", "Jump_Loop" }),
        ("SlamDive", new[] { "SlamDive", "Jump_Loop" }),
        ("GroundSlam", new[] { "GroundSlam", "Jump_Land" }),
        ("Dodge", new[] { "Dodge", "Roll" }),
        ("Hurt", new[] { "Hurt", "Hit_Chest" }),
        ("Death", new[] { "Death01" }),
        ("Talk", new[] { "Idle_Talking_Loop" }),
    };

    private static (string state, string[] clips)[] BruiserStates() => new[]
    {
        ("Idle", new[] { "Idle_Loop" }),
        ("Move", new[] { "Walk_Loop" }),
        ("Slam", new[] { "BruiserSlam", "Sword_Attack" }),
        ("Hit", new[] { "Hit_Chest" }),
        ("Death", new[] { "Death01" }),
    };

    private static (string state, string[] clips)[] Own(params string[] names) =>
        names.Select(n => (n, new[] { n })).ToArray();

    private static AnimationClip FindClip(string modelPath, string name)
    {
        foreach (var path in new[] { modelPath, Moves, UAL })
        {
            if (!File.Exists(path)) continue;
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name)));
            if (clip != null) return clip;
        }
        return null;
    }

    // ---- prefabs -------------------------------------------------------------------------------

    private static GameObject Character(string name, string modelPath, (string state, string[] clips)[] states, System.Action<GameObject> setup)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError("[GameAssets] Missing model " + modelPath);
            return null;
        }
        var controller = BuildController(name, modelPath, states);
        var root = new GameObject(name);
        try
        {
            return BuildPrefab(root, name, model, controller, setup);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject BuildPrefab(GameObject root, string name, GameObject model, AnimatorController controller, System.Action<GameObject> setup)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Model";
        inst.transform.SetParent(root.transform, false);
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
        setup(root);
        int layer = LayerMask.NameToLayer("Comic");
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        string path = $"{PrefabDir}/{name}.prefab";
        return PrefabUtility.SaveAsPrefabAsset(root, path);
    }

    private static AnimatorController BuildController(string name, string modelPath, (string state, string[] clips)[] states)
    {
        string path = $"{AnimDir}/{name}.controller";
        // Rebuilt in place, never deleted: a controller deleted and recreated at the same path gets
        // its old GUID back, and prefabs already loaded keep pointing at the dead object (no animation).
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var old in sm.states.ToArray()) sm.RemoveState(old.state);
        var missing = new List<string>();
        foreach (var (state, clips) in states)
        {
            AnimationClip clip = null;
            foreach (var c in clips)
            {
                clip = FindClip(modelPath, c);
                if (clip != null) break;
            }
            if (clip == null) { missing.Add(state); continue; }
            var s = sm.AddState(state);
            s.motion = clip;
            s.writeDefaultValues = false;
            if (sm.defaultState == null || state == "Idle" || state == "Fly") sm.defaultState = s;
        }
        if (missing.Count > 0) Debug.LogWarning($"[GameAssets] {name}: no clip for {string.Join(", ", missing)}");
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static void Controller(GameObject go, float radius, float height)
    {
        var cc = go.AddComponent<CharacterController>();
        cc.radius = radius;
        cc.height = height;
        cc.center = new Vector3(0f, height / 2f + 0.02f, 0f);
        cc.skinWidth = 0.03f;
        cc.stepOffset = 0.3f;
        cc.slopeLimit = 50f;
        cc.minMoveDistance = 0f;
    }

    private static void Points(GameObject go, bool hero, params float[] heights)
    {
        var l = go.AddComponent<Lightable>();
        l.isHero = hero;
        l.points = heights.Select(h => new Vector3(0f, h, 0f)).ToArray();
    }

    private static Health Health(GameObject go, Team team, float hp)
    {
        var h = go.AddComponent<Health>();
        h.team = team;
        h.maxHp = h.hp = hp;
        return h;
    }

    /// <summary>The library's cycles loop; one-shots don't.</summary>
    private static void LoopLibraryClips()
    {
        var importer = AssetImporter.GetAtPath(UAL) as ModelImporter;
        if (importer == null) return;
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool dirty = false;
        foreach (var c in clips)
        {
            bool loop = c.name.EndsWith("_Loop") || c.name.EndsWith("Idle");
            if (c.loopTime != loop) { c.loopTime = loop; dirty = true; }
        }
        if (dirty || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// Our moves come in as whatever takes ComicMoves.fbx has now (build_moves.py re-exports it):
    /// the clip list is rebuilt from the file every time, so new moves appear and re-timed ones
    /// keep their full length. Only the guard breath loops; the air poses hold their last frame.
    /// </summary>
    private static void ImportMoves()
    {
        var importer = AssetImporter.GetAtPath(Moves) as ModelImporter;
        if (importer == null) return;
        var clips = importer.defaultClipAnimations;
        foreach (var c in clips)
        {
            string shortName = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1);
            c.name = "Rig|" + shortName;
            c.loopTime = shortName == "Guard";
        }
        bool same = importer.clipAnimations.Length == clips.Length && importer.clipAnimations.Zip(clips, (a, b) =>
            a.takeName == b.takeName && a.name == b.name && a.loopTime == b.loopTime &&
            Mathf.Approximately(a.firstFrame, b.firstFrame) && Mathf.Approximately(a.lastFrame, b.lastFrame)).All(x => x);
        if (same) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    // ---- materials ------------------------------------------------------------------------------

    private static Material Mat(string name, float tone, float rim = 0.3f, bool flat = false, float outline = 3f, float scuffs = 0f,
        float toneBottom = -1f, float stars = 0f)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("PastBedtime/Comic/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_Tone", tone);
        m.SetFloat("_Rim", rim);
        m.SetFloat("_Flat", flat ? 1f : 0f);
        m.SetFloat("_OutlineWidth", outline);
        m.SetFloat("_DotCell", 6f);
        m.SetFloat("_Gloss", 0f);
        m.SetFloat("_Scuffs", scuffs);
        m.SetFloat("_ToneBottom", toneBottom);
        m.SetFloat("_Stars", stars);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Unlit(string name, Color color)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("PastBedtime/Comic/Flat")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Nothing()
    {
        string path = $"{MatDir}/Nothing.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("PastBedtime/Nothing")) { name = "Nothing" };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
