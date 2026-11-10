#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Pomocné příkazy pro spouštění z příkazové řádky (-batchmode -executeMethod ...).
// Při normální práci v Unity je nepotřebuješ.
public static class SnakeBatch
{
    // 1) Naimportuje TextMesh Pro Essential Resources (fonty pro texty v UI).
    public static void ImportTmp()
    {
        AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
        AssetDatabase.Refresh();
    }

    // 2) Postaví scénu (stejné jako menu Tools > Snake 3D > Vytvořit scénu).
    public static void BuildScene() => SnakeSceneSetup.BuildScene();

    // 3) Sestaví hru pro Linux do složky Build/Linux.
    public static void BuildLinux()
    {
        PlayerSettings.runInBackground = true; // hra běží i bez fokusu okna
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Snake3D.unity" }, "Build/Linux/Snake3D.x86_64",
            BuildTarget.StandaloneLinux64, BuildOptions.None);
        Debug.Log("[Snake3D] Build: " + r.summary.result);
        if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // 4) Sestaví hru pro macOS (Universal Intel + Apple Silicon, Mono) do Build/Mac/Snake3D.app.
    public static void BuildMac()
    {
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        TrySetMacUniversal();

        var r = BuildPipeline.BuildPlayer(
            new[] { "Assets/Scenes/Snake3D.unity" },
            "Build/Mac/Snake3D.app",
            BuildTarget.StandaloneOSX,
            BuildOptions.None);
        Debug.Log("[Snake3D] Mac build: " + r.summary.result);
        if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // 5) Sestaví hru pro WebGL do Build/WebGL (Gzip + decompression fallback, jde spustit i na jednoduchém hostingu).
    public static void BuildWebGL()
    {
        PlayerSettings.runInBackground = true;
        // GitHub Pages neumí hlavičky pro .gz/.br, proto bez komprese
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        var r = BuildPipeline.BuildPlayer(
            new[] { "Assets/Scenes/Snake3D.unity" },
            "Build/WebGL",
            BuildTarget.WebGL,
            BuildOptions.None);
        Debug.Log("[Snake3D] WebGL build: " + r.summary.result);
        if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    static void TrySetMacUniversal()
    {
        // 2 = Universal (x64 + ARM64) na macOS ve Unity 2022.3
        try
        {
            PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 2);
            Debug.Log("[Snake3D] SetArchitecture(Standalone, 2)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Snake3D] SetArchitecture: " + e.Message);
        }

        // Unity 2022.3: UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSXArchitecture.x64ARM64
        try
        {
            var enumType = FindType("UnityEditor.OSXStandalone.OSXArchitecture");
            var settingsType = FindType("UnityEditor.OSXStandalone.UserBuildSettings");
            if (enumType != null && settingsType != null)
            {
                var prop = settingsType.GetProperty("architecture");
                var value = Enum.Parse(enumType, "x64ARM64");
                prop.SetValue(null, value, null);
                Debug.Log("[Snake3D] macOS architecture = x64ARM64 (Universal)");
            }
            else
            {
                Debug.LogWarning("[Snake3D] OSXStandalone architecture API not found");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Snake3D] OSX architecture: " + e.Message);
        }
    }

    static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
#endif
