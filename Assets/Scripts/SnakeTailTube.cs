using System.Collections.Generic;
using UnityEngine;

// Spojitá trubka ocasu podél článků (místo řady teček). Čistě vizuální.
public class SnakeTailTube : MonoBehaviour
{
    public int sides = 16;
    public int extra = 3; // mezibody mezi články
    const int CapRings = 5;

    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;
    Material tubeMat;
    readonly List<Vector3> pts = new List<Vector3>();
    readonly List<float> rads = new List<float>();

    static Texture2D furMiku, furDelta, emitMiku, emitDelta;
    static Shader litShader;

    public void Ensure(Material fallback)
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
    }

    public void Hide()
    {
        if (mr) mr.enabled = false;
    }

    public void Rebuild(IList<Vector3> srcPts, IList<float> srcRads, Material fallback, Color emit, bool character, bool miku)
    {
        Ensure(fallback);
        if (srcPts == null || srcPts.Count < 2)
        {
            if (mr) mr.enabled = false;
            return;
        }
        if (mr) mr.enabled = true;

        pts.Clear(); rads.Clear();
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
                float s = t * t * (3f - 2f * t);
                pts.Add(Vector3.Lerp(a, b, s));
                rads.Add(Mathf.Lerp(ra, rb, s));
            }
        }

        // zaoblená, zúžená špička
        if (pts.Count >= 2)
        {
            Vector3 last = pts[pts.Count - 1];
            Vector3 prev = pts[pts.Count - 2];
            Vector3 tan = last - prev;
            if (tan.sqrMagnitude < 1e-8f) tan = Vector3.back;
            tan.Normalize();
            float r0 = Mathf.Max(0.02f, rads[rads.Count - 1]);
            for (int k = 1; k <= CapRings; k++)
            {
                float t = k / (float)CapRings;
                float phi = t * Mathf.PI * 0.5f;
                pts.Add(last + tan * r0 * Mathf.Sin(phi) * 1.15f);
                rads.Add(r0 * Mathf.Cos(phi));
            }
        }

        BuildMesh();
        ApplyLook(fallback, emit, character, miku);
    }

    void ApplyLook(Material fallback, Color emit, bool character, bool miku)
    {
        if (!mr) return;
        if (character)
        {
            EnsureTubeMat(fallback);
            if (tubeMat)
            {
                mr.sharedMaterial = tubeMat;
                Color rim = miku ? new Color(0.22f, 0.95f, 0.88f) : new Color(0.72f, 0.38f, 1.05f);
                Color dark = miku ? new Color(0.05f, 0.06f, 0.07f) : new Color(0.055f, 0.03f, 0.08f);
                var block = new MaterialPropertyBlock();
                mr.GetPropertyBlock(block);
                block.SetColor("_BaseColor", dark);
                block.SetColor("_EmissionColor", rim * 0.85f);
                block.SetTexture("_BaseMap", miku ? FurTex(true) : FurTex(false));
                block.SetTexture("_EmissionMap", miku ? EmitTex(true) : EmitTex(false));
                block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
                block.SetVector("_EmissionMap_ST", new Vector4(1f, 1f, 0f, 0f));
                mr.SetPropertyBlock(block);
                return;
            }
        }
        if (fallback) mr.sharedMaterial = fallback;
        var b2 = new MaterialPropertyBlock();
        mr.GetPropertyBlock(b2);
        b2.SetColor("_BaseColor", emit * 0.35f);
        b2.SetColor("_EmissionColor", emit);
        mr.SetPropertyBlock(b2);
    }

    void EnsureTubeMat(Material fallback)
    {
        if (tubeMat) return;
        Shader sh = (fallback && fallback.shader) ? fallback.shader : LitShader();
        if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (!sh) return;
        tubeMat = new Material(sh) { name = "SnakeTailTubeMat" };
        tubeMat.EnableKeyword("_EMISSION");
        tubeMat.SetFloat("_Smoothness", 0.18f);
        tubeMat.SetFloat("_Metallic", 0f);
    }

    static Shader LitShader()
    {
        if (!litShader) litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
        return litShader;
    }

    static Texture2D FurTex(bool miku)
    {
        if (miku) { if (!furMiku) furMiku = MakeFur(true, false); return furMiku; }
        if (!furDelta) furDelta = MakeFur(false, false);
        return furDelta;
    }

    static Texture2D EmitTex(bool miku)
    {
        if (miku) { if (!emitMiku) emitMiku = MakeFur(true, true); return emitMiku; }
        if (!emitDelta) emitDelta = MakeFur(false, true);
        return emitDelta;
    }

    // Tmavá srst, barevný lem nahoře a ke špičce (V = podél ocasu, U = dokola, šev dole).
    static Texture2D MakeFur(bool miku, bool emission)
    {
        const int w = 64, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapModeU = TextureWrapMode.Repeat;
        tex.wrapModeV = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color rim = miku ? new Color(0.28f, 1f, 0.92f) : new Color(0.75f, 0.4f, 1.1f);
        Color dark = miku ? new Color(0.04f, 0.05f, 0.055f) : new Color(0.045f, 0.025f, 0.07f);
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                float n = HashNoise(x, y, w);
                // lem nahoře (u=0.5) a lehký odlesk po stranách
                float top = 1f - Mathf.Abs(u - 0.5f) * 2f; // 1 nahoře, 0 dole
                float rimF = Mathf.Pow(Mathf.Clamp01(top), 1.6f);
                float tip = v * v;
                float k = Mathf.Max(rimF * 0.7f, tip * 0.95f);
                k = Mathf.Clamp01(k + (n - 0.5f) * 0.12f);
                if (emission)
                {
                    float e = Mathf.Max(rimF * 0.45f, tip * 0.85f) * (0.85f + n * 0.2f);
                    tex.SetPixel(x, y, new Color(e, e, e, 1f));
                }
                else
                {
                    Color c = Color.Lerp(dark, rim, k);
                    float g = 0.82f + n * 0.22f;
                    tex.SetPixel(x, y, new Color(c.r * g, c.g * g, c.b * g, 1f));
                }
            }
        }
        tex.Apply(true, true);
        tex.name = (miku ? "TubeFurMiku" : "TubeFurDelta") + (emission ? "Emit" : "");
        return tex;
    }

    static float HashNoise(int x, int y, int w)
    {
        float n = 0f, amp = 0.55f, fx = x, fy = y;
        for (int o = 0; o < 3; o++)
        {
            int ix = (int)fx, iy = (int)fy;
            float tx = fx - ix, ty = fy - iy;
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            float ux = tx * tx * (3f - 2f * tx), uy = ty * ty * (3f - 2f * ty);
            n += Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy) * amp;
            fx = fx * 2.1f + 13.7f; fy = fy * 2.1f + 8.3f; amp *= 0.5f;
        }
        return Mathf.Clamp01(n);
    }

    static float Hash(int x, int y)
    {
        int n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        return (n & 0x7fffffff) / (float)0x7fffffff;
    }

    void BuildMesh()
    {
        int n = pts.Count;
        if (n < 2) return;
        int sides = Mathf.Max(8, this.sides);
        int ring = sides + 1;
        int vertCount = n * ring + 1; // + špička
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
            Vector3 axis = Vector3.Cross(prevTan, tan);
            if (axis.sqrMagnitude > 1e-8f)
            {
                float ang = Vector3.Angle(prevTan, tan);
                var q = Quaternion.AngleAxis(ang, axis.normalized);
                nor = q * nor; bin = q * bin;
            }
            prevTan = tan;
            float v = n > 1 ? i / (n - 1f) : 0f;
            float r = Mathf.Max(0f, rads[i]);
            for (int s = 0; s <= sides; s++)
            {
                // šev dole (bin typicky míří dolů)
                float a = s * Mathf.PI * 2f / sides + Mathf.PI * 0.5f;
                Vector3 d = nor * Mathf.Cos(a) + bin * Mathf.Sin(a);
                d.Normalize();
                int vi = i * ring + s;
                verts[vi] = pts[i] + d * r;
                norms[vi] = d;
                uvs[vi] = new Vector2(s / (float)sides, v);
            }
        }

        int tip = n * ring;
        verts[tip] = pts[n - 1];
        norms[tip] = (pts[n - 1] - pts[n - 2]).normalized;
        uvs[tip] = new Vector2(0.5f, 1f);

        int quad = (n - 1) * sides * 6 + sides * 3;
        var tris = new int[quad];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int row = i * ring;
            int next = (i + 1) * ring;
            for (int s = 0; s < sides; s++)
            {
                int a = row + s, b = row + s + 1, c = next + s, d = next + s + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        }
        int lastRow = (n - 1) * ring;
        for (int s = 0; s < sides; s++)
        {
            tris[t++] = lastRow + s;
            tris[t++] = tip;
            tris[t++] = lastRow + s + 1;
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.SetTriangles(tris, 0, false);
        mesh.RecalculateBounds();
        // jemné sjednocení normál (bez zploštění stěn)
        mesh.RecalculateNormals();
    }
}
