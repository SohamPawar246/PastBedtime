using UnityEngine;

/// <summary>
/// The comic's own key light. Comic shaders read it from globals instead of URP
/// lights, so the bedroom's lamps never light the comic and this never lights
/// the bedroom. Point the transform's forward along the light's travel.
/// </summary>
[ExecuteAlways]
public class ComicLight : MonoBehaviour
{
    [Range(0f, 1f)] public float ambient = 0.35f;

    private static readonly int DirId = Shader.PropertyToID("_ComicLightDir");
    private static readonly int AmbientId = Shader.PropertyToID("_ComicAmbient");

    private void OnEnable() => Apply();
    private void Update() => Apply();

    public void Apply()
    {
        Vector3 from = -transform.forward;
        Shader.SetGlobalVector(DirId, new Vector4(from.x, from.y, from.z, 0f));
        Shader.SetGlobalFloat(AmbientId, ambient);
    }
}
