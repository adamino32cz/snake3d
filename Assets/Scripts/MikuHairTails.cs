using System.Collections.Generic;
using UnityEngine;

// Tělo hada u MIKU: dva blízké, zploštělé tyrkysové prameny (twintaily),
// které z nadhledu i zezadu čtou jako jeden hustý vlasový ocas.
public class MikuHairTails : MonoBehaviour
{
    SnakeTailTube left;
    SnakeTailTube right;
    readonly List<Vector3> pts = new List<Vector3>(80);
    readonly List<float> rads = new List<float>(80);
    readonly List<Vector3> smooth = new List<Vector3>(80);

    public void Hide()
    {
        if (left) left.Hide();
        if (right) right.Hide();
    }

    public void Rebuild(IList<Vector3> path, Transform head, Vector3 move)
    {
        Ensure();
        if (path == null || path.Count < 2)
        {
            Hide();
            return;
        }

        Vector3 fwd = move;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 rightDir = Vector3.Cross(Vector3.up, fwd);
        if (rightDir.sqrMagnitude < 1e-6f) rightDir = Vector3.right;
        rightDir.Normalize();

        // kořeny vzadu na hlavě, blízko u sebe (ne dva oddělené sloupy)
        Vector3 origin = head ? head.position : path[0] + Vector3.up * 0.45f;
        Vector3 rootL = origin - rightDir * 0.055f + Vector3.up * 0.04f - fwd * 0.08f;
        Vector3 rootR = origin + rightDir * 0.055f + Vector3.up * 0.04f - fwd * 0.08f;

        SmoothPath(path);
        BuildStrand(left, rootL, -1f, fwd);
        BuildStrand(right, rootR, 1f, fwd);
    }

    void SmoothPath(IList<Vector3> path)
    {
        smooth.Clear();
        int n = path.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = path[i];
            if (i > 0 && i < n - 1)
                p = (path[i - 1] + path[i] * 2f + path[i + 1]) * 0.25f;
            smooth.Add(p);
        }
    }

    void BuildStrand(SnakeTailTube tube, Vector3 root, float side, Vector3 fwd)
    {
        pts.Clear();
        rads.Clear();
        pts.Add(root);
        rads.Add(0.20f);

        int n = smooth.Count;
        float time = Time.time;
        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? i / (float)(n - 1) : 0f;
            Vector3 along;
            if (i + 1 < n) along = smooth[i + 1] - smooth[i];
            else if (i > 0) along = smooth[i] - smooth[i - 1];
            else along = -fwd;
            along.y = 0f;
            if (along.sqrMagnitude < 1e-6f) along = -fwd;
            along.Normalize();
            Vector3 sideV = Vector3.Cross(Vector3.up, along);
            if (sideV.sqrMagnitude < 1e-6f) sideV = Vector3.right;
            sideV.Normalize();

            // jemné S: dlouhá vlna + menší kmit, sílí ke špičce
            float sCurve = Mathf.Sin(t * Mathf.PI * 1.35f + time * 0.7f) * (0.22f * Mathf.SmoothStep(0.05f, 1f, t));
            float micro = Mathf.Sin(t * Mathf.PI * 3.4f + time * 1.45f + side * 0.5f) * (0.04f * t);
            // prameny skoro u sebe, ať z dálky splývají v jedno tělo
            float spread = Mathf.Lerp(0.035f, 0.055f, t);
            float lift = Mathf.Lerp(0.18f, 0.09f, t * t * 0.35f + t * 0.65f);
            Vector3 p = smooth[i] + sideV * (side * spread + sCurve + micro) + Vector3.up * lift;
            // plynulý přechod z kořene hlavy do cesty článků
            if (i == 0) p = Vector3.Lerp(root, p, 0.55f);
            pts.Add(p);
            // tlustší u hlavy, měkká zužující se špička
            rads.Add(Mathf.Lerp(0.19f, 0.045f, t * t * 0.35f + t * 0.65f));
        }

        Color emit = new Color(0.40f, 0.98f, 0.92f);
        tube.Rebuild(pts, rads, null, emit, true, true, true);
    }

    void Ensure()
    {
        if (left && right) return;
        if (!left) left = Make("HairL");
        if (!right) right = Make("HairR");
    }

    SnakeTailTube Make(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var tube = go.AddComponent<SnakeTailTube>();
        tube.flattenX = 1.35f;
        tube.flattenY = 0.78f;
        tube.extra = 4;
        tube.sides = 14;
        return tube;
    }
}
