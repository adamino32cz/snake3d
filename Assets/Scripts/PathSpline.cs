using System.Collections.Generic;
using UnityEngine;

// Catmull-Rom vzorkování cesty (hladké zatáčky místo lomené mřížky).
public static class PathSpline
{
    public static void CatmullRom(IList<Vector3> src, List<Vector3> dst, int perSeg)
    {
        dst.Clear();
        int n = src == null ? 0 : src.Count;
        if (n == 0) return;
        if (n == 1) { dst.Add(src[0]); return; }
        perSeg = Mathf.Max(2, perSeg);
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = src[Mathf.Max(i - 1, 0)];
            Vector3 p1 = src[i];
            Vector3 p2 = src[i + 1];
            Vector3 p3 = src[Mathf.Min(i + 2, n - 1)];
            int start = (i == 0) ? 0 : 1;
            for (int k = start; k <= perSeg; k++)
            {
                float t = k / (float)perSeg;
                dst.Add(Eval(p0, p1, p2, p3, t));
            }
        }
    }

    public static void CatmullRom(IList<Vector3> src, IList<float> srcRads, List<Vector3> dst, List<float> dstRads, int perSeg)
    {
        dst.Clear();
        dstRads.Clear();
        int n = src == null ? 0 : src.Count;
        if (n == 0) return;
        if (n == 1)
        {
            dst.Add(src[0]);
            dstRads.Add(srcRads != null && srcRads.Count > 0 ? srcRads[0] : 0.1f);
            return;
        }
        perSeg = Mathf.Max(2, perSeg);
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = src[Mathf.Max(i - 1, 0)];
            Vector3 p1 = src[i];
            Vector3 p2 = src[i + 1];
            Vector3 p3 = src[Mathf.Min(i + 2, n - 1)];
            float r1 = srcRads != null && i < srcRads.Count ? srcRads[i] : 0.1f;
            float r2 = srcRads != null && i + 1 < srcRads.Count ? srcRads[i + 1] : r1;
            int start = (i == 0) ? 0 : 1;
            for (int k = start; k <= perSeg; k++)
            {
                float t = k / (float)perSeg;
                dst.Add(Eval(p0, p1, p2, p3, t));
                dstRads.Add(Mathf.Lerp(r1, r2, t));
            }
        }
    }

    public static Vector3 Eval(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
