using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    public GridArena arena;
    public SnakeController snake;
    public GameManager gameManager;
    public Food[] foodPrefabs;      // jeden prefab pro každý typ
    public int foodOnBoard = 3;

    readonly List<Food> active = new List<Food>();
    public IReadOnlyList<Food> ActiveFood => active; // jídlo na ploše (čte ho demo režim)

    public void ResetFood()
    {
        foreach (var f in active) if (f) Destroy(f.gameObject);
        active.Clear();
        for (int i = 0; i < foodOnBoard; i++) Spawn();
    }

    public Food TakeAt(Vector2Int cell)
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i] && active[i].cell == cell)
            {
                var f = active[i]; active.RemoveAt(i); Spawn(); return f;
            }
        return null;
    }

    // MAGNET: každé jídlo blíž než "radius" políček se posune o 1 políčko k hlavě hada.
    public void PullToward(Vector2Int head, int radius)
    {
        foreach (var f in active)
        {
            if (!f) continue;
            Vector2Int d = head - f.cell;
            int dist = Mathf.Abs(d.x) + Mathf.Abs(d.y);
            if (dist <= 1 || dist > radius) continue;           // už je u hlavy, nebo moc daleko
            // krok ve směru, kde je hlava dál (x nebo y)
            Vector2Int step = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? new Vector2Int(System.Math.Sign(d.x), 0) : new Vector2Int(0, System.Math.Sign(d.y));
            Vector2Int c = f.cell + step;
            if (snake.Occupies(c) || gameManager.IsObstacleCell(c) || active.Exists(o => o && o.cell == c)) continue;
            f.MoveTo(c, arena.CellToWorld(c));
        }
    }

    static string OnlyFood()
    {
        var a = CmdArgs.Get();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-onlyfood") return a[i + 1];
        return null;
    }

    void Spawn()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0) return;
        float total = 0; foreach (var p in foodPrefabs) total += p.type.spawnWeight;
        float r = Random.value * total; Food prefab = foodPrefabs[0];
        foreach (var p in foodPrefabs) { r -= p.type.spawnWeight; if (r <= 0) { prefab = p; break; } }
        // test: spuštění s "-onlyfood Magnet" vytváří jen jeden druh jídla
        string only = OnlyFood();
        if (only != null) foreach (var p in foodPrefabs) if (p.type.kind.ToString() == only) prefab = p;

        for (int tries = 0; tries < 200; tries++)
        {
            var c = new Vector2Int(Random.Range(0, arena.width), Random.Range(0, arena.height));
            if (snake.Occupies(c) || gameManager.IsObstacleCell(c) || active.Exists(f => f && f.cell == c)) continue;
            var f = Instantiate(prefab, arena.CellToWorld(c), Quaternion.identity, transform);
            f.cell = c; active.Add(f); return;
        }
    }
}
