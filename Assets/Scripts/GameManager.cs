using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Řídí celou hru: úvodní obrazovku, skóre, čas, jídlo, konec hry a efekty.
public class GameManager : MonoBehaviour
{
    public GridArena arena;
    public SnakeController snake;
    public FoodSpawner spawner;
    public MovingObstacle[] obstacles;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timeText;
    public TMP_Text infoText;
    public TMP_Text powerText;         // aktivní vylepšení (TURBO / ŠTÍT / MAGNET)
    public TMP_Text hintText;          // nápověda při úplně první hře
    public TMP_Text bannerText;        // velký nápis "NOVÝ REKORD!"
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public GameObject titlePanel;      // úvodní obrazovka "3D SNAKE"
    public GameObject minimap;
    public Light sunLight;             // hlavní světlo (v tmavém levelu se ztlumí)
    public Light headLight;            // lampa nad hlavou hada (svítí jen ve tmě)
    Vector3[] obstacleBaseScale;         // malá mapa shora v rohu (ukazuje se jen při hraní)
    public TMP_Text pressStartText;    // blikající "stiskni mezerník"

    [Header("Efekty")]
    public NeonFX fx;                  // částice
    public CameraRig cameraRig;        // třesení kamery
    public SoundFX sound;              // zvuky a hudba
    public Material popupFontMaterial; // písmo plovoucích "+10" textů

    [Header("Levely")]
    public LevelData[] levels;            // nastavení levelů (vyplní SnakeSceneSetup)
    public Renderer groundRenderer;       // podlaha arény (mění barvu podle levelu)
    public Renderer outerRenderer;        // podlaha kolem arény
    public Renderer[] wallsA, wallsB;     // okraje arény
    public GameObject levelPanel;         // velký nápis "LEVEL 2"
    public TMP_Text levelText;
    public TMP_Text levelSelectText;      // výběr levelu na úvodní obrazovce
    public GameObject pausePanel;         // menu pauzy (Esc)
    public TMP_Text pauseText;
    public TMP_Text highScoreText;        // tabulka TOP 10 na obrazovce Konec hry

    public float timeLimit = 120f;

    enum State { Title, Playing, LevelUp, Paused, NameEntry, Over }
    State state;
    int score, best;
    float timeLeft;
    static bool skipTitle;             // po "Hrát znovu" už úvodní obrazovku nezobrazujeme
    int eatenCount, kindCount, bestCombo; float surviveTime;   // postup v úkolu levelu
    static int startLevel;             // level, kterým hra začíná (vybírá se na úvodní obrazovce)
    int level;                         // aktuální level (0 = první)
    State stateBeforePause;
    string playerName = "";
    int combo;                         // kolik jídel za sebou rychle po sobě
    float magnetTime, magnetTick;
    int bestAtStart; bool recordShown;  // pro oslavu nového rekordu      // jak dlouho ještě magnet přitahuje jídlo
    float lastEatTime = -10f;
    public float comboWindow = 2.5f;   // kolik sekund mezi jídly, aby combo pokračovalo
    string endReason;
    MaterialPropertyBlock block;

    public int Score => score;
    public int Level => level;
    public bool IsPlaying => state == State.Playing;
    public bool IsPaused => state == State.Paused;

