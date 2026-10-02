using UnityEditor;

/// <summary>
/// Import settings for the pre-rendered room art (Assets/Resources/Room, rendered in
/// Blender): full-screen UI textures, so no mipmaps, clamped, kept at native
/// resolution, high-quality compression.
/// </summary>
public class RoomArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Room/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        ti.filterMode = UnityEngine.FilterMode.Bilinear;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 2048;
        ti.alphaIsTransparency = assetPath.Contains("torch") || assetPath.Contains("/Door/door_") || assetPath.Contains("/gap_");
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
