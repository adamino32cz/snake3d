using UnityEngine;

// Custom head for secret skins DELTA (kočičí dívka) a MIKU (Tda-style Hatsune Miku).
// Výchozí pohyb: plazení po břiše (hlava vzhůru). V = varianta po čtyřech. Čistě vizuální.
[DefaultExecutionOrder(10)]
public class DeltaGirlCrawler : MonoBehaviour
{
    public string lookId = "delta";
    public float modelScale = 1.9f;     // pořád velká postava (~3 políčka na délku)
    public float hipBack = 1.25f;       // záloha; AutoFit počítá délku trupu samo
    public float hipHeight = 0.70f;     // výška boků v kleče
    public float torsoLean = 82f;       // sklon trupu k vodorovné (ze stoje)
    public float crawlSpeed = 6f;
    public float hairLengthScale = 1f;

    public const string PrefsAllFours = "Snake3D_AllFours";

    public static bool AllFours
    {
        get => PlayerPrefs.GetInt(PrefsAllFours, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefsAllFours, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ToggleAllFours()
    {
        AllFours = !AllFours;
        return AllFours;
    }

    public bool MixamoReady { get; private set; }
    Animator mixAnim;
    Avatar humanAvatar;
    const float MixamoYaw = 0f;

    public Transform HeadBone => head;

    Transform hips, spine, chest, upperChest, neck, head;
    Transform lUpper, lLower, rUpper, rLower;
    Transform lUpperLeg, lLowerLeg, rUpperLeg, rLowerLeg;
    Transform lHand, rHand, lFoot, rFoot;
    Quaternion hips0, spine0, chest0, upperChest0, neck0, head0;
    Quaternion lUpper0, lLower0, rUpper0, rLower0;
    Quaternion lUpperLeg0, lLowerLeg0, rUpperLeg0, rLowerLeg0;
    bool mapped, hairScaled, mmd;
    int fitFrames = 4;
    Transform beautyLight, rimLight, safetyShorts;

    public static DeltaGirlCrawler Attach(Transform headParent, GameObject prefab, string lookId)
    {
        if (!headParent || !prefab) return null;
        DeltaGirlCrawler found = null;
        foreach (var c in headParent.GetComponentsInChildren<DeltaGirlCrawler>(true))
        {
            if (c.lookId == lookId) found = c;
            else c.gameObject.SetActive(false);
        }
        if (found)
        {
            found.gameObject.SetActive(true);
            found.fitFrames = 4;
            found.FitToHead();
            return found;
        }
        var go = Instantiate(prefab, headParent, false);
        go.name = lookId == "miku" ? "MikuGirl" : "DeltaGirl";
        var comp = go.GetComponent<DeltaGirlCrawler>() ?? go.AddComponent<DeltaGirlCrawler>();
        comp.lookId = lookId;
        comp.FitToHead();
        return comp;
    }

    public void FitToHead()
    {
        transform.localScale = Vector3.one * modelScale;
        transform.localPosition = Vector3.zero;
        fitFrames = 8;
        EnsureLights();
        if (mapped) AlignCrawl();
    }

    void Awake()
    {
        foreach (var a in GetComponentsInChildren<Animator>(true))
            a.enabled = false;
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;
        MapBones();
        if (!mapped) return;
        hips0 = Rest(hips);
        spine0 = Rest(spine); chest0 = Rest(chest); upperChest0 = Rest(upperChest);
        neck0 = Rest(neck); head0 = Rest(head);
        lUpper0 = Rest(lUpper); lLower0 = Rest(lLower);
        rUpper0 = Rest(rUpper); rLower0 = Rest(rLower);
        lUpperLeg0 = Rest(lUpperLeg); lLowerLeg0 = Rest(lLowerLeg);
        rUpperLeg0 = Rest(rUpperLeg); rLowerLeg0 = Rest(rLowerLeg);
        LengthenHair();
        EnsureLights();
        SetupMixamoAnimator();
    }

    void SetupMixamoAnimator()
    {
        humanAvatar = null;
        foreach (var a in GetComponentsInChildren<Animator>(true))
        {
            if (a.avatar && a.avatar.isValid && a.avatar.isHuman)
            {
                humanAvatar = a.avatar;
                break;
            }
        }
        if (humanAvatar == null)
        {
            string key = lookId == "miku" ? "MikuHuman" : "DeltaHuman";
            var built = Resources.Load<Avatar>(key);
            if (built && built.isValid && built.isHuman) humanAvatar = built;
        }
        MixamoReady = humanAvatar != null;
        if (!MixamoReady)
        {
            if (mixAnim) mixAnim.enabled = false;
            return;
        }
        if (!mixAnim)
        {
            mixAnim = GetComponent<Animator>();
            if (!mixAnim) mixAnim = gameObject.AddComponent<Animator>();
        }
        mixAnim.avatar = humanAvatar;
        mixAnim.applyRootMotion = false;
        mixAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var ctrl = Resources.Load<RuntimeAnimatorController>("CrawlAnim");
        if (ctrl) mixAnim.runtimeAnimatorController = ctrl;
        ApplyCrawlStyle();
    }

    public void ApplyCrawlStyle()
    {
        bool use = MixamoReady && CrawlStyle.IsMixamoCurrent && mixAnim;
        if (!mixAnim) return;
        mixAnim.enabled = use;
        if (use)
        {
            string st = CrawlStyle.AnimatorState(CrawlStyle.Current);
            if (!string.IsNullOrEmpty(st)) mixAnim.Play(st, 0, 0f);
        }
    }

    static Quaternion Rest(Transform t) => t ? t.localRotation : Quaternion.identity;

    public Vector3 HipWorldPosition()
    {
        if (hips) return hips.position;
        return transform.position + Vector3.up * hipHeight;
    }

    // Oči níž – postava leze, hlava je u země.
    public Vector3 EyeWorldPosition()
    {
        Vector3 fwd = MoveDir();
        if (head) return head.position + Vector3.up * 0.05f + fwd * 0.16f;
        return transform.position + Vector3.up * 0.55f + fwd * 0.20f;
    }

    public void SetRenderersVisible(bool vis)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = vis;
    }

    Vector3 MoveDir()
    {
        Vector3 m = transform.parent ? transform.parent.forward : Vector3.forward;
        m.y = 0f;
        return m.sqrMagnitude > 1e-6f ? m.normalized : Vector3.forward;
    }

    void MapBones()
    {
        hips = FindBone("J_Bip_C_Hips", "腰");
        spine = FindBone("J_Bip_C_Spine", "上半身");
        chest = FindBone("J_Bip_C_Chest", "上半身");
        upperChest = FindBone("J_Bip_C_UpperChest", "上半身2");
        neck = FindBone("J_Bip_C_Neck", "首");
        head = FindBone("J_Bip_C_Head", "頭");
        lUpper = FindBone("J_Bip_L_UpperArm", "左腕");
        lLower = FindBone("J_Bip_L_LowerArm", "左ひじ");
        rUpper = FindBone("J_Bip_R_UpperArm", "右腕");
        rLower = FindBone("J_Bip_R_LowerArm", "右ひじ");
        lUpperLeg = FindBone("J_Bip_L_UpperLeg", "左足");
        lLowerLeg = FindBone("J_Bip_L_LowerLeg", "左ひざ");
        rUpperLeg = FindBone("J_Bip_R_UpperLeg", "右足");
        rLowerLeg = FindBone("J_Bip_R_LowerLeg", "右ひざ");
        lHand = FindBone("J_Bip_L_Hand", "左手首", "左手");
        rHand = FindBone("J_Bip_R_Hand", "右手首", "右手");
        lFoot = FindBone("J_Bip_L_Foot", "左足首", "左かかと");
        rFoot = FindBone("J_Bip_R_Foot", "右足首", "右かかと");
        mmd = hips && (hips.name == "腰" || hips.name == "センター" || head && head.name == "頭");
        mapped = hips && head;
        if (!mapped)
            Debug.LogWarning("[Snake3D] DeltaGirl: nenašel jsem kosti postavy. Postava se nenapozicuje.");
    }

    Transform FindBone(params string[] names)
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var n in names)
        {
            foreach (var t in all)
                if (t.name == n) return t;
        }
        return null;
    }

    void LengthenHair()
    {
        if (hairScaled || hairLengthScale <= 1.01f) return;
        hairScaled = true;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("HairJoint")) continue;
            if (t.parent && t.parent.name.StartsWith("HairJoint")) continue;
            int depth = 0;
            var x = t;
            while (x.childCount > 0 && x.GetChild(0).name.StartsWith("HairJoint"))
            { x = x.GetChild(0); depth++; }
            if (depth < 3) continue;
            var s = t.localScale;
            s.x *= hairLengthScale;
            s.y *= hairLengthScale;
            s.z *= hairLengthScale;
            t.localScale = s;
        }
    }

    void EnsureLights()
    {
        if (transform.Find("BeautyLight"))
        {
            beautyLight = transform.Find("BeautyLight");
            rimLight = transform.Find("RimLight");
            return;
        }
        bool miku = lookId == "miku";
        beautyLight = NewLight("BeautyLight", Vector3.zero,
            5.5f, 3.4f, new Color(1f, 0.96f, 0.92f)).transform;
        rimLight = NewLight("RimLight", Vector3.zero,
            4.5f, 1.8f, miku ? new Color(0.22f, 0.85f, 0.78f) : new Color(0.75f, 0.6f, 1f)).transform;
    }

    Light NewLight(string name, Vector3 localPos, float range, float intensity, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = color;
        l.shadows = LightShadows.None;
        return l;
    }

    void PlaceLights(Vector3 move)
    {
        Vector3 right = Vector3.Cross(Vector3.up, move);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();
        Vector3 mid = hips ? hips.position : transform.position;
        if (beautyLight) beautyLight.position = mid + Vector3.up * 1.35f + move * 0.9f + right * 0.55f;
        if (rimLight) rimLight.position = mid + Vector3.up * 0.9f - move * 0.8f - right * 0.45f;
    }

    void LateUpdate()
    {
        if (!mapped || !isActiveAndEnabled) return;
        transform.localScale = Vector3.one * modelScale;
        if (MixamoReady && CrawlStyle.IsMixamoCurrent)
        {
            ApplyCrawlStyle();
            PlaceAnimated();
            PlaceLights(MoveDir());
            DrapeSkirt();
            PlaceSafetyShorts();
            return;
        }
        if (mixAnim) mixAnim.enabled = false;
        transform.localRotation = Quaternion.identity;
        ResetBind();
        AlignCrawl();
        AutoFit();
        LiftHead();
        ApplyCrawlGait();
        PlaceLights(MoveDir());
        DrapeSkirt();
        PlaceSafetyShorts();
    }

    void PlaceAnimated()
    {
        Vector3 move = MoveDir();
        if (move.sqrMagnitude < 1e-8f) move = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(move, Vector3.up) * Quaternion.Euler(0f, MixamoYaw, 0f);
        Vector3 head = transform.parent ? transform.parent.position : transform.position;
        Vector3 want = head - move * 0.55f;
        want.y = 0.02f;
        if (hips)
        {
            Vector3 d = want - hips.position;
            d.y = 0f;
            transform.position += d;
        }
        else transform.position = want;
        float minY = transform.position.y;
        if (lFoot) minY = Mathf.Min(minY, lFoot.position.y);
        if (rFoot) minY = Mathf.Min(minY, rFoot.position.y);
        if (lHand) minY = Mathf.Min(minY, lHand.position.y);
        if (rHand) minY = Mathf.Min(minY, rHand.position.y);
        transform.position += Vector3.up * (0.04f - minY);
    }

    void ResetBind()
    {
        if (hips) hips.localRotation = hips0;
        if (spine) spine.localRotation = spine0;
        if (chest) chest.localRotation = chest0;
        if (upperChest) upperChest.localRotation = upperChest0;
        if (neck) neck.localRotation = neck0;
        if (head) head.localRotation = head0;
        if (lUpper) lUpper.localRotation = lUpper0;
        if (lLower) lLower.localRotation = lLower0;
        if (rUpper) rUpper.localRotation = rUpper0;
        if (rLower) rLower.localRotation = rLower0;
        if (lUpperLeg) lUpperLeg.localRotation = lUpperLeg0;
        if (lLowerLeg) lLowerLeg.localRotation = lLowerLeg0;
        if (rUpperLeg) rUpperLeg.localRotation = rUpperLeg0;
        if (rLowerLeg) rLowerLeg.localRotation = rLowerLeg0;
    }

    // Nejdřív vztyčit a otočit obličej vpřed (osvědčené), pak předklonit na čtyři.
    void AlignCrawl()
    {
        if (!hips || !head) return;
        Vector3 spineDir = head.position - hips.position;
        if (spineDir.sqrMagnitude < 1e-8f) return;
        Vector3 move = MoveDir();
        transform.rotation = Quaternion.FromToRotation(spineDir.normalized, Vector3.up) * transform.rotation;
        transform.Rotate(Vector3.up, 180f, Space.World);
        Vector3 right = Vector3.Cross(Vector3.up, move);
        float lean = AllFours ? torsoLean : 90f;
        if (right.sqrMagnitude > 1e-6f)
            transform.Rotate(right.normalized, lean, Space.World);
    }

    void LiftHead()
    {
        Vector3 move = MoveDir();
        Vector3 right = Vector3.Cross(Vector3.up, move);
        if (right.sqrMagnitude < 1e-6f) return;
        right.Normalize();
        // po předklonu hledí do země – zvedneme krk a hlavu, ať kouká dopředu
        if (neck) neck.Rotate(right, AllFours ? -52f : -48f, Space.World);
        if (head) head.Rotate(right, AllFours ? -28f : -24f, Space.World);
    }

    void AutoFit()
    {
        if (!hips || !transform.parent) return;
        Vector3 move = MoveDir();
        float torso = head ? Vector3.Distance(hips.position, head.position) : 1.2f;
        Vector3 desired = transform.parent.position - move * Mathf.Max(0.95f, torso * 0.82f);
        desired.y = AllFours ? (hipHeight > 0.2f ? hipHeight : 0.70f) : 0.24f;
        transform.position += desired - hips.position;
    }

    float GaitPhase()
    {
        float step = 0.16f;
        var snake = GetComponentInParent<SnakeController>();
        if (snake)
            step = Mathf.Max(0.08f, snake.IsBoosted ? snake.stepInterval * 0.6f : snake.stepInterval);
        // po čtyřech: cyklus za 2 políčka; plazení: pomalejší tah za 4 políčka
        float cells = AllFours ? 2f : 4f;
        return Time.time * (2f * Mathf.PI / (step * cells));
    }

    void ApplyCrawlGait()
    {
        Vector3 move = MoveDir();
        Vector3 right = Vector3.Cross(Vector3.up, move);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();

        float phase = GaitPhase();
        float lSwing = Mathf.Sin(phase);   // + = levá ruka vpřed, levá noha vzad
        float rSwing = -lSwing;

        // póly blíž k tělu, ať se nohy a lokty nerozjíždějí do stran
        float handOut = AllFours ? 0.55f : 0.18f;
        float footOut = AllFours ? 0.22f : 0.04f;
        TwoBoneReach(lUpper, lLower, lHand, lUpper0, lLower0,
            HandTarget(lUpper, move, right, -1f, lSwing),
            (lUpper ? lUpper.position : Vector3.zero) + right * -handOut + Vector3.up * 0.10f - move * 0.15f);
        TwoBoneReach(rUpper, rLower, rHand, rUpper0, rLower0,
            HandTarget(rUpper, move, right,  1f, rSwing),
            (rUpper ? rUpper.position : Vector3.zero) + right *  handOut + Vector3.up * 0.10f - move * 0.15f);

        TwoBoneReach(lUpperLeg, lLowerLeg, lFoot, lUpperLeg0, lLowerLeg0,
            FootTarget(lUpperLeg, move, right, -1f, -lSwing),
            (lUpperLeg ? lUpperLeg.position : Vector3.zero) + right * -footOut - move * 0.55f + Vector3.up * 0.08f);
        TwoBoneReach(rUpperLeg, rLowerLeg, rFoot, rUpperLeg0, rLowerLeg0,
            FootTarget(rUpperLeg, move, right,  1f, -rSwing),
            (rUpperLeg ? rUpperLeg.position : Vector3.zero) + right *  footOut - move * 0.55f + Vector3.up * 0.08f);
    }

    static Vector3 HandTarget(Transform shoulder, Vector3 move, Vector3 right, float side, float swing)
    {
        Vector3 root = shoulder ? shoulder.position : Vector3.zero;
        if (!AllFours)
        {
            return new Vector3(root.x, 0f, root.z)
                 + move * (0.22f + 0.20f * swing)
                 + right * side * 0.12f
                 + Vector3.up * 0.12f;
        }
        return new Vector3(root.x, 0f, root.z)
             + move * (0.50f + 0.36f * swing)
             + right * side * 0.34f
             + Vector3.up * 0.12f;
    }

    static Vector3 FootTarget(Transform thigh, Vector3 move, Vector3 right, float side, float swing)
    {
        Vector3 root = thigh ? thigh.position : Vector3.zero;
        if (!AllFours)
        {
            // nohy u sebe, natažené vzad, nad zemí (ne skrz podlahu)
            return new Vector3(root.x, 0f, root.z)
                 - move * 0.92f
                 + right * side * 0.03f
                 + Vector3.up * 0.12f;
        }
        return new Vector3(root.x, 0f, root.z)
             - move * (0.42f - 0.30f * swing)
             + right * side * 0.18f
             + Vector3.up * 0.12f;
    }

    // Dvoukostý dosah k cíli (světové osy, bez rolování kosti).
    static void TwoBoneReach(Transform a, Transform b, Transform c, Quaternion a0, Quaternion b0, Vector3 target, Vector3 pole)
    {
        if (!a || !b) return;
        a.localRotation = a0;
        b.localRotation = b0;
        if (!c && b.childCount > 0) c = b.GetChild(0);

        if (!c)
        {
            Vector3 along = b.position - a.position;
            if (along.sqrMagnitude < 1e-10f) return;
            Vector3 want = target - a.position;
            if (want.sqrMagnitude < 1e-10f) return;
            a.rotation = Quaternion.FromToRotation(along.normalized, want.normalized) * a.rotation;
            return;
        }

        float l1 = Vector3.Distance(a.position, b.position);
        float l2 = Vector3.Distance(b.position, c.position);
        if (l1 < 1e-5f || l2 < 1e-5f) return;

        Vector3 start = a.position;
        Vector3 toT = target - start;
        float dist = toT.magnitude;
        if (dist < 1e-5f) return;
        dist = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.05f, (l1 + l2) * 0.97f);
        Vector3 dir = toT.normalized;
        Vector3 goal = start + dir * dist;

        Vector3 n = Vector3.Cross(dir, pole - start);
        if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(dir, Vector3.up);
        if (n.sqrMagnitude < 1e-8f) n = Vector3.right;
        n.Normalize();

        float cosA = (l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist);
        float angA = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 wantUpper = Quaternion.AngleAxis(angA, n) * dir;
        Vector3 curUpper = (b.position - a.position).normalized;
        a.rotation = Quaternion.FromToRotation(curUpper, wantUpper) * a.rotation;

        Vector3 curLower = (c.position - b.position).normalized;
        Vector3 wantLower = goal - b.position;
        if (wantLower.sqrMagnitude < 1e-10f) return;
        b.rotation = Quaternion.FromToRotation(curLower, wantLower.normalized) * b.rotation;
    }

    // Tmavé „safety shorts“ pod sukní, ať 3. osoba nikdy nekouká pod šaty.
    void PlaceSafetyShorts()
    {
        if (!hips) return;
        if (!safetyShorts)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SafetyShorts";
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);
            var r = go.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (sh)
            {
                var mat = new Material(sh);
                mat.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.06f, 1f));
                if (r) r.sharedMaterial = mat;
            }
            else if (r) r.material.color = new Color(0.05f, 0.04f, 0.06f, 1f);
            if (r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            go.transform.SetParent(transform, true);
            safetyShorts = go.transform;
        }
        Vector3 move = MoveDir();
        if (move.sqrMagnitude < 1e-6f) move = Vector3.forward;
        float ls = Mathf.Max(0.01f, transform.lossyScale.x);
        if (!AllFours)
        {
            // pod sukní podél stehen, ať 3. osoba nevidí odkrytá stehna
            safetyShorts.position = hips.position - move * 0.32f + Vector3.down * 0.05f;
            safetyShorts.rotation = Quaternion.LookRotation(move, Vector3.up);
            safetyShorts.localScale = new Vector3(0.46f, 0.20f, 1.05f) / ls;
        }
        else
        {
            safetyShorts.position = hips.position + Vector3.down * 0.10f + move * 0.02f;
            safetyShorts.rotation = Quaternion.identity;
            safetyShorts.localScale = new Vector3(0.52f, 0.20f, 0.42f) / ls;
        }
    }

    // DELTA má sukňové kosti – mírně je svěsíme dolů (bez trhání).
    void DrapeSkirt()
    {
        Vector3 want = AllFours
            ? Vector3.down
            : (Vector3.down * 0.25f - MoveDir() * 1.0f);
        if (want.sqrMagnitude < 1e-6f) want = Vector3.down;
        want.Normalize();
        float maxDeg = AllFours ? 28f : 70f;
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            string n = tr.name;
            if (n.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (n.EndsWith("_end")) continue;
            if (tr.childCount == 0) continue;
            Vector3 along = tr.GetChild(0).position - tr.position;
            if (along.sqrMagnitude < 1e-8f) continue;
            float ang = Vector3.Angle(along, want);
            if (ang < 10f) continue;
            Vector3 axis = Vector3.Cross(along, want);
            if (axis.sqrMagnitude < 1e-8f) continue;
            tr.rotation = Quaternion.AngleAxis(Mathf.Min(ang, maxDeg), axis.normalized) * tr.rotation;
        }
    }

}
