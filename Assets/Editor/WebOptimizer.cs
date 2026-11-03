using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class WebOptimizer
{
    [MenuItem("Tools/Apply Web Release Settings")]
    public static void Optimize()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUtility.DisplayDialog(
                "WebGL target required",
                "Switch the active build target to WebGL, then run this command again.",
                "OK");
            return;
        }

        var target = NamedBuildTarget.WebGL;
        PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.stripUnusedMeshComponents = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.wasm2023 = true;
        UnityEditor.WebGL.UserBuildSettings.codeOptimization =
            UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;

        AssetDatabase.SaveAssets();

        Debug.Log(
            "Web release settings saved:\n"
            + "  target = " + EditorUserBuildSettings.activeBuildTarget + "\n"
            + "  stripEngineCode = " + PlayerSettings.stripEngineCode + "\n"
            + "  managedStrippingLevel = " + PlayerSettings.GetManagedStrippingLevel(target) + "\n"
            + "  compressionFormat = " + PlayerSettings.WebGL.compressionFormat + "\n"
            + "  decompressionFallback = " + PlayerSettings.WebGL.decompressionFallback + "\n"
            + "  exceptionSupport = " + PlayerSettings.WebGL.exceptionSupport + "\n"
            + "  debugSymbolMode = " + PlayerSettings.WebGL.debugSymbolMode + "\n"
            + "  wasm2023 = " + PlayerSettings.WebGL.wasm2023 + "\n"
            + "  codeOptimization = " + UnityEditor.WebGL.UserBuildSettings.codeOptimization);
    }
}

