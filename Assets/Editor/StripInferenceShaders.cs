using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

/// <summary>
/// The game never runs the AI-inference package (Sentis, there for the editor's AI tooling), but its shaders
/// ship in every player because they sit in that package's Resources folder: about 9.5 MB of the web build.
/// Every variant of them is stripped from player builds, so they cost next to nothing; the editor keeps them.
/// </summary>
internal class StripInferenceShaders : IPreprocessShaders, IPreprocessComputeShaders
{
    private const string Package = "Packages/com.unity.ai.inference/";
    private static readonly Dictionary<int, bool> _isInference = new();

    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (FromInference(shader)) data.Clear();
    }

    public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
    {
        if (FromInference(shader)) data.Clear();
    }

    private static bool FromInference(Object shader)
    {
        int id = shader.GetInstanceID();
        if (!_isInference.TryGetValue(id, out bool yes))
            _isInference[id] = yes = AssetDatabase.GetAssetPath(shader).StartsWith(Package);
        return yes;
    }
}
