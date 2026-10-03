using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Past Bedtime > Build Comic Cast
///
/// Imports the comic's characters, built in Blender (PastBedtime_AssetSources/blender):
///  - Max Voltage (characters/build_character.py) on the Quaternius UAL1 mannequin;
///  - Baron Blot's Inkies (enemies/build_enemy.py): the Smudge goon, the Bruiser, the
///    Splotch bat, the Dot-Shot gunner and the Eraser; and Baron Blot himself.
/// Max and the Bruiser keep the mannequin's rig and bone paths, so the Universal Animation
/// Library clips (UAL1_Standard.fbx) play on them as Generic animation. The other Inkies
/// have their own small rigs and bring their own clips (Idle, Move, attacks, Hit, Death).
/// Blender materials are remapped to comic ink materials; the comic is black-and-white
/// print, so they differ only in tone.
/// </summary>
public static class ComicCastSetup
{
    private const string ClipFbx = "Assets/Art/Characters/Quaternius/UAL1_Standard.fbx";
    private const string MatDir = "Assets/Art/Comic/Materials";
    private const string Inkies = "Assets/Art/Characters/Inkies/";

    public const string Max = "Assets/Art/Characters/MaxVoltage/MaxVoltage.fbx";
    public const string Smudge = Inkies + "Smudge.fbx";
    public const string Bruiser = Inkies + "Bruiser.fbx";
    public const string Splotch = Inkies + "Splotch.fbx";
    public const string DotShot = Inkies + "DotShot.fbx";
    public const string Eraser = Inkies + "Eraser.fbx";
    public const string Blot = Inkies + "Blot.fbx";

    private struct Ink { public string name; public float tone, rim, outline, gloss; public bool flat; }
    private static Ink I(string name, float tone, float rim = 0.35f, float outline = 3f, bool flat = false, float gloss = 0f)
        => new Ink { name = name, tone = tone, rim = rim, outline = outline, flat = flat, gloss = gloss };

    private static readonly Ink[] MaxInks =
        { I("MV_Suit", 0.97f, 0.4f), I("MV_Joint", 0.55f), I("MV_Ink", 0f, 0.25f), I("MV_Cape", 0.42f, 0.3f) };

    // Shared by every Inkie: wet black ink, flat white eyes and teeth, and the few props.
    private static readonly Ink[] EnemyInks =
    {
        I("EN_Ink", 0f, 0f, gloss: 1f),
        I("EN_Eye", 1f, 0f, 2f, flat: true),
        I("EN_Pupil", 0f, 0f, 1f, flat: true),
        I("EN_Rubber", 0.8f, 0.35f),
        I("EN_Paper", 0.97f, 0.3f),
        I("EN_Glass", 0.5f, 0.45f),
        I("EN_Metal", 0.66f, 0.5f),
    };

    private static readonly string[] Looping = { "Idle", "Move", "Fly", "Laugh" };

    public static readonly string[] Enemies = { Smudge, Bruiser, Splotch, DotShot, Eraser, Blot };

    [MenuItem("Past Bedtime/Build Comic Cast")]
    public static void Build()
    {
        Configure(Max, MaxInks, ownClips: false);
        foreach (var e in Enemies) Configure(e, EnemyInks, ownClips: e != Bruiser);
        AssetDatabase.SaveAssets();
        Debug.Log("[ComicCast] Imported Max and " + Enemies.Length + " Inkies");
    }

    private static void Configure(string path, Ink[] inks, bool ownClips)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) { Debug.LogError("[ComicCast] Missing " + path); return; }
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = ownClips;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (var old in importer.GetExternalObjectMap().Keys.ToList()) importer.RemoveRemap(old);
        foreach (var ink in inks)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), ink.name), Mat(ink));
        if (ownClips)
        {
            // Blender names takes "Rig|Idle": keep the short name, loop the cycles.
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                c.loopTime = Looping.Contains(c.name);
            }
            importer.clipAnimations = clips;
        }
        importer.SaveAndReimport();
    }

    private static AnimationClip FindClip(string fbxPath, string clipName)
    {
        var own = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName && !c.name.StartsWith("__preview__"));
        if (own != null) return own;
        foreach (var lib in new[] { "Assets/Art/Characters/MaxVoltage/ComicMoves.fbx", ClipFbx })
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(lib).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name.EndsWith("|" + clipName) && !c.name.StartsWith("__preview__"));
            if (clip != null) return clip;
        }
        return null;
    }

    /// <summary>One member of a line-up: which model, posed on which clip, where.</summary>
    public struct Pose
    {
        public string fbx, clip;
        public float time, yaw, scale;
        public Vector3 position;
        public Pose(string fbx, string clip, float time, Vector3 position, float yaw = 180f, float scale = 1f)
        { this.fbx = fbx; this.clip = clip; this.time = time; this.position = position; this.yaw = yaw; this.scale = scale; }
    }

    /// <summary>Renders characters posed on frames of their clips, through the comic shader, to a PNG.</summary>
    public static string Lineup(string pngPath, Pose[] cast, Vector3 camPos, Vector3 lookAt, float fov = 28f,
        int width = 1600, int height = 900)
    {
        int layer = LayerMask.NameToLayer("Comic");
        var root = new GameObject("CastPreview");
        var log = new List<string>();
        try
        {
            foreach (var p in cast)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(p.fbx);
                if (model == null) { log.Add("missing " + p.fbx); continue; }
                var who = (GameObject)Object.Instantiate(model, root.transform);
                who.transform.position = p.position;
                who.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);
                who.transform.localScale = Vector3.one * p.scale;
                var clip = FindClip(p.fbx, p.clip);
                if (clip != null) clip.SampleAnimation(who, clip.length * p.time);
                if (who.GetComponentsInChildren<Transform>().Any(t => t.name == "cape.01"))
                    who.AddComponent<CapeSway>().Settle();
                foreach (var r in who.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
                log.Add($"{Path.GetFileNameWithoutExtension(p.fbx)}:{(clip != null ? clip.name : "no clip")}");
            }

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.ComicPaper;
            cam.fieldOfView = fov;
            cam.cullingMask = 1 << layer;
            camGo.transform.position = camPos;
            camGo.transform.rotation = Quaternion.LookRotation(lookAt - camPos);

            var lightGo = new GameObject("ComicLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, -0.7f, -0.45f));
            var light = lightGo.AddComponent<ComicLight>();
            light.ambient = 0.32f;
            light.Apply();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            return "Saved " + pngPath + " (" + string.Join(", ", log) + ")";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material Mat(Ink ink)
    {
        string path = $"{MatDir}/{ink.name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("PastBedtime/Comic/Lit")) { name = ink.name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Tone", ink.tone);
        mat.SetFloat("_Rim", ink.rim);
        mat.SetFloat("_Flat", ink.flat ? 1f : 0f);
        mat.SetFloat("_Gloss", ink.gloss);
        mat.SetFloat("_OutlineWidth", ink.outline);
        mat.SetFloat("_DotCell", 6f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
