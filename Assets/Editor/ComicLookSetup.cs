using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Past Bedtime > Build Comic Look
///
/// Sets up the black-and-white comic pipeline and bakes the cover set:
///  - a "Comic" layer (the comic world never shares lights or cameras with the room)
///  - the Quaternius Universal Animation Library mannequin imported as a Generic rig
///  - ink materials (PastBedtime/Comic/Lit, PastBedtime/Comic/Burst)
///  - Resources/Comic/CoverSet.prefab: Max mid-punch, an Inkie reeling, a halftone
///    speed-burst behind them, a camera and the comic key light. Poses are sampled
///    from the animation clips at build time, so the prefab needs no Animator.
/// </summary>
public static class ComicLookSetup
{
    private const string FbxPath = "Assets/Art/Characters/Quaternius/UAL1_Standard.fbx";
    private const string MatDir = "Assets/Art/Comic/Materials";
    private const string PrefabDir = "Assets/Resources/Comic";
    private const string PrefabPath = PrefabDir + "/CoverSet.prefab";

    // Cover direction, in one place so it is easy to iterate on.
    private const string HeroClip = "Punch_Cross";
    private const float HeroPose = 0.42f;      // normalised time in the clip
    private const string FoeClip = "Hit_Chest";
    private const float FoePose = 0.35f;

    [MenuItem("Past Bedtime/Build Comic Look")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[ComicLook] Leave play mode first."); return; }
        EnsureFolder(MatDir);
        EnsureFolder(PrefabDir);

        int layer = EnsureLayer("Comic");
        var model = ConfigureModel();
        if (model == null) return;

        var paper = Palette.ComicPaper;
        var mats = new Mats
        {
            heroSuit = Mat("Hero_Suit", 0.97f, rim: 0.4f),
            heroJoint = Mat("Hero_Joint", 0.5f),
            inkBody = Mat("Inkie_Body", 0.0f, rim: 0f),
            inkJoint = Mat("Inkie_Joint", 0.04f, rim: 0f),
            eye = Mat("Eye", 1f, flat: true, outline: 1.5f),
            blob = Mat("Shadow_Blob", 0f, flat: true, outline: 0f),
            burst = BurstMat("Speed_Burst"),
        };

        BuildCoverSet(model, layer, mats, paper);
        AssetDatabase.SaveAssets();
        Debug.Log("[ComicLook] Built " + PrefabPath);
    }

    /// <summary>Renders the cover in edit mode and writes it to a PNG (for iterating on the art).</summary>
    public static string PreviewCover(string pngPath, int width = 1140, int height = 1580)
    {
        var rt = ComicCover.Render(width, height);
        if (rt == null) return "Cover set missing.";
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        return "Saved " + pngPath;
    }

    private struct Mats { public Material heroSuit, heroJoint, inkBody, inkJoint, eye, blob, burst; }

    // ---- Model -----------------------------------------------------------------------------

