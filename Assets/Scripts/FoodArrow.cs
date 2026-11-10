using UnityEngine;

// Svítící šipka nad hlavou hada, která ukazuje k nejbližšímu jídlu.
// Když je jídlo blízko (do 3 políček), šipka se schová.
public class FoodArrow : MonoBehaviour
{
    public SnakeController snake;
    public FoodSpawner spawner;
    public float height = 1.3f;       // výška nad hlavou
    public float hideDistance = 3f;   // pod touto vzdáleností šipku nepotřebujeme

    void LateUpdate()
    {
        if (!snake || !spawner) return;
        // najdeme nejbližší jídlo
        Food nearest = null; float best = float.MaxValue;
        foreach (var f in spawner.ActiveFood)
        {
            if (!f) continue;
            float d = (f.transform.position - snake.transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = f; }
        }
        bool show = nearest != null && Mathf.Sqrt(best) > hideDistance && snake.IsAlive;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = show;
        if (!show) return;

        // šipka se vznáší nad hlavou a trochu se pohupuje
        transform.position = snake.transform.position + Vector3.up * (height + Mathf.Sin(Time.time * 5f) * 0.08f);
        Vector3 dir = nearest.transform.position - snake.transform.position;
        dir.y = 0;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}
