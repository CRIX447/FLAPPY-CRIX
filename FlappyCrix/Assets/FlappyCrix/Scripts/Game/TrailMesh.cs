// Trails, drawn exactly like the website (cosmetic-art.js drawTrail): the same layered glow strips
// and the same per-style extras, worked out on the same playfield coordinates, then built into one
// mesh each frame.
//   bands  - coloured stripes side by side (rainbow, pastel)
//   fire   - three glowing layers plus sparks that drift up and back
//   frost  - icy layers plus twinkling white crosses
//   toxic  - green layers plus bubbles (rings) that rise
//   void   - dark layers plus dots orbiting the trail
//   ghost  - soft puffs that drift off, plus a faint core
//   tinsel - two layers plus glitter squares that twinkle
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlappyCrix
{
    public sealed class TrailMesh
    {
        const int Pieces = 5;          // the site fades each strip in 5 steps along its length

        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly List<Vector3> verts = new List<Vector3>(1024);
        readonly List<Color32> cols = new List<Color32>(1024);
        readonly List<Vector2> uvs = new List<Vector2>(1024);
        readonly List<int> tris = new List<int>(2048);

        static Material material;
        static readonly Vector2 SolidUv = new Vector2(1 / 6f, 0.5f);

        // the playfield-to-world conversion for this frame
        float slide, unit, height;

        struct P { public float X, Y, Nx, Ny, T; }
        readonly List<P> pts = new List<P>(64);

        public TrailMesh(Transform parent, int sortingOrder)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Trail" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.sortingOrder = sortingOrder;
        }

        /// <summary>A small texture: a solid square, a soft disc and a ring, side by side.</summary>
        static Material Material()
        {
            if (material != null) return material;
            const int S = 32;
            var t = new Texture2D(S * 3, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "trail-atlas" };
            var px = new Color32[S * 3 * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S * 3; x++)
                {
                    int cell = x / S; float cx = x % S + 0.5f - S / 2f, cy = y + 0.5f - S / 2f;
                    float d = Mathf.Sqrt(cx * cx + cy * cy), r = S / 2f - 1;
                    float a = cell == 0 ? 1 : cell == 1 ? Mathf.Clamp01(r - d + 0.5f) : Mathf.Clamp01(1.6f - Mathf.Abs(d - (r - 2.2f)));
                    px[y * S * 3 + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            var sh = Shader.Find("Sprites/Default");
            material = new Material(sh) { mainTexture = t, name = "Trail" };
            return material;
        }

        public void Hide() { if (renderer.enabled) renderer.enabled = false; }

        /// <summary>
        /// flat: x,y pairs oldest first (playfield coordinates, y down). seq: how many points the trail
        /// has ever had (so sparks stay on their point as it moves). size: the bird's drawn size.
        /// </summary>
        public void Draw(List<float> flat, long seq, uint[] colours, string style, float size, float slideX, float u, float h)
        {
            int n = flat.Count / 2;
            if (n < 2 || colours == null || colours.Length == 0) { Hide(); return; }
            renderer.enabled = true;
            slide = slideX; unit = u; height = h;
            verts.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();

            // the site's trailFrame: each point with its sideways normal and how far along it is (0 old .. 1 new)
            pts.Clear();
            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
                float tx = flat[b * 2] - flat[a * 2], ty = flat[b * 2 + 1] - flat[a * 2 + 1];
                float l = Mathf.Sqrt(tx * tx + ty * ty); if (l < 1e-4f) l = 1;
                tx /= l; ty /= l;
                pts.Add(new P { X = flat[i * 2], Y = flat[i * 2 + 1], Nx = -ty, Ny = tx, T = n > 1 ? i / (float)(n - 1) : 1 });
            }
            float now = Time.time * 1000f;
            string fx = style ?? (colours.Length >= 4 ? "bands" : "fire");
            Func<int, Color> c = i => Res.C(colours[Math.Min(i, colours.Length - 1)]);
            Func<int, long> id = i => seq - (n - 1 - i);

            if (fx == "bands")
            {
                int K = colours.Length;
                Func<float, float> W = t => size * (0.34f + 0.44f * Mathf.Sqrt(t));
                for (int k = 0; k < K; k++)
                {
                    int kk = k;
                    Strip(t => -W(t) / 2 + W(t) * kk / K - 0.3f, t => -W(t) / 2 + W(t) * (kk + 1) / K + 0.3f,
                          c(k), t => 0.1f + 0.85f * Mathf.Pow(t, 1.4f));
                }
            }
            else
            {
                Func<float, float> hw = t => size * (0.06f + 0.3f * Mathf.Pow(t, 0.8f));
                switch (fx)
                {
                    case "frost":
                        Glow(hw, c(2), 1.3f, 0.3f, 1, c(1), 0.75f, 0.7f, 1.2f, c(0), 0.32f, 0.95f, 1.5f);
                        for (int i = 0; i < n; i++)
                        {
                            long k = id(i); if (k % 4 != 0) continue;
                            var q = pts[i]; float tw = 0.5f + 0.5f * Mathf.Sin(now * 0.012f + k);
                            float r = size * (0.07f + 0.07f * Rnd(k)) * (0.5f + tw * 0.5f);
                            float x = q.X + (Rnd(k + 1) - 0.5f) * size * 0.6f, y = q.Y + (Rnd(k + 2) - 0.5f) * size * 0.6f;
                            float lw = Mathf.Max(1, size * 0.04f);
                            Rect(x, y, r, lw / 2, Color.white, q.T * tw);
                            Rect(x, y, lw / 2, r, Color.white, q.T * tw);
                        }
                        break;
                    case "toxic":
                        Glow(hw, c(2), 1.3f, 0.4f, 1, c(1), 0.8f, 0.85f, 1.2f, c(0), 0.35f, 0.9f, 1.6f);
                        for (int i = 0; i < n; i++)
                        {
                            long k = id(i); if (k % 4 != 0) continue;
                            var q = pts[i]; int age = n - 1 - i;
                            Dot(q.X + (Rnd(k) - 0.5f) * size * 0.5f, q.Y - age * 0.32f - size * 0.1f,
                                size * (0.04f + 0.06f * (1 - q.T) + 0.03f * Rnd(k + 1)) + size * 0.02f, c(0), q.T * 0.9f, 2);
                        }
                        break;
                    case "void":
                        Glow(hw, c(1), 1.45f, 0.45f, 1, c(2), 0.95f, 0.95f, 0.9f, c(0), 0.2f, 0.55f, 1.6f);
                        for (int i = 0; i < n; i++)
                        {
                            long k = id(i); if (k % 3 != 0) continue;
                            var q = pts[i]; float ang = now * 0.004f + k, rr = size * (0.28f + 0.22f * (1 - q.T));
                            Dot(q.X + Mathf.Cos(ang) * rr, q.Y + Mathf.Sin(ang) * rr * 0.6f, size * 0.04f, Rnd(k) > 0.5f ? c(0) : c(1), q.T * 0.9f, 1);
                        }
                        break;
                    case "ghost":
                        for (int i = 0; i < n; i++)
                        {
                            long k = id(i); if (k % 2 != 0) continue;
                            var q = pts[i]; int age = n - 1 - i;
                            Dot(q.X - age * 0.2f, q.Y - age * 0.3f + (Rnd(k) - 0.5f) * size * 0.2f, size * (0.14f + 0.3f * (1 - q.T)),
                                Rnd(k) > 0.6f ? c(1) : c(0), 0.16f * q.T + 0.04f, 1);
                        }
                        Glow(hw, c(2), 0.7f, 0.35f, 1.5f, c(0), 0.3f, 0.85f, 2f);
                        break;
                    case "tinsel":
                        Glow(hw, c(1), 1.1f, 0.8f, 1.2f, c(0), 0.45f, 0.95f, 1.4f);
                        for (int i = 0; i < n; i++)
                        {
                            long k = id(i); if (k % 2 != 0) continue;
                            var q = pts[i]; float tw = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(now * 0.01f + k * 1.7f));
                            Color col = (k % 4) == 0 ? c(1) : (k % 4) == 1 ? c(2) : (k % 4) == 2 ? c(3) : Color.white;
                            float x = q.X + (Rnd(k) - 0.5f) * size * 0.7f, y = q.Y + (Rnd(k + 1) - 0.5f) * size * 0.7f, r = size * (0.03f + 0.03f * Rnd(k + 2));
                            Rect(x, y, r, r, col, q.T * tw);
                        }
                        break;
                    default:   // fire
                        Glow(hw, c(2), 1.25f, 0.32f, 1.2f, c(1), 0.8f, 0.8f, 1.3f, c(0), 0.38f, 0.95f, 1.6f);
                        for (int i = 0; i < n; i++)
                        {
                            long k = id(i); if (k % 3 != 0) continue;
                            var q = pts[i]; int age = n - 1 - i;
                            Dot(q.X + (Rnd(k + 1) - 0.5f) * size * 0.45f - age * 0.15f, q.Y - age * 0.5f - Rnd(k + 2) * size * 0.25f,
                                size * (0.035f + 0.04f * Rnd(k + 3)), Rnd(k) > 0.5f ? c(0) : c(1), Mathf.Max(0, q.T * 1.1f - 0.1f), 1);
                        }
                        break;
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        static float Rnd(long k) { double x = Math.Sin(k * 127.1 + 311.7) * 43758.5453; return (float)(x - Math.Floor(x)); }

        Vector3 W(float x, float y) => new Vector3((x - slide) * unit, (height - y) * unit, 0);

        static Color32 Col(Color c, float a) => new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(Mathf.Clamp01(a) * 255));

        /// <summary>The site's trailGlow: a few strips, widest and faintest first.</summary>
        void Glow(Func<float, float> hw, Color c1, float w1, float a1, float p1, Color c2, float w2, float a2, float p2,
                  Color c3 = default(Color), float w3 = 0, float a3 = 0, float p3 = 1)
        {
            Strip(t => -hw(t) * w1, t => hw(t) * w1, c1, t => a1 * Mathf.Pow(t, p1));
            Strip(t => -hw(t) * w2, t => hw(t) * w2, c2, t => a2 * Mathf.Pow(t, p2));
            if (w3 > 0) Strip(t => -hw(t) * w3, t => hw(t) * w3, c3, t => a3 * Mathf.Pow(t, p3));
        }

        /// <summary>The site's trailStrip: a ribbon between two offsets along the trail, fading in steps.</summary>
        void Strip(Func<float, float> off0, Func<float, float> off1, Color colour, Func<float, float> alpha)
        {
            int n = pts.Count;
            int step = Math.Max(1, (int)Math.Ceiling((n - 1) / (double)Pieces));
            for (int s = 0; s < n - 1; s += step)
            {
                int e = Math.Min(s + step, n - 1);
                float a = alpha((pts[s].T + pts[e].T) / 2);
                if (a <= 0.01f) continue;
                var c = Col(colour, Mathf.Min(1, a));
                int start = verts.Count;
                for (int i = s; i <= e; i++)
                {
                    var q = pts[i]; float o0 = off0(q.T), o1 = off1(q.T);
                    verts.Add(W(q.X + q.Nx * o0, q.Y + q.Ny * o0)); cols.Add(c); uvs.Add(SolidUv);
                    verts.Add(W(q.X + q.Nx * o1, q.Y + q.Ny * o1)); cols.Add(c); uvs.Add(SolidUv);
                }
                for (int i = 0; i < e - s; i++)
                {
                    int v = start + i * 2;
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                    tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
                }
            }
        }

        /// <summary>A disc (cell 1) or a ring (cell 2) of radius r.</summary>
        void Dot(float x, float y, float r, Color colour, float alpha, int cell)
        {
            if (alpha <= 0.01f || r <= 0) return;
            Quad(x - r, y - r, x + r, y + r, Col(colour, alpha), cell);
        }

        /// <summary>A solid rectangle with half sizes hx, hy.</summary>
        void Rect(float x, float y, float hx, float hy, Color colour, float alpha)
        {
            if (alpha <= 0.01f) return;
            Quad(x - hx, y - hy, x + hx, y + hy, Col(colour, alpha), 0);
        }

        void Quad(float x0, float y0, float x1, float y1, Color32 c, int cell)
        {
            int v = verts.Count;
            float u0 = cell / 3f, u1 = (cell + 1) / 3f;
            if (cell == 0) { u0 = u1 = 1 / 6f; }
            verts.Add(W(x0, y0)); uvs.Add(new Vector2(u0, 1));
            verts.Add(W(x1, y0)); uvs.Add(new Vector2(u1, 1));
            verts.Add(W(x0, y1)); uvs.Add(new Vector2(u0, 0));
            verts.Add(W(x1, y1)); uvs.Add(new Vector2(u1, 0));
            for (int i = 0; i < 4; i++) cols.Add(c);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
            tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
        }
    }
}
