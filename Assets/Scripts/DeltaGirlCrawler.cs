using System.Collections.Generic;
using UnityEngine;

// Tajný skin DELTA: anime postava leží na břiše na místě hlavy hada a plazí se
// dopředu. Články těla hada tvoří její rostoucí ocas (to řeší SnakeController).
// Čistě vizuální – kolize a logika hada se nemění.
public class DeltaGirlCrawler : MonoBehaviour
{
    public float modelScale = 0.72f;
    public float hipBack = 0.35f;       // boky kousek za středem políčka (směrem k ocasu)
    public float hipHeight = 0.22f;     // výška boků nad podlahou
    public float crawlSpeed = 8f;
    public float headLift = 62f;        // zvednutí hlavy, ať je vidět obličej a uši
    public float chestLift = 22f;

    Transform hips, spine, chest, upperChest, neck, head;
    Transform lUpper, lLower, rUpper, rLower;
    Transform lUpperLeg, lLowerLeg, rUpperLeg, rLowerLeg;
    Quaternion hips0, spine0, chest0, upperChest0, neck0, head0;
    Quaternion lUpper0, lLower0, rUpper0, rLower0;
    Quaternion lUpperLeg0, lLowerLeg0, rUpperLeg0, rLowerLeg0;
    bool mapped;
    int fitFrames = 4;

    public static DeltaGirlCrawler Attach(Transform headParent, GameObject prefab)
    {
        if (!headParent || !prefab) return null;
        var existing = headParent.GetComponentInChildren<DeltaGirlCrawler>(true);
        if (existing)
        {
            existing.gameObject.SetActive(true);
            existing.fitFrames = 4;
            existing.FitToHead();
            return existing;
        }
        var go = Instantiate(prefab, headParent, false);
        go.name = "DeltaGirl";
        var c = go.GetComponent<DeltaGirlCrawler>() ?? go.AddComponent<DeltaGirlCrawler>();
        c.FitToHead();
        return c;
    }

    public void FitToHead()
    {
        transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = Vector3.one * modelScale;
        transform.localPosition = new Vector3(0f, -0.2f, 0f);
        fitFrames = 4;
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

    void LateUpdate()
    {
        if (!mapped || !isActiveAndEnabled) return;
        ApplyCrawlPose();
        if (fitFrames > 0)
        {
            AutoFit();
            fitFrames--;
        }
    }

    void ApplyCrawlPose()
    {
        // Bez škálování kostí (to roztrhá mesh) a bez kumulativního otáčení vlasů.
        // Nohy rovně dozadu do ocasu, ruce se střídavě natahují dopředu, hlava zvednutá.
        if (spine) spine.localRotation = spine0 * Quaternion.Euler(-8f, 0f, 0f);
        if (chest) chest.localRotation = chest0 * Quaternion.Euler(-chestLift * 0.5f, 0f, 0f);
        if (upperChest) upperChest.localRotation = upperChest0 * Quaternion.Euler(-chestLift, 0f, 0f);
        if (neck) neck.localRotation = neck0 * Quaternion.Euler(-headLift * 0.55f, 0f, 0f);
        if (head) head.localRotation = head0 * Quaternion.Euler(-headLift * 0.45f, 0f, 0f);

        float phase = Time.time * crawlSpeed;
        float lReach = Mathf.Sin(phase);
        float rReach = Mathf.Sin(phase + Mathf.PI);
        float sway = Mathf.Sin(phase * 0.5f) * 6f;
        if (hips) hips.localRotation = hips0 * Quaternion.Euler(8f, sway, 0f);

        // T-pose paže jdou do stran; přetočíme je dopředu k hlavě a střídavě natahujeme.
        if (lUpper) lUpper.localRotation = lUpper0 * Quaternion.Euler(10f, 0f, 70f + 18f * lReach);
        if (lLower) lLower.localRotation = lLower0 * Quaternion.Euler(0f, 0f, 50f - 20f * lReach);
        if (rUpper) rUpper.localRotation = rUpper0 * Quaternion.Euler(10f, 0f, -70f - 18f * rReach);
        if (rLower) rLower.localRotation = rLower0 * Quaternion.Euler(0f, 0f, -50f + 20f * rReach);

        // Nohy dozadu podél ocasu (skryjí se v prvních článcích).
        if (lUpperLeg) lUpperLeg.localRotation = lUpperLeg0 * Quaternion.Euler(-12f, 0f, 8f);
        if (rUpperLeg) rUpperLeg.localRotation = rUpperLeg0 * Quaternion.Euler(-12f, 0f, -8f);
        if (lLowerLeg) lLowerLeg.localRotation = lLowerLeg0 * Quaternion.Euler(8f, 0f, 0f);
        if (rLowerLeg) rLowerLeg.localRotation = rLowerLeg0 * Quaternion.Euler(8f, 0f, 0f);
    }

    void AutoFit()
    {
        // Pozici bereme z kosti boků (AABB skinned meshe z FBX může být mimo a
        // postavu by to zabořilo pod podlahu).
        if (!hips || !transform.parent) return;
        Vector3 desired = transform.parent.position - transform.parent.forward * hipBack;
        desired.y = hipHeight;
        transform.position += desired - hips.position;
    }
}
