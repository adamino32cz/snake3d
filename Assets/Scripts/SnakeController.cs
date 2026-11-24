using System.Collections.Generic;
using UnityEngine;

// Pohyb hada po mřížce: každých stepInterval sekund se posune o jednu buňku.
// LOGIKA (kolize, jídlo) je pořád po políčkách. Jen OBRAZ (kuličky těla) se mezi
// políčky plynule posouvá, takže had nevypadá, že "skáče".
[DefaultExecutionOrder(20)]
public class SnakeController : MonoBehaviour
{
    public GridArena arena;
    public GameManager gameManager;
    public Material bodyMaterial;
    public float stepInterval = 0.16f;
    public int startLength = 4;

    [Header("Vzhled (neon)")]
    public Color headColor = new Color(0.1f, 1f, 0.6f);   // barva u hlavy
    public Color tailColor = new Color(0.1f, 0.3f, 1f);   // barva na konci ocasu
    public Color emissionColor = new Color(0.2f, 1.2f, 0.7f); // záře (HDR)
    public float headGlow = 1.8f;                            // síla záře u hlavy
    public float tailGlow = 1f;                          // síla záře na ocasu
    public float bodySize = 0.8f;                          // velikost kuliček těla
    public float tailSize = 0.5f;                          // velikost poslední kuličky
    public bool smoothMovement = true;                     // plynulý pohyb mezi políčky
    public TrailRenderer boostTrail;                       // svítící stopa při zrychlení
    public Material tailMaterial;                          // materiál chlupatého ocasu (DELTA / MIKU)
    public Material tailFurMaterial;                       // měkká "srst" kolem článků ocasu
    public CameraRig cameraRig;                            // přepínání pohledu (C)

    DeltaGirlCrawler character;
    SnakeTailTube tailTube;
    bool firstPersonHide;
    string characterLook; // "delta" / "miku" / null
    bool HasCharacter => !string.IsNullOrEmpty(characterLook);

    readonly List<Vector2Int> cells = new List<Vector2Int>();   // buňky hada (0 = hlava)
    readonly List<Transform> segments = new List<Transform>();  // 3D objekty článků
    readonly List<Vector3> fromPos = new List<Vector3>();       // odkud článek jede
    readonly List<Vector3> toPos = new List<Vector3>();         // kam článek jede
    Vector2Int dir = Vector2Int.up, nextDir = Vector2Int.up;
    float timer, speedBoostTimer, interval;
    float shieldTimer;                            // jak dlouho ještě platí štít
    int pendingGrow;
    bool alive;
    MaterialPropertyBlock block;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public IReadOnlyList<Vector2Int> Cells => cells;
    public IReadOnlyList<Transform> Segments => segments;  // 3D články (0 = hlava)
    public Vector2Int Direction => dir;          // aktuální směr pohybu
    public bool IsBoosted => speedBoostTimer > 0; // běží zrychlení?
    public bool IsAlive => alive;
    public bool IsShielded => shieldTimer > 0;   // má had štít? (projede překážkou i sám sebou)
    public float ShieldLeft => shieldTimer;
    public float BoostLeft => speedBoostTimer;   // zbývající čas turba

    void Start()
    {
        if (!cameraRig) cameraRig = FindObjectOfType<CameraRig>();
        if (cameraRig) cameraRig.onViewChanged += ApplyCameraView;
    }

    // Zapne štít na "seconds" sekund.
    public void Shield(float seconds) => shieldTimer = seconds;

    // Nastaví další směr (používá ho demo režim SnakeAutoplay). Otočka o 180° se ignoruje.
    public void SetDirection(Vector2Int d) { if (d != -dir) nextDir = d; }

    public void ResetSnake()
    {
        foreach (var s in segments) if (s != null && s != transform) Destroy(s.gameObject);
        segments.Clear(); cells.Clear(); fromPos.Clear(); toPos.Clear();
        var start = new Vector2Int(arena.width / 2, arena.height / 2 - 3);
        dir = nextDir = Vector2Int.up;
        cells.Add(start); segments.Add(transform);
        var p = arena.CellToWorld(start);
        fromPos.Add(p); toPos.Add(p);
        transform.position = p;
        transform.rotation = Quaternion.LookRotation(Vector3.forward);
        for (int i = 1; i < startLength; i++) AddSegment(start - dir * i);
        pendingGrow = 0; speedBoostTimer = 0; shieldTimer = 0; timer = 0; interval = stepInterval; alive = true;
        if (boostTrail) { boostTrail.emitting = false; boostTrail.Clear(); }
        ApplySkin(SkinCatalog.Current());
    }

