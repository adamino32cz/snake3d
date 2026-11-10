using UnityEngine;

// Pomocná třída: převod mezi buňkou mřížky a světovou pozicí.
public class GridArena : MonoBehaviour
{
    public int width = 20;
    public int height = 20;
    public float cellSize = 1f;

    public Vector3 CellToWorld(Vector2Int c, float y = 0.5f)
    {
        float ox = -(width - 1) * cellSize * 0.5f;
        float oz = -(height - 1) * cellSize * 0.5f;
        return new Vector3(ox + c.x * cellSize, y, oz + c.y * cellSize);
    }

    public Vector2Int WorldToCell(Vector3 p)
    {
        float ox = -(width - 1) * cellSize * 0.5f;
        float oz = -(height - 1) * cellSize * 0.5f;
        return new Vector2Int(Mathf.RoundToInt((p.x - ox) / cellSize), Mathf.RoundToInt((p.z - oz) / cellSize));
    }

    public bool Inside(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;
}
