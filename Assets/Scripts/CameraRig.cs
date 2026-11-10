using UnityEngine;

// Kamera: lehce sleduje hada (aby scéna působila živěji) a umí se zatřást (např. při nárazu).
public class CameraRig : MonoBehaviour
{
    public Transform target;          // koho sledovat (hlava hada)
    public float follow = 0.15f;      // 0 = kamera stojí, 1 = jede přesně za hadem
    public float smooth = 3f;         // jak plynule se kamera posouvá

    public bool orbit;                // true = kamera pomalu krouží kolem arény (úvodní obrazovka)
    public float orbitSpeed = 12f;    // stupně za sekundu
    Vector3 startPos, followOffset;
    Quaternion startRot;
    float orbitWeight, orbitAngle;
    float shakeTime, shakeDuration, shakeStrength;

    void Awake() { startPos = transform.position; startRot = transform.rotation; }

    // Zatřese kamerou. duration = jak dlouho (s), strength = jak silně.
    public void Shake(float duration, float strength)
    {
        shakeDuration = shakeTime = duration;
        shakeStrength = strength;
    }

    void LateUpdate()
    {
        if (target)
        {
            Vector3 wanted = new Vector3(target.position.x, 0f, target.position.z) * follow;
            followOffset = Vector3.Lerp(followOffset, wanted, smooth * Time.deltaTime);
        }
        Vector3 shake = Vector3.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            float k = shakeTime / shakeDuration;                  // třesení postupně slábne
            shake = Random.insideUnitSphere * shakeStrength * k;
        }
        Vector3 pos = startPos + followOffset + shake;
        Quaternion rot = startRot;
        // kroužení: plynule se zapne/vypne (orbitWeight jde od 0 do 1)
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
