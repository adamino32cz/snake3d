using System.Collections.Generic;
using UnityEngine;

// Dva tyrkysové prameny vlasů podél cesty článků – tělo hada u skinu MIKU.
public class MikuHairTails : MonoBehaviour
{
    SnakeTailTube left;
    SnakeTailTube right;
    readonly List<Vector3> pts = new List<Vector3>(64);
    readonly List<float> rads = new List<float>(64);

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

        Vector3 origin = head ? head.position : path[0] + Vector3.up * 0.45f;
        Vector3 rootL = origin - rightDir * 0.11f + Vector3.up * 0.08f - fwd * 0.04f;
        Vector3 rootR = origin + rightDir * 0.11f + Vector3.up * 0.08f - fwd * 0.04f;

        BuildStrand(left, rootL, path, -1f, fwd);
        BuildStrand(right, rootR, path, 1f, fwd);
    }

    void BuildStrand(SnakeTailTube tube, Vector3 root, IList<Vector3> path, float side, Vector3 fwd)
    {
        pts.Clear();
        rads.Clear();
        pts.Add(root);
        rads.Add(0.11f);

        int n = path.Count;
        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? i / (float)(n - 1) : 0f;
            Vector3 along;
            if (i + 1 < n) along = path[i + 1] - path[i];
            else if (i > 0) along = path[i] - path[i - 1];
            else along = -fwd;
            along.y = 0f;
            if (along.sqrMagnitude < 1e-6f) along = -fwd;
            along.Normalize();
            Vector3 sideV = Vector3.Cross(Vector3.up, along);
            if (sideV.sqrMagnitude < 1e-6f) sideV = Vector3.right;
            sideV.Normalize();

            float wave = Mathf.Sin(Time.time * 2.1f + i * 0.62f + side * 1.1f) * (0.06f + 0.07f * t);
            float spread = Mathf.Lerp(0.08f, 0.20f, t);
            float lift = Mathf.Lerp(0.16f, 0.08f, t);
            Vector3 p = path[i] + sideV * (side * spread + wave) + Vector3.up * lift;
            pts.Add(p);
            rads.Add(Mathf.Lerp(0.105f, 0.045f, t));
        }

        Color emit = new Color(0.25f, 0.95f, 0.88f);
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
        return go.AddComponent<SnakeTailTube>();
    }
}
