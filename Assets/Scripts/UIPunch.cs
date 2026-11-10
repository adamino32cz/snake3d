using UnityEngine;

// Krátké "poskočení" (zvětšení a zpět) textu nebo jiného UI prvku – např. když přibydou body.
public class UIPunch : MonoBehaviour
{
    public float returnSpeed = 8f;
    float scale = 1f;

    public void Punch(float amount = 0.35f) { scale = 1f + amount; }

    void Update()
    {
        scale = Mathf.Lerp(scale, 1f, returnSpeed * Time.deltaTime); // pomalu zpátky na normální velikost
        transform.localScale = Vector3.one * scale;
    }
}
