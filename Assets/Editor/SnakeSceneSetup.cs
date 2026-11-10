#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

// Automaticky postaví celou scénu Snake 3D v NEONOVÉM stylu.
// Menu: Tools > Snake 3D > Vytvořit scénu. Při prvním otevření projektu se spustí samo.
// Co dělá:
//  1) nastaví URP (Universal Render Pipeline) – modernější grafika Unity s post-processingem,
//  2) vytvoří svítící (emisní) materiály, texturu mřížky a efekty (bloom, vinětace...),
//  3) postaví arénu, hada, jídlo, překážky, kameru a UI.
[InitializeOnLoad]
public static class SnakeSceneSetup
{
    const string ScenePath = "Assets/Scenes/Snake3D.unity";
    const string TmpEssentials = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";

    // Neonové barvy, které používáme všude
    static readonly Color Cyan = new Color(0.1f, 0.9f, 1f);
    static readonly Color Magenta = new Color(1f, 0.15f, 0.85f);
    static readonly Color Danger = new Color(1f, 0.25f, 0.05f);

    static SnakeSceneSetup()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    static void AutoBuild()
    {
        if (File.Exists(ScenePath) || SessionState.GetBool("Snake3D_AutoBuilt", false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoBuild; return; }
        SessionState.SetBool("Snake3D_AutoBuilt", true);
        Debug.Log("[Snake3D] Scéna neexistuje – vytvářím ji automaticky.");
        BuildScene();
    }

    static void EnsureTmp()
    {
        if (!Directory.Exists("Assets/TextMesh Pro") && File.Exists(Path.GetFullPath(TmpEssentials)))
        {
            AssetDatabase.ImportPackage(TmpEssentials, false);
            AssetDatabase.Refresh();
        }
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder("Assets", path.Substring(7));
    }

    // ---------- 1) URP: vytvoří nastavení grafiky a zapne ho ----------
    [MenuItem("Tools/Snake 3D/Nastavit URP grafiku")]
    public static void SetupURP()
    {
        EnsureFolder("Assets/Settings");
        const string rendererPath = "Assets/Settings/Snake_URP_Renderer.asset";
        const string assetPath = "Assets/Settings/Snake_URP.asset";

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }
        // data pro post-processing (bloom atd.) – bez nich by efekty nefungovaly
        if (rendererData.postProcessData == null)
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        EditorUtility.SetDirty(rendererData);

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (urp == null)
        {
            urp = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urp, assetPath);
        }
        urp.supportsHDR = true;        // HDR = barvy můžou být jasnější než bílá -> bloom pak hezky září
        urp.msaaSampleCount = 4;       // vyhlazení hran
        urp.shadowDistance = 60f;
        EditorUtility.SetDirty(urp);

