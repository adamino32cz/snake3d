using UnityEngine;
using TMPro;

// Plovoucí text ve 3D (např. "+10" nad snězeným jídlem). Stoupá nahoru, zvětší se a zmizí.
public class FloatingText : MonoBehaviour
{
    public float lifetime = 1.2f;     // jak dlouho je text vidět (s)
    public float riseSpeed = 2.5f;  // jak rychle stoupá
    TextMeshPro text;
    float age;

    // Vytvoří nový plovoucí text. Volá ho GameManager.
    public static void Spawn(string message, Vector3 pos, Color color, Material fontMaterial)
    {
        var go = new GameObject("FloatingText");
        go.layer = 2;   // vrstva "Ignore Raycast" – minimapa ji nekreslí
        go.transform.position = pos + Vector3.up * 1.2f;
        if (Camera.main) go.transform.rotation = Camera.main.transform.rotation; // text se dívá do kamery
        var t = go.AddComponent<TextMeshPro>();
        t.font = TMP_Settings.defaultFontAsset;
        if (fontMaterial) t.fontSharedMaterial = fontMaterial;
        t.text = message; t.fontSize = 14; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        go.AddComponent<FloatingText>().text = t;
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = age / lifetime;                                  // 0..1
        transform.position += Vector3.up * riseSpeed * (1 - k) * Time.deltaTime; // nahoru, postupně zpomalí
        transform.localScale = Vector3.one * (0.6f + 0.6f * Mathf.Min(1, k * 5)); // rychle "vyskočí"
        text.alpha = 1 - k * k;                                    // zprůhlední
        if (age >= lifetime) Destroy(gameObject);
    }
}
