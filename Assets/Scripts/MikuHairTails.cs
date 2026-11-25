using System.Collections.Generic;
using UnityEngine;

// Tělo hada u MIKU: spousta tenkých tyrkysových pramenů po hladké Catmull-Rom křivce.
public class MikuHairTails : MonoBehaviour
{
    const int StrandCount = 18;
    const int PerSeg = 10;

    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;
    Material mat;
    static Texture2D strandTex;

    readonly List<Vector3> controls = new List<Vector3>(64);
    readonly List<Vector3> spline = new List<Vector3>(256);
    readonly List<Vector3> verts = new List<Vector3>(2048);
    readonly List<Vector3> norms = new List<Vector3>(2048);
    readonly List<Vector2> uvs = new List<Vector2>(2048);
    readonly List<int> tris = new List<int>(4096);

    public void Hide()
    {
        if (mr) mr.enabled = false;
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

        Vector3 origin = head ? head.position : path[0] + Vector3.up * 0.4f;
        Vector3 root = origin - fwd * 0.08f + Vector3.up * 0.05f;

        controls.Clear();
        controls.Add(root);
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = path[i];
            float t = path.Count > 1 ? i / (float)(path.Count - 1) : 0f;
            p.y += Mathf.Lerp(0.16f, 0.08f, t);
            controls.Add(p);
        }
        PathSpline.CatmullRom(controls, spline, PerSeg);
        if (spline.Count < 2) { Hide(); return; }

        BuildStrands();
        if (mr) mr.enabled = true;
    }

    void BuildStrands()
    {
        verts.Clear(); norms.Clear(); uvs.Clear(); tris.Clear();
        int n = spline.Count;
        float time = Time.time;

        for (int s = 0; s < StrandCount; s++)
        {
            // dvě skupiny (twintaily), prameny v malé elipse – objem, ne plošná stužka
            bool left = s < StrandCount / 2;
            int idx = s % (StrandCount / 2);
            int bunchN = StrandCount / 2;
            float ang = (idx / (float)Mathf.Max(1, bunchN)) * Mathf.PI * 2f + (left ? 0.2f : 1.1f);
            float side0 = (left ? -0.07f : 0.07f) + Mathf.Cos(ang) * 0.055f;
            float lift0 = 0.05f + Mathf.Sin(ang) * 0.048f;
            float phase = s * 0.73f;
            float width0 = 0.026f + (s % 3) * 0.005f;

            int v0 = verts.Count;
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 0f;
                Vector3 tan = (i + 1 < n ? spline[i + 1] - spline[i] : spline[i] - spline[i - 1]);
                if (tan.sqrMagnitude < 1e-10f) tan = Vector3.forward;
                tan.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, tan);
                if (side.sqrMagnitude < 1e-10f) side = Vector3.right;
                side.Normalize();
                Vector3 up = Vector3.Cross(tan, side).normalized;

                float sway = Mathf.Sin(t * Mathf.PI * 1.4f + time * 0.8f + phase) * (0.05f * t);
                float micro = Mathf.Sin(t * Mathf.PI * 4f + time * 1.6f + phase * 1.7f) * (0.018f * t);
                float spread = Mathf.Lerp(0.75f, 1.2f, t);
                float lift = lift0 * Mathf.Lerp(1f, 0.7f, t) + Mathf.Sin(t * Mathf.PI * 2.2f + phase) * 0.03f * t;
                Vector3 p = spline[i] + side * (side0 * spread + sway + micro) + up * lift;

                float w = width0 * Mathf.Lerp(1f, 0.18f, t * t * 0.4f + t * 0.6f);
                verts.Add(p - side * w);
                verts.Add(p + side * w);
                norms.Add(up);
                norms.Add(up);
                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));
            }

            // oboustranné pruhy
            for (int i = 0; i < n - 1; i++)
            {
                int a = v0 + i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0, false);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    void Ensure()
    {
        if (!mf)
        {
            mf = gameObject.AddComponent<MeshFilter>();
            mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mesh = new Mesh { name = "MikuHairStrands" };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }
        if (!mat)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
            mat.SetFloat("_AlphaClip", 1f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cutoff", 0.25f);
            mat.SetColor("_BaseColor", new Color(0.28f, 0.90f, 0.85f, 1f));
            mat.SetColor("_Color", new Color(0.28f, 0.90f, 0.85f, 1f));
            var tex = StrandTex();
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mr.sharedMaterial = mat;
        }
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    static Texture2D StrandTex()
    {
        if (strandTex) return strandTex;
        const int w = 32, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (w - 1f);
            float v = y / (h - 1f);
            float ridge = 1f - Mathf.Abs(u - 0.5f) * 2f;
            ridge = Mathf.Pow(Mathf.Clamp01(ridge), 1.15f);
            float alpha = Mathf.SmoothStep(0f, 1f, ridge);
            if (v > 0.82f) alpha *= 1f - (v - 0.82f) / 0.18f;
            Color dark = new Color(0.08f, 0.36f, 0.38f, alpha);
            Color mid = new Color(0.25f, 0.85f, 0.80f, alpha);
            Color hi = new Color(0.72f, 1f, 0.96f, alpha);
            Color c = Color.Lerp(dark, mid, 0.45f + 0.4f * ridge);
            c = Color.Lerp(c, hi, Mathf.Pow(ridge, 2.2f) * 0.55f);
            t.SetPixel(x, y, c);
        }
        t.Apply(false, true);
        strandTex = t;
        return t;
    }
}