    // Nastaví barvy podle vybraného skinu a hned je aplikuje na hlavu i tělo.
    // customHeadId ("delta" / "miku") zapne model postavy a chlupatý ocas (jen vzhled).
    public void ApplySkin(SkinData skin)
    {
        if (skin == null) return;
        headColor = skin.headColor;
        tailColor = skin.bodyColor;
        emissionColor = skin.emissionColor;
        headGlow = skin.headGlow;
        tailGlow = skin.bodyGlow;
        if (!string.IsNullOrEmpty(skin.customHeadId)) EnableCharacterLook(skin.customHeadId);
        else DisableCharacterLook();
        UpdateLook();
    }

    void EnableCharacterLook(string lookId)
    {
        characterLook = lookId;
        foreach (var c in GetComponentsInChildren<DeltaGirlCrawler>(true))
            c.gameObject.SetActive(c.lookId == lookId);

        character = null;
        foreach (var c in GetComponentsInChildren<DeltaGirlCrawler>(true))
            if (c.lookId == lookId) { character = c; break; }

        if (character == null)
        {
            string res = lookId == "miku" ? "MikuGirl" : "DeltaGirl";
            var prefab = Resources.Load<GameObject>(res);
            if (prefab) character = DeltaGirlCrawler.Attach(transform, prefab, lookId);
            else Debug.LogWarning("[Snake3D] Chybí Resources/" + res + ".prefab – skin " + lookId + " bez postavy.");
        }
        else
        {
            character.gameObject.SetActive(true);
            character.FitToHead();
        }
        SetDefaultHeadVisible(false);
        for (int i = 1; i < segments.Count; i++) StyleTailSegment(segments[i], true);
    }

    void DisableCharacterLook()
    {
        characterLook = null;
        foreach (var c in GetComponentsInChildren<DeltaGirlCrawler>(true))
            c.gameObject.SetActive(false);
        character = null;
        SetDefaultHeadVisible(true);
        for (int i = 1; i < segments.Count; i++) StyleTailSegment(segments[i], false);
    }

