#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Import modelu postavy pro skiny DELTA a MIKU: zmenší textury, nastaví URP materiály
// (výřez vlasů/šatů, průhledné řasy) a vyrobí prefaby do Resources.
public class DeltaGirlPostprocessor : AssetPostprocessor
{
    bool IsOurs { get { var p = assetPath.Replace('\\', '/'); return p.Contains("/Models/DeltaGirl/") || p.Contains("/Models/TdaMiku/"); } }

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
    const string PrefabDelta = "Assets/Resources/DeltaGirl.prefab";
    const string PrefabMiku = "Assets/Resources/MikuGirl.prefab";
    const string TexDelta = "Assets/Models/DeltaGirl/Textures";
    const string TexMiku = "Assets/Models/DeltaGirl/TexturesMiku";
    const string MatDelta = "Assets/Models/DeltaGirl/Materials";
    const string MatMiku = "Assets/Models/DeltaGirl/MaterialsMiku";

    // Zavolá se ze SnakeSceneSetup.BuildScene – prefaby musí existovat před hrou.
    public static GameObject EnsurePrefab()
    {
        AssetDatabase.Refresh();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (!model)
        {
            Debug.LogWarning("[Snake3D] Chybí " + FbxPath);
            return null;
        }

        EnsureFolder("Assets/Resources");
        EnsureFolder(MatDelta);
        EnsureFolder(MatMiku);

        // Materiály se přiřazují jen na instanci prefabu, FBX se nemění
        // (DELTA a MIKU sdílejí stejný model, ale mají vlastní materiály).
        EnsureOne(model, PrefabDelta, "DeltaGirl", "delta", 1f, MatDelta, new[] { TexDelta });
        EnsureOne(model, PrefabMiku, "MikuGirl", "miku", 1.45f, MatMiku, new[] { TexDelta, TexMiku });
        // Pokud je k dispozici Tda Miku FBX, přepíše prefab MIKU skutečným modelem.
        EnsureTdaMiku();
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDelta);
    }

    static void EnsureFolder(string path)
    {
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static Dictionary<string, Texture2D> LoadTextures(string[] folders)
    {
        var textures = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(g);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                if (tex) textures[Path.GetFileNameWithoutExtension(tp)] = tex;
            }
        }
        return textures;
    }

    static GameObject EnsureOne(GameObject model, string prefabPath, string wrapName, string lookId,
        float hairLength, string matFolder, string[] texFolders)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
        var textures = LoadTextures(texFolders);

        var wrap = new GameObject(wrapName);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.transform.SetParent(wrap.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        if (PrefabUtility.IsPartOfPrefabInstance(instance))
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        ApplyOnGameObject(instance, shader, textures, matFolder);

        var crawler = wrap.AddComponent<DeltaGirlCrawler>();
        crawler.lookId = lookId;
        crawler.hairLengthScale = hairLength;
        crawler.modelScale = 1.9f;
        crawler.hipBack = 1.10f;
        crawler.hipHeight = 0.70f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(wrap, prefabPath);
        Object.DestroyImmediate(wrap);
        Debug.Log("[Snake3D] Prefab postavy: " + prefabPath);
        return prefab;
    }

    static void ApplyOnGameObject(GameObject go, Shader shader, Dictionary<string, Texture2D> textures, string matFolder)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i]) continue;
                string matPath = matFolder + "/" + Sanitize(mats[i].name) + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                    SetupMaterial(mat, mats[i].name, textures);
                }
                else
                {
                    mat.shader = shader;
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

    static Texture2D FindTex(string matName, Dictionary<string, Texture2D> textures)
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
        if (matName.IndexOf("HAIR_01", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            textures.TryGetValue("F00_000_Hair_00_01", out var h1)) return h1;
        if (matName.IndexOf("HAIR_02", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            textures.TryGetValue("F00_000_Hair_00_02", out var h2)) return h2;
        if (textures.TryGetValue(n, out var t)) return t;
        foreach (var kv in textures)
            if (n.StartsWith(kv.Key) || kv.Key.StartsWith(n)) return kv.Value;
        return null;
    }

    static void SetupMaterial(Material mat, string srcName, Dictionary<string, Texture2D> textures)
    {
        string n = srcName.ToLowerInvariant();
        bool cutout = n.Contains("hair") || n.Contains("tops") || n.Contains("shoes") || n.Contains("iris") || n.Contains("body");
        bool blend = n.Contains("eyelash") || n.Contains("eyeline") || n.Contains("brow") || n.Contains("highlight") || n.Contains("eyeextra");
        var tex = FindTex(srcName, textures);
        if (tex) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);

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

        EditorUtility.SetDirty(mat);
    }

    const string TdaFbx = "Assets/Models/TdaMiku/TdaMiku.fbx";
    const string TdaMat = "Assets/Models/TdaMiku/Materials";
    const string TdaTex = "Assets/Models/TdaMiku/Textures";

    static void EnsureTdaMiku()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(TdaFbx);
        if (!model)
        {
            Debug.LogWarning("[Snake3D] Chybi " + TdaFbx + " – MIKU zustane prebarveny CC0 model.");
            return;
        }
        EnsureFolder(TdaMat);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
        var textures = LoadTextures(new[] { TdaTex });

        var wrap = new GameObject("MikuGirl");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.transform.SetParent(wrap.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        if (PrefabUtility.IsPartOfPrefabInstance(instance))
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        AssignGenericUnlit(instance, shader, textures, TdaMat);

        var crawler = wrap.AddComponent<DeltaGirlCrawler>();
        crawler.lookId = "miku";
        crawler.hairLengthScale = 1f;
        crawler.modelScale = 1.9f;
        crawler.hipBack = 1.10f;
        crawler.hipHeight = 0.70f;

        PrefabUtility.SaveAsPrefabAsset(wrap, PrefabMiku);
        Object.DestroyImmediate(wrap);
        Debug.Log("[Snake3D] Prefab postavy (Tda Miku): " + PrefabMiku);
    }


    // Číslované materiály z Tda VRM/FBX (mapování podle původního GLB).
    static readonly System.Collections.Generic.Dictionary<string, string> TdaMatTex = new System.Collections.Generic.Dictionary<string, string>
    {
        {"0","mikuv4x_body00.tga"},{"1","mikuv4x_body00.tga"},{"2","mikuv4x_body00.tga"},
        {"3","mikuv4x_body00.tga"},{"4","mikuv4x_body00.tga"},{"5","mikuv4x_body00.tga"},
        {"6","mikuv4x_body00.tga"},{"15","mikuv4x_body00.tga"},
        {"7","face_Mikuv4x.tga"},{"8","face_Mikuv4x.tga"},{"9","face_Mikuv4x.tga"},
        {"10","face_Mikuv4x.tga"},{"13","face_Mikuv4x.tga"},{"14","face_Mikuv4x.tga"},
        {"11","hair_Mikuv4x.tga"},{"12","hair_Mikuv4x.tga"},
        {"16","body02_MikuAp.tga"},{"17","body02_MikuAp.tga"},
    };

    static Texture2D ResolveTdaTexture(Material src, Dictionary<string, Texture2D> textures)
    {
        string key;
        if (TdaMatTex.TryGetValue(src.name, out key) || TdaMatTex.TryGetValue(src.name.Replace(" (Instance)", ""), out key))
        {
            Texture2D mapped;
            if (textures.TryGetValue(key, out mapped) && mapped) return mapped;
        }
        var tex = src.GetTexture("_BaseMap") as Texture2D;
        if (!tex) tex = src.mainTexture as Texture2D;
        if (tex) return tex;
        string mn = src.name;
        bool numeric = mn.Length > 0 && mn.Length <= 3; if (numeric) for (int ci = 0; ci < mn.Length; ci++) if (!char.IsDigit(mn[ci])) { numeric = false; break; }
        if (!numeric)
        {
            foreach (var kv in textures)
                if (mn.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (kv.Key.Length > 3 && kv.Key.IndexOf(mn, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    return kv.Value;
            string low = mn.ToLowerInvariant();
            if (low.Contains("hair") && textures.TryGetValue("hair_Mikuv4x.tga", out tex)) return tex;
            if ((low.Contains("face") || low.Contains("eye")) && textures.TryGetValue("face_Mikuv4x.tga", out tex)) return tex;
            if ((low.Contains("ap") || low.Contains("acc")) && textures.TryGetValue("body02_MikuAp.tga", out tex)) return tex;
        }
        textures.TryGetValue("mikuv4x_body00.tga", out tex);
        return tex;
    }

    static void AssignGenericUnlit(GameObject go, Shader shader, Dictionary<string, Texture2D> textures, string matFolder)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i]) continue;
                string matPath = matFolder + "/" + Sanitize(mats[i].name) + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = shader;
                Texture2D tex = ResolveTdaTexture(mats[i], textures);
                if (tex) mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                bool cutout = tex && (tex.name.ToLowerInvariant().Contains("hair") || tex.name.ToLowerInvariant().Contains("face") || tex.name.ToLowerInvariant().Contains("ap"));
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
                if (cutout)
                {
                    mat.SetFloat("_Cutoff", 0.35f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else
                {
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.renderQueue = (int)RenderQueue.Geometry;
                }
                EditorUtility.SetDirty(mat);
                if (mats[i] != mat) { mats[i] = mat; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
    }
}
#endif
