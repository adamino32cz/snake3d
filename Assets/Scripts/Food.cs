using UnityEngine;

// Jídlo na hrací ploše. Samo se točí a pomalu "poskakuje" nahoru a dolů,
// aby bylo dobře vidět a vypadalo živě.
public class Food : MonoBehaviour
{
    public FoodType type;
    public Vector2Int cell;          // buňka mřížky, kde jídlo leží

    [Header("Animace")]
    public float spinSpeed = 120f;   // stupně za sekundu
    public float bobHeight = 0.15f;  // jak vysoko poskakuje
    public float bobSpeed = 3f;      // jak rychle poskakuje

    Vector3 basePos, baseScale, targetPos;
    float phase, age;

    void Start()
    {
        basePos = targetPos = transform.position;
        phase = Random.value * 6.28f; // každé jídlo poskakuje v jiném rytmu
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    // Posune jídlo na jinou buňku (používá magnet).
    public void MoveTo(Vector2Int newCell, Vector3 worldPos)
    {
        cell = newCell;
        targetPos = worldPos;
    }

    void Update()
    {
        // "vyskočení" při objevení: z nuly se zvětší kousek přes plnou velikost a zpátky (0,4 s)
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / 0.4f);
        float pop = 1f + 2.7f * Mathf.Pow(k - 1f, 3) + 1.7f * Mathf.Pow(k - 1f, 2); // tzv. "ease out back"
        transform.localScale = baseScale * pop;

        // otáčení kolem svislé osy
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        // magnet: jídlo plynule "dojede" na novou buňku
        basePos = Vector3.MoveTowards(basePos, targetPos, 6f * Time.deltaTime);
        // poskakování pomocí funkce sinus (vlnka nahoru a dolů)
        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
    }
}