    private static GameObject ConfigureModel()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) { Debug.LogError("[ComicLook] Missing " + FbxPath); return null; }
        bool dirty = false;
        if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; dirty = true; }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
        if (dirty) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
    }

    private static AnimationClip FindClip(string name)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        var clip = clips.FirstOrDefault(c => c.name == name) ?? clips.FirstOrDefault(c => c.name.EndsWith(name));
        if (clip == null) Debug.LogWarning($"[ComicLook] Clip '{name}' not found. Available: {string.Join(", ", clips.Select(c => c.name))}");
        return clip;
    }

    // ---- Materials --------------------------------------------------------------------------

    private static Material Mat(string name, float tone, float rim = 0.35f, bool flat = false, float outline = 3f)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("PastBedtime/Comic/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Tone", tone);
        mat.SetFloat("_Rim", rim);
        mat.SetFloat("_Flat", flat ? 1f : 0f);
        mat.SetFloat("_OutlineWidth", outline);
        mat.SetFloat("_DotCell", 6f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material BurstMat(string name)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("PastBedtime/Comic/Burst")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetFloat("_Hole", 0.03f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---- Cover set ---------------------------------------------------------------------------

    private static void BuildCoverSet(GameObject model, int layer, Mats m, Color paper)
    {
        var root = new GameObject("CoverSet");

        // Camera: a low, slightly wide hero angle. Disabled: ComicCover renders it once.
        var camGo = new GameObject("CoverCamera");
        camGo.transform.SetParent(root.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = paper;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 40f;
        cam.cullingMask = 1 << layer;
        // The camera looks down -z, so world +x is the LEFT of the picture.
        camGo.transform.localPosition = new Vector3(0.02f, 0.9f, 5.4f);
        camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0.02f, 1.05f, 0f) - camGo.transform.localPosition);

        // Key light from the upper left, slightly in front.
        var lightGo = new GameObject("ComicLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0.6f, -0.7f, -0.45f));
        lightGo.AddComponent<ComicLight>().ambient = 0.32f;

        // Speed-burst backdrop behind the impact.
        var burst = GameObject.CreatePrimitive(PrimitiveType.Quad);
        burst.name = "SpeedBurst";
        Object.DestroyImmediate(burst.GetComponent<Collider>());
        burst.transform.SetParent(root.transform, false);
        burst.transform.localPosition = new Vector3(0f, 1.4f, -2.5f);
        burst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // face the camera
        burst.transform.localScale = new Vector3(7f, 7f, 1f);
        burst.GetComponent<MeshRenderer>().sharedMaterial = m.burst;
        m.burst.SetVector("_Center", new Vector4(0.53f, 0.54f, 0f, 0f)); // rays fan out from the impact

        // The mannequin faces -z, so yaw 108 turns it toward picture-right (world -x)
        // and a little toward camera; -108 toward picture-left.
        // Max on the left, mid cross-punch, three-quarters to camera.
        Character(model, "Max", root.transform, new Vector3(0.5f, 0f, 0.1f), 108f, HeroClip, HeroPose,
            m.heroSuit, m.heroJoint);
        // An Inkie on the right, taking the hit.
        Character(model, "Inkie", root.transform, new Vector3(-0.45f, 0f, -0.2f), -100f, FoeClip, FoePose,
            m.inkBody, m.inkJoint, go => AddEyes(go, m.eye));

        Blob(root.transform, new Vector3(0.5f, 0.005f, 0.1f), new Vector3(0.9f, 0.004f, 0.45f), m.blob);
        Blob(root.transform, new Vector3(-0.45f, 0.005f, -0.2f), new Vector3(0.8f, 0.004f, 0.4f), m.blob);

        SetLayer(root, layer);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static GameObject Character(GameObject model, string name, Transform parent, Vector3 pos, float yaw,
        string clipName, float pose, Material main, Material accent, System.Action<GameObject> decorate = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // Normalise to 1.8 m tall whatever the source units are.
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (b.size.y > 0.01f) go.transform.localScale *= 1.8f / b.size.y;
        }

        // Attach props in the upright bind pose, then pose: props follow their bones.
        decorate?.Invoke(go);
        var clip = FindClip(clipName);
        if (clip != null) clip.SampleAnimation(go, clip.length * pose);

        foreach (var r in renderers)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = i == 0 ? main : accent;
            r.sharedMaterials = mats;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
        foreach (var a in go.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
        return go;
    }

    /// <summary>Two white oval eyes on the head bone, placed in the bind pose so they ride the head.</summary>
    private static void AddEyes(GameObject character, Material eye)
    {
        var head = character.GetComponentsInChildren<Transform>()
            .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("head") && !t.name.ToLowerInvariant().Contains("end"));
        if (head == null) { Debug.LogWarning("[ComicLook] No head bone for eyes."); return; }
        var fwd = -character.transform.forward;   // the mannequin faces -z
        var right = -character.transform.right;
        for (int i = -1; i <= 1; i += 2)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(e.GetComponent<Collider>());
            e.name = i < 0 ? "EyeL" : "EyeR";
            e.GetComponent<MeshRenderer>().sharedMaterial = eye;
            e.transform.position = head.position + Vector3.up * 0.1f + fwd * 0.085f + right * (0.042f * i);
            e.transform.rotation = Quaternion.LookRotation(fwd);
            e.transform.localScale = new Vector3(0.045f, 0.065f, 0.03f);
            e.transform.SetParent(head, true);
        }
    }

    private static void Blob(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(b.GetComponent<Collider>());
        b.name = "InkShadow";
        b.transform.SetParent(parent, false);
        b.transform.localPosition = pos;
        b.transform.localScale = scale;
        b.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---- Utilities --------------------------------------------------------------------------

    private static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    private static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = name;
                tm.ApplyModifiedProperties();
                return i;
            }
        }
        Debug.LogError("[ComicLook] No free layer for " + name);
        return 0;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
