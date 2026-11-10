using System.Collections.Generic;
using UnityEngine;

// Pohyb hada po mřížce: každých stepInterval sekund se posune o jednu buňku.
// LOGIKA (kolize, jídlo) je pořád po políčkách. Jen OBRAZ (kuličky těla) se mezi
// políčky plynule posouvá, takže had nevypadá, že "skáče".
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
    public float headGlow = 1.8f;                            // síla záře u hlavy
    public float tailGlow = 1f;                          // síla záře na ocasu
    public float bodySize = 0.8f;                          // velikost kuliček těla
    public float tailSize = 0.5f;                          // velikost poslední kuličky
    public bool smoothMovement = true;                     // plynulý pohyb mezi políčky
    public TrailRenderer boostTrail;                       // svítící stopa při zrychlení

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
        UpdateLook();
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
                    p += side * Mathf.Sin(Time.time * 7f - i * 0.7f) * 0.07f * Mathf.Min(1f, i / 3f);
                }
            }
            segments[i].position = p;
        }

        // Hlava se plynule natáčí ve směru pohybu.
        var look = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 15f * Time.deltaTime);
    }

    void ReadInput()
    {
        Vector2Int d = nextDir;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) d = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) d = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) d = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) d = Vector2Int.right;
        if (d != -dir) nextDir = d; // nelze se otočit o 180°
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

    // Obarví tělo: od hlavy (jasná barva) k ocasu (jiná barva, slabší záře) a ocas zúží.
    void UpdateLook()
    {
        if (block == null) block = new MaterialPropertyBlock();
        int n = segments.Count;
        foreach (var sgm in segments) sgm.GetComponent<Renderer>().enabled = true;
        if (n > 0) { var hr = segments[0].GetComponent<Renderer>(); hr.GetPropertyBlock(block); block.Clear(); hr.SetPropertyBlock(block); }
        for (int i = 1; i < n; i++)
        {
            float t = n > 2 ? (i - 1f) / (n - 2f) : 0f;           // 0 = u hlavy, 1 = konec ocasu
            Color c = Color.Lerp(headColor, tailColor, t);
            float glow = Mathf.Lerp(headGlow, tailGlow, t);
            var r = segments[i].GetComponent<Renderer>();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c * 0.3f);
            block.SetColor(EmissionId, c * glow);
            r.SetPropertyBlock(block);
            segments[i].localScale = Vector3.one * Mathf.Lerp(bodySize, tailSize, t);
        }
    }

    // Duhové barvy (štít): každý článek má jiný odstín a barvy se posouvají v čase.
    void RainbowLook()
    {
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < segments.Count; i++)
        {
            var r = segments[i].GetComponent<Renderer>();
            Color c = Color.HSVToRGB((Time.time * 0.8f + i * 0.07f) % 1f, 0.7f, 1f);
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, c * 2.2f);
            r.SetPropertyBlock(block);
        }
        // blikání v poslední sekundě = varování, že štít končí
        bool blink = shieldTimer < 1f && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
        for (int i = 0; i < segments.Count; i++) segments[i].GetComponent<Renderer>().enabled = !blink || i == 0;
    }

    bool HitsSelf(Vector2Int c)
    {
        for (int i = 0; i < cells.Count - 1; i++) if (cells[i] == c) return true; // ocas se posune
        return false;
    }

    public bool Occupies(Vector2Int c) => cells.Contains(c);
}
