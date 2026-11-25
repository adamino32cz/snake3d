using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// DEMO REŽIM (nepovinný) – NENÍ součástí hlavní logiky hry!
// Zapne se jen, když hru spustíš z příkazové řádky s parametrem:
//   -autoplay      had jezdí sám: hledá nejkratší cestu k nejbližšímu jídlu (BFS)
//                  a vyhýbá se zdem, svému tělu a překážkám
//   -screenshots   uloží pár obrázků (PNG) do složky "Screenshots" vedle hry
//                  nebo do složky zadané jako -shotdir <cesta>
//   -openskins     otevře menu SKINY (s -screenshots udělá jen krátké fotky menu a hry)
//   -skin <id>     vybere vzhled (neon / fire / ice / gold / delta / miku) – nastaví GameManager
//   -shotfood <Kind>  s -screenshots: počká, až daný druh jídla bude na ploše, vyfotí a skončí
// Při normálním hraní se tento skript vůbec nespustí.
public class SnakeAutoplay : MonoBehaviour
{
    SnakeController snake;
    GameManager gm;
    FoodSpawner spawner;
    GridArena arena;
    bool steering = true;
    string shotDir;
    static bool pendingTitleShot;

    static bool HasArg(string a) => System.Array.IndexOf(CmdArgs.Get(), a) >= 0;

