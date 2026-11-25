using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Naimportuje Mixamo FBX jako Humanoid (loop, bez root motion) a zkusí Humanoid avatar u DELTA/MIKU.
public static class MixamoSetup
{
    const string MixamoDir = "Assets/Animations/Mixamo";
    const string ControllerPath = "Assets/Resources/CrawlAnim.controller";
    const string ReportPath = "/tmp/mixamo-avatar-report.txt";

    static readonly string[] AnimPaths =
    {
        MixamoDir + "/anim1.fbx",
        MixamoDir + "/anim2.fbx",
        MixamoDir + "/anim3.fbx"
    };

    static readonly (string human, string vroid, string mmd)[] BoneMap =
    {
        ("Hips", "J_Bip_C_Hips", "腰"),
        ("Spine", "J_Bip_C_Spine", "上半身"),
        ("Chest", "J_Bip_C_Chest", "上半身2"),
        ("UpperChest", "J_Bip_C_UpperChest", "上半身2"),
        ("Neck", "J_Bip_C_Neck", "首"),
        ("Head", "J_Bip_C_Head", "頭"),
        ("LeftShoulder", "J_Bip_L_Shoulder", "左肩"),
        ("RightShoulder", "J_Bip_R_Shoulder", "右肩"),
        ("LeftUpperArm", "J_Bip_L_UpperArm", "左腕"),
        ("RightUpperArm", "J_Bip_R_UpperArm", "右腕"),
        ("LeftLowerArm", "J_Bip_L_LowerArm", "左ひじ"),
        ("RightLowerArm", "J_Bip_R_LowerArm", "右ひじ"),
        ("LeftHand", "J_Bip_L_Hand", "左手首"),
        ("RightHand", "J_Bip_R_Hand", "右手首"),
        ("LeftUpperLeg", "J_Bip_L_UpperLeg", "左足"),
        ("RightUpperLeg", "J_Bip_R_UpperLeg", "右足"),
        ("LeftLowerLeg", "J_Bip_L_LowerLeg", "左ひざ"),
        ("RightLowerLeg", "J_Bip_R_LowerLeg", "右ひざ"),
        ("LeftFoot", "J_Bip_L_Foot", "左足首"),
        ("RightFoot", "J_Bip_R_Foot", "右足首"),
        ("LeftToes", "J_Bip_L_ToeBase", "左つま先"),
        ("RightToes", "J_Bip_R_ToeBase", "右つま先"),
    };

