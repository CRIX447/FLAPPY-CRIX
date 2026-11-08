// The game world, drawn with Unity sprites: a night sky, three layers of city moving at
// different speeds, neon-edged pipes, spinning coins, the bird with its trail, particles,
// screen shake. The rules are NativeSim (the website's game, tick for tick, 60 a second).
//
// The website shows a 400 x 600 box. Here the view always contains that box and grows to fill
// the screen: wider screens see further ahead, taller phones see more sky and ground.
// 1 world unit = 100 sim pixels; sim y grows downwards, world y upwards.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using FlappyCrix.Native;
using FlappyCrix.Online;
using UnityEngine;

namespace FlappyCrix
{
    public sealed partial class World : MonoBehaviour
    {
        public const float U = 0.01f;           // world units per sim pixel
        const float Tick = 1f / 60f;
        const float BirdDraw = 46f;              // drawn size (the hit box stays the site's)

        public NativeSim Sim;
        public Camera Cam;
        /// <summary>Plays itself (menus): an autopilot flies and never crashes.</summary>
        public bool Attract = true;
        public bool Frozen;
        public float SimSpeed = 1;
        public Season Season;
        /// <summary>The save whose hat and trail the bird wears.</summary>
        public Func<SaveData> SaveRef;
        /// <summary>Multiplayer: the other players' birds are drawn while a match is on.</summary>
        public Multiplayer Mp;

        public event Action<int> Scored;
        public event Action<int> Died;
        public event Action<int> CoinsGained;

        // what can be seen (sim pixels)
        public float ViewLeft { get; private set; }
        public float ViewTop { get; private set; }
        public float ViewW { get; private set; }
        public float ViewH { get; private set; }

        float acc;
        Transform root;
        SpriteRenderer sky, moon, moonGlow, bird, birdGlow, groundTop;
        readonly List<SpriteRenderer>[] city = { new List<SpriteRenderer>(), new List<SpriteRenderer>(), new List<SpriteRenderer>() };
        readonly float[] cityWidth = new float[3];
        readonly List<SpriteRenderer> grounds = new List<SpriteRenderer>();
        float groundWidth;
        readonly List<Transform> stars = new List<Transform>();
        readonly List<Transform> clouds = new List<Transform>();
        readonly List<Vector2> cloudSeed = new List<Vector2>();
        readonly List<SpriteRenderer> bushes = new List<SpriteRenderer>();
        float bushWidth;
        /// <summary>The site draws no trail unless one is equipped.</summary>
        bool HasTrail;
        LineRenderer trail;
        Sprite pipeBody, pipeCap;
        readonly List<PipeView> pipes = new List<PipeView>();
        readonly List<CoinView> coins = new List<CoinView>();
        ParticleSystem burst, sparks, puffs;
        float scroll, shake, punch, flash;
        Color neon, accent;
        Vector3 camBase;

        sealed class PipeView { public Transform Root; public SpriteRenderer TopBody, TopCap, BotBody, BotCap, TopGlow, BotGlow; }
        sealed class CoinView { public Transform Root; public SpriteRenderer Coin, Glow; }

        // ------------------------------------------------------------------ setup