    void Start()
    {
        best = PlayerPrefs.GetInt("Snake3D_Best", 0);
        var font = TMP_Settings.defaultFontAsset;
        foreach (var t in new[] { scoreText, timeText, infoText, gameOverText, pressStartText })
            if (t && t.font == null && font) t.font = font;
        // spuštění z příkazové řádky s "-startlevel 3" začne rovnou ve 3. levelu
        var args = CmdArgs.Get();
        int a = System.Array.IndexOf(args, "-startlevel");
        if (a >= 0 && a + 1 < args.Length && int.TryParse(args[a + 1], out int lv)) { startLevel = lv - 1; PlayerPrefs.SetInt("Snake3D_Unlocked", Mathf.Max(PlayerPrefs.GetInt("Snake3D_Unlocked", 1), lv)); }
        if (System.Array.IndexOf(args, "-resetcampaign") >= 0) PlayerPrefs.SetInt("Snake3D_Unlocked", 1);   // test: zamkne zase všechno
        if (levels != null && levels.Length > 0) startLevel = Mathf.Clamp(startLevel, 0, levels.Length - 1);
        level = startLevel;
        ApplyLevel(level);
        StartGame();
        if (skipTitle || titlePanel == null) BeginPlay();
        else { state = State.Title; snake.Stop(); titlePanel.SetActive(true); if (cameraRig) cameraRig.orbit = true; }
    }

    // Připraví nové kolo (had, jídlo, čas, skóre).
    public void StartGame()
    {
        score = 0;
        ResetObjective();
        if (gameOverPanel) gameOverPanel.SetActive(false);
        snake.ResetSnake();
        spawner.ResetFood();
        Info("", Color.white);
        UpdateUI();
    }

    // ---------- KAMPAŇ: úkoly levelů ----------
    LevelData Cur => levels != null && levels.Length > 0 ? levels[level] : null;
    bool NoTimeLimit => Cur != null && Cur.timeLimit <= 0f;