    // Schová (nebo zase ukáže) hadí hlavu z primitiv. Šipka k jídlu a turbo stopa zůstávají.
    void SetDefaultHeadVisible(bool vis)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is TrailRenderer) continue;
            if (IsUnder(r.transform, "DeltaGirl")) continue;
            if (IsUnder(r.transform, "MikuGirl")) continue;
            if (IsUnder(r.transform, "FoodArrow")) continue;
            if (IsUnder(r.transform, "BoostTrail")) continue;
            if (IsUnder(r.transform, "FurHalo")) continue;
            r.enabled = vis;
        }
    }

    static bool IsUnder(Transform t, string name)
    {
        while (t != null)
        {
            if (t.name == name) return true;
            t = t.parent;
        }
        return false;
    }

    void AddSegment(Vector2Int c)
    {
        // článek těla = koule (vypadá oblejší než kostka)
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SnakeBody";
        Destroy(go.GetComponent<Collider>());
        if (bodyMaterial) go.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        var p = arena.CellToWorld(c);
        go.transform.position = p;
        cells.Add(c); segments.Add(go.transform); fromPos.Add(p); toPos.Add(p);
        if (HasCharacter) StyleTailSegment(go.transform, true);
    }

    public void Stop() => alive = false;
    public void Resume() => alive = true;
    public void Grow(int n) => pendingGrow += n;
    public void SpeedBoost(float seconds) => speedBoostTimer = seconds;

    void Update()
    {
        if (!alive) return;
        ReadInput();
        if (speedBoostTimer > 0) speedBoostTimer -= Time.deltaTime;
        if (shieldTimer > 0)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer > 0) RainbowLook();   // při štítu had hraje všemi barvami
            else UpdateLook();                    // štít skončil -> normální barvy
        }
        if (boostTrail) boostTrail.emitting = IsBoosted; // stopa jen při zrychlení
        interval = speedBoostTimer > 0 ? stepInterval * 0.6f : stepInterval;
        timer += Time.deltaTime;
        if (timer >= interval) { timer -= interval; Step(); }
        if (!alive) return;

        // Plynulý pohyb: t = jak daleko jsme mezi dvěma kroky (0 = začátek, 1 = konec).
        float t = smoothMovement ? Mathf.Clamp01(timer / interval) : 1f;
        for (int i = 0; i < segments.Count; i++)
        {
            Vector3 p = Vector3.Lerp(fromPos[i], toPos[i], t);
            // jemné vlnění těla do stran (sinus), hlava (i = 0) zůstává rovně
            if (i > 0)
            {
                Vector3 along = toPos[i - 1] - toPos[i]; along.y = 0;
                if (along.sqrMagnitude > 0.001f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
                    float wave = 0.07f * Mathf.Min(1f, i / 3f);
                    // u postavy první články skoro bez vlny, ať ocas plynule navazuje na boky
                    if (HasCharacter) wave *= Mathf.SmoothStep(0f, 1f, (i - 1f) / 3f);
                    p += side * Mathf.Sin(Time.time * 7f - i * 0.7f) * wave;
                }
            }
            if (HasCharacter && i > 0)
            {
                // ocas táhne při zemi a články jsou jako zaoblené kapsle ve směru těla
                float u = segments.Count > 2 ? (i - 1f) / (segments.Count - 2f) : 0f;
                p.y = Mathf.Lerp(0.48f, 0.14f, u); // u boků výš, ať ocas navazuje na sukni
                segments[i].position = p;
                Vector3 along = (i > 0 ? (i - 1 < toPos.Count ? toPos[Mathf.Max(0, i - 1)] : p) - p : Vector3.forward);
                along.y = 0;
                if (along.sqrMagnitude < 0.0001f) along = new Vector3(dir.x, 0, dir.y);
                if (along.sqrMagnitude > 0.0001f)
                    segments[i].rotation = Quaternion.Slerp(segments[i].rotation, Quaternion.LookRotation(along.normalized), 12f * Time.deltaTime);
            }
            else segments[i].position = p;
        }

        // Hlava se plynule natáčí ve směru pohybu.
        var look = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 15f * Time.deltaTime);
    }

    void LateUpdate()
    {
        // po napozicování postavy: trubka začíná u aktuálních boků a vede za hada
        if (alive) RebuildTailTube();
    }

    public bool UsesRelativeTurn => cameraRig && cameraRig.View != CameraView.Overview;

    void ReadInput()
    {
        Vector2Int d = nextDir;
        bool rel = UsesRelativeTurn;
        // v 3. / 1. osobě: vlevo/vpravo zatáčí vzhledem k aktuálnímu směru hada
        Vector2Int fwd = dir;
        Vector2Int left = new Vector2Int(-fwd.y, fwd.x);
        Vector2Int right = new Vector2Int(fwd.y, -fwd.x);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) d = rel ? fwd : Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) d = rel ? -fwd : Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) d = rel ? left : Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) d = rel ? right : Vector2Int.right;
        if (d != -dir) nextDir = d; // nelze se otočit o 180°
    }

    public void ApplyCameraView(CameraView view)
    {
        bool fp = view == CameraView.FirstPerson;
        firstPersonHide = fp;
        if (fp)
        {
            if (HasCharacter && character) character.SetRenderersVisible(false);
            else SetDefaultHeadVisible(false);
        }
        else
        {
            if (HasCharacter && character)
            {
                character.SetRenderersVisible(true);
                SetDefaultHeadVisible(false);
            }
            else SetDefaultHeadVisible(true);
        }
        UpdateLook();
    }

    void OnDestroy()
    {
        if (tailTube) Destroy(tailTube.gameObject);
    }

    void RebuildTailTube()
    {
        if (tailTube == null)
        {
            var go = new GameObject("TailTube");
            // ve světových souřadnicích – ne jako dítě hlavy, jinak se trubka otočí před postavu
            if (arena) go.transform.SetParent(arena.transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            tailTube = go.AddComponent<SnakeTailTube>();
        }
        // držet identitu, ať vertex pozice (world) zůstanou na místě
        tailTube.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        int n = segments.Count;
        int start = HasCharacter ? 2 : 1; // hlava (a u postavy sukně) zůstane mimo trubku
        var pts = new System.Collections.Generic.List<Vector3>(n + 1);
        var rads = new System.Collections.Generic.List<float>(n + 1);
        if (HasCharacter)
        {
            Vector3 hip = character ? character.HipWorldPosition() : transform.position;
            Vector3 fwd = new Vector3(dir.x, 0f, dir.y);
            if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 1e-6f) fwd.Normalize();
            // začátek u boků, mírně vzadu, ať trubka vede ZA směr pohybu
            pts.Add(hip - fwd * 0.08f);
            rads.Add(Mathf.Max(0.16f, bodySize * 0.58f));
        }
        if (n - start < 2 && pts.Count == 0) { tailTube.Hide(); return; }
        for (int i = start; i < n; i++)
        {
            pts.Add(segments[i].position);
            float s = segments[i].localScale.x;
            rads.Add(Mathf.Max(0.08f, s * 0.52f));
        }
        if (pts.Count < 2) { tailTube.Hide(); return; }
        var mat = (HasCharacter && tailMaterial) ? tailMaterial : bodyMaterial;
        Color emit = emissionColor * (HasCharacter ? 0.7f : 1f);
        if (HasCharacter && characterLook == "miku") emit = new Color(0.25f, 1.1f, 1.0f);
        else if (HasCharacter) emit = new Color(0.7f, 0.35f, 1.1f);
        tailTube.Rebuild(pts, rads, mat, emit, HasCharacter, characterLook == "miku");
    }

    void Step()
    {
        dir = nextDir;
        Vector2Int head = cells[0] + dir;
        // se štítem had projede překážkou i vlastním tělem (do zdi ale narazí vždy)
        bool blocked = gameManager.IsObstacleCell(head) || HitsSelf(head);
        if (!arena.Inside(head) || (blocked && !IsShielded))
        {
            // dokonči pohyb do posledního políčka, ať had nezůstane "napůl"
            for (int i = 0; i < segments.Count; i++) segments[i].position = toPos[i];
            Debug.Log($"[Snake3D] Náraz v buňce {head}: zeď={!arena.Inside(head)} překážka={gameManager.IsObstacleCell(head)} tělo={HitsSelf(head)}");
            gameManager.GameOver("Narazil jsi!");
            return;
        }
        if (pendingGrow > 0) { AddSegment(cells[cells.Count - 1]); pendingGrow--; }
        else if (pendingGrow < 0 && cells.Count > 2)
        {
            int last = segments.Count - 1;
            Destroy(segments[last].gameObject);
            segments.RemoveAt(last); cells.RemoveAt(last); fromPos.RemoveAt(last); toPos.RemoveAt(last);
            pendingGrow++;
        }
        else if (pendingGrow < 0) pendingGrow = 0;

        // každý článek jede z místa, kde je teď, na místo článku před ním
        for (int i = 0; i < cells.Count; i++) fromPos[i] = arena.CellToWorld(cells[i]);
        for (int i = cells.Count - 1; i > 0; i--) cells[i] = cells[i - 1];
        cells[0] = head;
        for (int i = 0; i < cells.Count; i++) toPos[i] = arena.CellToWorld(cells[i]);
        if (!smoothMovement) for (int i = 0; i < segments.Count; i++) segments[i].position = toPos[i];

        UpdateLook();
        // při zrychlení za hlavou "jiskří" žluté částice
        if (IsBoosted && gameManager.fx) gameManager.fx.Burst(fromPos[0], new Color(3f, 2.2f, 0.4f), 8, 0.18f);
        gameManager.OnSnakeMoved(head);
    }

    // Obarví hlavu i tělo podle aktuálního skinu: od hlavy k ocasu a ocas zúží.
    void UpdateLook()
    {
        if (block == null) block = new MaterialPropertyBlock();
        int n = segments.Count;
        if (HasCharacter)
        {
            if (n > 0) SetDefaultHeadVisible(false);
            bool miku = characterLook == "miku";
            Color root = miku ? new Color(0.05f, 0.07f, 0.08f) : new Color(0.06f, 0.02f, 0.10f);
            Color tip  = miku ? new Color(0.12f, 0.62f, 0.58f) : new Color(0.45f, 0.28f, 0.62f);
            Color etip = miku ? new Color(0.35f, 1.4f, 1.25f) : new Color(1.1f, 0.55f, 1.5f);
            for (int i = 1; i < n; i++)
            {
                float t = n > 2 ? (i - 1f) / (n - 2f) : 0f;       // 0 = u boků, 1 = špička
                // DELTA: fialovo-černý ocas; MIKU: tmavý s tyrkysovým lemem ke špičce
                Color c = Color.Lerp(root, tip, t * t);
                Color e = Color.Lerp(emissionColor * 0.35f, etip, t);
                float glow = Mathf.Lerp(headGlow * 0.45f, tailGlow * 1.1f, t);
                TintPart(segments[i], c, e, glow, false);
                float rad = Mathf.Lerp(bodySize * 1.05f, tailSize * 0.42f, t);
                segments[i].localScale = new Vector3(rad, rad, rad * Mathf.Lerp(1.35f, 0.85f, t));
                var halo = segments[i].Find("FurHalo");
                // první článek je u sukně (mezi hlavou a boky) – schováme ho, ať netrčí dírou ve šatech
                // trubka nahradí tečky; 1. článek je pod sukní
                bool hide = i >= 1;
                var sr = segments[i].GetComponent<Renderer>();
                if (sr) sr.enabled = !hide;
                if (halo)
                {
                    halo.localScale = Vector3.one * 1.28f;
                    TintPart(halo, c, e * 0.7f, glow * 0.5f, false);
                    var hr = halo.GetComponent<Renderer>();
                    if (hr) hr.enabled = false;
                }
            }
            RebuildTailTube();
            return;
        }
        foreach (var sgm in segments)
        {
            var r = sgm.GetComponent<Renderer>();
            if (r) r.enabled = true;
        }
        if (n > 0) TintPart(segments[0], headColor, emissionColor, headGlow, true);
        for (int i = 1; i < n; i++)
        {
            float t = n > 2 ? (i - 1f) / (n - 2f) : 0f;           // 0 = u hlavy, 1 = konec ocasu
            Color c = Color.Lerp(headColor, tailColor, t);
            Color e = Color.Lerp(emissionColor, emissionColor * 0.55f, t);
            float glow = Mathf.Lerp(headGlow, tailGlow, t);
            TintPart(segments[i], c, e, glow, false);
            segments[i].localScale = Vector3.one * Mathf.Lerp(bodySize, tailSize, t);
            // tělo kreslí trubka, koule schováme (pozice zůstávají)
            var r = segments[i].GetComponent<Renderer>();
            if (r) r.enabled = false;
        }
        if (firstPersonHide) SetDefaultHeadVisible(false);
    }

    // Přepne článek těla na chlupatý ocas (nebo zpět na obyčejnou kouli).
    void StyleTailSegment(Transform seg, bool fur)
    {
        if (!seg) return;
        var halo = seg.Find("FurHalo");
        if (fur)
        {
            if (tailMaterial)
            {
                var r = seg.GetComponent<Renderer>();
                if (r) r.sharedMaterial = tailMaterial;
            }
            if (halo == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "FurHalo";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(seg, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 1.28f;
                var r = go.GetComponent<Renderer>();
                if (tailFurMaterial) r.sharedMaterial = tailFurMaterial;
                halo = go.transform;
            }
            halo.gameObject.SetActive(true);
        }
        else if (halo) Destroy(halo.gameObject);
    }

    // Obarví renderer (u hlavy i čumák, oči a obočí nechá být).
    void TintPart(Transform part, Color surface, Color emit, float glow, bool isHead)
    {
        if (block == null) block = new MaterialPropertyBlock();
        void Apply(Renderer r)
        {
            if (!r || r is TrailRenderer) return;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, surface * 0.3f);
            block.SetColor(EmissionId, emit * glow);
            r.SetPropertyBlock(block);
        }
        if (isHead)
        {
            Apply(part.GetComponent<Renderer>());
            for (int i = 0; i < part.childCount; i++)
            {
                var ch = part.GetChild(i);
                if (ch.name == "Snout") Apply(ch.GetComponent<Renderer>());
            }
        }
        else Apply(part.GetComponent<Renderer>());
    }

    // Duhové barvy (štít): každý článek má jiný odstín a barvy se posouvají v čase.
    void RainbowLook()
    {
        if (block == null) block = new MaterialPropertyBlock();
        int start = HasCharacter ? 1 : 0;
        for (int i = start; i < segments.Count; i++)
        {
            var r = segments[i].GetComponent<Renderer>();
            if (!r) continue;
            Color c = Color.HSVToRGB((Time.time * 0.8f + i * 0.07f) % 1f, 0.7f, 1f);
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, c * 2.2f);
            r.SetPropertyBlock(block);
            var halo = segments[i].Find("FurHalo");
            if (halo)
            {
                var hr = halo.GetComponent<Renderer>();
                if (hr)
                {
                    hr.GetPropertyBlock(block);
                    block.SetColor(EmissionId, c * 1.4f);
                    hr.SetPropertyBlock(block);
                }
            }
        }
        // blikání v poslední sekundě = varování, že štít končí
        bool blink = shieldTimer < 1f && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
        for (int i = start; i < segments.Count; i++)
        {
            var r = segments[i].GetComponent<Renderer>();
            if (r) r.enabled = !blink || i == 0;
            var halo = segments[i].Find("FurHalo");
            if (halo)
            {
                var hr = halo.GetComponent<Renderer>();
                if (hr) hr.enabled = !blink;
            }
        }
    }

    bool HitsSelf(Vector2Int c)
    {
        for (int i = 0; i < cells.Count - 1; i++) if (cells[i] == c) return true; // ocas se posune
        return false;
    }

    public bool Occupies(Vector2Int c) => cells.Contains(c);
}
