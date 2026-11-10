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
// Při normálním hraní se tento skript vůbec nespustí.
public class SnakeAutoplay : MonoBehaviour
{
    SnakeController snake;
    GameManager gm;
    FoodSpawner spawner;
    GridArena arena;
    bool steering = true;
    string shotDir;

    static bool HasArg(string a) => System.Array.IndexOf(CmdArgs.Get(), a) >= 0;

    // Unity tuhle metodu zavolá samo po načtení scény.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!HasArg("-autoplay") && !HasArg("-screenshots")) return;
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
        if (HasArg("-screenshots")) { StartCoroutine(TakeShots()); StartCoroutine(WatchLevels()); }
        else gm.BeginPlay(); // bez fotek rovnou hrajeme (přeskočí úvodní obrazovku)
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
