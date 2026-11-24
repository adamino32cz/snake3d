using UnityEngine;

// Kamera: přehled (výchozí shora) / 3. osoba / 1. osoba. Přepíná se klávesou C.
public enum CameraView { Overview, ThirdPerson, FirstPerson }

[DefaultExecutionOrder(50)]
public class CameraRig : MonoBehaviour
{
    public Transform target;          // koho sledovat (hlava hada)
    public float follow = 0.15f;      // 0 = kamera stojí, 1 = jede přesně za hadem
    public float smooth = 3f;         // jak plynule se kamera posouvá
    public bool orbit;                // true = kamera pomalu krouží kolem arény (úvodní obrazovka)
    public float orbitSpeed = 12f;    // stupně za sekundu

    public CameraView View { get; private set; } = CameraView.Overview;
    public static string ViewLabel(CameraView v) =>
        v == CameraView.Overview ? "přehled" : v == CameraView.ThirdPerson ? "3. osoba" : "1. osoba";

    public System.Action<CameraView> onViewChanged;

    Vector3 startPos, followOffset;
    Quaternion startRot;
    float orbitWeight, orbitAngle;
    float shakeTime, shakeDuration, shakeStrength;
    Vector3 chasePos;
    Quaternion chaseRot;
    bool chaseInit;

    void Awake() { startPos = transform.position; startRot = transform.rotation; }

    public void Shake(float duration, float strength)
    {
        shakeDuration = shakeTime = duration;
        shakeStrength = strength;
    }

    public void CycleView()
    {
        SetView((CameraView)(((int)View + 1) % 3));
    }

    public void SetView(CameraView v)
    {
        if (View == v) return;
        View = v;
        chaseInit = false;
        onViewChanged?.Invoke(View);
    }

    static Vector3 FlatForward(Transform t)
    {
        Vector3 f = t ? t.forward : Vector3.forward;
        f.y = 0f;
        return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
    }

    Vector3 EyePosition()
    {
        if (!target) return transform.position;
        var girl = target.GetComponentInChildren<DeltaGirlCrawler>();
        if (girl && girl.isActiveAndEnabled) return girl.EyeWorldPosition();
        return target.position + Vector3.up * 0.28f + FlatForward(target) * 0.12f;
    }

    void LateUpdate()
    {
        Vector3 shake = Vector3.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            float k = shakeDuration > 0f ? shakeTime / shakeDuration : 0f;
            shake = Random.insideUnitSphere * shakeStrength * k;
        }

        if (View != CameraView.Overview && target && !orbit)
        {
            Vector3 fwd = FlatForward(target);
            Vector3 wantPos;
            Quaternion wantRot;
            if (View == CameraView.ThirdPerson)
            {
                // za lezoucí postavou, níž, pohled dopředu podél těla a ocasu
                wantPos = target.position - fwd * 5.8f + Vector3.up * 1.55f;
                Vector3 look = target.position + Vector3.up * 0.40f + fwd * 1.5f;
                wantRot = Quaternion.LookRotation((look - wantPos).normalized, Vector3.up);
            }
            else
            {
                wantPos = EyePosition();
                wantRot = Quaternion.LookRotation(fwd, Vector3.up);
            }
            if (!chaseInit)
            {
                chasePos = wantPos;
                chaseRot = wantRot;
                chaseInit = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
                chasePos = Vector3.Lerp(chasePos, wantPos, k);
                chaseRot = Quaternion.Slerp(chaseRot, wantRot, k);
            }
            transform.SetPositionAndRotation(chasePos + shake, chaseRot);
            return;
        }

        if (target)
        {
            Vector3 wanted = new Vector3(target.position.x, 0f, target.position.z) * follow;
            followOffset = Vector3.Lerp(followOffset, wanted, smooth * Time.deltaTime);
        }
        Vector3 pos = startPos + followOffset + shake;
        Quaternion rot = startRot;
        orbitWeight = Mathf.MoveTowards(orbitWeight, orbit ? 1f : 0f, Time.deltaTime * 1.5f);
        if (orbit) orbitAngle += orbitSpeed * Time.deltaTime;
        else if (orbitWeight <= 0f) orbitAngle = 0f;
        if (orbitWeight > 0f)
        {
            var turn = Quaternion.Euler(0f, orbitAngle, 0f);
            float w = Mathf.SmoothStep(0f, 1f, orbitWeight);
            pos = Vector3.Lerp(pos, turn * startPos, w);
            rot = Quaternion.Slerp(rot, turn * startRot, w);
        }
        transform.SetPositionAndRotation(pos, rot);
    }
}
