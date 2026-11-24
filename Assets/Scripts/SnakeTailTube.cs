using System.Collections.Generic;
using UnityEngine;

// Spojitá trubka ocasu podél článků (místo řady teček). Čistě vizuální.
public class SnakeTailTube : MonoBehaviour
{
    public int sides = 8;
    public int extra = 2; // mezibody mezi články
    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;
    readonly List<Vector3> pts = new List<Vector3>();
    readonly List<float> rads = new List<float>();

    public void Ensure(Material mat)
    {
        if (!mf)
        {
            mf = gameObject.AddComponent<MeshFilter>();
            mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mesh = new Mesh { name = "SnakeTailTube" };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }
        if (mat && mr.sharedMaterial != mat) mr.sharedMaterial = mat;
    }

    public void Hide()
    {
        if (mr) mr.enabled = false;
    }

    public void Rebuild(IList<Vector3> srcPts, IList<float> srcRads, Material mat, Color emit)
    {
        Ensure(mat);
        if (srcPts == null || srcPts.Count < 2)
        {
            if (mr) mr.enabled = false;
            return;
        }
        if (mr) mr.enabled = true;

        pts.Clear(); rads.Clear();
        // lehké vyhlazení: vloží mezibody
        for (int i = 0; i < srcPts.Count; i++)
        {
            pts.Add(srcPts[i]);
            rads.Add(srcRads[i]);
            if (i >= srcPts.Count - 1 || extra <= 0) continue;
            Vector3 a = srcPts[i], b = srcPts[i + 1];
            float ra = srcRads[i], rb = srcRads[i + 1];
            for (int k = 1; k <= extra; k++)
            {
                float t = k / (extra + 1f);
                // smoothstep
                float s = t * t * (3f - 2f * t);
                pts.Add(Vector3.Lerp(a, b, s));
                rads.Add(Mathf.Lerp(ra, rb, s));
            }
        }
        BuildMesh();
        if (mr && emit.a >= 0f)
        {
            var block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetColor("_BaseColor", emit * 0.35f);
            block.SetColor("_EmissionColor", emit);
            mr.SetPropertyBlock(block);
        }
    }

    void BuildMesh()
    {
        int n = pts.Count;
        if (n < 2) return;
        int sides = Mathf.Max(5, this.sides);
        int vertCount = n * (sides + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        Vector3 prevTan = (pts[1] - pts[0]).normalized;
        if (prevTan.sqrMagnitude < 1e-8f) prevTan = Vector3.forward;
        Vector3 nor = Vector3.Cross(prevTan, Vector3.up);
        if (nor.sqrMagnitude < 1e-6f) nor = Vector3.Cross(prevTan, Vector3.right);
        nor.Normalize();
        Vector3 bin = Vector3.Cross(prevTan, nor).normalized;

        for (int i = 0; i < n; i++)
        {
            Vector3 tan = i < n - 1 ? (pts[i + 1] - pts[i]) : (pts[i] - pts[i - 1]);
            if (tan.sqrMagnitude < 1e-8f) tan = prevTan;
            tan.Normalize();
            // parallel transport
            Vector3 axis = Vector3.Cross(prevTan, tan);
            if (axis.sqrMagnitude > 1e-8f)
            {
                float ang = Vector3.Angle(prevTan, tan);
                var q = Quaternion.AngleAxis(ang, axis.normalized);
                nor = q * nor; bin = q * bin;
            }
            prevTan = tan;
            float u = n > 1 ? i / (n - 1f) : 0f;
            float r = rads[i];
            for (int s = 0; s <= sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                Vector3 d = nor * Mathf.Cos(a) + bin * Mathf.Sin(a);
                int vi = i * (sides + 1) + s;
                verts[vi] = pts[i] + d * r;
                norms[vi] = d;
                uvs[vi] = new Vector2(s / (float)sides, u);
            }
        }

        int quad = (n - 1) * sides * 6;
        var tris = new int[quad];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int row = i * (sides + 1);
            int next = (i + 1) * (sides + 1);
            for (int s = 0; s < sides; s++)
            {
                int a = row + s, b = row + s + 1, c = next + s, d = next + s + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }
}
