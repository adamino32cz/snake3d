using UnityEngine;

// "Dýchající" neonová záře: síla svícení (emission) se plynule zvětšuje a zmenšuje.
// Používají ji překážky, aby vypadaly nebezpečně.
public class NeonPulse : MonoBehaviour
{
    public Color glowColor = new Color(1f, 0.25f, 0.05f);
    public float minIntensity = 1f;
    public float maxIntensity = 3.5f;
    public float speed = 4f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    Renderer[] rends;   // všechny části modelu (i podřízené objekty)
    MaterialPropertyBlock block; // změní barvu jen tomuto objektu, ne celému materiálu

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float k = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;       // číslo 0..1
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, k);
        foreach (var rend in rends)
        {
            if (!rend.enabled) continue;
            rend.GetPropertyBlock(block);
            block.SetColor(EmissionId, glowColor * intensity);     // barva * síla = HDR barva (svítí víc než bílá)
            rend.SetPropertyBlock(block);
        }
    }
}
