using UnityEngine;

// Překážka jezdí tam a zpět mezi waypointy. Had narazí, pokud vstoupí do buňky, kterou překážka zabírá.
public class MovingObstacle : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 2f;
    int index;

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Vector3 target = waypoints[index].position;
        target.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.0001f) index = (index + 1) % waypoints.Length;
    }

    // Nastaví novou cestu (používá se při změně levelu).
    public void SetPath(Vector3 a, Vector3 b, float newSpeed)
    {
        if (waypoints == null || waypoints.Length < 2) return;
        waypoints[0].position = a; waypoints[1].position = b;
        transform.position = a;
        speed = newSpeed; index = 1;
    }

    public bool Covers(GridArena arena, Vector2Int cell)
    {
        // pokrývá buňku, ve které je střed + buňky, kterých se dotýká scale
        Vector3 p = arena.CellToWorld(cell, transform.position.y);
        Vector3 half = transform.localScale * 0.5f;
        return Mathf.Abs(p.x - transform.position.x) < half.x + 0.4f &&
               Mathf.Abs(p.z - transform.position.z) < half.z + 0.4f;
    }
}
