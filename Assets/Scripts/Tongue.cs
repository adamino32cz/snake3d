using UnityEngine;

// Hadí jazyk: občas rychle vystrčí a zase schová ("mlsání").
public class Tongue : MonoBehaviour
{
    public float minPause = 1.2f, maxPause = 2.8f;   // jak dlouho je jazyk schovaný
    float wait, t = -1f;

    void Start() { wait = Random.Range(minPause, maxPause); transform.localScale = new Vector3(1, 1, 0); }

    public bool hold;   // true = jazyk zůstane vystrčený (pro fotku v demo režimu)

    void Update()
    {
        if (hold) { transform.localScale = Vector3.one; transform.localRotation = Quaternion.identity; return; }
        if (t < 0f)
        {
            wait -= Time.deltaTime;
            if (wait <= 0f) t = 0f;               // začni mlsat
            return;
        }
        t += Time.deltaTime;
        // 0,35 s: ven a zpátky, s malým kmitáním nahoru/dolů
        float k = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI);
        transform.localScale = new Vector3(1, 1, k);
        transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 60f) * 12f, 0, 0);
        if (t >= 0.35f) { t = -1f; wait = Random.Range(minPause, maxPause); transform.localScale = new Vector3(1, 1, 0); }
    }
}