        // zapneme URP pro celý projekt i pro všechny úrovně kvality
        GraphicsSettings.defaultRenderPipeline = urp;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urp;
        }
        QualitySettings.SetQualityLevel(current, false);
        PlayerSettings.colorSpace = ColorSpace.Linear; // lineární barvy = realističtější světlo a záře
        AssetDatabase.SaveAssets();
    }

    // ---------- 2) Post-processing: bloom, tonemapping, vinětace ----------
    static VolumeProfile CreateVolumeProfile()
    {
        const string path = "Assets/Settings/NeonVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path); // vždy vytvoříme znovu, ať je nastavení čerstvé
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);          // BLOOM = svítící věci kolem sebe "září"
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(1.0f);                // co je jasnější než tohle, to září
        bloom.scatter.Override(0.75f);                 // jak daleko se záře rozlévá
        bloom.highQualityFiltering.Override(true);

        var tone = profile.Add<Tonemapping>(true);     // TONEMAPPING = "filmový" převod jasu na obrazovku
        tone.mode.Override(TonemappingMode.ACES);

        var vignette = profile.Add<Vignette>(true);    // VINĚTACE = ztmavení okrajů obrazu
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);

        var color = profile.Add<ColorAdjustments>(true); // úprava barev: kontrast a sytost
        color.postExposure.Override(0.4f);
        color.contrast.Override(18f);
        color.saturation.Override(20f);

        var ca = profile.Add<ChromaticAberration>(true); // jemné "rozostření barev" na okrajích
        ca.intensity.Override(0.15f);

        foreach (var c in profile.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    [MenuItem("Tools/Snake 3D/Vytvořit scénu")]
    public static void BuildScene()
    {
        EnsureTmp();
        SetupURP();
        foreach (var d in new[] { "Assets/Prefabs", "Assets/Materials", "Assets/Scenes", "Assets/Textures" }) EnsureFolder(d);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Textury (vytvoříme je kódem)
        var gridTex = GridTexture("Assets/Textures/NeonGrid.png", 128, 3f);
        var dotTex = DotTexture("Assets/Textures/SoftDot.png", 64);

        // Materiály: základní barva tmavá, svítí "emission" (záře)
        var matGround = Mat("Ground", new Color(0.02f, 0.02f, 0.05f), Cyan, 1.4f, 0.85f);
        matGround.SetTexture("_EmissionMap", gridTex);
        matGround.SetTextureScale("_BaseMap", new Vector2(20, 20));
        var matOuter = Mat("GroundOuter", new Color(0.01f, 0.01f, 0.03f), Magenta, 0.5f, 0.6f);
        matOuter.SetTexture("_EmissionMap", gridTex);
        matOuter.SetTextureScale("_BaseMap", new Vector2(100, 100));
        var matWallC = Mat("Wall", Cyan * 0.3f, Cyan, 3f);
        var matWallM = Mat("WallMagenta", Magenta * 0.3f, Magenta, 3f);
        var matHead = Mat("SnakeHead", new Color(0.1f, 0.5f, 0.3f), new Color(0.2f, 1f, 0.5f), 0.9f, 0.9f);   // slabší záře, ať jsou vidět oči
        var matBody = Mat("SnakeBody", new Color(0.05f, 0.3f, 0.2f), new Color(0.1f, 1f, 0.6f), 2f, 0.9f);
        // hadí šupiny: textura vyrobená kódem (světlé "rybí šupiny" s tmavými okraji)
        var scaleTex = ScaleTexture("Assets/Textures/SnakeScales.png", 128);
        matBody.SetTexture("_BaseMap", scaleTex); matBody.SetTextureScale("_BaseMap", new Vector2(4, 2));
        matBody.SetTexture("_EmissionMap", scaleTex); matBody.SetTextureScale("_EmissionMap", new Vector2(4, 2));
        matHead.SetTexture("_BaseMap", scaleTex); matHead.SetTextureScale("_BaseMap", new Vector2(6, 3));   // šupiny i na hlavě
        var matEye = Mat("SnakeEye", Color.white, new Color(1f, 1f, 0.7f), 1.5f);
        var matObst = Mat("Obstacle", new Color(0.3f, 0.05f, 0.02f), Danger, 2.5f, 0.6f);
        var matParticle = ParticleMat("NeonParticle", dotTex);

        // Aréna
        var arenaGo = new GameObject("Arena");
        var arena = arenaGo.AddComponent<GridArena>();
        int w = arena.width, h = arena.height;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground"; ground.transform.SetParent(arenaGo.transform);
        ground.transform.localScale = new Vector3(w, 0.2f, h);
        ground.transform.position = new Vector3(0, -0.1f, 0);
        ground.GetComponent<Renderer>().sharedMaterial = matGround;
        // velká podlaha kolem arény (do dálky mizí v mlze)
        var outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
        outer.name = "OuterFloor"; outer.transform.SetParent(arenaGo.transform);
        outer.transform.localScale = new Vector3(20, 1, 20);
        outer.transform.position = new Vector3(0, -0.6f, 0);
        outer.GetComponent<Renderer>().sharedMaterial = matOuter;
        Object.DestroyImmediate(outer.GetComponent<Collider>());
        // svítící okraje (tenké neonové tyče)
        float t = 0.3f;
        var wallsA = new[] {
            Wall(arenaGo, matWallC, new Vector3(0, 0.25f, h / 2f + t / 2), new Vector3(w + 2 * t, 0.5f, t)),
            Wall(arenaGo, matWallC, new Vector3(0, 0.25f, -h / 2f - t / 2), new Vector3(w + 2 * t, 0.5f, t)) };
        var wallsB = new[] {
            Wall(arenaGo, matWallM, new Vector3(w / 2f + t / 2, 0.25f, 0), new Vector3(t, 0.5f, h)),
            Wall(arenaGo, matWallM, new Vector3(-w / 2f - t / 2, 0.25f, 0), new Vector3(t, 0.5f, h)) };

        // Had – hlava je zploštělá koule se svítícíma očima
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "SnakeHead";
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = matHead;
        head.transform.localScale = new Vector3(0.95f, 0.8f, 1.05f);
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye"; Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(head.transform, false);
            eye.transform.localPosition = new Vector3(0.25f * side, 0.28f, 0.3f);
            eye.transform.localScale = Vector3.one * 0.2f;
            eye.GetComponent<Renderer>().sharedMaterial = matEye;
            // zornička: malá černá kulička vpředu na oku
            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil"; Object.DestroyImmediate(pupil.GetComponent<Collider>());
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0, 0.05f, 0.38f);
            pupil.transform.localScale = Vector3.one * 0.5f;
            pupil.GetComponent<Renderer>().sharedMaterial = Mat("SnakePupil", Color.black, Color.black, 0f, 0.95f);
        }
        // nozdry/čumák: menší zploštělá koule vpředu na hlavě
        var snout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        snout.name = "Snout"; Object.DestroyImmediate(snout.GetComponent<Collider>());
        snout.transform.SetParent(head.transform, false);
        snout.transform.localPosition = new Vector3(0, -0.08f, 0.4f);
        snout.transform.localScale = new Vector3(0.7f, 0.5f, 0.45f);
        snout.GetComponent<Renderer>().sharedMaterial = matHead;
        // nozdry: dvě malé tmavé tečky na čumáku
        foreach (float side in new[] { -1f, 1f })
        {
            var n = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            n.name = "Nostril"; Object.DestroyImmediate(n.GetComponent<Collider>());
            n.transform.SetParent(snout.transform, false);
            n.transform.localPosition = new Vector3(0.18f * side, 0.3f, 0.38f);
            n.transform.localScale = Vector3.one * 0.14f;
            n.GetComponent<Renderer>().sharedMaterial = Mat("SnakePupil", Color.black, Color.black, 0f, 0.95f);
        }
        // obočí: tmavé ploché kvádříky nad očima – had vypadá "naštvaně"
        foreach (float side in new[] { -1f, 1f })
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Brow"; Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(head.transform, false);
            b.transform.localPosition = new Vector3(0.25f * side, 0.42f, 0.3f);
            b.transform.localRotation = Quaternion.Euler(0, 0, -20f * side);
            b.transform.localScale = new Vector3(0.24f, 0.05f, 0.12f);
            b.GetComponent<Renderer>().sharedMaterial = Mat("SnakeBrow", new Color(0.02f, 0.1f, 0.06f), Color.black, 0f, 0.5f);
        }
        // rozeklaný jazyk: kořen + dvě špičky do V; skript Tongue ho občas vystrčí
        var tonguePivot = new GameObject("Tongue");
        tonguePivot.transform.SetParent(head.transform, false);
        tonguePivot.transform.localPosition = new Vector3(0, -0.15f, 0.55f);
        var matTongue = Mat("SnakeTongue", new Color(1f, 0.1f, 0.3f), new Color(1f, 0.1f, 0.3f), 2f);
        void TonguePart(Vector3 pos, Vector3 scale, float yaw)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "TonguePart"; Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(tonguePivot.transform, false);
            p.transform.localPosition = pos; p.transform.localRotation = Quaternion.Euler(0, yaw, 0); p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = matTongue;
        }
        TonguePart(new Vector3(0, 0, 0.25f), new Vector3(0.06f, 0.03f, 0.5f), 0);
        TonguePart(new Vector3(-0.05f, 0, 0.55f), new Vector3(0.04f, 0.03f, 0.18f), -30);
        TonguePart(new Vector3(0.05f, 0, 0.55f), new Vector3(0.04f, 0.03f, 0.18f), 30);
        tonguePivot.AddComponent<Tongue>();
        var snake = head.AddComponent<SnakeController>();
        // Šipka k nejbližšímu jídlu: dvě tenké svítící tyčky do tvaru ">" (z primitiv)
        var arrow = new GameObject("FoodArrow");
        var matArrow = Mat("FoodArrow", new Color(1f, 0.9f, 0.4f), new Color(1f, 0.85f, 0.3f), 3f);
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm"; Object.DestroyImmediate(arm.GetComponent<Collider>());
            arm.transform.SetParent(arrow.transform, false);
            arm.transform.localPosition = new Vector3(0.17f * side, 0, 0.1f);
            arm.transform.localRotation = Quaternion.Euler(0, -45f * side, 0);
            arm.transform.localScale = new Vector3(0.1f, 0.06f, 0.5f);
            arm.GetComponent<Renderer>().sharedMaterial = matArrow;
        }
        snake.arena = arena; snake.bodyMaterial = matBody;
        // svítící stopa za hlavou, když je had zrychlený
        var trailGo = new GameObject("BoostTrail");
        trailGo.transform.SetParent(head.transform, false);
        var trail = trailGo.AddComponent<TrailRenderer>();
        trailGo.transform.localPosition = new Vector3(0, 0.45f, 0); // kousek nad tělem, ať ji tělo nezakryje
        trail.time = 0.9f; trail.minVertexDistance = 0.1f;
        trail.widthMultiplier = 1f;
        trail.widthCurve = AnimationCurve.Linear(0, 1.0f, 1, 0.05f);   // široká u hlavy, úzká na konci
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.alignment = LineAlignment.View;
        var tg = new Gradient();
        tg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1) },
                   new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1) });
        trail.colorGradient = tg;
        trail.sharedMaterial = TrailMat("NeonTrail");
        trail.numCapVertices = 4;
        trail.emitting = false;
        snake.boostTrail = trail;

        // Prefaby jídla – každý druh má jiný tvar a svítí svou barvou
        var types = new[]
        {
            new FoodType{kind=FoodKind.Normal, name="Jablko",   color=new Color(1f,0.15f,0.3f),   points=10, grow=1,  bonusTime=0,  spawnWeight=5},
            new FoodType{kind=FoodKind.Gold,   name="Zlaté",    color=new Color(1f,0.8f,0.1f),    points=30, grow=2,  bonusTime=0,  spawnWeight=1},
            new FoodType{kind=FoodKind.Speed,  name="Rychlost", color=new Color(0.2f,0.5f,1f),    points=15, grow=1,  bonusTime=0,  spawnWeight=1.5f},
            new FoodType{kind=FoodKind.Time,   name="Čas",      color=new Color(0.2f,1f,0.85f),   points=5,  grow=0,  bonusTime=10, spawnWeight=1.5f},
            new FoodType{kind=FoodKind.Shrink, name="Zmenšení", color=new Color(0.75f,0.25f,1f),  points=5,  grow=-2, bonusTime=0,  spawnWeight=1},
            new FoodType{kind=FoodKind.Shield, name="Štít",     color=new Color(1f,0.45f,0.75f),  points=10, grow=0,  bonusTime=0,  spawnWeight=0.8f},
            new FoodType{kind=FoodKind.Magnet, name="Magnet",   color=new Color(1f,0.05f,0.12f),   points=10, grow=0,  bonusTime=0,  spawnWeight=0.8f},
        };
        var prefabs = new Food[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            var ft = types[i];
            var go = new GameObject("Food_" + ft.kind);
            // tvar podle druhu: koule, mince, kapsle, diamant (natočená kostka)
            PrimitiveType shape = ft.kind == FoodKind.Normal ? PrimitiveType.Sphere
                                : ft.kind == FoodKind.Gold ? PrimitiveType.Cylinder
                                : ft.kind == FoodKind.Time ? PrimitiveType.Cube : PrimitiveType.Capsule;
            // 3D model z balíčku Kenney (CC0). Když model chybí, použije se jednoduchý tvar jako dřív.
            string modelPath = FoodModel(ft.kind);
            GameObject model;
            if (ft.kind == FoodKind.Magnet)
                model = MagnetModel(ft.color);   // magnet si poskládáme z kostek (žádný CC0 model jsme neměli)
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) != null)
            {
                model = ModelInstance(modelPath, KenneyMat("Kenney_Food_" + ft.kind, "Assets/Models/KenneyFoodKit/Textures/colormap.png", ft.color, 0.4f), 1.1f, out _);
                if (ft.kind == FoodKind.Gold || ft.kind == FoodKind.Shield)   // hvězda a srdce jsou z jiného balíčku (jiná textura)
                    foreach (var r in model.GetComponentsInChildren<Renderer>()) SetAllMats(r, KenneyMat("Kenney_Platformer_" + ft.kind, "Assets/Models/KenneyPlatformerKit/Textures/colormap.png", ft.color, 0.8f));
            }
            else
            {
                model = GameObject.CreatePrimitive(shape);
                Object.DestroyImmediate(model.GetComponent<Collider>());
                model.transform.localScale = Vector3.one * 0.6f;
                model.GetComponent<Renderer>().sharedMaterial = Mat("Food_" + ft.kind, ft.color, ft.color, 1.8f, 0.9f);
            }
            model.name = "Model";
            model.transform.SetParent(go.transform, false);
            // svítící kruh (aura) pod jídlem v barvě druhu – drží neonový styl
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "Halo"; Object.DestroyImmediate(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = new Vector3(0, -0.45f, 0);
            halo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            halo.transform.localScale = Vector3.one * 1.6f;
            var haloMat = ParticleMat("Halo_" + ft.kind, dotTex, "Universal Render Pipeline/Unlit"); // obyčejný mesh nemá barvy vrcholů -> Unlit, ne Particles
            haloMat.SetColor("_BaseColor", ft.color * 2.5f);
            halo.GetComponent<Renderer>().sharedMaterial = haloMat;
            halo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // malé barevné světlo – jídlo osvětlí podlahu kolem sebe
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            // světlo je nad modelem, ne uvnitř (světlo přímo u povrchu dělá chybné pixely)
            light.transform.localPosition = new Vector3(0, 1.3f, 0);
            light.type = LightType.Point; light.color = ft.color; light.range = 4f; light.intensity = 5f;
            light.shadows = LightShadows.None;
            go.AddComponent<Food>().type = ft;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Prefabs/Food_{ft.kind}.prefab");
            Object.DestroyImmediate(go);
            prefabs[i] = prefab.GetComponent<Food>();
        }

        // Překážky (svítí červenooranžově a "dýchají")
        var obstRoot = new GameObject("Obstacles");
        var obstacles = new[]
        {
            Obstacle(obstRoot, arena, matObst, "Obstacle_1", new Vector2Int(3, 15), new Vector2Int(16, 15), 2.5f, new Vector3(0.9f, 0.9f, 0.9f)),
            Obstacle(obstRoot, arena, matObst, "Obstacle_2", new Vector2Int(5, 4),  new Vector2Int(5, 12),  2f,   new Vector3(0.9f, 0.9f, 0.9f)),
            Obstacle(obstRoot, arena, matObst, "Obstacle_3", new Vector2Int(14, 3), new Vector2Int(14, 11), 3f,   new Vector3(0.9f, 0.9f, 1.9f)),  // dlouhá
            Obstacle(obstRoot, arena, matObst, "Obstacle_4", new Vector2Int(6, 12), new Vector2Int(9, 12),  2f,   new Vector3(0.9f, 0.9f, 0.9f)),
            Obstacle(obstRoot, arena, matObst, "Obstacle_5", new Vector2Int(1, 1),  new Vector2Int(1, 5),   2f,   new Vector3(0.9f, 0.9f, 0.9f)),
            Obstacle(obstRoot, arena, matObst, "Obstacle_6", new Vector2Int(2, 1),  new Vector2Int(2, 5),   2f,   new Vector3(0.9f, 0.9f, 0.9f)),
            Obstacle(obstRoot, arena, matObst, "Obstacle_7", new Vector2Int(3, 1),  new Vector2Int(3, 5),   2f,   new Vector3(0.9f, 0.9f, 0.9f)),
            Obstacle(obstRoot, arena, matObst, "Obstacle_8", new Vector2Int(4, 1),  new Vector2Int(4, 5),   2f,   new Vector3(0.9f, 0.9f, 0.9f)),
        };

        // GameManager + spawner + efekty
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var spGo = new GameObject("FoodSpawner");
        var sp = spGo.AddComponent<FoodSpawner>();
        sp.arena = arena; sp.snake = snake; sp.gameManager = gm; sp.foodPrefabs = prefabs;
        var fa = arrow.AddComponent<FoodArrow>();
        fa.snake = snake; fa.spawner = sp;
        snake.gameManager = gm;
        gm.arena = arena; gm.snake = snake; gm.spawner = sp; gm.obstacles = obstacles;
        var fx = new GameObject("NeonFX").AddComponent<NeonFX>();
        fx.particleMaterial = matParticle;
        gm.fx = fx;
        // Zvuky (WAV soubory vyrobí Tools/generate_audio.py)
        var snd = new GameObject("SoundFX").AddComponent<SoundFX>();
        snd.eatClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/eat.wav");
        snd.turboClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/turbo.wav");
        snd.crashClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/crash.wav");
        snd.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/music.wav");
        gm.sound = snd;

        // Kamera: tmavé pozadí, HDR a zapnutý post-processing
        var cam = Camera.main;
        cam.transform.position = new Vector3(0, 21, -17);
        cam.transform.rotation = Quaternion.Euler(52, 0, 0);
        cam.fieldOfView = 55;
        cam.backgroundColor = new Color(0.01f, 0.005f, 0.03f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = true;
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.stopNaN = true;   // pojistka: neplatné pixely (NaN) se nerozlijí bloomem přes celý obraz
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var rig = cam.gameObject.AddComponent<CameraRig>();
        rig.target = head.transform;
        gm.cameraRig = rig;

        // Světlo a mlha: slabé modravé světlo, ať vynikne záře
        var sun = Object.FindObjectOfType<Light>();
        gm.sunLight = sun;
        // lampa nad hlavou hada – zapne se jen v tmavých levelech
        var hl = new GameObject("HeadLight").AddComponent<Light>();
        hl.transform.SetParent(head.transform, false);
        hl.transform.localPosition = new Vector3(0, 2.5f, 1.5f);   // kousek před hlavou, ať ji nepřesvítí
        hl.type = LightType.Point; hl.range = 6f; hl.intensity = 9f; hl.color = new Color(1f, 0.95f, 0.8f);
        hl.shadows = LightShadows.None; hl.enabled = false;
        gm.headLight = hl;
        if (sun) { sun.intensity = 0.35f; sun.color = new Color(0.6f, 0.7f, 1f); sun.transform.rotation = Quaternion.Euler(55, -30, 0); sun.shadows = LightShadows.Soft; }
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.025f;
        RenderSettings.fogColor = new Color(0.02f, 0.0f, 0.05f);

        // Globální Volume = "filtr" přes celý obraz (bloom atd.)
        var volGo = new GameObject("PostProcessVolume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = CreateVolumeProfile();

        // Levely: každý má jiné barvy, rychlost a rozmístění překážek
        gm.groundRenderer = ground.GetComponent<Renderer>();
        gm.outerRenderer = outer.GetComponent<Renderer>();
        gm.wallsA = wallsA; gm.wallsB = wallsB;
        // KAMPAŇ: 10 levelů, každý má svůj úkol (cíl). Obtížnost roste pomalu.
        // Route(x1, y1, x2, y2, rychlost, velikost) – stejné body + rychlost 0 = stojící pila.
        // Překážka č. 3 (index 2) je dlouhá (2 pily). Goal(úkol, počet, druh jídla, bez turba, časový limit)
        gm.levels = new[]
        {
            Lvl("Neonové město", Cyan, Cyan, Magenta, Magenta, 0.17f, Goal(Objective.EatFood, 10), "Klidný začátek, pila stojí",
                Route(14, 14, 14, 14, 0f)),
            Lvl("Dvě pily", new Color(0.2f, 1f, 0.3f), new Color(0.5f, 1f, 0.1f), new Color(1f, 0.85f, 0.1f), new Color(0f, 0.8f, 0.5f), 0.16f, Goal(Objective.EatFood, 15), "Pár stojících pil",
                Route(5, 14, 5, 14, 0f), Route(15, 5, 15, 5, 0f), Route(14, 11, 14, 11, 0f)),
            Lvl("První pohyb", new Color(0.65f, 0.2f, 1f), new Color(1f, 0.45f, 0f), new Color(1f, 0.1f, 0.5f), new Color(1f, 0.1f, 0.1f), 0.155f, Goal(Objective.EatFood, 15), "Pila se pomalu hýbe",
                Route(3, 15, 16, 15, 1.2f), Route(5, 4, 5, 4, 0f)),
            Lvl("Zlatá hvězda", new Color(1f, 0.75f, 0.1f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.5f, 0f), new Color(0.8f, 0.5f, 0f), 0.15f, Goal(Objective.EatKind, 2, FoodKind.Gold), "Hledej zlaté hvězdy",
                Route(3, 15, 16, 15, 2f), Route(5, 4, 5, 12, 1.5f)),
            Lvl("Dlouhý had", new Color(0.1f, 0.5f, 1f), new Color(0.2f, 1f, 1f), new Color(0.3f, 0.4f, 1f), new Color(0.1f, 0.2f, 1f), 0.145f, Goal(Objective.ReachLength, 15), "Vyrůst! Pozor na houby",
                Route(2, 16, 17, 16, 2.5f), Route(4, 3, 4, 14, 2f), Route(15, 3, 15, 12, 2.5f)),
            Lvl("Bez turba", new Color(1f, 0.3f, 0.8f), new Color(1f, 0.5f, 1f), new Color(0.5f, 0.2f, 1f), new Color(0.7f, 0f, 0.6f), 0.14f, Goal(Objective.EatFood, 12, noTurbo: true), "Modré plechovky jsou zakázané",
                Route(1, 11, 18, 11, 3f), Route(13, 1, 13, 18, 3f), Route(1, 4, 18, 4, 2.5f)),
            Lvl("Combo mistr", new Color(0.4f, 1f, 1f), new Color(0.2f, 0.8f, 1f), new Color(1f, 1f, 1f), new Color(0f, 0.3f, 0.5f), 0.135f, Goal(Objective.Combo, 3), "Jez rychle za sebou: COMBO x3",
                Route(2, 17, 17, 17, 3f), Route(3, 2, 3, 15, 3f), Route(16, 4, 16, 15, 3f), Route(6, 12, 9, 12, 2f)),
            Lvl("Temnota", new Color(0.3f, 0.5f, 1f), new Color(0.3f, 0.3f, 1f), new Color(0.6f, 0.2f, 1f), new Color(0.1f, 0.1f, 0.4f), 0.135f, Goal(Objective.Survive, 45), "TMA! Přežij 45 sekund",
                dark: true, paths: new[] { Route(3, 14, 16, 14, 2.5f), Route(6, 3, 6, 12, 2.5f), Route(14, 3, 14, 11, 2.5f) }),
            Lvl("Obří pila", new Color(1f, 0.2f, 0.2f), new Color(1f, 0.3f, 0.1f), new Color(0.8f, 0f, 0.2f), new Color(0.6f, 0f, 0f), 0.13f, Goal(Objective.EatFood, 20, time: 75f), "BOSS: obří pila + časový limit",
                Route(3, 13, 16, 13, 1.8f, 2.4f), Route(2, 2, 17, 2, 3f), Route(17, 17, 2, 17, 3f)),
            Lvl("FINÁLE", new Color(1f, 1f, 1f), new Color(1f, 0.2f, 0.6f), new Color(0.2f, 1f, 1f), new Color(0.6f, 0.2f, 1f), 0.115f, Goal(Objective.EatFood, 25, time: 90f), "Všechno najednou. Hodně štěstí!",
                Route(3, 13, 16, 13, 2.2f, 2.4f), Route(1, 2, 18, 2, 4f), Route(18, 17, 1, 17, 4f), Route(2, 4, 2, 15, 3.5f),
                Route(14, 4, 14, 10, 3f), Route(17, 15, 17, 4, 3.5f), Route(6, 16, 11, 16, 2.5f), Route(8, 4, 12, 4, 3f)),
        };

        BuildUI(gm);
        BuildMinimap(gm, arena);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Snake3D] Scéna vytvořena: " + ScenePath + " – stiskni Play.");
    }

    // ---------- Minimapa: druhá kamera kouká shora a kreslí do textury ----------
    static void BuildMinimap(GameManager gm, GridArena arena)
    {
        // RenderTexture = "obrazovka", do které kreslí kamera místo monitoru
        string rtPath = "Assets/Textures/MinimapRT.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null) { rt = new RenderTexture(256, 256, 16); AssetDatabase.CreateAsset(rt, rtPath); }

        var camGo = new GameObject("MinimapCamera");
        camGo.transform.position = new Vector3(0, 12, 0);
        camGo.transform.rotation = Quaternion.Euler(90, 0, 0);   // dívá se kolmo dolů
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;                                  // bez perspektivy = jako mapa
        cam.orthographicSize = arena.width * arena.cellSize * 0.5f + 0.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0.02f, 1f);
        cam.targetTexture = rt;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 20f;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;                        // šetříme výkon
        data.renderShadows = false;
        cam.cullingMask = ~(1 << 2);   // nekreslí vrstvu 2 (plovoucí texty s body)

        // V UI: rámeček vpravo dole a v něm obrázek z kamery
        var canvas = Object.FindObjectOfType<Canvas>().transform;
        var frame = new GameObject("Minimap", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(canvas, false);
        var frt = frame.GetComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(1, 0);
        frt.anchoredPosition = new Vector2(-30, 30); frt.sizeDelta = new Vector2(250, 250);
        frame.GetComponent<Image>().color = new Color(0f, 1f, 1f, 0.8f);   // tyrkysový okraj
        var img = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(frame.transform, false);
        var irt = img.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(4, 4); irt.offsetMax = new Vector2(-4, -4);
        img.GetComponent<RawImage>().texture = rt;
        frame.transform.SetAsFirstSibling();   // ať ji překryjí panely Pauza / Konec hry
        gm.minimap = frame;
    }

    // ---------- UI: neonové texty, úvodní obrazovka, konec hry ----------
    static void BuildUI(GameManager gm)
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // materiály písma se září (glow) a obrysem
        var fontCyan = NeonFontMat("NeonText_Cyan", Cyan);
        var fontMagenta = NeonFontMat("NeonText_Magenta", Magenta);
        var fontPlain = NeonFontMat("NeonText_Plain", Color.black, false);  // bez záře, s tmavým obrysem – barvy zůstanou syté
        gm.popupFontMaterial = NeonFontMat("NeonText_Popup", new Color(0.05f, 0f, 0.1f), false);

        var ct = canvasGo.transform;
        gm.scoreText = Text(ct, "ScoreText", new Vector2(0, 1), new Vector2(40, -30), TextAlignmentOptions.TopLeft, 52, fontCyan);
        gm.timeText = Text(ct, "TimeText", new Vector2(1, 1), new Vector2(-40, -30), TextAlignmentOptions.TopRight, 52, fontMagenta);
        gm.infoText = Text(ct, "InfoText", new Vector2(0.5f, 1), new Vector2(0, -40), TextAlignmentOptions.Top, 40, fontCyan);
        gm.scoreText.rectTransform.sizeDelta = new Vector2(1100, 220);
        gm.timeText.rectTransform.sizeDelta = new Vector2(800, 160);
        // řádek s aktivními vylepšeními: materiál bez záře, aby barvy zůstaly syté
        gm.powerText = Text(ct, "PowerText", new Vector2(1, 1), new Vector2(-40, -150), TextAlignmentOptions.TopRight, 30, fontPlain);
        gm.powerText.rectTransform.sizeDelta = new Vector2(900, 60);
        // nápověda při první hře (dole uprostřed)
        gm.hintText = Text(ct, "HintText", new Vector2(0.5f, 0), new Vector2(0, 150), TextAlignmentOptions.Center, 36, fontPlain);
        gm.hintText.rectTransform.sizeDelta = new Vector2(1500, 140); gm.hintText.text = "";
        // velký nápis "NOVÝ REKORD!"
        gm.bannerText = Text(ct, "BannerText", new Vector2(0.5f, 0.5f), new Vector2(0, 260), TextAlignmentOptions.Center, 96, NeonFontMat("NeonText_Gold", new Color(1f, 0.75f, 0.1f)));
        gm.bannerText.rectTransform.sizeDelta = new Vector2(1700, 260); gm.bannerText.text = "";
        foreach (var x in new[] { gm.scoreText, gm.infoText }) x.gameObject.AddComponent<UIPunch>();

        // Úvodní obrazovka
        var title = Panel(ct, "TitlePanel", new Color(0.01f, 0f, 0.04f, 0.55f));
        var big = Text(title.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 140), TextAlignmentOptions.Center, 170, fontMagenta);
        big.rectTransform.sizeDelta = new Vector2(1600, 260); big.text = "3D SNAKE"; big.characterSpacing = 12;
        var sub = Text(title.transform, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0, 10), TextAlignmentOptions.Center, 40, fontCyan);
        sub.rectTransform.sizeDelta = new Vector2(1400, 80); sub.text = "NEON EDITION";
        sub.characterSpacing = 30;
        var press = Text(title.transform, "PressStart", new Vector2(0.5f, 0.5f), new Vector2(0, -150), TextAlignmentOptions.Center, 52, fontCyan);
        press.rectTransform.sizeDelta = new Vector2(1400, 90); press.text = "stiskni MEZERNÍK";
        var help = Text(title.transform, "Help", new Vector2(0.5f, 0.5f), new Vector2(0, -255), TextAlignmentOptions.Center, 32, fontCyan);
        help.rectTransform.sizeDelta = new Vector2(1600, 60);
        help.text = "Šipky / WASD = řízení";
        // legenda jídla: vlastní materiál bez záře, jinak by barvy vybledly do bíla
        var legend = Text(title.transform, "FoodLegend", new Vector2(0.5f, 0.5f), new Vector2(0, -315), TextAlignmentOptions.Center, 36, fontPlain);
        legend.rectTransform.sizeDelta = new Vector2(1700, 60);
        legend.text = "<color=#FF2050>● jablko</color>   <color=#FFC000>● zlaté</color>   <color=#3070FF>● turbo</color>   <color=#00FFC8>● +čas</color>   <color=#B030FF>● zmenšení</color>   <color=#FF70C0>● štít</color>   <color=#FF2040>● magnet</color>";
        var lvlSel = Text(title.transform, "LevelSelect", new Vector2(0.5f, 0.5f), new Vector2(0, -60), TextAlignmentOptions.Center, 44, fontMagenta);
        lvlSel.rectTransform.sizeDelta = new Vector2(1600, 110);
        gm.levelSelectText = lvlSel;
        gm.titlePanel = title; gm.pressStartText = press;

        // Velký nápis při postupu do dalšího levelu
        var lvlPanel = Panel(ct, "LevelPanel", new Color(0, 0, 0, 0.35f));
        var lvlText = Text(lvlPanel.transform, "LevelText", new Vector2(0.5f, 0.5f), new Vector2(0, 40), TextAlignmentOptions.Center, 150, fontMagenta);
        lvlText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        lvlText.rectTransform.sizeDelta = new Vector2(1600, 400);
        gm.levelPanel = lvlPanel; gm.levelText = lvlText;
        lvlPanel.SetActive(false);

        // Menu pauzy (Esc)
        var pause = Panel(ct, "PausePanel", new Color(0.01f, 0f, 0.04f, 0.75f));
        var pTitle = Text(pause.transform, "PauseTitle", new Vector2(0.5f, 0.5f), new Vector2(0, 200), TextAlignmentOptions.Center, 120, fontCyan);
        pTitle.rectTransform.sizeDelta = new Vector2(1200, 180); pTitle.text = "PAUZA";
        var pText = Text(pause.transform, "PauseText", new Vector2(0.5f, 0.5f), new Vector2(0, -60), TextAlignmentOptions.Center, 44, fontPlain);
        pText.rectTransform.sizeDelta = new Vector2(1500, 320);
        gm.pausePanel = pause; gm.pauseText = pText;
        pause.SetActive(false);

        // Konec hry: ztmavení + rámeček s neonovým okrajem
        var panel = Panel(ct, "GameOverPanel", new Color(0, 0, 0, 0.6f));
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Outline));
        box.transform.SetParent(panel.transform, false);
        var brt0 = box.GetComponent<RectTransform>();
        brt0.anchorMin = brt0.anchorMax = new Vector2(0.5f, 0.5f); brt0.sizeDelta = new Vector2(1500, 760);
        box.GetComponent<Image>().color = new Color(0.03f, 0.01f, 0.07f, 1f);
        var ol = box.GetComponent<Outline>(); ol.effectColor = Magenta; ol.effectDistance = new Vector2(5, -5);
        var goTitle = Text(box.transform, "GameOverTitle", new Vector2(0.5f, 0.5f), new Vector2(0, 280), TextAlignmentOptions.Center, 110, fontMagenta);
        goTitle.rectTransform.sizeDelta = new Vector2(1400, 160); goTitle.text = "KONEC HRY";
        var got = Text(box.transform, "GameOverText", new Vector2(0.5f, 0.5f), new Vector2(-340, 10), TextAlignmentOptions.Center, 44, fontCyan);
        got.rectTransform.sizeDelta = new Vector2(700, 420);
        // tabulka TOP 10 vpravo (písmo bez záře, ať jsou čísla čitelná)
        var hs = Text(box.transform, "HighScoreText", new Vector2(0.5f, 0.5f), new Vector2(360, 0), TextAlignmentOptions.TopLeft, 34, fontPlain);
        hs.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hs.rectTransform.sizeDelta = new Vector2(620, 460);
        gm.highScoreText = hs;
        gm.gameOverText = got; gm.gameOverPanel = panel;

        var btnGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        btnGo.transform.SetParent(box.transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0, -300); brt.sizeDelta = new Vector2(420, 100);
        btnGo.GetComponent<Image>().color = new Color(0.02f, 0.15f, 0.2f);
        var bol = btnGo.GetComponent<Outline>(); bol.effectColor = Cyan; bol.effectDistance = new Vector2(4, -4);
        var bt = Text(btnGo.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center, 46, fontCyan);
        bt.text = "HRÁT ZNOVU"; bt.rectTransform.sizeDelta = brt.sizeDelta;
        UnityEventTools.AddPersistentListener(btnGo.GetComponent<Button>().onClick, gm.RestartGame);
        panel.SetActive(false);
    }

    static GameObject Panel(Transform parent, string name, Color c)
    {
        var p = new GameObject(name, typeof(RectTransform), typeof(Image));
        p.transform.SetParent(parent, false);
        var rt = p.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        p.GetComponent<Image>().color = c;
        return p;
    }

    // Materiál URP/Lit: baseColor = barva povrchu, emission = záře (glow * síla).
    static Material Mat(string name, Color baseColor, Color glow, float glowPower, float smoothness = 0.5f)
    {
        string path = $"Assets/Materials/{name}.mat";
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;                                  // starý materiál (Standard) převedeme na URP
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_EMISSION");                   // zapne svícení
        m.SetColor("_EmissionColor", glow * glowPower); // HDR barva – víc než 1 = svítí přes bloom
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetTexture("_EmissionMap", null);
        m.SetTextureScale("_BaseMap", Vector2.one);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Průhledný "sčítací" (additive) materiál pro částice a stopu – překrývající se částice víc září.
    static Material ParticleMat(string name, Texture2D tex, string shaderName = "Universal Render Pipeline/Particles/Unlit")
    {
        string path = $"Assets/Materials/{name}.mat";
        var sh = Shader.Find(shaderName);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white * 2f);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);  // průhledný + additive
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Který model patří ke kterému jídlu (Kenney Food Kit + Platformer Kit, licence CC0).
    // Magnet ve tvaru písmene U: červené tělo ze 3 kvádrů + stříbrné konce.
    static GameObject MagnetModel(Color c)
    {
        var root = new GameObject("Magnet");
        var red = Mat("Magnet_Red", c * 0.45f, c, 0.7f, 0.6f);          // tmavší, ať ho světlo nevybělí
        var silver = Mat("Magnet_Silver", new Color(0.45f, 0.5f, 0.55f), new Color(0.6f, 0.8f, 1f), 0.5f, 0.9f);
        void Box(Vector3 pos, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = pos; b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = m;
        }
        Box(new Vector3(0, -0.28f, 0), new Vector3(0.75f, 0.22f, 0.25f), red);       // spodní oblouk
        Box(new Vector3(-0.27f, 0.02f, 0), new Vector3(0.22f, 0.45f, 0.25f), red);   // levé rameno
        Box(new Vector3(0.27f, 0.02f, 0), new Vector3(0.22f, 0.45f, 0.25f), red);    // pravé rameno
        Box(new Vector3(-0.27f, 0.33f, 0), new Vector3(0.22f, 0.18f, 0.25f), silver); // stříbrné konce
        Box(new Vector3(0.27f, 0.33f, 0), new Vector3(0.22f, 0.18f, 0.25f), silver);
        return root;
    }

    static string FoodModel(FoodKind k)
    {
        switch (k)
        {
            case FoodKind.Normal: return "Assets/Models/KenneyFoodKit/apple.fbx";       // jablko
            case FoodKind.Gold:   return "Assets/Models/KenneyPlatformerKit/star.fbx";  // zlatá hvězda
            case FoodKind.Speed:  return "Assets/Models/KenneyFoodKit/soda-can.fbx";    // energetický nápoj = turbo
            case FoodKind.Time:   return "Assets/Models/KenneyFoodKit/cup-coffee.fbx";  // káva = víc času
            case FoodKind.Shield: return "Assets/Models/KenneyPlatformerKit/heart.fbx"; // srdce = štít (nesmrtelnost)
            default:              return "Assets/Models/KenneyFoodKit/mushroom.fbx";    // houba = zmenšení (jako Alenka v říši divů)
        }
    }

    // Vloží 3D model a zmenší/zvětší ho tak, aby se vešel do krychle o hraně "size" a měl střed v nule.
    // "thinAxis" vrátí nejtenčí směr modelu (hodí se pro otáčení pily).
    static GameObject ModelInstance(string path, Material mat, float size, out Vector3 thinAxis)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        // Tangenty vypneme: modely Kenney mají celé plochy se stejnou UV souřadnicí (jedna barva z palety)
        // a výpočet tangent by z nich udělal neplatná čísla (NaN) -> na obrazovce bílé "výbuchy" bloomu.
        if (imp.importTangents != ModelImporterTangents.None)
        {
            imp.importNormals = ModelImporterNormals.Import;   // normály z modelu
            imp.importTangents = ModelImporterTangents.None;
            imp.materialImportMode = ModelImporterMaterialImportMode.None; // materiály dáváme vlastní
            imp.SaveAndReimport();
        }
        // textura Kenney je paleta barev: bez rozmazávání (Point) a bez mipmap, jinak na hranách barev vzniká šum
        string texPath = Path.GetDirectoryName(path).Replace('\\', '/') + "/Textures/colormap.png";
        if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (ti.filterMode != FilterMode.Point || ti.mipmapEnabled))
        { ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
        var inst = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        inst.transform.position = Vector3.zero;
        var rends = inst.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            SetAllMats(r, mat);
            // malé modely si samy na sebe vrhaly stín -> tmavé tečky ("shadow acne"). Stíny na nich vypneme.
            r.receiveShadows = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float s = size / Mathf.Max(b.size.x, b.size.y, b.size.z);
        thinAxis = b.size.x <= b.size.y && b.size.x <= b.size.z ? Vector3.right : b.size.y <= b.size.z ? Vector3.up : Vector3.forward;
        var root = new GameObject("Model");
        inst.transform.SetParent(root.transform, false);
        inst.transform.localScale *= s;
        inst.transform.localPosition = -b.center * s;
        return root;
    }

    static void SetAllMats(Renderer r, Material m)
    {
        var mats = new Material[r.sharedMaterials.Length];
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        r.sharedMaterials = mats;
    }

    // Materiál pro modely Kenney: barevná textura (colormap) + záře. Model svítí svými barvami,
    // trochu přibarvenými barvou "tint" – tak zapadne do neonového stylu.
    static Material KenneyMat(string name, string texPath, Color tint, float glow)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var m = Mat(name, Color.white, Color.Lerp(Color.white, tint, 0.35f), glow, 0.6f);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_EmissionMap", tex);   // svítí podle barev textury
        EditorUtility.SetDirty(m);
        return m;
    }

    // Materiál pro svítící stopu turba: additive (sčítací), bez textury, HDR barva = silná záře v bloomu.
    static Material TrailMat(string name)
    {
        var m = ParticleMat(name, null);
        m.SetColor("_BaseColor", new Color(3f, 2.2f, 0.8f, 1f)); // víc než 1 = září
        EditorUtility.SetDirty(m);
        return m;
    }

    // Vyrobí texturu jednoho políčka mřížky: černo, na okrajích svítící čára.
    static Texture2D GridTexture(string path, int size, float line)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)); // vzdálenost od okraje
                float v = Mathf.Clamp01(1f - d / line);                 // ostrá čára
                v = Mathf.Max(v, 0.25f * Mathf.Exp(-d / (line * 3f)));  // + jemná záře kolem
                tex.SetPixel(x, y, new Color(v, v, v, 1));
            }
        return SaveTexture(tex, path);
    }

    // Měkká kulatá tečka pro částice.
    // Šupiny: každá šupina je půlkruh; uprostřed světlá, na okraji tmavá.
    static Texture2D ScaleTexture(string path, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        int cells = 4; float cs = size / (float)cells;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float best = 9f;
                for (int row = -1; row <= cells; row++)
                    for (int col = -1; col <= cells; col++)
                    {
                        float cx = (col + (row % 2 == 0 ? 0f : 0.5f)) * cs, cy = row * cs;   // každá druhá řada posunutá
                        float dx = Mathf.Repeat(x - cx + size / 2f, size) - size / 2f, dy = y - cy;
                        if (dy < 0) continue;                                              // jen horní půlka = šupina
                        best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy) / cs);
                    }
                float v = best > 1f ? 0.35f : Mathf.Lerp(1f, 0.45f, Mathf.Pow(best, 3f));
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        tex.Apply();
        return SaveTexture(tex, path);
    }

    static Texture2D DotTexture(string path, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d); a *= a;
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        return SaveTexture(tex, path);
    }

    static Texture2D SaveTexture(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 8; imp.mipmapEnabled = true;
        imp.filterMode = FilterMode.Trilinear; imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Materiál písma TextMesh Pro se září a obrysem (neonový text).
    static Material NeonFontMat(string name, Color glow, bool useGlow = true)
    {
        string path = $"Assets/Materials/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        ShaderUtilities.GetShaderPropertyIDs();          // načte jména vlastností shaderu TMP
        var font = TMP_Settings.defaultFontAsset;
        var m = new Material(font.material);
        m.shader = Shader.Find("TextMeshPro/Distance Field"); // plná verze shaderu umí i záři (glow)
        m.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
        m.SetColor(ShaderUtilities.ID_OutlineColor, glow);
        if (useGlow) m.EnableKeyword(ShaderUtilities.Keyword_Glow);   // zapne záři kolem písmen
        else { m.DisableKeyword(ShaderUtilities.Keyword_Glow); m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f); }
        m.SetColor(ShaderUtilities.ID_GlowColor, new Color(glow.r, glow.g, glow.b, 0.8f));
        m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
        m.SetFloat(ShaderUtilities.ID_GlowPower, 0.6f);
        ShaderUtilities.UpdateShaderRatios(m);           // přepočítá velikosti obrysu a záře
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static ObstaclePath Route(int x1, int y1, int x2, int y2, float speed, float size = 1f) =>
        new ObstaclePath { from = new Vector2Int(x1, y1), to = new Vector2Int(x2, y2), speed = speed, size = size };

    // Pomocník pro zápis levelu na jeden řádek: jméno, barvy (mřížka, okraje A, okraje B, okolí), rychlost hada, body na další level, popis, pily.
    static LevelData Lvl(string name, Color grid, Color wallA, Color wallB, Color outer, float step, LevelData goal, string hint, params ObstaclePath[] paths) =>
        Lvl(name, grid, wallA, wallB, outer, step, goal, hint, false, paths);
    static LevelData Lvl(string name, Color grid, Color wallA, Color wallB, Color outer, float step, LevelData goal, string hint, bool dark, ObstaclePath[] paths)
    {
        var L = goal;   // úkol už je v objektu, doplníme zbytek
        L.name = name; L.gridColor = grid; L.wallColorA = wallA; L.wallColorB = wallB; L.outerColor = outer;
        L.fogColor = outer * 0.05f; L.stepInterval = step; L.hint = hint; L.dark = dark; L.obstacles = paths;
        return L;
    }

    // Úkol levelu: co, kolik, (druh jídla), (zákaz turba), (časový limit v s, 0 = bez limitu)
    static LevelData Goal(Objective o, int target, FoodKind kind = FoodKind.Normal, bool noTurbo = false, float time = 0f) =>
        new LevelData { objective = o, target = target, foodKind = kind, noTurbo = noTurbo, timeLimit = time };

    static Renderer Wall(GameObject parent, Material m, Vector3 pos, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Wall"; g.transform.SetParent(parent.transform);
        g.transform.position = pos; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        return r;
    }

    static MovingObstacle Obstacle(GameObject root, GridArena arena, Material m, string name, Vector2Int a, Vector2Int b, float speed, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name; g.transform.SetParent(root.transform);
        g.transform.position = arena.CellToWorld(a, 0.45f); g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        // Vzhled: místo kostky kotoučové pily z balíčku Kenney Platformer Kit (CC0).
        // Kostka zůstává (podle její velikosti se počítá kolize), jen ji nekreslíme.
        const string sawPath = "Assets/Models/KenneyPlatformerKit/saw.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sawPath) != null)
        {
            g.GetComponent<Renderer>().enabled = false;
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(scale.x, scale.z)));   // dlouhá překážka = víc pil
            for (int k = 0; k < count; k++)
            {
                var saw = ModelInstance(sawPath, KenneyMat("Kenney_Saw", "Assets/Models/KenneyPlatformerKit/Textures/colormap.png", Danger, 1f), 1.25f, out Vector3 thin);
                var inner = saw.transform.GetChild(0);
                if (thin == Vector3.up)
                {
                    // pila leží naplocho -> postavíme ji na hranu (jako kotouč cirkulárky)
                    var holder = new GameObject("Blade").transform;
                    holder.SetParent(saw.transform, false);
                    inner.SetParent(holder, true);
                    holder.localRotation = Quaternion.Euler(90, 90, 0);
                }
                saw.name = "Saw";
                saw.transform.SetParent(g.transform, false);
                float off = (k - (count - 1) / 2f);
                saw.transform.localPosition = scale.z >= scale.x ? new Vector3(0, 0.2f / scale.y, off / scale.z) : new Vector3(off / scale.x, 0.2f / scale.y, 0);
                saw.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);  // vyrovná protažení kostky
                // pila se otáčí kolem své nejtenčí osy (jako skutečný kotouč)
                var spin = inner.gameObject.AddComponent<Spin>();
                spin.axis = inner.InverseTransformDirection(thin); spin.speed = 400f;
            }
            // červená svítící skvrna na podlaze pod překážkou = varování "sem nejezdi"
            var warn = GameObject.CreatePrimitive(PrimitiveType.Quad);
            warn.name = "DangerHalo"; Object.DestroyImmediate(warn.GetComponent<Collider>());
            warn.transform.SetParent(g.transform, false);
            warn.transform.localPosition = new Vector3(0, -0.45f / scale.y, 0);
            warn.transform.localRotation = Quaternion.Euler(90, 0, 0);
            warn.transform.localScale = new Vector3(1.8f / scale.x, 1.8f * Mathf.Max(1f, scale.z) / scale.z, 1f);
            var warnMat = ParticleMat("Halo_Danger", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/SoftDot.png"), "Universal Render Pipeline/Unlit");
            warnMat.SetColor("_BaseColor", Danger * 2.5f);
            warn.GetComponent<Renderer>().sharedMaterial = warnMat;
            var redLight = new GameObject("Glow").AddComponent<Light>();
            redLight.transform.SetParent(g.transform, false);
            redLight.transform.localPosition = new Vector3(0, 1.4f, 0);
            redLight.type = LightType.Point; redLight.color = Danger; redLight.range = 4f; redLight.intensity = 4f;
        }
        var pulse = g.AddComponent<NeonPulse>(); pulse.glowColor = Danger; // pulzující záře
        var wa = new GameObject(name + "_WP_A").transform; wa.SetParent(root.transform); wa.position = arena.CellToWorld(a);
        var wb = new GameObject(name + "_WP_B").transform; wb.SetParent(root.transform); wb.position = arena.CellToWorld(b);
        var mo = g.AddComponent<MovingObstacle>();
        mo.waypoints = new[] { wa, wb }; mo.speed = speed;
        return mo;
    }

    static TMP_Text Text(Transform parent, string name, Vector2 anchor, Vector2 pos, TextAlignmentOptions align, float size, Material fontMat)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.font = TMP_Settings.defaultFontAsset;
        if (fontMat) t.fontSharedMaterial = fontMat;
        t.fontSize = size; t.alignment = align; t.color = Color.white; t.text = "";
        t.fontStyle = FontStyles.Bold;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(700, 80);
        return t;
    }
}
#endif