    // Unity tuhle metodu zavolá samo po načtení scény.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!HasArg("-autoplay") && !HasArg("-screenshots") && !HasArg("-openskins") && !HasArg("-shotfood")) return;
        Debug.Log("[Snake3D] Demo režim zapnut.");
        new GameObject("SnakeAutoplay").AddComponent<SnakeAutoplay>();
    }

    void Start()
    {
        snake = FindObjectOfType<SnakeController>();
        gm = FindObjectOfType<GameManager>();
        spawner = FindObjectOfType<FoodSpawner>();
        arena = FindObjectOfType<GridArena>();
        steering = HasArg("-autoplay");
        var args = CmdArgs.Get();
        int i = System.Array.IndexOf(args, "-shotdir");
        shotDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.dataPath, "..", "Screenshots");
        DeltaGirlCrawler.AllFours = false;
        if (pendingTitleShot && HasArg("-screenshots")) { StartCoroutine(ShotTitleAfterMenu()); return; }
        if (HasArg("-screenshots") && HasArg("-shotnormal")) { StartCoroutine(ShotNormalSkin()); return; }
        if (HasArg("-screenshots") && HasArg("-shotv10")) { StartCoroutine(ShotV10()); return; }
        if (HasArg("-screenshots") && HasArg("-shotmixamo")) { StartCoroutine(ShotMixamo()); return; }
        if (HasArg("-screenshots") && HasArg("-shotpolish")) { StartCoroutine(ShotPolish()); return; }
        if (HasArg("-screenshots") && HasArg("-shotgameover")) StartCoroutine(ShotGameOverMenu());
        else if (HasArg("-screenshots") && HasArg("-openskins")) StartCoroutine(ShotSkins());
        else if (HasArg("-screenshots") && ShotFoodKind() != null) StartCoroutine(ShotObjectiveFood());
        else if (HasArg("-screenshots") && IsCharacterSkin()) StartCoroutine(ShotCharacter());
        else if (HasArg("-screenshots")) { StartCoroutine(TakeShots()); StartCoroutine(WatchLevels()); }
        else if (HasArg("-openskins")) gm.OpenSkinMenu();
        else gm.BeginPlay(); // bez fotek rovnou hrajeme (přeskočí úvodní obrazovku)
    }


    IEnumerator ShotGameOverMenu()
    {
        Directory.CreateDirectory(shotDir);
        var delta = SkinCatalog.Get("delta");
        var miku = SkinCatalog.Get("miku");
        if (delta != null) SkinCatalog.Unlock(delta);
        if (miku != null) SkinCatalog.Unlock(miku);

        yield return new WaitForSeconds(0.35f);
        if (miku != null && SkinCatalog.Select("miku") && snake) snake.ApplySkin(SkinCatalog.Current());
        if (gm) gm.BeginPlay();
        if (snake) snake.Grow(12);
        yield return new WaitForSeconds((snake ? snake.stepInterval : 0.16f) * 6f + 0.25f);
        if (snake) snake.Stop();
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (rig) { rig.enabled = true; rig.SetView(CameraView.ThirdPerson); }
        if (snake) snake.ApplyCameraView(CameraView.ThirdPerson);
        yield return new WaitForSeconds(0.8f);
        Shot("03_third_miku.png");
        yield return new WaitForSeconds(0.2f);

        if (delta != null && SkinCatalog.Select("delta") && snake) snake.ApplySkin(SkinCatalog.Current());
        if (rig) { rig.SetView(CameraView.ThirdPerson); }
        if (snake) snake.ApplyCameraView(CameraView.ThirdPerson);
        yield return new WaitForSeconds(0.7f);
        Shot("04_third_delta.png");
        yield return new WaitForSeconds(0.2f);

        if (rig) rig.SetView(CameraView.Overview);
        if (snake) snake.ApplyCameraView(CameraView.Overview);
        if (gm) gm.GameOver("Narazil jsi!");
        yield return new WaitForSeconds(1.35f);
        Shot("01_gameover.png");
        yield return new WaitForSeconds(0.25f);
        pendingTitleShot = true;
        if (gm) gm.ReturnToMenu();
    }

    IEnumerator ShotTitleAfterMenu()
    {
        pendingTitleShot = false;
        Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(0.7f);
        Shot("02_title_after_menu.png");
        yield return new WaitForSeconds(0.2f);
        Application.Quit();
    }

    static string ShotFoodKind()
    {
        var a = CmdArgs.Get();
        int i = System.Array.IndexOf(a, "-shotfood");
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    Food FindFood(FoodKind kind)
    {
        if (spawner == null) return null;
        foreach (var f in spawner.ActiveFood)
        {
            if (!f) continue;
            if (f.type != null && f.type.kind == kind) return f;
            if (f.name.IndexOf(kind.ToString(), System.StringComparison.OrdinalIgnoreCase) >= 0) return f;
        }
        var kinds = new System.Text.StringBuilder();
        foreach (var f in spawner.ActiveFood)
            if (f) kinds.Append((f.type != null ? f.type.kind.ToString() : "?") + "(" + f.name + ") ");
        Debug.Log("[Snake3D] Jídlo na ploše: " + kinds);
        return null;
    }

    // Krátké fotky menu skinů + hry s jiným vzhledem, pak konec (bez dlouhého dema).
    IEnumerator ShotSkins()
    {
        Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(1.5f);
        Shot("00_title.png");
        yield return new WaitForSeconds(0.4f);
        gm.OpenSkinMenu();
        yield return new WaitForSeconds(0.5f);
        Shot("01_skins_menu.png");
        yield return new WaitForSeconds(0.4f);
        gm.TryUnlockCode("DELTA");
        gm.TryUnlockCode("MIKU");
        yield return new WaitForSeconds(0.4f);
        Shot("02_skins_delta_unlocked.png");
        yield return new WaitForSeconds(0.4f);
        SkinCatalog.Select("fire");
        if (snake) { snake.ApplySkin(SkinCatalog.Current()); snake.Stop(); }
        gm.CloseSkinMenu();
        yield return new WaitForSeconds(0.3f);
        gm.BeginPlay();
        yield return new WaitForSeconds(2.5f);
        Shot("03_play_ohniovy.png");
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    // Počká, až bude na ploše konkrétní druh jídla (třeba Gold), vyfotí ho a skončí.
    IEnumerator ShotObjectiveFood()
    {
        Directory.CreateDirectory(shotDir);
        string kindName = ShotFoodKind();
        FoodKind kind = (FoodKind)System.Enum.Parse(typeof(FoodKind), kindName, true);
        // počkáme, až GameManager.Start položí jídlo (první deltaTime umí být obří)
        yield return null;
        yield return null;
        if (spawner) spawner.EnsureObjectiveFood();
        if (!gm.IsPlaying) gm.BeginPlay();
        float t = 0f;
        Food found = null;
        while (t < 8f)
        {
            found = FindFood(kind);
            if (found) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (found)
        {
            // přiblížíme kameru k jídlu, ať je na fotce dobře vidět
            var cam = Camera.main; var rig = cam ? cam.GetComponent<CameraRig>() : null;
            Vector3 p = cam.transform.position; Quaternion r = cam.transform.rotation;
            if (rig) rig.enabled = false;
            cam.transform.position = found.transform.position + new Vector3(0, 2.2f, -3.2f);
            cam.transform.LookAt(found.transform.position);
            yield return new WaitForSeconds(0.4f);
            Shot($"00_level{gm.Level + 1}_{kindName.ToLower()}.png");
            yield return new WaitForSeconds(0.4f);
            cam.transform.SetPositionAndRotation(p, r);
            if (rig) rig.enabled = true;
            yield return new WaitForSeconds(0.4f);
            Shot($"01_level{gm.Level + 1}_{kindName.ToLower()}_play.png");
        }
        else
        {
            Debug.LogWarning("[Snake3D] Na ploše není jídlo druhu " + kindName);
            Shot($"00_level{gm.Level + 1}_{kindName.ToLower()}_MISSING.png");
        }
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    void Update()
    {
        if (steering && snake) snake.SetDirection(ChooseDirection());
    }

    // Je buňka volná (uvnitř arény, bez těla a překážky)?
    bool Free(Vector2Int c, HashSet<Vector2Int> body) =>
        arena.Inside(c) && !body.Contains(c) && !gm.IsObstacleCell(c);

    // Je vedle buňky překážka? Překážky se hýbou, takže se jim demo raději vyhne o políčko.
    bool NearObstacle(Vector2Int c) =>
        gm.IsObstacleCell(c + Vector2Int.up) || gm.IsObstacleCell(c + Vector2Int.down) ||
        gm.IsObstacleCell(c + Vector2Int.left) || gm.IsObstacleCell(c + Vector2Int.right);

    Vector2Int ChooseDirection()
    {
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var cells = snake.Cells;
        var head = cells[0];
        var body = new HashSet<Vector2Int>(cells);
        body.Remove(cells[cells.Count - 1]); // ocas se v dalším kroku posune

        var food = new HashSet<Vector2Int>();
        foreach (var f in spawner.ActiveFood) if (f) food.Add(f.cell);

        // BFS: hledáme nejbližší jídlo a pamatujeme si, kterým prvním krokem jsme vyrazili.
        var first = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        foreach (var d in dirs)
        {
            if (d == -snake.Direction) continue;
            var n = head + d;
            if (!Free(n, body) || first.ContainsKey(n) || NearObstacle(n)) continue;
            first[n] = d; queue.Enqueue(n);
        }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (food.Contains(c)) return first[c];
            foreach (var d in dirs)
            {
                var n = c + d;
                if (!Free(n, body) || first.ContainsKey(n)) continue;
                first[n] = first[c]; queue.Enqueue(n);
            }
        }
        // Cesta k jídlu neexistuje – vezmi aspoň nějaký bezpečný směr.
        foreach (var d in dirs) if (d != -snake.Direction && Free(head + d, body)) return d;
        return snake.Direction;
    }

    static bool IsCharacterSkin()
    {
        var s = SkinCatalog.Current();
        return s != null && !string.IsNullOrEmpty(s.customHeadId);
    }

    IEnumerator ShotMixamo()
    {
        Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(0.5f);
        for (int style = 0; style < CrawlStyle.Count; style++)
        {
            CrawlStyle.Current = style;
            yield return ShotStyleSkin("delta", style);
            yield return ShotStyleSkin("miku", style);
        }
        Application.Quit();
    }

    IEnumerator ShotStyleSkin(string id, int style)
    {
        var skin = SkinCatalog.Get(id);
        if (skin != null) SkinCatalog.Unlock(skin);
        SkinCatalog.Select(id);
        if (gm && !gm.IsPlaying) gm.BeginPlay();
        if (snake)
        {
            snake.ResetSnake();
            snake.ApplySkin(SkinCatalog.Current());
            foreach (var c in snake.GetComponentsInChildren<DeltaGirlCrawler>(true))
                if (c) c.ApplyCrawlStyle();
            snake.Grow(12);
            snake.Resume();
        }
        float step = snake ? snake.stepInterval : 0.16f;
        yield return new WaitForSeconds(step * 4f + 0.15f);
        if (snake) snake.SetDirection(Vector2Int.right);
        yield return new WaitForSeconds(step * 2.2f + 0.15f);
        if (snake) snake.Stop();
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (cam && snake)
        {
            if (rig) rig.enabled = false;
            yield return null;
            Transform h = snake.transform;
            Vector3 fwd = h.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            cam.transform.position = h.position - fwd * 4.2f + right * 2.8f + Vector3.up * 3.4f;
            cam.transform.LookAt(h.position + Vector3.up * 0.45f - fwd * 1.2f);
            yield return new WaitForSecondsRealtime(0.55f);
            Shot($"style{style}_{id}.png");
            yield return new WaitForSecondsRealtime(0.2f);
            if (rig) { rig.enabled = true; rig.SetView(CameraView.Overview); }
        }
    }

    IEnumerator ShotV10()
    {
        Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(0.6f);
        yield return ShotV10Skin("miku");
        yield return ShotV10Skin("delta");
        Application.Quit();
    }

    IEnumerator ShotV10Skin(string id)
    {
        var skin = SkinCatalog.Get(id);
        if (skin != null) SkinCatalog.Unlock(skin);
        SkinCatalog.Select(id);
        DeltaGirlCrawler.AllFours = false;
        if (gm && !gm.IsPlaying) gm.BeginPlay();
        if (snake)
        {
            snake.ResetSnake();
            snake.ApplySkin(SkinCatalog.Current());
            snake.Grow(14);
            snake.Resume();
        }
        float step = snake ? snake.stepInterval : 0.16f;
        yield return new WaitForSeconds(step * 5f + 0.2f);
        if (snake) snake.SetDirection(Vector2Int.right);
        yield return new WaitForSeconds(step * 2.2f + 0.2f);
        if (snake) snake.Stop();

        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (cam && snake)
        {
            if (rig) rig.enabled = false;
            yield return null;
            Transform h = snake.transform;
            Vector3 fwd = h.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            // mírně z boku, ať je v záběru zatáčka vlasů/ocasu
            cam.transform.position = h.position - fwd * 4.2f + right * 2.8f + Vector3.up * 3.4f;
            cam.transform.LookAt(h.position + Vector3.up * 0.45f - fwd * 1.2f);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot(id + "_third.png");
            yield return new WaitForSecondsRealtime(0.25f);
            cam.transform.position = h.position + fwd * 3.0f + right * 2.2f + Vector3.up * 1.35f;
            cam.transform.LookAt(h.position + Vector3.up * 0.32f - fwd * 0.4f);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot(id + "_closeup.png");
            yield return new WaitForSecondsRealtime(0.15f);
            if (rig) { rig.enabled = true; rig.SetView(CameraView.Overview); }
        }
    }

    IEnumerator ShotPolish()
    {
        Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(0.7f);
        yield return ShotPolishSkin("miku");
        yield return ShotPolishSkin("delta");
        // regresi výchozího skinu
        SkinCatalog.Select("neon");
        if (snake) snake.ApplySkin(SkinCatalog.Current());
        DeltaGirlCrawler.AllFours = false;
        if (gm) gm.BeginPlay();
        if (snake) snake.Grow(8);
        if (snake) { snake.ResetSnake(); snake.ApplySkin(SkinCatalog.Current()); snake.Grow(8); snake.Resume(); }
        yield return new WaitForSeconds((snake ? snake.stepInterval : 0.16f) * 4f + 0.25f);
        if (snake) snake.Stop();
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (rig) { rig.enabled = true; rig.SetView(CameraView.Overview); }
        if (snake) snake.ApplyCameraView(CameraView.Overview);
        yield return new WaitForSeconds(0.4f);
        Shot("neon_overview.png");
        yield return new WaitForSeconds(0.2f);
        Application.Quit();
    }

    IEnumerator ShotPolishSkin(string id)
    {
        var skin = SkinCatalog.Get(id);
        if (skin != null) SkinCatalog.Unlock(skin);
        SkinCatalog.Select(id);
        if (snake) snake.ApplySkin(SkinCatalog.Current());
        DeltaGirlCrawler.AllFours = false;
        if (gm && !gm.IsPlaying) gm.BeginPlay();
        else if (gm) { /* už běží – jen reset vzhledu */ }
        if (snake)
        {
            snake.ResetSnake();
            snake.ApplySkin(SkinCatalog.Current());
            snake.Grow(12);
            snake.Resume();
        }
        if (snake) { snake.ResetSnake(); snake.ApplySkin(SkinCatalog.Current()); snake.Grow(8); snake.Resume(); }
        yield return new WaitForSeconds((snake ? snake.stepInterval : 0.16f) * 4f + 0.25f);
        if (snake) snake.Stop();
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (rig) rig.enabled = true;

        yield return ShotPolishViews(id, "slither");

        // close-up plazení (rig vypnout a počkat snímek, ať zůstane ruční kamera)
        if (cam && snake)
        {
            if (rig) rig.enabled = false;
            yield return null;
            Transform h = snake.transform;
            Vector3 fwd = h.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            cam.transform.position = h.position + fwd * 3.2f + right * 2.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(h.position + Vector3.up * 0.36f - fwd * 0.15f);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot(id + "_slither_closeup.png");
            yield return new WaitForSecondsRealtime(0.15f);
            if (rig) { rig.enabled = true; rig.SetView(CameraView.Overview); }
            if (snake) snake.ApplyCameraView(CameraView.Overview);
        }

        DeltaGirlCrawler.AllFours = true;
        yield return new WaitForSeconds(0.45f);
        yield return ShotPolishViews(id, "allfours");
        DeltaGirlCrawler.AllFours = false;
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator ShotPolishViews(string id, string pose)
    {
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (rig)
        {
            rig.SetView(CameraView.Overview);
            if (snake) snake.ApplyCameraView(CameraView.Overview);
            yield return new WaitForSeconds(0.4f);
            Shot(id + "_" + pose + "_overview.png");
            yield return new WaitForSeconds(0.2f);
            rig.SetView(CameraView.ThirdPerson);
            if (snake) snake.ApplyCameraView(CameraView.ThirdPerson);
            yield return new WaitForSeconds(0.7f);
            Shot(id + "_" + pose + "_third.png");
            yield return new WaitForSeconds(0.2f);
            rig.SetView(CameraView.FirstPerson);
            if (snake) snake.ApplyCameraView(CameraView.FirstPerson);
            yield return new WaitForSeconds(0.5f);
            Shot(id + "_" + pose + "_first.png");
            yield return new WaitForSeconds(0.2f);
            rig.SetView(CameraView.Overview);
            if (snake) snake.ApplyCameraView(CameraView.Overview);
        }
    }

    // Krátké fotky výchozího (neon) skinu: přehled / 3. osoba / 1. osoba a klávesa V.
    IEnumerator ShotNormalSkin()
    {
        Directory.CreateDirectory(shotDir);
        SkinCatalog.Select("neon");
        if (snake) snake.ApplySkin(SkinCatalog.Current());
        yield return new WaitForSeconds(0.8f);
        Shot("00_title.png");
        yield return new WaitForSeconds(0.3f);
        if (gm) gm.BeginPlay();
        if (snake) snake.Grow(12);
        float step = snake ? snake.stepInterval : 0.16f;
        yield return new WaitForSeconds(step * 8f + 0.3f);
        if (snake) snake.Stop();
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (rig) { rig.enabled = true; Time.timeScale = 1f; }

        if (rig) { rig.SetView(CameraView.Overview); }
        if (snake) snake.ApplyCameraView(CameraView.Overview);
        yield return new WaitForSeconds(0.45f);
        Shot("01_overview.png");
        yield return new WaitForSeconds(0.25f);

        if (rig) { rig.SetView(CameraView.ThirdPerson); }
        if (snake) snake.ApplyCameraView(CameraView.ThirdPerson);
        yield return new WaitForSeconds(0.7f);
        Shot("02_third.png");
        yield return new WaitForSeconds(0.25f);

        if (rig) { rig.SetView(CameraView.FirstPerson); }
        if (snake) snake.ApplyCameraView(CameraView.FirstPerson);
        yield return new WaitForSeconds(0.55f);
        Shot("03_first.png");
        yield return new WaitForSeconds(0.2f);

        if (rig) { rig.SetView(CameraView.Overview); }
        if (snake) snake.ApplyCameraView(CameraView.Overview);
        DeltaGirlCrawler.AllFours = true;
        yield return new WaitForSeconds(0.4f);
        Shot("04_v_toggle.png");
        DeltaGirlCrawler.AllFours = false;
        yield return new WaitForSeconds(0.35f);
        Shot("normal_fixed.png");
        yield return new WaitForSeconds(0.2f);
        Application.Quit();
    }

    // Krátké fotky skinu DELTA / MIKU: detail postavy a hra s dlouhým ocasem.
    IEnumerator ShotCharacter()
    {
        Directory.CreateDirectory(shotDir);
        string id = SkinCatalog.Current() != null ? SkinCatalog.Current().id : "delta";
        yield return new WaitForSeconds(1.2f);
        Shot("00_title.png");
        yield return new WaitForSeconds(0.35f);
        gm.BeginPlay();
        if (snake) snake.Grow(16);
        // necháme hada vyrůst, ale zastavíme ho před zdí (start je uprostřed, jde nahoru)
        float step = snake ? snake.stepInterval : 0.16f;
        yield return new WaitForSeconds(step * 9f + 0.35f);
        if (snake) snake.Stop();
        yield return new WaitForSeconds(0.2f);
        Shot("02_midgame_tail.png");
        yield return new WaitForSeconds(0.35f);
        yield return CharacterCloseUp(id);
        yield return new WaitForSeconds(0.3f);
        Shot("03_long_tail.png");
        yield return new WaitForSeconds(0.35f);
        yield return ShotCameraModes();
        yield return new WaitForSeconds(0.3f);
        Application.Quit();
    }

    IEnumerator ShotCameraModes()
    {
        var cam = Camera.main;
        var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (!rig || !snake) yield break;
        rig.enabled = true;
        Time.timeScale = 1f;
        rig.SetView(CameraView.Overview);
        snake.ApplyCameraView(CameraView.Overview);
        yield return new WaitForSeconds(0.35f);
        Shot("04_cam_overview.png");
        yield return new WaitForSeconds(0.25f);
        rig.SetView(CameraView.ThirdPerson);
        snake.ApplyCameraView(CameraView.ThirdPerson);
        yield return new WaitForSeconds(0.7f);
        Shot("05_cam_third.png");
        yield return new WaitForSeconds(0.25f);
        rig.SetView(CameraView.FirstPerson);
        snake.ApplyCameraView(CameraView.FirstPerson);
        yield return new WaitForSeconds(0.55f);
        Shot("06_cam_first.png");
        yield return new WaitForSeconds(0.2f);
        rig.SetView(CameraView.Overview);
        snake.ApplyCameraView(CameraView.Overview);
    }

    IEnumerator CharacterCloseUp(string id)
    {
        var cam = Camera.main; var rig = cam ? cam.GetComponent<CameraRig>() : null;
        if (!cam || !snake) yield break;
        Vector3 p = cam.transform.position; Quaternion r = cam.transform.rotation;
        if (rig) rig.enabled = false;
        float oldScale = Time.timeScale; Time.timeScale = 0.08f;
        Transform h = snake.transform;
        // 3/4 záběr na plazení po břiše (níže u země)
        cam.transform.position = h.position + h.forward * 3.6f + h.right * 2.3f + Vector3.up * 1.35f;
        cam.transform.LookAt(h.position + Vector3.up * 0.38f - h.forward * 0.25f);
        yield return new WaitForSecondsRealtime(0.45f);
        Shot($"01e_{id}_closeup.png");
        yield return new WaitForSecondsRealtime(0.3f);
        // bližší pohled na obličej zepředu (níž)
        cam.transform.position = h.position + h.forward * 2.5f + h.right * 0.50f + Vector3.up * 0.95f;
        cam.transform.LookAt(h.position + Vector3.up * 0.42f);
        yield return new WaitForSecondsRealtime(0.45f);
        Shot($"01e2_{id}_face.png");
        yield return new WaitForSecondsRealtime(0.3f);
        // pohled shora podél ocasu
        cam.transform.position = h.position - h.forward * 0.4f + h.right * 0.9f + Vector3.up * 5.2f;
        cam.transform.LookAt(h.position - h.forward * 3.4f + Vector3.up * 0.22f);
        yield return new WaitForSecondsRealtime(0.45f);
        Shot($"01f_{id}_tail_closeup.png");
        yield return new WaitForSecondsRealtime(0.3f);
        cam.transform.SetPositionAndRotation(p, r);
        if (rig) rig.enabled = true;
        Time.timeScale = oldScale;
    }

    IEnumerator TakeShots()
    {
        Directory.CreateDirectory(shotDir);
        yield return new WaitForSeconds(1.5f);
        Shot("00_title.png");          // úvodní obrazovka
        yield return new WaitForSeconds(0.3f); // fotka se uloží až na konci snímku – počkáme
        gm.BeginPlay();
        yield return new WaitForSeconds(2.5f);
        Shot("01_start.png");
        yield return CloseUp();
        // počkáme, až bude had delší a ideálně zrychlený
        float t = 0;
        while (t < 40f && !(snake.Cells.Count >= 10 && snake.IsBoosted)) { t += Time.deltaTime; yield return null; }
        Shot("02_midgame.png");
        // počkáme na zrychlení (turbo) a vyfotíme svítící stopu
        t = 0;
        while (!snake.IsBoosted && t < 25f && gm.IsPlaying) { t += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.5f);
        if (snake.IsBoosted) Shot("02b_turbo_trail.png");
        var tr = FindObjectOfType<TrailRenderer>();
        if (tr) Debug.Log($"[Snake3D] Stopa: emitting={tr.emitting} bodů={tr.positionCount}");
        yield return new WaitForSeconds(6f);
        Shot("03_longer_snake.png");
        // počkáme, až had sní další jídlo, a vyfotíme výbuch částic
        int sc = gm.Score; t = 0;
        while (gm.Score == sc && t < 15f) { t += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.12f);
        Shot("03b_eat_burst.png");
        // pauza s nastavením hlasitosti
        yield return new WaitForSeconds(0.3f);   // nejdřív se musí uložit předchozí fotka
        gm.SetPaused(true);
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("03d_pause.png");
        yield return new WaitForSecondsRealtime(0.3f);
        gm.SetPaused(false);
        // vypneme řízení – had narazí do zdi a ukáže se obrazovka Konec hry
        steering = false;
        t = 0;
        while (gm.IsPlaying && t < 10f) { t += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.15f);
        Shot("03c_crash.png");         // náraz: částice + třesení kamery
        yield return new WaitForSeconds(2f);
        if (!gm.IsPlaying) { Shot("04a_name_entry.png"); yield return new WaitForSeconds(0.4f); gm.SubmitName("Demo"); yield return new WaitForSeconds(0.5f); }
        Shot("04_game_over.png");
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    // Když hra postoupí do dalšího levelu, vyfotí přechod a pak nový level.
    IEnumerator WatchLevels()
    {
        int last = gm.Level;
        bool shieldShot = false, recordShot = false, hintShot = false;
        while (true)
        {
            if (!hintShot && gm.hintText && gm.hintText.text != "" && gm.hintText.alpha > 0.95f)
            { hintShot = true; Shot("08_tutorial_hint.png"); }
            if (!recordShot && gm.bannerText && gm.bannerText.text != "")
            {
                recordShot = true;
                yield return new WaitForSeconds(0.35f);
                Shot("09_new_record.png");      // oslava nového rekordu
            }
            if (!shieldShot && snake.IsShielded)
            {
                shieldShot = true;
                yield return new WaitForSeconds(1f);
                Shot("07_shield_rainbow.png");   // had se štítem hraje duhou
            }
            if (gm.Level != last)
            {
                last = gm.Level;
                yield return new WaitForSeconds(0.5f);
                Shot($"05_levelup_{last + 1}.png");
                yield return new WaitForSeconds(3.5f);
                Shot($"06_level{last + 1}_play.png");
            }
            yield return null;
        }
    }

    // Detailní záběr: kamera se na chvíli přiblíží k jídlu a k překážce (ať jsou vidět 3D modely).
    IEnumerator CloseUp()
    {
        var cam = Camera.main; var rig = cam.GetComponent<CameraRig>();
        Vector3 p = cam.transform.position; Quaternion r = cam.transform.rotation;
        if (rig) rig.enabled = false;
        float oldScale = Time.timeScale; Time.timeScale = 0.05f; // zpomalíme hru, ať had mezitím nenarazí
        int n = 0;
        foreach (var f in spawner.ActiveFood)
        {
            if (!f || n >= 2) continue;
            cam.transform.position = f.transform.position + new Vector3(0, 1.6f, -2.2f);
            cam.transform.LookAt(f.transform.position);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot($"01c_food_closeup_{n++}.png");
            yield return new WaitForSecondsRealtime(0.3f);
        }
        var obst = FindObjectOfType<MovingObstacle>();
        if (obst)
        {
            cam.transform.position = obst.transform.position + new Vector3(0, 2f, -2.8f);
            cam.transform.LookAt(obst.transform.position);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("01d_obstacle_closeup.png");
            yield return new WaitForSecondsRealtime(0.3f);
        }
        // detail hlavy hada: šupiny, oči, obočí a vystrčený jazyk
        var tongue = snake.GetComponentInChildren<Tongue>();
        if (tongue) tongue.hold = true;
        Transform h = snake.transform;
        cam.transform.position = h.position + h.forward * 2.0f + h.right * 0.9f + Vector3.up * 1.1f;
        cam.transform.LookAt(h.position - h.forward * 0.8f);
        yield return new WaitForSecondsRealtime(0.4f);
        Shot("01e_snake_closeup.png");
        yield return new WaitForSecondsRealtime(0.3f);
        if (tongue) tongue.hold = false;
        cam.transform.SetPositionAndRotation(p, r);
        if (rig) rig.enabled = true;
        Time.timeScale = oldScale;
    }

    // Unity umí uložit jen jednu fotku za snímek – když chtějí dvě najednou,
    // druhá počká na další snímek.
    int lastShotFrame = -1;
    void Shot(string name) { StartCoroutine(ShotRoutine(name)); }
    IEnumerator ShotRoutine(string name)
    {
        while (Time.frameCount <= lastShotFrame) yield return null;
        lastShotFrame = Time.frameCount;
        string p = Path.Combine(shotDir, name);
        ScreenCapture.CaptureScreenshot(p);
        Debug.Log("[Snake3D] Screenshot: " + p);
    }
}
