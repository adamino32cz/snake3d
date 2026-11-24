using UnityEngine;

// Custom head for secret skins DELTA (kočičí dívka) a MIKU (přebarvený stejný CC0 model).
// Vzpřímená lamia: trup stojí, ocas hada navazuje u boků. Čistě vizuální.
public class DeltaGirlCrawler : MonoBehaviour
{
    public string lookId = "delta";
    public float modelScale = 3f;       // ~3 políčka na výšku (od boků k vlasům dominantní figura)
    public float hipBack = 1.25f;       // boky nad začátkem ocasu, ať šaty nekříží první článek
    public float hipHeight = 0.50f;     // výška boků = začátek ocasu
    public float torsoLean = 16f;       // mírný předklon
    public float crawlSpeed = 6f;
    public float hairLengthScale = 1f;  // MIKU: delší culíky

    Transform hips, spine, chest, upperChest, neck, head;
    Transform lUpper, lLower, rUpper, rLower;
    Transform lUpperLeg, lLowerLeg, rUpperLeg, rLowerLeg;
    Quaternion hips0, spine0, chest0, upperChest0, neck0, head0;
    Quaternion lUpper0, lLower0, rUpper0, rLower0;
    Quaternion lUpperLeg0, lLowerLeg0, rUpperLeg0, rLowerLeg0;
    bool mapped, hairScaled;
    int fitFrames = 4;

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
        if (mapped) AlignUpright();
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
    }

    static Quaternion Rest(Transform t) => t ? t.localRotation : Quaternion.identity;

    void MapBones()
    {
        hips = FindBone("J_Bip_C_Hips");
        spine = FindBone("J_Bip_C_Spine");
        chest = FindBone("J_Bip_C_Chest");
        upperChest = FindBone("J_Bip_C_UpperChest");
        neck = FindBone("J_Bip_C_Neck");
        head = FindBone("J_Bip_C_Head");
        lUpper = FindBone("J_Bip_L_UpperArm");
        lLower = FindBone("J_Bip_L_LowerArm");
        rUpper = FindBone("J_Bip_R_UpperArm");
        rLower = FindBone("J_Bip_R_LowerArm");
        lUpperLeg = FindBone("J_Bip_L_UpperLeg");
        lLowerLeg = FindBone("J_Bip_L_LowerLeg");
        rUpperLeg = FindBone("J_Bip_R_UpperLeg");
        rLowerLeg = FindBone("J_Bip_R_LowerLeg");
        mapped = hips && head && lUpper && rUpper;
        if (!mapped)
            Debug.LogWarning("[Snake3D] DeltaGirl: nenašel jsem kosti VRoid (J_Bip_…). Postava se nenapozicuje.");
    }

    Transform FindBone(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
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
            // Jen delší řetězy (culíky), ať se neprotáhnou ofiny / uši.
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
        if (transform.Find("BeautyLight")) return;
        bool miku = lookId == "miku";
        var key = NewLight("BeautyLight", new Vector3(0.55f, 1.55f, 0.95f),
            5.5f, 3.4f, new Color(1f, 0.96f, 0.92f));
        NewLight("RimLight", new Vector3(-0.55f, 1.25f, -0.7f),
            4.5f, 1.8f, miku ? new Color(0.22f, 0.85f, 0.78f) : new Color(0.75f, 0.6f, 1f));
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

    void LateUpdate()
    {
        if (!mapped || !isActiveAndEnabled) return;
        transform.localScale = Vector3.one * modelScale;
        transform.localRotation = Quaternion.identity;
        ApplyLamiaPose();
        AlignUpright();
        AutoFit();
    }

    void ApplyLamiaPose()
    {
        // Jemný, vzpřímený postoj – obličej mírně vzhůru (lépe vidět z kamery).
        if (spine) spine.localRotation = spine0 * Quaternion.Euler(-6f, 0f, 0f);
        if (chest) chest.localRotation = chest0 * Quaternion.Euler(-4f, 0f, 0f);
        if (upperChest) upperChest.localRotation = upperChest0 * Quaternion.Euler(-3f, 0f, 0f);
        if (neck) neck.localRotation = neck0 * Quaternion.Euler(-14f, 0f, 0f);
        if (head) head.localRotation = head0 * Quaternion.Euler(-10f, 0f, 0f);

        float phase = Time.time * crawlSpeed;
        float sway = Mathf.Sin(phase * 0.5f);
        if (hips) hips.localRotation = hips0 * Quaternion.Euler(0f, sway * 4f, 0f);

        // Uvolněné paže, lehký pohyb (ne plazení po břiše).
        // Výchozí T-pose má paže vodorovně – spustíme je podél těla (opačné Z než při zdvižení).
        if (lUpper) lUpper.localRotation = lUpper0 * Quaternion.Euler(8f, 6f, -70f + 6f * sway);
        if (lLower) lLower.localRotation = lLower0 * Quaternion.Euler(0f, 0f, 16f);
        if (rUpper) rUpper.localRotation = rUpper0 * Quaternion.Euler(8f, -6f, 70f - 6f * sway);
        if (rLower) rLower.localRotation = rLower0 * Quaternion.Euler(0f, 0f, -14f);

        // Nohy dozadu do ocasu, ať neprocházejí podlahou.
        if (lUpperLeg) lUpperLeg.localRotation = lUpperLeg0 * Quaternion.Euler(-85f, 6f, 8f);
        if (rUpperLeg) rUpperLeg.localRotation = rUpperLeg0 * Quaternion.Euler(-85f, -6f, -8f);
        if (lLowerLeg) lLowerLeg.localRotation = lLowerLeg0 * Quaternion.Euler(18f, 0f, 0f);
        if (rLowerLeg) rLowerLeg.localRotation = rLowerLeg0 * Quaternion.Euler(18f, 0f, 0f);
    }

    int _dbg;
    void AlignUpright()
    {
        if (!hips || !head) return;
        // Každý snímek od identity wrapperu, ať se rotace nesčítá.
        // Změříme skutečný směr páteře (nezávisle na osách FBX) a vztyčíme ho.
        Vector3 spine = head.position - hips.position;
        if (spine.sqrMagnitude < 1e-8f) return;
        Vector3 move = transform.parent ? transform.parent.forward : Vector3.forward;
        move.y = 0f;
        if (move.sqrMagnitude < 1e-6f) move = Vector3.forward;
        move.Normalize();
        // nejdřív páteř nahoru, pak obličej vpřed, pak mírný předklon
        transform.rotation = Quaternion.FromToRotation(spine.normalized, Vector3.up) * transform.rotation;
        transform.Rotate(Vector3.up, 180f, Space.World);
        Vector3 right = Vector3.Cross(Vector3.up, move);
        // mírný záklon: obličej jde vzhůru k vysoké herní kameře
        if (right.sqrMagnitude > 1e-6f)
            transform.Rotate(right.normalized, -torsoLean, Space.World);
        if (_dbg < 2)
        {
            Vector3 s2 = head.position - hips.position;
            Debug.Log($"[Lamia] spine0={spine} spine1={s2} hips={hips.position} head={head.position} wrap={transform.eulerAngles} move={move}");
            _dbg++;
        }
    }

    void AutoFit()
    {
        if (!hips || !transform.parent) return;
        Vector3 desired = transform.parent.position - transform.parent.forward * hipBack;
        desired.y = hipHeight;
        transform.position += desired - hips.position;
    }
}