    public static void Ensure()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();
        foreach (var p in AnimPaths) ConfigureClip(p, log);
        ConfigureCharacter("Assets/Models/DeltaGirl/HairSample_Female.fbx", false, log);
        ConfigureCharacter("Assets/Models/TdaMiku/TdaMiku.fbx", true, log);
        BuildController(log);
        File.WriteAllText(ReportPath, log.ToString());
        Debug.Log("[Snake3D] MixamoSetup:\n" + log);
        AssetDatabase.SaveAssets();
    }

    static void ConfigureClip(string path, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine("MISSING " + path); return; }
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (!importer) { log.AppendLine("NO IMPORTER " + path); return; }
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        var src = importer.defaultClipAnimations;
        if (src == null || src.Length == 0) src = importer.clipAnimations;
        if (src == null || src.Length == 0)
        {
            log.AppendLine("NO CLIPS " + path);
            importer.SaveAndReimport();
            return;
        }
        var clips = new ModelImporterClipAnimation[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            var c = src[i];
            c.loopTime = true;
            c.loopPose = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.heightFromFeet = false;
            c.lockRootPositionXZ = true;
            c.keepOriginalPositionXZ = true;
            clips[i] = c;
            log.AppendLine($"CLIP {path} name={c.name} take={c.takeName} {c.firstFrame}-{c.lastFrame}");
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var av = AssetDatabase.LoadAssetAtPath<Avatar>(path);
        log.AppendLine($"  avatar valid={(av && av.isValid)} human={(av && av.isHuman)}");
    }

    static void ConfigureCharacter(string path, bool mmd, StringBuilder log)
    {
        if (!File.Exists(path)) { log.AppendLine("MISSING " + path); return; }
        var names = CollectBoneNames(path);
        log.AppendLine($"CHARACTER {path} bones={names.Count} mmd={mmd}");

        var human = new List<HumanBone>();
        var usedModel = new HashSet<string>();
        foreach (var (humanName, vroid, mmdName) in BoneMap)
        {
            string bone = mmd ? FindName(names, mmdName, vroid) : FindName(names, vroid, mmdName);
            if (string.IsNullOrEmpty(bone)) { log.AppendLine("  UNMAPPED " + humanName); continue; }
            if (usedModel.Contains(bone) && (humanName == "UpperChest" || humanName == "Chest"))
            {
                log.AppendLine("  SKIP duplicate " + humanName + " -> " + bone);
                continue;
            }
            usedModel.Add(bone);
            human.Add(new HumanBone
            {
                humanName = humanName,
                boneName = bone,
                limit = new HumanLimit { useDefaultValues = true }
            });
            log.AppendLine("  MAP " + humanName + " -> " + bone);
        }
        if (human.Count < 10)
        {
            log.AppendLine("  FAIL too few mapped bones (" + human.Count + ")");
            return;
        }

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!src) { log.AppendLine("  NO GameObject at path"); return; }
        var inst = UnityEngine.Object.Instantiate(src);
        inst.name = src.name;
        try
        {
            var skeleton = new List<SkeletonBone>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                skeleton.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }
            var hd = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };
            var av = AvatarBuilder.BuildHumanAvatar(inst, hd);
            bool ok = av && av.isValid && av.isHuman;
            log.AppendLine($"  BUILDER isValid={(av && av.isValid)} isHuman={(av && av.isHuman)}");
            string dest = mmd ? "Assets/Resources/MikuHuman.asset" : "Assets/Resources/DeltaHuman.asset";
            if (ok)
            {
                av.name = mmd ? "MikuHuman" : "DeltaHuman";
                var existing = AssetDatabase.LoadAssetAtPath<Avatar>(dest);
                if (existing) AssetDatabase.DeleteAsset(dest);
                AssetDatabase.CreateAsset(av, dest);
                log.AppendLine("  SAVED " + dest);
            }
            else
            {
                log.AppendLine("  BUILDER failed – Mixamo retarget na tuto postavu nepůjde");
                if (av) UnityEngine.Object.DestroyImmediate(av);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    static string FindName(HashSet<string> names, params string[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrEmpty(c) && names.Contains(c)) return c;
        return null;
    }

    static HashSet<string> CollectBoneNames(string path)
    {
        var set = new HashSet<string>();
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var o in all)
        {
            if (o is GameObject go) set.Add(go.name);
            else if (o is Transform t) set.Add(t.name);
        }
        // also walk prefab instance hierarchy
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                set.Add(t.name);
        return set;
    }

    static void BuildController(StringBuilder log)
    {
        Directory.CreateDirectory("Assets/Resources");
        var clips = new AnimationClip[3];
        for (int i = 0; i < 3; i++)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AnimPaths[i]))
                if (a is AnimationClip c && !c.name.StartsWith("__preview"))
                { clips[i] = c; break; }
            log.AppendLine($"CTRL clip{i+1}={(clips[i] ? clips[i].name + " len=" + clips[i].length : "NULL")}");
        }
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        string[] names = { "Mixamo1", "Mixamo2", "Mixamo3" };
        AnimatorState first = null;
        for (int i = 0; i < 3; i++)
        {
            var st = sm.AddState(names[i]);
            st.motion = clips[i];
            st.speed = 1f;
            if (first == null) first = st;
        }
        if (first) sm.defaultState = first;
        EditorUtility.SetDirty(ctrl);
        log.AppendLine("CONTROLLER " + ControllerPath);
    }
}
