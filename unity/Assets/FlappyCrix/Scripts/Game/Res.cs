// Everything the game loads: the site's pictures, sounds and font from Resources/FlappyCrix,
// plus the textures it makes itself (sky, city, pipes, glows). LoadAll runs step by step so
// the loading screen can show real progress.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlappyCrix
{
    public static class Res
    {
        public const string Root = "FlappyCrix/";

        public static Font Font;
        public static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        public static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
        public static Material Additive, PostFx;
        public static Sprite White, Glow, Soft, Slant, SlantOutline, Ring;
        public static bool Loaded;

        static readonly string[] ImageNames = { "bird", "bird-bare", "coin", "hat-bucket", "hat-cap", "hat-crown", "witch", "santasley", "crix-studios", "crixgamingvr", "icon" };
        static readonly string[] SoundNames =
        {
            "jump", "coin", "death", "milestone", "67", "boom", "victory", "ach", "powerup", "purchase", "unlock", "select",
            "rank", "woosh", "pop", "error", "smash", "hover", "click", "swoosh", "start", "countdown", "levelup",
            "music", "halloweenmusic", "christmasmusic",
        };

        /// <summary>What the splash screen needs before anything else (tiny, loaded at once).</summary>
        public static void LoadBoot()
        {
            if (White != null) return;
            Font = Resources.Load<Font>(Root + "font");
            if (Font == null) Font = BuiltinFont();
            White = MakeSprite(Solid(4, 4, Color.white), 4, "white");
            Glow = MakeSprite(Radial(128, 2.2f), 128, "glow");
            Soft = MakeSprite(Radial(32, 1.4f), 32, "soft");
            Ring = MakeSprite(RingTex(128), 128, "ring");
            Slant = MakeSprite(SlantTex(512, 96, false), 100, "slant", new Vector4(40, 0, 40, 0));
            SlantOutline = MakeSprite(SlantTex(512, 96, true), 100, "slant-outline", new Vector4(40, 0, 40, 0));
            var add = Resources.Load<Shader>(Root + "Shaders/Additive");
            Additive = new Material(add != null && add.isSupported ? add : Shader.Find("Sprites/Default")) { name = "FlappyCrix additive" };
            var post = Resources.Load<Shader>(Root + "Shaders/Post");
            if (post != null && post.isSupported) PostFx = new Material(post) { name = "FlappyCrix post" };
            var splash = Resources.Load<Texture2D>(Root + "Images/crix-studios");
            if (splash != null) Sprites["crix-studios"] = MakeSprite(splash, splash.height, "crix-studios");
        }

        static Font BuiltinFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { }
            return f;
        }

        /// <summary>Loads the rest; progress(0..1, what) is called as it goes.</summary>
        public static IEnumerator LoadAll(Action<float, string> progress)
        {
            int total = ImageNames.Length + SoundNames.Length, done = 0;
            foreach (var n in ImageNames)
            {
                progress(done / (float)total, "Loading art");
                if (!Sprites.ContainsKey(n))
                {
                    var req = Resources.LoadAsync<Texture2D>(Root + "Images/" + n);
                    yield return req;
                    var t = req.asset as Texture2D;
                    if (t != null) Sprites[n] = MakeSprite(t, t.height, n);
                }
                done++;
            }
            foreach (var n in SoundNames)
            {
                progress(done / (float)total, n.Contains("music") ? "Loading music" : "Loading sounds");
                var req = Resources.LoadAsync<AudioClip>(Root + "Sounds/" + n);
                yield return req;
                var c = req.asset as AudioClip;
                if (c != null) { Sounds[n] = c; if (c.loadState == AudioDataLoadState.Unloaded && !n.Contains("music")) c.LoadAudioData(); }
                done++;
            }
            progress(1, "Ready");
            Loaded = true;
        }

        public static Sprite Get(string name) { Sprite s; return name != null && Sprites.TryGetValue(name, out s) ? s : null; }

        /// <summary>An emoji picture by its code ("1f381"), or the coin for "coin". Loaded the first time it's needed.</summary>
        public static Sprite Emoji(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (code == "coin") return Get("coin");
            string key = "emoji/" + code;
            Sprite s;
            if (Sprites.TryGetValue(key, out s)) return s;
            var t = Resources.Load<Texture2D>(Root + "Emoji/" + code);
            s = t != null ? MakeSprite(t, t.height, key) : null;
            Sprites[key] = s;
            return s;
        }
        public static AudioClip Clip(string name) { AudioClip c; return name != null && Sounds.TryGetValue(name, out c) ? c : null; }

        // ------------------------------------------------------------------ texture makers

        public static Sprite MakeSprite(Texture2D t, float pixelsPerUnit, string name, Vector4 border = default(Vector4))
        {
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            s.name = name;
            return s;
        }

        static Texture2D NewTex(int w, int h, string name, bool clamp = true)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name, wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        }

        public static Texture2D Solid(int w, int h, Color c)
        {
            var t = NewTex(w, h, "solid");
            var px = new Color32[w * h];
            Color32 k = c;
            for (int i = 0; i < px.Length; i++) px[i] = k;
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>A white spot that fades to nothing at the edge (power = how soft).</summary>
        public static Texture2D Radial(int size, float power)
        {
            var t = NewTex(size, size, "radial");
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = Mathf.Clamp01(1 - d);
                    a = Mathf.Pow(a, power);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        static Texture2D RingTex(int size)
        {
            var t = NewTex(size, size, "ring");
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.85f) / 0.12f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>A slanted bar (a parallelogram) for the menu's edgy buttons.</summary>
        static Texture2D SlantTex(int w, int h, bool outline)
        {
            var t = NewTex(w, h, outline ? "slant-outline" : "slant");
            var px = new Color32[w * h];
            float slope = 0.35f;     // x shift per y pixel
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float left = (y) * slope + 1, right = w - 1 - (h - 1 - y) * slope;
                    float a = Mathf.Clamp01(Mathf.Min(x - left + 1, right - x + 1));
                    if (outline)
                    {
                        float edge = Mathf.Min(Mathf.Min(x - left, right - x), Mathf.Min(y, h - 1 - y));
                        a *= edge < 3 ? 1 : 0;
                    }
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        // ------------------------------------------------------------------ the website's shapes (rounded cards, pills)

        static readonly Dictionary<string, Sprite> shapes = new Dictionary<string, Sprite>();

        /// <summary>
        /// A rounded rectangle for UI (9-sliced, radius r): fill, or just its edge (edge = border width).
        /// left / right colours make a left-to-right gradient (the site's red-to-orange buttons).
        /// </summary>
        public static Sprite Rounded(int r, int edge, Color left, Color right)
        {
            string key = r + "|" + edge + "|" + ColorUtility.ToHtmlStringRGBA(left) + ColorUtility.ToHtmlStringRGBA(right);
            Sprite s;
            if (shapes.TryGetValue(key, out s)) return s;
            int w = r * 2 + 64, h = r * 2 + 4;
            var t = NewTex(w, h, "rounded");
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = Mathf.Clamp(y + 0.5f, r, h - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);                          // anti-aliased outside
                    if (edge > 0) a *= Mathf.Clamp01(d - (r - edge) + 0.5f);         // hollow inside
                    Color c = Color.Lerp(left, right, x / (float)(w - 1));
                    px[y * w + x] = new Color(c.r, c.g, c.b, c.a * a);
                }
            t.SetPixels32(px); t.Apply(false);
            s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r + 1, r + 1, r + 1, r + 1));
            s.name = "rounded";
            shapes[key] = s;
            return s;
        }

        public static Sprite Rounded(int r, int edge = 0) { return Rounded(r, edge, Color.white, Color.white); }

        /// <summary>The site's background: near black with a faint red grid (tiles).</summary>
        public static Sprite Grid()
        {
            Sprite s;
            if (shapes.TryGetValue("grid", out s)) return s;
            int n = 48;
            var t = NewTex(n, n, "grid", false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = (x == 0 || y == 0) ? new Color32(255, 70, 85, 16) : new Color32(0, 0, 0, 0);
            t.SetPixels32(px); t.Apply(false);
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            shapes["grid"] = s;
            return s;
        }

        /// <summary>Vertical gradient (top colour first).</summary>
        public static Texture2D VGradient(Color top, Color bottom, int h = 256)
        {
            var t = NewTex(4, h, "gradient");
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                Color32 c = Color.Lerp(bottom, top, y / (float)(h - 1));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>A strip of city skyline with lit windows (it tiles left to right).</summary>
        public static Texture2D Skyline(int w, int h, Color body, Color window, float windowChance, int seed, float minH, float maxH)
        {
            var t = NewTex(w, h, "skyline", false);
            t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            Color32 clear = new Color32(0, 0, 0, 0), b = body, win = window;
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            int x0 = 0;
            while (x0 < w)
            {
                int bw = 18 + rng.Next(46);
                if (x0 + bw > w) bw = w - x0;
                int bh = (int)(h * (minH + (float)rng.NextDouble() * (maxH - minH)));
                bool antenna = rng.NextDouble() < 0.25;
                for (int x = x0; x < x0 + bw; x++)
                    for (int y = 0; y < bh; y++) px[y * w + x] = b;
                if (antenna) { int ax = x0 + bw / 2; for (int y = bh; y < Mathf.Min(h, bh + 14); y++) px[y * w + ax] = b; }
                for (int wy = 6; wy < bh - 6; wy += 8)
                    for (int wx = x0 + 4; wx < x0 + bw - 4; wx += 7)
                        if (rng.NextDouble() < windowChance)
                            for (int dy = 0; dy < 4; dy++) for (int dx = 0; dx < 3; dx++) px[(wy + dy) * w + wx + dx] = win;
                x0 += bw + rng.Next(3);
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>A soft puffy cloud: a few overlapping round blobs, white, edges faded.</summary>
        public static Texture2D Cloud()
        {
            int w = 256, h = 112;
            var t = NewTex(w, h, "cloud");
            var px = new Color32[w * h];
            float[,] blobs = { { 60, 44, 38 }, { 110, 62, 50 }, { 165, 52, 42 }, { 205, 40, 30 }, { 128, 34, 40 } };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0;
                    for (int b = 0; b < blobs.GetLength(0); b++)
                    {
                        float dx = x - blobs[b, 0], dy = y - blobs[b, 1];
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        a = Mathf.Max(a, Mathf.Clamp01((blobs[b, 2] - d) / 3f));
                    }
                    if (y < 14) a *= y / 14f;       // flat bottom
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>The row of round green bushes the site draws in front of the city (tiles left to right).</summary>
        public static Texture2D Bushes(Color colour)
        {
            int w = 512, h = 64;
            var t = NewTex(w, h, "bushes", false);
            t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color32[w * h];
            Color32 c = colour, clear = new Color32(0, 0, 0, 0);
            Color32 hi = Color.Lerp(colour, Color.white, 0.12f);
            var rng = new System.Random(21);
            var bumps = new List<Vector3>();
            for (int x = 0; x < w; x += 26 + rng.Next(18)) bumps.Add(new Vector3(x, 0, 22 + rng.Next(18)));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = clear;
                    foreach (var b in bumps)
                    {
                        // wrap so the tile joins up
                        float dx = Mathf.Min(Mathf.Abs(x - b.x), w - Mathf.Abs(x - b.x));
                        float d = Mathf.Sqrt(dx * dx + y * y);
                        if (d < b.z) { px[y * w + x] = d > b.z - 3 && y > 6 ? hi : c; break; }
                    }
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>Horizontal gradient across a pipe, dark edges to a bright middle, the site's look.</summary>
        public static Texture2D PipeBody(Color[] stops, Color neon)
        {
            int w = 64, h = 4;
            var t = NewTex(w, h, "pipe");
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float f = x / (float)(w - 1);
                float seg = f * (stops.Length - 1);
                int i = Mathf.Min(stops.Length - 2, (int)seg);
                Color c = Color.Lerp(stops[i], stops[i + 1], seg - i);
                if (neon.a > 0 && (x < 2 || x >= w - 2)) c = neon;
                for (int y = 0; y < h; y++) px[y * w + x] = c;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>The ground, as on the site: a dark strip with soft diagonal stripes and a lighter lip on top.</summary>
        public static Texture2D Ground(Color baseC, Color stripe, Color edge)
        {
            int w = 128, h = 128;
            var t = NewTex(w, h, "ground", false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = baseC * (0.85f + 0.15f * (y / (float)h));
                    c.a = 1;
                    bool s = ((x + y) / 16) % 2 == 0 && y < h - 8;
                    if (s) c = Color.Lerp(c, stripe, 0.5f);
                    if (y >= h - 8) c = edge;
                    else if (y >= h - 10) c = Color.Lerp(c, Color.black, 0.35f);
                    px[y * w + x] = c;
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        public static Color C(uint packed)
        {
            return new Color32((byte)(packed & 0xFF), (byte)((packed >> 8) & 0xFF), (byte)((packed >> 16) & 0xFF), 255);
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }
}