        public void Build(Camera cam, Season season)
        {
            Cam = cam;
            Season = season;
            Sim = new NativeSim();
            // the bird flying behind the menus is silent; sounds only play in a real run
            Sim.Sound += (n, v) => { if (!Attract) Sfx.Play(n, v); };
            Sim.Scored += s => { if (!Attract) { punch = 1; if (Scored != null) Scored(s); } };
            Sim.Died += s => OnDied(s);
            Sim.CoinsGained += n => { if (!Attract && CoinsGained != null) CoinsGained(n); };
            root = new GameObject("World").transform;
            root.SetParent(transform, false);

            // palette: the season's, or the website's own night look
            Color skyTop, skyLow, cityC, groundC, stripe, bushC, groundLip;
            Color[] pipeStops;
            if (season != null)
            {
                skyTop = Res.C(season.SkyTop); skyLow = Res.C(season.SkyLow); cityC = Res.C(season.City);
                groundC = Res.C(season.Ground); stripe = Res.C(season.Stripe); accent = Res.C(season.Accent);
                pipeStops = new Color[season.Pipe.Length];
                for (int i = 0; i < pipeStops.Length; i++) pipeStops[i] = Res.C(season.Pipe[i]);
                neon = new Color(0, 0, 0, 0);
                bushC = Color.Lerp(cityC, pipeStops[1], 0.5f);
                groundLip = Color.Lerp(groundC, Color.white, 0.18f);
            }
            else
            {
                skyTop = Res.Hex("#12203A"); skyLow = Res.Hex("#1B3352"); cityC = new Color32(35, 60, 95, 255);
                groundC = Res.Hex("#2A2A22"); stripe = Res.Hex("#3A3A2E"); accent = Res.Hex("#FF4655");
                pipeStops = new[] { Res.Hex("#0B8F56"), Res.Hex("#00CC7A"), Res.Hex("#4DE8A8"), Res.Hex("#00CC7A"), Res.Hex("#076B41") };
                neon = new Color(0, 0, 0, 0);
                bushC = Res.Hex("#1E4A2E");
                groundLip = Res.Hex("#3D3D31");
            }

            sky = NewSprite("Sky", Res.MakeSprite(Res.VGradient(skyTop, skyLow), 100, "sky"), -100);
            // stars
            var rng = new System.Random(7);
            for (int i = 0; i < (season != null ? 70 : 0); i++)
            {
                var s = NewSprite("Star", Res.Soft, -95);
                float k = 0.03f + (float)rng.NextDouble() * 0.05f;
                s.transform.localScale = Vector3.one * k;
                s.color = new Color(1, 1, 1, 0.25f + (float)rng.NextDouble() * 0.6f);
                s.transform.localPosition = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), 0);   // placed in Layout
                stars.Add(s.transform);
            }
            moonGlow = NewSprite("MoonGlow", Res.Glow, -94, true);
            moonGlow.color = season != null ? Res.C(season.Moon) * 0.5f : new Color(1f, 0.25f, 0.3f, 0.5f);
            moon = NewSprite("Moon", Res.Soft, -93);
            moon.color = season != null ? Res.C(season.Moon) : Res.Hex("#FFD3C9");
            moon.enabled = moonGlow.enabled = season != null;

            // dim clouds drifting slowly
            var cloudSpr = Res.MakeSprite(Res.Cloud(), 100, "cloud");
            var crng = new System.Random(11);
            for (int i = 0; i < 6; i++)
            {
                var c = NewSprite("Cloud", cloudSpr, -94);
                c.color = new Color(1, 1, 1, 0.05f + (float)crng.NextDouble() * 0.05f);
                c.transform.localScale = Vector3.one * (0.9f + (float)crng.NextDouble() * 1.2f);
                clouds.Add(c.transform);
                cloudSeed.Add(new Vector2((float)crng.NextDouble(), 0.45f + (float)crng.NextDouble() * 0.5f));
            }

            // three city layers, far to near
            Color[] layerC = { Color.Lerp(cityC, skyLow, 0.55f), Color.Lerp(cityC, skyLow, 0.25f), cityC };
            Color win = season != null ? new Color(1f, 0.84f, 0.47f) : new Color(0.55f, 0.68f, 0.85f);
            for (int l = 0; l < 3; l++)
            {
                float winMix = season != null ? 0.55f + l * 0.15f : 0.12f + l * 0.06f;   // the site's windows are faint
                var tex = Res.Skyline(512, 256, layerC[l], Color.Lerp(layerC[l], win, winMix), 0.18f + l * 0.06f, 11 + l * 31, 0.25f + l * 0.08f, 0.6f + l * 0.12f);
                var spr = Sprite.Create(tex, new Rect(0, 0, 512, 256), new Vector2(0, 0), 100f / (1.6f + l * 0.4f));   // bottom-left pivot
                cityWidth[l] = spr.bounds.size.x;
                for (int k = 0; k < 4; k++) city[l].Add(NewSprite("City" + l, spr, -90 + l * 2));
            }

            // bushes in front of the city
            var btex = Res.Bushes(bushC);
            var bspr = Sprite.Create(btex, new Rect(0, 0, 512, 64), new Vector2(0, 0), 100f / 1.4f);
            bushWidth = bspr.bounds.size.x;
            for (int k = 0; k < 4; k++) bushes.Add(NewSprite("Bushes", bspr, -84));

            // ground
            var gtex = Res.Ground(groundC, stripe, groundLip);
            var gspr = Sprite.Create(gtex, new Rect(0, 0, 128, 128), new Vector2(0, 1), 128f / (NativeSim.GroundH * U * 1.3f));
            groundWidth = gspr.bounds.size.x;
            for (int k = 0; k < 26; k++) grounds.Add(NewSprite("Ground", gspr, 20));
            groundTop = NewSprite("GroundGlow", Res.White, 21, true);
            groundTop.color = new Color(0, 0, 0, 0);
            groundTop.enabled = false;