    // Kolik levelů má hráč odemčeno (uloženo v PlayerPrefs).
    public int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt("Snake3D_Unlocked", 1), 1, levels != null ? levels.Length : 1);

    void ResetObjective()
    {
        eatenCount = kindCount = bestCombo = 0; surviveTime = 0f;
        timeLeft = Cur != null && Cur.timeLimit > 0 ? Cur.timeLimit : 0f;
    }

    // Text úkolu, např. "Sněz 10 jídel".
    public string ObjectiveText(LevelData L)
    {
        string s;
        switch (L.objective)
        {
            case Objective.EatKind: s = $"Sněz {L.target}× {KindName(L.foodKind)}"; break;
            case Objective.ReachLength: s = $"Doroste na {L.target} článků"; break;
            case Objective.Combo: s = $"Udělej COMBO x{L.target}"; break;
            case Objective.Survive: s = $"Přežij {L.target} s"; break;
            default: s = $"Sněz {L.target} jídel"; break;
        }
        if (L.noTurbo) s += " (bez turba!)";
        if (L.timeLimit > 0) s += $" do {L.timeLimit:0} s";
        return s;
    }

    static string KindName(FoodKind k) => k == FoodKind.Gold ? "zlatou hvězdu" : k == FoodKind.Speed ? "turbo" : k == FoodKind.Time ? "kávu" :
                                          k == FoodKind.Shrink ? "houbu" : k == FoodKind.Shield ? "srdce" : k == FoodKind.Magnet ? "magnet" : "jablko";

    int Progress()
    {
        var L = Cur;
        switch (L.objective)
        {
            case Objective.EatKind: return kindCount;
            case Objective.ReachLength: return snake.Cells.Count;
            case Objective.Combo: return bestCombo;
            case Objective.Survive: return Mathf.FloorToInt(surviveTime);
            default: return eatenCount;
        }
    }

    // Je úkol splněný? Pak hurá do dalšího levelu.
    void CheckObjective()
    {
        if (Cur == null || state != State.Playing) return;
        if (Progress() >= Cur.target) StartCoroutine(LevelUpRoutine());
    }

    // Spustí hru (po stisku mezerníku na úvodní obrazovce).
    public void BeginPlay()
    {
        magnetTime = 0f;
        bestAtStart = best; recordShown = false;
        if (Cur != null) StartCoroutine(ObjectiveCard());
        if (bannerText) bannerText.text = "";
        // nápověda jen při první hře (nebo s parametrem -tutorial)
        bool forceTut = System.Array.IndexOf(CmdArgs.Get(), "-tutorial") >= 0;
        if (hintText && (forceTut || PlayerPrefs.GetInt("Snake3D_TutorialDone", 0) == 0))
        {
            PlayerPrefs.SetInt("Snake3D_TutorialDone", 1);
            StartCoroutine(TutorialRoutine());
        }
        if (titlePanel) titlePanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (levelPanel) levelPanel.SetActive(false);
        state = State.Playing;
        if (cameraRig) cameraRig.orbit = false;   // kamera se vrátí na místo
        snake.Resume();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        skipTitle = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Update()
    {
        PulseToMusic();
        // magnet: každých 0,25 s přitáhne blízké jídlo o kousek k hlavě
        if (state == State.Playing && magnetTime > 0f)
        {
            magnetTime -= Time.deltaTime; magnetTick -= Time.deltaTime;
            if (magnetTick <= 0f && snake.Cells.Count > 0) { magnetTick = 0.25f; spawner.PullToward(snake.Cells[0], 7); }
        }
        if (minimap) minimap.SetActive(state == State.Playing || state == State.LevelUp || state == State.Paused);
        if (state == State.Title)
        {
            // blikání nápisu: průhlednost se mění podle sinusu
            if (pressStartText) pressStartText.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
            // výběr levelu klávesami 1, 2, 3 nebo šipkami vlevo/vpravo
            int n = levels != null ? levels.Length : 1;
            int chosen = level;
            for (int i = 0; i < n && i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) chosen = i;
            if (n >= 10 && Input.GetKeyDown(KeyCode.Alpha0)) chosen = 9;   // 0 = level 10
            int u = Unlocked;                                   // vybrat jde jen odemčené levely
            if (Input.GetKeyDown(KeyCode.LeftArrow)) chosen = (level + u - 1) % u;
            if (Input.GetKeyDown(KeyCode.RightArrow)) chosen = (level + 1) % u;
            if (chosen >= u) chosen = level;
            if (chosen != level) SelectLevel(chosen);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) BeginPlay();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (state == State.Playing) SetPaused(true);
            else if (state == State.Paused) SetPaused(false);
        }
        if (state == State.Paused) { PauseMenuInput(); return; }
        if (state == State.LevelUp) { UpdateUI(); return; }
        if (state == State.NameEntry) { NameEntryInput(); return; }
        if (state == State.Over)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space)) RestartGame();
            return;
        }
        surviveTime += Time.deltaTime;
        if (!NoTimeLimit)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0) { timeLeft = 0; GameOver("Čas vypršel! Úkol nesplněn."); }
        }
        if (Cur != null && Cur.objective == Objective.Survive) CheckObjective();
        UpdateUI();
    }

    // ---------- LEVELY ----------

    // Na úvodní obrazovce: vybere level, kterým hra začne.
    public void SelectLevel(int i)
    {
        level = startLevel = i;
        ApplyLevel(level);
        snake.ResetSnake(); snake.Stop();
        spawner.ResetFood();
        UpdateUI();
    }

    // Nastaví barvy arény, rychlost hada a překážky podle levelu.
    void ApplyLevel(int i)
    {
        if (levels == null || levels.Length == 0) return;
        var L = levels[i];
        if (block == null) block = new MaterialPropertyBlock();
        Tint(groundRenderer, L.gridColor * 1.4f);
        Tint(outerRenderer, L.outerColor * 0.5f);
        if (wallsA != null) foreach (var w in wallsA) Tint(w, L.wallColorA * 2f);
        if (wallsB != null) foreach (var w in wallsB) Tint(w, L.wallColorB * 2f);
        RenderSettings.fogColor = L.fogColor;
        snake.stepInterval = L.stepInterval;
        // tma: hlavní světlo skoro zhasne, mřížka ztmavne a rozsvítí se lampa nad hadem
        if (sunLight) sunLight.intensity = L.dark ? 0.03f : 0.35f;
        if (headLight) headLight.enabled = L.dark;
        if (L.dark)
        {
            Tint(groundRenderer, L.gridColor * 0.3f);
            if (wallsA != null) foreach (var w in wallsA) Tint(w, L.wallColorA * 0.6f);
            if (wallsB != null) foreach (var w in wallsB) Tint(w, L.wallColorB * 0.6f);
            Tint(outerRenderer, L.outerColor * 0.1f);
        }
        RenderSettings.fogDensity = L.dark ? 0.06f : 0.025f;
        if (obstacleBaseScale == null)
        {
            obstacleBaseScale = new Vector3[obstacles.Length];
            for (int k = 0; k < obstacles.Length; k++) obstacleBaseScale[k] = obstacles[k].transform.localScale;
        }
        // překážky: kolik jich level potřebuje, tolik zapneme a pošleme po jejich cestě
        for (int k = 0; k < obstacles.Length; k++)
        {
            var o = obstacles[k];
            bool used = L.obstacles != null && k < L.obstacles.Length;
            o.gameObject.SetActive(used);
            if (!used) continue;
            float sz = L.obstacles[k].size > 0 ? L.obstacles[k].size : 1f;
            o.transform.localScale = obstacleBaseScale[k] * sz;         // obří pila = větší a zabírá víc políček
            float y = 0.45f * sz;
            o.SetPath(arena.CellToWorld(L.obstacles[k].from, y), arena.CellToWorld(L.obstacles[k].to, y), L.obstacles[k].speed);
        }
        if (levelSelectText)
        {
            // řada zámečků: odemčené levely jsou čísla, zamčené jsou šedé
            string row = "";
            for (int k = 0; k < levels.Length; k++)
                row += k == i ? $"<color=#FFE14D>[{k + 1}]</color> " : k < Unlocked ? $"{k + 1} " : "<color=#555577>zámek</color> ";
            levelSelectText.text = $"<  LEVEL {i + 1}: {L.name}  >\n<size=55%>Cíl: {ObjectiveText(L)}</size>\n<size=45%>{row}</size>";
        }
    }

    // Mřížka na podlaze "pulzuje" do rytmu hudby: na každou dobu krátce zesílí.
    void PulseToMusic()
    {
        if (levels == null || levels.Length == 0 || block == null) return;
        float bpm = sound ? sound.bpm : 110f;
        float t = sound ? sound.MusicTime : Time.time;
        float phase = (t * bpm / 60f) % 1f;            // 0..1 v rámci jedné doby
        float beat = Mathf.Exp(-6f * phase);           // na začátku doby 1, pak rychle klesá
        Tint(groundRenderer, levels[level].gridColor * 1.4f * (0.8f + 0.5f * beat));
    }

    // Změní barvu záře (emission) jednoho objektu, ale materiál samotný nechá být.
    void Tint(Renderer r, Color emission)
    {
        if (!r) return;
        r.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    // Krátká nápověda při první hře: 3 tipy po sobě, každý 3 s.
    System.Collections.IEnumerator TutorialRoutine()
    {
        string[] tips =
        {
            "<color=#80FFFF>Šipky / WASD</color> = zatáčení. Sbírej jídlo a rosteš!",
            "Nenarážej do <color=#FF6080>zdí</color>, do <color=#FF9040>pil</color> ani do sebe.",
            "Barevná jídla dávají vylepšení. <color=#FFE14D>Esc</color> = pauza.",
        };
        foreach (var tip in tips)
        {
            float t = 0;
            while (t < 3f)
            {
                if (state != State.Paused) t += Time.deltaTime;
                if (state == State.Over || state == State.NameEntry || state == State.Title) { hintText.text = ""; yield break; }
                // jemné zjevení a zmizení textu
                float a = Mathf.Clamp01(Mathf.Min(t, 3f - t) * 3f);
                hintText.text = tip; hintText.alpha = a;
                yield return null;
            }
        }
        hintText.text = "";
    }

    // Krátká "karta" s úkolem na začátku levelu (2,5 s).
    System.Collections.IEnumerator ObjectiveCard()
    {
        if (!bannerText) yield break;
        float t = 0;
        while (t < 2.5f && state == State.Playing && !recordShown)
        {
            t += Time.deltaTime;
            bannerText.text = $"<size=60%>LEVEL {level + 1}: {Cur.name}</size>\n<size=50%>Cíl: {ObjectiveText(Cur)}</size>";
            bannerText.alpha = Mathf.Clamp01((2.5f - t) * 3f);
            bannerText.transform.localScale = Vector3.one;
            yield return null;
        }
        if (!recordShown) bannerText.text = "";
    }

    // Překonal hráč během hry svůj rekord? Jednou za hru to oslavíme.
    void CheckRecord()
    {
        if (recordShown || bestAtStart <= 0 || score <= bestAtStart) return;
        recordShown = true;
        StartCoroutine(RecordRoutine());
    }

    System.Collections.IEnumerator RecordRoutine()
    {
        if (fx) for (int i = 0; i < 3; i++) fx.Burst(snake.transform.position + Random.insideUnitSphere * 1.5f, new Color(1f, 0.8f, 0.2f) * 5f, 90, 0.3f);
        if (sound) { sound.Eat(1.8f); sound.Turbo(); }
        if (cameraRig) cameraRig.Shake(0.25f, 0.2f);
        float t = 0;
        while (t < 2.2f)
        {
            t += Time.deltaTime;
            if (bannerText)
            {
                bannerText.text = "NOVÝ REKORD!";
                // nápis "naskočí" a pak se houpe, na konci zmizí
                float pop = t < 0.25f ? Mathf.Lerp(2.2f, 1f, t / 0.25f) : 1f + 0.06f * Mathf.Sin(t * 12f);
                bannerText.transform.localScale = Vector3.one * pop;
                bannerText.alpha = Mathf.Clamp01((2.2f - t) * 3f);
                bannerText.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), Color.white, Mathf.PingPong(t * 4f, 1f));
            }
            yield return null;
        }
        if (bannerText) bannerText.text = "";
    }

    // Má hráč dost bodů na další level?
    void CheckLevelUp()
    {
        if (levels == null || level >= levels.Length - 1) return;
        int need = levels[level].scoreToNext;
        if (need > 0 && score >= need) StartCoroutine(LevelUpRoutine());
    }

    System.Collections.IEnumerator LevelUpRoutine()
    {
        state = State.LevelUp;
        snake.Stop();
        // poslední level splněn = vyhrál jsi celou kampaň
        if (level >= levels.Length - 1) { PlayerPrefs.SetInt("Snake3D_Unlocked", levels.Length); GameOver("KAMPAŇ DOKONČENA! Jsi mistr hada!"); yield break; }
        level++;
        ResetObjective();                        // nový úkol začíná od nuly
        PlayerPrefs.SetInt("Snake3D_Unlocked", Mathf.Max(Unlocked, level + 1));   // odemkne další level
        PlayerPrefs.Save();
        var L = levels[level];
        // 1. obrazovka: LEVEL SPLNĚN, 2. (po přepnutí): karta s novým úkolem
        if (levelText) levelText.text = $"<color=#7CFF7C>LEVEL {level} SPLNĚN!</color>\n<size=45%>další: {level + 1}. {L.name}</size>\n<size=32%>Cíl: {ObjectiveText(L)}</size>";
        if (levelPanel) levelPanel.SetActive(true);
        if (levelText) levelText.transform.localScale = Vector3.one * 2f;
        if (fx) fx.Burst(snake.transform.position, L.gridColor * 4f, 150, 0.35f);
        if (sound) { sound.Eat(1.6f); sound.Turbo(); }
        if (cameraRig) cameraRig.Shake(0.3f, 0.25f);
        // nápis se "zabouchne" do správné velikosti
        float t = 0;
        while (t < 2.2f)
        {
            t += Time.deltaTime;
            if (levelText) levelText.transform.localScale = Vector3.one * Mathf.Lerp(levelText.transform.localScale.x, 1f, 10f * Time.deltaTime);
            if (t > 1f && t - Time.deltaTime <= 1f) { ApplyLevel(level); snake.ResetSnake(); snake.Stop(); spawner.ResetFood(); }
            yield return null;
        }
        Info($"LEVEL {level + 1}: {ObjectiveText(L)}", L.gridColor);
        if (levelPanel) levelPanel.SetActive(false);
        state = State.Playing;
        snake.Resume();
    }

    // ---------- PAUZA ----------

    public void SetPaused(bool paused)
    {
        if (paused) { stateBeforePause = state; state = State.Paused; Time.timeScale = 0f; }
        else { state = stateBeforePause; Time.timeScale = 1f; }
        if (pausePanel) pausePanel.SetActive(paused);
        UpdatePauseText();
    }

    // V pauze: R = znovu, Q = úvodní obrazovka, šipky = hlasitost
    void PauseMenuInput()
    {
        if (Input.GetKeyDown(KeyCode.R)) { RestartGame(); return; }
        if (Input.GetKeyDown(KeyCode.Q)) { Time.timeScale = 1f; skipTitle = false; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); return; }
        if (!sound) return;
        bool changed = true;
        if (Input.GetKeyDown(KeyCode.RightArrow)) sound.musicVolume = Mathf.Clamp01(sound.musicVolume + 0.1f);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) sound.musicVolume = Mathf.Clamp01(sound.musicVolume - 0.1f);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) sound.effectsVolume = Mathf.Clamp01(sound.effectsVolume + 0.1f);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) sound.effectsVolume = Mathf.Clamp01(sound.effectsVolume - 0.1f);
        else changed = false;
        if (changed) { sound.SaveVolumes(); UpdatePauseText(); }
    }

    void UpdatePauseText()
    {
        if (!pauseText) return;
        string mv = sound ? Bar(sound.musicVolume) : "-", ev = sound ? Bar(sound.effectsVolume) : "-";
        pauseText.text = $"Hudba  {mv}   <size=70%>(šipky vlevo/vpravo)</size>\n" +
                         $"Zvuky  {ev}   <size=70%>(šipky nahoru/dolů)</size>\n\n" +
                         "<size=80%>Esc = pokračovat    R = znovu    Q = menu</size>";
    }

    // Ukazatel hlasitosti z čárek, např. "||||||----"
    static string Bar(float v)
    {
        int n = Mathf.RoundToInt(v * 10);
        return "<color=#33FFE0>" + new string('|', n) + "</color><color=#444466>" + new string('|', 10 - n) + "</color>";
    }

    public bool IsObstacleCell(Vector2Int c)
    {
        if (obstacles == null) return false;
        foreach (var o in obstacles) if (o && o.isActiveAndEnabled && o.Covers(arena, c)) return true;
        return false;
    }

    public void OnSnakeMoved(Vector2Int head)
    {
        var f = spawner.TakeAt(head);
        if (f == null) return;
        var t = f.type;
        // COMBO: když sníš další jídlo do 3 sekund, combo roste a body se násobí (x2 od 3 jídel, x3 od 6)
        combo = Time.time - lastEatTime < comboWindow ? combo + 1 : 1;
        lastEatTime = Time.time;
        int mult = Mathf.Min(1 + combo / 3, 3);
        int points = t.points * mult;
        score += points;
        timeLeft += t.bonusTime;
        snake.Grow(t.grow);
        if (t.kind == FoodKind.Speed) snake.SpeedBoost(5f);
        if (t.kind == FoodKind.Shield) snake.Shield(6f);    // štít na 6 sekund
        if (t.kind == FoodKind.Magnet) magnetTime = 8f;     // magnet na 8 sekund
        Info($"{t.name}: +{points} b." + (mult > 1 ? $"  COMBO x{mult}" : "") + (t.bonusTime > 0 ? $" +{t.bonusTime:0}s" : "") +
             (t.kind == FoodKind.Speed ? "  TURBO!" : "") + (t.kind == FoodKind.Shield ? "  ŠTÍT 6 s!" : "") + (t.kind == FoodKind.Magnet ? "  MAGNET 8 s!" : ""), t.color);
        if (fx) fx.Burst(f.transform.position, t.color * 4f, 45);   // výbuch částic v barvě jídla (×4 = víc svítí)
        // plovoucí text s body (u času i "+10s")
        FloatingText.Spawn($"+{points}" + (t.bonusTime > 0 ? $"  +{t.bonusTime:0}s" : "") + (mult > 1 ? $"\n<size=70%>COMBO x{mult}</size>" : ""), f.transform.position,
                           Color.Lerp(t.color, Color.white, 0.2f), popupFontMaterial);
        if (sound)
        {
            float pitch = t.kind == FoodKind.Gold ? 1.3f : t.kind == FoodKind.Shrink ? 0.7f : 1f; // jiná výška tónu podle jídla
            sound.Eat(pitch * (1f + 0.06f * Mathf.Min(combo - 1, 8)));                            // s combem tón stoupá
            if (t.kind == FoodKind.Speed) sound.Turbo();
        }
        Punch(scoreText);
        Destroy(f.gameObject);
        CheckRecord();
        eatenCount++;
        if (Cur != null && t.kind == Cur.foodKind) kindCount++;
        bestCombo = Mathf.Max(bestCombo, mult);
        if (Cur != null && Cur.noTurbo && t.kind == FoodKind.Speed) { GameOver("Snědl jsi turbo – v tomhle levelu je zakázané!"); return; }
        CheckObjective();
    }

    void Info(string s, Color c)
    {
        if (!infoText) return;
        infoText.text = s; infoText.color = Color.Lerp(c, Color.white, 0.35f);
        Punch(infoText);
    }

    static void Punch(TMP_Text t)
    {
        if (t && t.TryGetComponent(out UIPunch p)) p.Punch();
    }

    public void GameOver(string reason)
    {
        if (state == State.Over || state == State.NameEntry) return;
        state = State.Over;
        magnetTime = 0f; UpdateUI();   // schová řádek s vylepšeními
        Time.timeScale = 1f;
        endReason = reason;
        snake.Stop();
        if (fx) fx.Burst(snake.transform.position, new Color(4f, 0.8f, 0.3f), 220, 0.45f); // velký výbuch při nárazu
        StartCoroutine(ExplodeSnake());
        if (cameraRig) cameraRig.Shake(0.5f, 0.6f);
        if (sound) sound.Crash();
        if (score > best) { best = score; PlayerPrefs.SetInt("Snake3D_Best", best); }
        Invoke(nameof(ShowGameOverPanel), 0.9f); // panel ukážeme až po chvilce, ať je vidět výbuch
        if (HighScores.Qualifies(score))
        {
            // výsledek patří do TOP 10 -> hráč napíše své jméno
            state = State.NameEntry;
            playerName = PlayerPrefs.GetString("Snake3D_LastName", "");
            ShowNameEntry();
        }
        else ShowResults(0);
    }

    // Článek po článku had "vybuchne" – od hlavy k ocasu.
    System.Collections.IEnumerator ExplodeSnake()
    {
        var segs = new System.Collections.Generic.List<Transform>(snake.Segments);
        for (int i = 0; i < segs.Count; i++)
        {
            if (!segs[i]) continue;
            Color c = Color.Lerp(snake.headColor, snake.tailColor, i / Mathf.Max(1f, segs.Count - 1f));
            if (fx) fx.Burst(segs[i].position, c * 4f, 12, 0.25f);
            foreach (var r in segs[i].GetComponentsInChildren<Renderer>()) r.enabled = false;
            yield return new WaitForSeconds(0.035f);
        }
    }

    void ShowNameEntry()
    {
        if (gameOverText) gameOverText.text =
            $"{endReason}\n<size=130%>Skóre: <color=#FFE14D>{score}</color></size>\n\n" +
            $"<color=#FF40D0>Jsi v TOP 10!</color>\nNapiš jméno:\n<color=#33FFE0>{playerName}_</color>\n<size=70%>Enter = uložit</size>";
        if (highScoreText) highScoreText.text = HighScores.Format(0);
    }

    // Psaní jména: Input.inputString obsahuje písmena napsaná v tomto snímku.
    void NameEntryInput()
    {
        foreach (char c in Input.inputString)
        {
            if (c == '\b') { if (playerName.Length > 0) playerName = playerName.Substring(0, playerName.Length - 1); }
            else if (c == '\n' || c == '\r') { SubmitName(playerName); return; }
            else if (!char.IsControl(c) && playerName.Length < 12) playerName += c;
        }
        ShowNameEntry();
    }

    // Uloží jméno do tabulky (volá se po Enteru, nebo z demo režimu).
    public void SubmitName(string name)
    {
        if (state != State.NameEntry) return;
        name = string.IsNullOrWhiteSpace(name) ? "Hráč" : name.Trim();
        PlayerPrefs.SetString("Snake3D_LastName", name);
        int rank = HighScores.Add(name, score, level + 1);
        state = State.Over;
        magnetTime = 0f; UpdateUI();   // schová řádek s vylepšeními
        ShowResults(rank);
    }

    void ShowResults(int rank)
    {
        if (gameOverText) gameOverText.text =
            $"{endReason}\n\n<size=130%>Skóre: <color=#FFE14D>{score}</color></size>\nRekord: {best}\nLevel: {level + 1}" +
            (rank > 0 ? $"\n<color=#FF40D0>{rank}. místo v TOP 10!</color>" : "") +
            "\n\n<size=70%>[R] nebo mezerník = znovu</size>";
        if (highScoreText) highScoreText.text = HighScores.Format(rank);
    }

    void ShowGameOverPanel() { if (gameOverPanel) gameOverPanel.SetActive(true); }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = $"SKÓRE  {score}\n<size=60%>REKORD  {best}</size>" +
            (Cur != null && state != State.Title ? $"\n<size=50%><color=#FFE14D>Cíl: {ObjectiveText(Cur)}  {Mathf.Min(Progress(), Cur.target)}/{Cur.target}</color></size>" : "");
        if (timeText)
        {
            // pod časem ukážeme aktivní vylepšení a kolik sekund jim zbývá
            string pw = "";
            if (state == State.Playing || state == State.Paused || state == State.LevelUp)
            {
                if (snake.IsBoosted) pw += $"<color=#60A0FF>TURBO {Mathf.CeilToInt(snake.BoostLeft)}</color>  ";
                if (snake.IsShielded) pw += $"<color=#FF80C8>ŠTÍT {Mathf.CeilToInt(snake.ShieldLeft)}</color>  ";
                if (magnetTime > 0f) pw += $"<color=#FF4060>MAGNET {Mathf.CeilToInt(magnetTime)}</color>";
            }
            timeText.text = (NoTimeLimit ? "ČAS  --" : $"ČAS  {Mathf.CeilToInt(timeLeft)}") + $"\n<size=60%>LEVEL {level + 1}</size>";
            if (powerText) powerText.text = pw.TrimEnd();
            // posledních 10 sekund: text zčervená a bliká
            bool hurry = !NoTimeLimit && timeLeft < 10f && state == State.Playing;
            timeText.color = hurry ? Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f)) : Color.white;
        }
    }
}
