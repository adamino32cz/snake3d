#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Import modelu postavy pro skin DELTA: zmenší textury, nastaví URP materiály
// (výřez vlasů/šatů, průhledné řasy) a vyrobí prefab do Resources.
public class DeltaGirlPostprocessor : AssetPostprocessor
{
    bool IsOurs => assetPath.Replace('\\', '/').Contains("/Models/DeltaGirl/");

    void OnPreprocessTexture()
    {
        if (!IsOurs) return;
        var t = (TextureImporter)assetImporter;
        string n = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        t.maxTextureSize = 1024;
        t.mipmapEnabled = true;
        t.anisoLevel = 4;
        t.alphaIsTransparency = true;
        if (n.Contains("nml") || n.Contains("normal"))
        {
            t.textureType = TextureImporterType.NormalMap;
            t.maxTextureSize = 512;
            t.alphaIsTransparency = false;
        }
        if (n.Contains("hair") || n.Contains("eye") || n.Contains("brow") || n.Contains("lash") || n.Contains("line"))
            t.maxTextureSize = 512;
    }

    void OnPreprocessModel()
    {
        if (!IsOurs || !assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;
        var m = (ModelImporter)assetImporter;
        m.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        m.materialLocation = ModelImporterMaterialLocation.External;
        m.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        m.materialSearch = ModelImporterMaterialSearch.Local;
        m.animationType = ModelImporterAnimationType.Generic;
        m.importAnimation = false;
        m.importBlendShapes = false;
        m.importLights = false;
        m.importCameras = false;
        m.meshCompression = ModelImporterMeshCompression.Medium;
        m.isReadable = false;
        m.importTangents = ModelImporterTangents.None;
        m.globalScale = 1f;
        m.useFileScale = true;
        m.weldVertices = true;
    }
}

public static class DeltaGirlBuilder
{
    const string FbxPath = "Assets/Models/DeltaGirl/HairSample_Female.fbx";
    const string PrefabPath = "Assets/Resources/DeltaGirl.prefab";
    const string TexFolder = "Assets/Models/DeltaGirl/Textures";
    const string MatFolder = "Assets/Models/DeltaGirl/Materials";

    // Zavolá se ze SnakeSceneSetup.BuildScene – prefab musí existovat před hrou.
    public static GameObject EnsurePrefab()
    {
        AssetDatabase.Refresh();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (!model)
        {
            Debug.LogWarning("[Snake3D] Chybí " + FbxPath);
            return null;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/DeltaGirl"))
                AssetDatabase.CreateFolder("Assets/Models", "DeltaGirl");
            AssetDatabase.CreateFolder("Assets/Models/DeltaGirl", "Materials");
        }

        RemapMaterials(model);

        var wrap = new GameObject("DeltaGirl");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.transform.SetParent(wrap.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        if (PrefabUtility.IsPartOfPrefabInstance(instance))
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        // materiály přiřadíme na instanci, ať se uloží do našeho prefabu
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
        var textures = new System.Collections.Generic.Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
        {
            var tp = AssetDatabase.GUIDToAssetPath(g);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            if (tex) textures[Path.GetFileNameWithoutExtension(tp)] = tex;
        }
        ApplyOnGameObject(instance, shader, textures);
        wrap.AddComponent<DeltaGirlCrawler>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(wrap, PrefabPath);
        Object.DestroyImmediate(wrap);
        Debug.Log("[Snake3D] Prefab postavy: " + PrefabPath);
        return prefab;
    }

    static void RemapMaterials(GameObject model)
    {
        // Textury v Textures/ přiřadíme materiálům podle názvu a nastavíme URP/Unlit
        // (VRoid je kreslený jako unlit / toon, Lit by ho ztmavil).
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
        var textures = new System.Collections.Generic.Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex) textures[Path.GetFileNameWithoutExtension(p)] = tex;
        }

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        var seen = new System.Collections.Generic.HashSet<Material>();
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (!src || !seen.Add(src)) continue;
                string matPath = MatFolder + "/" + Sanitize(src.name) + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = shader;
                SetupMaterial(mat, src.name, textures);
                EditorUtility.SetDirty(mat);
            }
        }

        // Přiřadí nové materiály na instancích v FBX (název se shoduje).
        // ModelImporter.externalObjects je složitější – přepíšeme to na prefabu
        // až po Instantiate: viz ApplyOnInstance, volané z EnsurePrefab přes instance.
        ApplyOnGameObject(model, shader, textures);
    }

    static void ApplyOnGameObject(GameObject go, Shader shader, System.Collections.Generic.Dictionary<string, Texture2D> textures)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i]) continue;
                string matPath = MatFolder + "/" + Sanitize(mats[i].name) + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                    SetupMaterial(mat, mats[i].name, textures);
                }
                if (mats[i] != mat) { mats[i] = mat; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    static string Sanitize(string n)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n;
    }

    static Texture2D FindTex(string matName, System.Collections.Generic.Dictionary<string, Texture2D> textures)
    {
        string n = matName;
        foreach (var suffix in new[] { "_HAIR_01", "_HAIR_02", "_SKIN", "_CLOTH", "_FACE", "_EYE" })
        {
            if (n.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            {
                n = n.Substring(0, n.Length - suffix.Length);
                break;
            }
        }
        if (n.EndsWith("_HAIR_00")) { /* leave */ }
        // Hair materials map to _01 / _02
        if (matName.IndexOf("HAIR_01", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            textures.TryGetValue("F00_000_Hair_00_01", out var h1)) return h1;
        if (matName.IndexOf("HAIR_02", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            textures.TryGetValue("F00_000_Hair_00_02", out var h2)) return h2;
        if (textures.TryGetValue(n, out var t)) return t;
        // prefix match
        foreach (var kv in textures)
            if (n.StartsWith(kv.Key) || kv.Key.StartsWith(n)) return kv.Value;
        return null;
    }

    static Texture2D FindNormal(string matName, System.Collections.Generic.Dictionary<string, Texture2D> textures)
    {
        if (matName.IndexOf("Face_00", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            textures.TryGetValue("F00_000_00_Face_00_nml", out var fn)) return fn;
        if (matName.IndexOf("Body_00", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            textures.TryGetValue("F00_002_02_Body_00_nml", out var bn)) return bn;
        return null;
    }

    static void SetupMaterial(Material mat, string srcName, System.Collections.Generic.Dictionary<string, Texture2D> textures)
    {
        string n = srcName.ToLowerInvariant();
        bool cutout = n.Contains("hair") || n.Contains("tops") || n.Contains("shoes") || n.Contains("iris") || n.Contains("body");
        bool blend = n.Contains("eyelash") || n.Contains("eyeline") || n.Contains("brow") || n.Contains("highlight") || n.Contains("eyeextra");
        var tex = FindTex(srcName, textures);
        if (tex) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);

        // default opaque
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_ZWrite", 1f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)RenderQueue.Geometry;
        mat.SetInt("_SrcBlend", (int)BlendMode.One);
        mat.SetInt("_DstBlend", (int)BlendMode.Zero);

        if (cutout)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else if (blend)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        // Normály na Unlit nic neudělají; necháme je být.
        EditorUtility.SetDirty(mat);
    }
}
#endif