            // pipes
            var ptex = Res.PipeBody(pipeStops, neon);
            pipeBody = Sprite.Create(ptex, new Rect(0, 0, 64, 4), new Vector2(0, 0), 64f / (NativeSim.PipeWidth * U));
            pipeCap = Sprite.Create(ptex, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f), 64f / ((NativeSim.PipeWidth + 8) * U));

            // bird
            birdGlow = NewSprite("BirdGlow", Res.Glow, 49, true);
            birdGlow.color = new Color(accent.r, accent.g, accent.b, 0f);
            bird = NewSprite("Bird", Res.Get("bird"), 50);
            if (bird.sprite != null) bird.transform.localScale = Vector3.one * (BirdDraw * U / bird.sprite.bounds.size.y);

            var tgo = new GameObject("Trail");
            tgo.transform.SetParent(root, false);
            trail = tgo.AddComponent<LineRenderer>();
            trail.material = Res.Additive;
            trail.useWorldSpace = false;
            trail.numCapVertices = 4;
            trail.sortingOrder = 45;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0f), new Keyframe(1, 0.16f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(accent, 0), new GradientColorKey(Color.Lerp(accent, Color.yellow, 0.5f), 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.75f, 1) });
            trail.colorGradient = g;

            burst = MakeParticles("Burst", 60, new Color(1f, 0.42f, 0.2f), 0.05f, 0.14f, 1.4f, 4.5f, 0.9f, 2f);
            sparks = MakeParticles("Sparks", 30, new Color(1f, 0.85f, 0.3f), 0.03f, 0.08f, 0.35f, 2.2f, 0.5f, 0.3f);
            puffs = MakeParticles("Puffs", 20, new Color(1, 1, 1, 0.35f), 0.08f, 0.16f, 0.35f, 0.8f, 0.4f, -0.2f);

            Layout();
            Sim.StartRun();
            Sim.Immortal = true;
        }

        SpriteRenderer NewSprite(string name, Sprite s, int order, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingOrder = order;
            if (additive && Res.Additive != null) r.sharedMaterial = Res.Additive;
            return r;
        }

        ParticleSystem MakeParticles(string name, int max, Color c, float minSize, float maxSize, float minLife, float speed, float maxLife, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = c;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.08f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = fade;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Res.Additive;
            r.sortingOrder = 60;
            if (Res.Soft != null) { r.renderMode = ParticleSystemRenderMode.Billboard; }
            var tex = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
            if (tex == null && Res.Soft != null)
            {
                var m = new Material(Res.Additive) { mainTexture = Res.Soft.texture };
                r.sharedMaterial = m;
            }
            return ps;
        }

        // ------------------------------------------------------------------ view

        /// <summary>Fits the view to the screen (call when it changes size).</summary>
        public void Layout()
        {
            float aspect = Cam.aspect;
            ViewW = Mathf.Max(NativeSim.W, NativeSim.H * aspect);
            ViewH = Mathf.Max(NativeSim.H, ViewW / aspect);
            ViewW = ViewH * aspect;
            // the bird about a third of the way across, never showing less than the site's box
            ViewLeft = Mathf.Min(0, NativeSim.BirdX - ViewW * 0.3f);
            if (ViewLeft + ViewW < NativeSim.W) ViewLeft = NativeSim.W - ViewW;
            ViewTop = (NativeSim.H - ViewH) / 2;
            Sim.SpawnX = ViewLeft + ViewW + 40;
            Sim.DespawnX = ViewLeft - 40;

            Cam.orthographic = true;
            Cam.orthographicSize = ViewH * U / 2;
            camBase = new Vector3((ViewLeft + ViewW / 2) * U, (NativeSim.H - (ViewTop + ViewH / 2)) * U, -10);
            Cam.transform.position = camBase;

            float left = ViewLeft * U, right = (ViewLeft + ViewW) * U, top = (NativeSim.H - ViewTop) * U, bottom = (NativeSim.H - ViewTop - ViewH) * U;
            sky.transform.localPosition = new Vector3((left + right) / 2, (top + bottom) / 2, 0);
            sky.transform.localScale = new Vector3((right - left) / sky.sprite.bounds.size.x * 1.02f, (top - bottom) / sky.sprite.bounds.size.y * 1.02f, 1);
            var rng = new System.Random(3);
            foreach (var s in stars)
                s.localPosition = new Vector3(Mathf.Lerp(left, right, (float)rng.NextDouble()), Mathf.Lerp(bottom + (top - bottom) * 0.35f, top, (float)rng.NextDouble()), 0);
            moon.transform.localPosition = new Vector3(Mathf.Lerp(left, right, 0.78f), Mathf.Lerp(bottom, top, 0.8f), 0);
            moon.transform.localScale = Vector3.one * 0.55f;
            moonGlow.transform.localPosition = moon.transform.localPosition;
            moonGlow.transform.localScale = Vector3.one * 2.6f;
            groundTop.transform.localScale = new Vector3((right - left) / groundTop.sprite.bounds.size.x * 1.05f, 0.05f / groundTop.sprite.bounds.size.y, 1);
        }

        float lastAspect;

        // ------------------------------------------------------------------ running

        public void NewRun()
        {
            Sim.Immortal = false;
            Sim.StartRun();
            acc = 0;
            Frozen = false;
            SimSpeed = 1;
            ClearViews();
        }

        public void StartAttract()
        {
            Attract = true;
            Sim.Immortal = true;
            if (Sim.Screen != "playing" || Sim.Mp) { Sim.LeaveMp(); Sim.StartRun(); }
            Frozen = false;
            SimSpeed = 1;
        }

        void ClearViews()
        {
            foreach (var p in pipes) p.Root.gameObject.SetActive(false);
            foreach (var c in coins) c.Root.gameObject.SetActive(false);
        }

        public bool Flap()
        {
            if (Frozen || Sim.Screen != "playing") return false;
            bool ok = Sim.Flap();
            if (ok && Settings.Effects != Settings.Fx.Off) Emit(puffs, BirdWorld() + new Vector3(-0.15f, -0.05f, 0), 4);
            return ok;
        }

        void OnDied(int score)
        {
            if (Settings.Effects != Settings.Fx.Off)
            {
                Emit(burst, BirdWorld(), 45);
                Emit(sparks, BirdWorld(), 20);
            }
            if (Settings.Shake) shake = 0.45f;
            flash = 1;
            if (Died != null) Died(score);
        }

        static void Emit(ParticleSystem ps, Vector3 at, int n)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            ps.Emit(ep, n);
        }

        Vector3 BirdWorld() => new Vector3(NativeSim.BirdX * U, (NativeSim.H - Sim.BirdY) * U, 0);

        /// <summary>The death flash (0..1) for the screen effects.</summary>
        public float Flash => flash;
        public float Punch => punch;

        void Update()
        {
            if (Sim == null) return;
            if (!Mathf.Approximately(lastAspect, Cam.aspect)) { lastAspect = Cam.aspect; Layout(); }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!Frozen)
            {
                acc += dt * SimSpeed;
                int steps = 0;
                while (acc >= Tick && steps < 8)
                {
                    if (Attract) Autopilot();
                    Sim.Step();
                    acc -= Tick; steps++;
                    if (Attract && Sim.Screen != "playing") { Sim.StartRun(); Sim.Immortal = true; }
                }
                Sim.TickRealTime(dt);
            }
            float alpha = Frozen ? 0 : Mathf.Clamp01(acc / Tick);
            bool moving = Sim.Screen == "playing" && !Frozen;
            if (moving) scroll += NativeSim.Speed * dt * 60 * SimSpeed;

            Draw(alpha, moving);

            shake = Mathf.Max(0, shake - dt);
            punch = Mathf.Max(0, punch - dt * 3);
            flash = Mathf.Max(0, flash - dt * 2.5f);
            var p = camBase;
            if (shake > 0 && Settings.Shake)
            {
                float k = shake * 0.12f;
                p += new Vector3(UnityEngine.Random.Range(-k, k), UnityEngine.Random.Range(-k, k), 0);
            }
            Cam.transform.position = p;
            Cam.orthographicSize = ViewH * U / 2 * (1 - punch * 0.012f);
        }

        void Autopilot()
        {
            NativeSim.Pipe next = null;
            foreach (var pp in Sim.Pipes) if (pp.X + NativeSim.PipeWidth > NativeSim.BirdX - 14) { next = pp; break; }
            float target = next != null ? (next.Top + next.Bottom) / 2 + 14 : 300;
            if (Sim.BirdY > target && Sim.Velocity > -1) Sim.Flap();
        }

        void Draw(float alpha, bool moving)
        {
            float left = ViewLeft * U, right = (ViewLeft + ViewW) * U;
            float groundY = (NativeSim.H - NativeSim.SkyH) * U;
            float bottom = (NativeSim.H - ViewTop - ViewH) * U;
            float t = Time.unscaledTime;

            // stars twinkle
            for (int i = 0; i < stars.Count; i++)
            {
                var sr = stars[i].GetComponent<SpriteRenderer>();
                var c = sr.color; c.a = 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(t * (0.7f + i % 5 * 0.3f) + i));
                sr.color = c;
            }

            // city layers
            float[] speeds = { 0.08f, 0.2f, 0.4f };
            for (int l = 0; l < 3; l++)
            {
                float w = cityWidth[l];
                float off = -((scroll * speeds[l] * U) % w);
                for (int k = 0; k < city[l].Count; k++)
                    city[l][k].transform.localPosition = new Vector3(left + off + k * w - 0.01f, groundY - 0.02f, 0);
            }

            // clouds drift very slowly, wrapping round
            float span = right - left + 6f, top = (NativeSim.H - ViewTop) * U;
            for (int i = 0; i < clouds.Count; i++)
            {
                float cx = left - 3f + Mathf.Repeat(cloudSeed[i].x * span - scroll * 0.03f * U - t * 0.05f, span);
                clouds[i].localPosition = new Vector3(cx, Mathf.Lerp(groundY, top, cloudSeed[i].y), 0);
            }
            float bOff = -((scroll * 0.6f * U) % bushWidth);
            for (int k = 0; k < bushes.Count; k++)
                bushes[k].transform.localPosition = new Vector3(left + bOff + k * bushWidth - 0.01f, groundY - 0.02f, 0);

            // ground (moves with the pipes)
            float gOff = -((scroll * U) % groundWidth);
            for (int k = 0; k < grounds.Count; k++)
            {
                grounds[k].transform.localPosition = new Vector3(left + gOff + k * groundWidth, groundY, 0);
                float h = groundY - bottom;
                grounds[k].transform.localScale = new Vector3(1, Mathf.Max(1, h / grounds[k].sprite.bounds.size.y), 1);
            }
            groundTop.transform.localPosition = new Vector3((left + right) / 2, groundY, 0);

            // pipes
            int pi = 0;
            float slide = alpha * NativeSim.Speed;
            foreach (var p in Sim.Pipes)
            {
                var v = PipeAt(pi++);
                float x = (p.X - slide) * U;
                float topEnd = (NativeSim.H - p.Top) * U, botStart = (NativeSim.H - p.Bottom) * U;
                float skyTop = (NativeSim.H - ViewTop) * U + 0.2f;
                v.Root.localPosition = new Vector3(x, 0, 0);
                float topLen = skyTop - topEnd;
                v.TopBody.transform.localPosition = new Vector3(0, topEnd, 0);
                v.TopBody.transform.localScale = new Vector3(1, topLen / pipeBody.bounds.size.y, 1);
                v.TopCap.transform.localPosition = new Vector3(NativeSim.PipeWidth * U / 2, topEnd + 0.1f, 0);
                float botLen = botStart - groundY + 0.05f;
                v.BotBody.transform.localPosition = new Vector3(0, groundY - 0.05f, 0);
                v.BotBody.transform.localScale = new Vector3(1, botLen / pipeBody.bounds.size.y, 1);
                v.BotCap.transform.localPosition = new Vector3(NativeSim.PipeWidth * U / 2, botStart - 0.1f, 0);
                float glow = 0.18f + 0.08f * Mathf.Sin(t * 3 + p.X * 0.02f);
                v.TopGlow.color = new Color(neon.r, neon.g, neon.b, neon.a > 0 ? glow : 0);
                v.BotGlow.color = v.TopGlow.color;
                v.TopGlow.transform.localPosition = new Vector3(NativeSim.PipeWidth * U / 2, topEnd + 0.1f, 0);
                v.BotGlow.transform.localPosition = new Vector3(NativeSim.PipeWidth * U / 2, botStart - 0.1f, 0);
            }
            for (int i = pi; i < pipes.Count; i++) if (pipes[i].Root.gameObject.activeSelf) pipes[i].Root.gameObject.SetActive(false);

            // coins
            int ci = 0;
            foreach (var c in Sim.CoinList)
            {
                if (c.HiddenUntil > Sim.Ticks) continue;      // someone else took it (multiplayer)
                var v = CoinAt(ci++);
                v.Root.localPosition = new Vector3((c.X + 10 - slide) * U, (NativeSim.H - c.Y - 10) * U + Mathf.Sin(t * 3 + c.X * 0.05f) * 0.03f, 0);
                v.Coin.transform.localScale = new Vector3(Mathf.Abs(Mathf.Cos(t * 3.5f + c.X * 0.03f)) * 0.9f + 0.1f, 1, 1) * CoinScale();
            }
            for (int i = ci; i < coins.Count; i++) if (coins[i].Root.gameObject.activeSelf) coins[i].Root.gameObject.SetActive(false);

            // bird (between the last two ticks, so it's smooth at any frame rate)
            float by = Mathf.Lerp(Sim.PrevBirdY, Sim.BirdY, alpha);
            if (Sim.Screen == "dead") by = Sim.BirdY;
            var bp = new Vector3(NativeSim.BirdX * U, (NativeSim.H - by) * U, 0);
            bird.transform.localPosition = bp;
            bird.transform.localRotation = Quaternion.Euler(0, 0, -Sim.BirdRotation * Mathf.Rad2Deg);
            bird.enabled = Sim.Screen != "dead";
            birdGlow.transform.localPosition = bp;
            birdGlow.transform.localScale = Vector3.one * (0.9f + 0.1f * Mathf.Sin(t * 6));
            birdGlow.enabled = false;

            Dress(t);
            DrawRemotes();

            // trail
            int n = Sim.Trail.Count / 2;
            trail.enabled = n > 1 && bird.enabled && HasTrail;
            if (trail.enabled)
            {
                trail.positionCount = n;
                for (int i = 0; i < n; i++)
                    trail.SetPosition(i, new Vector3((Sim.Trail[i * 2] - slide) * U, (NativeSim.H - Sim.Trail[i * 2 + 1]) * U, 0));
            }
        }

        float CoinScale() => 20f * 1.25f * U / (Res.Get("coin") != null ? Res.Get("coin").bounds.size.y : 1);

        PipeView PipeAt(int i)
        {
            while (pipes.Count <= i)
            {
                var v = new PipeView { Root = new GameObject("Pipe").transform };
                v.Root.SetParent(root, false);
                v.TopBody = Child(v.Root, "TopBody", pipeBody, 30);
                v.BotBody = Child(v.Root, "BotBody", pipeBody, 30);
                v.TopCap = Child(v.Root, "TopCap", pipeCap, 31);
                v.BotCap = Child(v.Root, "BotCap", pipeCap, 31);
                v.TopCap.transform.localScale = new Vector3(1, 0.2f / pipeCap.bounds.size.y, 1);
                v.BotCap.transform.localScale = v.TopCap.transform.localScale;
                v.TopGlow = Child(v.Root, "TopGlow", Res.Glow, 32, true);
                v.BotGlow = Child(v.Root, "BotGlow", Res.Glow, 32, true);
                v.TopGlow.transform.localScale = new Vector3(1.4f, 0.5f, 1);
                v.BotGlow.transform.localScale = v.TopGlow.transform.localScale;
                pipes.Add(v);
            }
            var r = pipes[i];
            if (!r.Root.gameObject.activeSelf) r.Root.gameObject.SetActive(true);
            return r;
        }

        CoinView CoinAt(int i)
        {
            while (coins.Count <= i)
            {
                var v = new CoinView { Root = new GameObject("Coin").transform };
                v.Root.SetParent(root, false);
                v.Glow = Child(v.Root, "Glow", Res.Glow, 39, true);
                v.Glow.color = new Color(1f, 0.8f, 0.2f, 0.35f);
                v.Glow.transform.localScale = Vector3.one * 0.5f;
                v.Coin = Child(v.Root, "Coin", Res.Get("coin"), 40);
                coins.Add(v);
            }
            var r = coins[i];
            if (!r.Root.gameObject.activeSelf) r.Root.gameObject.SetActive(true);
            return r;
        }

        SpriteRenderer Child(Transform parent, string name, Sprite s, int order, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s; r.sortingOrder = order;
            if (additive && Res.Additive != null) r.sharedMaterial = Res.Additive;
            return r;
        }

        /// <summary>Sparkles where a coin was picked up.</summary>
        public void CoinBurst() { if (Settings.Effects != Settings.Fx.Off) Emit(sparks, BirdWorld() + new Vector3(0.1f, 0, 0), 10); }
    }
}
