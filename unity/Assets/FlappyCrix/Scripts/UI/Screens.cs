// Every screen of the game, built in code. GameRoot shows one at a time (plus the HUD).
//   Splash   - the CRIX STUDIOS boot screen (instead of Unity's)
//   Loading  - progress bar, what's loading, tips
//   Title    - giant glitchy logo over the live game, PRESS ANY BUTTON
//   Menu     - in MenuScreen.cs
//   Settings - display (fullscreen / windowed / resolution), frame rate, effects, sound
//   Credits, Hud, Pause, GameOver
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using System.Globalization;
using FlappyCrix.Native;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    public abstract class Panel
    {
        public RectTransform Root;
        public CanvasGroup Group;
        public bool Visible { get; private set; }
        float alpha, target;
        int laidOut;
        public float FadeSpeed = 6;
        protected Selectable FirstSelect;

        protected Panel(Transform canvas, string name)
        {
            Root = Ui.Fill(Ui.Rect(canvas, name));
            Group = Ui.Group(Root.gameObject);
            Group.alpha = 0; Group.interactable = false; Group.blocksRaycasts = false;
            Root.gameObject.SetActive(false);
        }

        public virtual void Show()
        {
            Visible = true; target = 1;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Group.interactable = true; Group.blocksRaycasts = true;
            OnShow();
            if (FirstSelect != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(FirstSelect.gameObject);
        }

        public virtual void Hide()
        {
            Visible = false; target = 0;
            Group.interactable = false; Group.blocksRaycasts = false;
        }

        protected virtual void OnShow() { }
        public virtual void Tick(float dt) { }

        public void Update(float dt)
        {
            if (!Root.gameObject.activeSelf) return;
            alpha = Mathf.MoveTowards(alpha, target, dt * FadeSpeed);
            Group.alpha = alpha;
            if (alpha <= 0 && !Visible) { Root.gameObject.SetActive(false); return; }
            if (laidOut != Ui.FormVersion) { laidOut = Ui.FormVersion; Layout(Ui.CurrentForm); }
            Tick(dt);
        }

        /// <summary>Arrange the screen for this kind of device (desktop, phone sideways, upright).</summary>
        protected virtual void Layout(Ui.Form form) { }

        /// <summary>The website's background: near black (#0A0A0F) with its faint red grid.</summary>
        protected static Image Shade(Transform parent, float a)
        {
            var i = Ui.Img(parent, "Shade", Res.White, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, a));
            Ui.Fill(i.rectTransform);
            var g = Ui.Img(parent, "Grid", Res.Grid(), new Color(1, 1, 1, Mathf.Clamp01(a + 0.1f)));
            g.type = Image.Type.Tiled;
            Ui.Fill(g.rectTransform);
            return i;
        }

        /// <summary>The site's little green line under the title.</summary>
        protected static Text Tagline(Transform parent, string text, int size)
        {
            return Ui.Label(parent, text, size, Ui.Green, TextAnchor.MiddleLeft, false);
        }
    }

    // ---------------------------------------------------------------------- splash

    public sealed class SplashScreen : Panel
    {
        Image logo;
        public float Age;

        public SplashScreen(Transform canvas) : base(canvas, "Splash")
        {
            FadeSpeed = 2.5f;
            var bg = Ui.Img(Root, "Black", Res.White, Color.black);
            Ui.Fill(bg.rectTransform);
            logo = Ui.Img(Root, "CRIX STUDIOS", Res.Get("crix-studios"), Color.white);
            logo.preserveAspect = true;
            Ui.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 1000));
        }

        public override void Tick(float dt)
        {
            Age += dt;
            float s = 1 + Age * 0.015f;
            logo.rectTransform.localScale = new Vector3(s, s, 1);
            // short glitches, as the logo arrives and now and then
            if (Age > 0.25f && Age < 0.4f) PostFx.Glitch(0.9f);
            if (UnityEngine.Random.value < dt * 0.9f) PostFx.Glitch(0.5f);
        }
    }

    // ---------------------------------------------------------------------- loading

    public sealed class LoadingScreen : Panel
    {
        Image fill, birdIcon;
        Text status, tip, percent;
        float shown, want;
        float tipAt;
        int tipIndex;
        static readonly string[] Tips =
        {
            "Tap, click, SPACE or A to flap.",
            "Coins sit between the pipes. Spend them on hats and trails.",
            "Every 10 pipes is a milestone. Don't get cocky.",
            "F11 or Alt+Enter switches between fullscreen and a window.",
            "F9 records a clip. F12 takes a screenshot.",
            "Your best score is saved on this device.",
            "The gap gets smaller the higher your score.",
            "Made by CRIX STUDIOS.",
        };

        public LoadingScreen(Transform canvas) : base(canvas, "Loading")
        {
            var bg = Ui.Img(Root, "Bg", Res.MakeSprite(Res.VGradient(new Color(0.08f, 0.02f, 0.06f), new Color(0.01f, 0.01f, 0.02f)), 100, "loadbg"), Color.white);
            Ui.Fill(bg.rectTransform);
            var title = GlitchText.Make(Root, "FLAPPY CRIX", 120, Color.white, TextAnchor.MiddleCenter);
            Ui.Place((RectTransform)title.transform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 200));
            title.Main.color = Color.white;

            var barBack = Ui.Img(Root, "BarBack", Res.White, new Color(1, 1, 1, 0.08f));
            Ui.Place(barBack.rectTransform, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 10));
            fill = Ui.Img(barBack.rectTransform, "Fill", Res.Rounded(5, 0, Ui.Red, Ui.Orange), Color.white);
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.pivot = new Vector2(0, 0.5f); fill.rectTransform.sizeDelta = new Vector2(0, 0);
            var glow = Ui.Img(fill.rectTransform, "Glow", Res.Glow, new Color(1, 0.4f, 0.3f, 0.8f));
            Ui.Place(glow.rectTransform, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));

            status = Ui.Label(Root, "LOADING", 30, Color.white, TextAnchor.MiddleLeft);
            Ui.Place(status.rectTransform, new Vector2(0.5f, 0.32f), new Vector2(0, 0.5f), new Vector2(-450, 42), new Vector2(700, 40));
            percent = Ui.Label(Root, "0%", 30, Ui.Orange, TextAnchor.MiddleRight);
            Ui.Place(percent.rectTransform, new Vector2(0.5f, 0.32f), new Vector2(1, 0.5f), new Vector2(450, 42), new Vector2(200, 40));
            tip = Ui.Label(Root, "", 26, Ui.Dim, TextAnchor.MiddleCenter, false);
            Ui.Place(tip.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 60));

            birdIcon = Ui.Img(Root, "Bird", Res.Get("bird"), Color.white);
            birdIcon.preserveAspect = true;
            Ui.Place(birdIcon.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 50), new Vector2(110, 110));
            var cs = Ui.Label(Root, "CRIX STUDIOS", 22, new Color(1, 1, 1, 0.35f), TextAnchor.LowerLeft, false);
            Ui.Place(cs.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 50), new Vector2(500, 40));
            tipIndex = UnityEngine.Random.Range(0, Tips.Length);
            tip.text = "TIP: " + Tips[tipIndex];
        }

        public void SetProgress(float p, string what) { want = Mathf.Clamp01(p); status.text = what.ToUpperInvariant(); }
        public bool Done => shown >= 0.999f;

        public override void Tick(float dt)
        {
            shown = Mathf.MoveTowards(shown, want, dt * 1.6f);
            fill.rectTransform.sizeDelta = new Vector2(900 * shown, 0);
            percent.text = Mathf.RoundToInt(shown * 100) + "%";
            float t = Time.unscaledTime;
            birdIcon.rectTransform.anchoredPosition = new Vector2(-60, 50 + Mathf.Abs(Mathf.Sin(t * 4)) * 26);
            birdIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 4) * 12);
            if (t > tipAt) { tipAt = t + 3.5f; tipIndex = (tipIndex + 1) % Tips.Length; tip.text = "TIP: " + Tips[tipIndex]; }
        }
    }

    // ---------------------------------------------------------------------- title

    public sealed class TitleScreen : Panel
    {
        Text press;
        GlitchText logo;

        public TitleScreen(Transform canvas, string version) : base(canvas, "Title")
        {
            Shade(Root, 0.35f);
            var band = Ui.Img(Root, "Band", Res.White, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.55f));
            Ui.Place(band.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 330));
            logo = GlitchText.Make(Root, "FLAPPY CRIX", 190, Color.white, TextAnchor.MiddleCenter);
            Ui.Place((RectTransform)logo.transform, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2000, 260));
            logo.Intensity = 1.4f;
            var sub = Ui.Label(Root, "T A P   ·   C O L L E C T   ·   D R I P", 32, Ui.Green, TextAnchor.MiddleCenter, false);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 50));
            press = Ui.Label(Root, Settings.IsMobile ? "TAP TO START" : "PRESS ANY BUTTON", 44, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(press.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 70));
            var cs = Ui.Label(Root, "© CRIX STUDIOS", 22, new Color(1, 1, 1, 0.45f), TextAnchor.LowerLeft, false);
            Ui.Place(cs.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(50, 36), new Vector2(600, 40));
            var v = Ui.Label(Root, "v" + version, 22, new Color(1, 1, 1, 0.45f), TextAnchor.LowerRight, false);
            Ui.Place(v.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 36), new Vector2(400, 40));
        }

        protected override void Layout(Ui.Form form)
        {
            bool tall = form == Ui.Form.Portrait;
            logo.Main.fontSize = tall ? 128 : 190;
            ((RectTransform)logo.transform).sizeDelta = new Vector2(tall ? 1040 : 2000, tall ? 180 : 260);
        }

        public override void Tick(float dt)
        {
            float t = Time.unscaledTime;
            var c = press.color; c.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 2.2f)); press.color = c;
            float s = 1 + Mathf.Sin(t * 1.3f) * 0.012f;
            logo.transform.localScale = new Vector3(s, s, 1);
        }
    }

    // ---------------------------------------------------------------------- settings

    public sealed class SettingsScreen : Panel
    {
        readonly List<OptionRow> rows = new List<OptionRow>();
        public Action Closed;

        public SettingsScreen(Transform canvas) : base(canvas, "Settings")
        {
            Shade(Root, 0.82f);
            var title = GlitchText.Make(Root, "SETTINGS", 90, Color.white, TextAnchor.MiddleLeft);
            Ui.Place((RectTransform)title.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -60), new Vector2(1000, 120));
            var list = Ui.Rect(Root, "Rows");
            Ui.Place(list, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1000, 760));
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10; v.childControlHeight = false; v.childControlWidth = false; v.childForceExpandHeight = false; v.childForceExpandWidth = false;
            v.childAlignment = TextAnchor.UpperCenter;

            if (Settings.IsDesktop)
            {
                rows.Add(Ui.Option(list, "DISPLAY", () => Settings.DisplayMode == Settings.Display.Windowed ? "WINDOWED" : Settings.DisplayMode == Settings.Display.Exclusive ? "EXCLUSIVE" : "FULLSCREEN",
                    d => { Settings.DisplayMode = (Settings.Display)(((int)Settings.DisplayMode + d + 3) % 3); Apply(); }));
                rows.Add(Ui.Option(list, "RESOLUTION", () => Settings.ResW > 0 ? Settings.ResW + " x " + Settings.ResH : "NATIVE",
                    d => { var all = Settings.Resolutions(); int i = all.FindIndex(r => r.x == Settings.ResW && r.y == Settings.ResH); i = (i + d + all.Count + 1) % (all.Count + 1);
                           if (i == all.Count) { Settings.ResW = 0; Settings.ResH = 0; } else { Settings.ResW = all[i].x; Settings.ResH = all[i].y; } Apply(); }));
                rows.Add(Ui.Option(list, "V-SYNC", () => Settings.VSync ? "ON" : "OFF", d => { Settings.VSync = !Settings.VSync; Apply(); }));
            }
            rows.Add(Ui.Option(list, "FRAME RATE", () => Settings.FpsCap == 0 ? "UNLIMITED" : Settings.FpsCap + " FPS",
                d => { int i = Array.IndexOf(Settings.FpsChoices, Settings.FpsCap); if (i < 0) i = 1; i = (i + d + Settings.FpsChoices.Length) % Settings.FpsChoices.Length; Settings.FpsCap = Settings.FpsChoices[i]; Apply(); }));
            rows.Add(Ui.Option(list, "EFFECTS", () => Settings.Effects.ToString().ToUpperInvariant(),
                d => { Settings.Effects = (Settings.Fx)(((int)Settings.Effects + d + 3) % 3); Settings.Save(); }));
            rows.Add(Ui.Option(list, "SCREEN SHAKE", () => Settings.Shake ? "ON" : "OFF", d => { Settings.Shake = !Settings.Shake; Settings.Save(); }));
            rows.Add(Ui.Option(list, "MUSIC", () => Mathf.RoundToInt(Settings.Music * 10) * 10 + "%", d => { Settings.Music = Mathf.Clamp01(Mathf.Round(Settings.Music * 10 + d) / 10); Settings.Save(); }));
            rows.Add(Ui.Option(list, "SOUND EFFECTS", () => Mathf.RoundToInt(Settings.Sfx * 10) * 10 + "%", d => { Settings.Sfx = Mathf.Clamp01(Mathf.Round(Settings.Sfx * 10 + d) / 10); Settings.Save(); Sfx.Play("coin"); }));
            rows.Add(Ui.Option(list, "SHOW FPS", () => Settings.ShowFps ? "ON" : "OFF", d => { Settings.ShowFps = !Settings.ShowFps; Settings.Save(); }));
            Ui.Chain(rows.ToArray());
            FirstSelect = rows[0];

            var hint = Ui.Label(Root, Settings.IsMobile ? "TAP A ROW TO CHANGE IT" : "ARROWS / D-PAD TO CHANGE  ·  ESC / B TO GO BACK", 22, Ui.Dim, TextAnchor.MiddleCenter, false);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(1600, 40));
            var back = Ui.MenuButton(Root, "BACK", null, () => { if (Closed != null) Closed(); }, 300, 72);
            Ui.Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(90, 50), new Vector2(300, 72));
        }

        static void Apply() { Settings.ApplyDisplay(); Settings.Save(); }

        protected override void OnShow() { foreach (var r in rows) r.Refresh(); }
    }

    // ---------------------------------------------------------------------- credits

    public sealed class CreditsScreen : Panel
    {
        public Action Closed;

        public CreditsScreen(Transform canvas, string version) : base(canvas, "Credits")
        {
            Shade(Root, 0.88f);
            var title = GlitchText.Make(Root, "CREDITS", 90, Color.white, TextAnchor.MiddleCenter);
            Ui.Place((RectTransform)title.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1000, 120));
            string body =
                "FLAPPY CRIX\n<color=#FF4655>CRIX STUDIOS</color>\n\n" +
                "GAME, ART, SOUNDS AND MUSIC\nCRIX  -  crixgamingvr.com\n\n" +
                "UNITY VERSION\nmade with AI (Claude by Anthropic)\n\n" +
                "FONT  Bubble Sans (SIL Open Font License)\n" +
                "Made with Unity\n\n" +
                "Inspired by Flappy Bird by Dong Nguyen.\nNot affiliated with or endorsed by Dong Nguyen or .Gears.\n\n" +
                "v" + version;
            var t = Ui.Label(Root, body, 30, Color.white, TextAnchor.MiddleCenter, false);
            t.supportRichText = true;
            t.lineSpacing = 1.15f;
            Ui.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1500, 800));
            var back = Ui.MenuButton(Root, "BACK", null, () => { if (Closed != null) Closed(); }, 300, 72);
            Ui.Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(90, 50), new Vector2(300, 72));
            FirstSelect = back;
        }
    }

    // ---------------------------------------------------------------------- in game

    public sealed class HudScreen : Panel
    {
        readonly Text score, coins, hint, fps, rec, standings, centre, mode;
        readonly GameObject endButton;
        readonly GameObject pauseButton;
        float pop, fpsAt, fpsAcc; int fpsFrames;
        public float HintAlpha;

        public HudScreen(Transform canvas, Action pause, Action endMatch) : base(canvas, "Hud")
        {
            FadeSpeed = 10;
            score = Ui.Label(Root, "0", 150, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(score.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(600, 180));
            var outline = score.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f); outline.effectDistance = new Vector2(4, -4);

            var coinIcon = Ui.Img(Root, "CoinIcon", Res.Get("coin"), Color.white);
            coinIcon.preserveAspect = true;
            Ui.Place(coinIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -50), new Vector2(64, 64));
            coins = Ui.Label(Root, "0", 50, Ui.Gold);
            Ui.Place(coins.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, -52), new Vector2(400, 64));

            hint = Ui.Label(Root, Settings.IsMobile ? "TAP TO FLAP" : "SPACE / CLICK / A TO FLAP", 36, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 60));

            var pb = Ui.Rect(Root, "Pause");
            Ui.Place(pb, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(110, 110));
            var img = pb.gameObject.AddComponent<Image>();
            img.sprite = Res.White; img.color = new Color(0, 0, 0, 0.35f);
            var b = pb.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => pause());
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var bars = Ui.Label(pb, "II", 54, Color.white, TextAnchor.MiddleCenter);
            Ui.Fill(bars.rectTransform);
            pauseButton = pb.gameObject;

            standings = Ui.Label(Root, "", 28, Color.white, TextAnchor.UpperRight);
            standings.supportRichText = true;
            Ui.Place(standings.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -170), new Vector2(560, 400));
            mode = Ui.Label(Root, "", 26, Ui.Gold, TextAnchor.UpperLeft);
            Ui.Place(mode.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -130), new Vector2(700, 40));
            centre = Ui.Label(Root, "", 64, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(centre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1600, 100));
            var eb = Ui.SmallButton(Root, "END MATCH", endMatch, new Color(0.4f, 0.1f, 0.15f), 300, 64, 24);
            Ui.Place((RectTransform)eb.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(300, 64));
            var enav = eb.navigation; enav.mode = Navigation.Mode.None; eb.navigation = enav;
            endButton = eb.gameObject;
            endButton.SetActive(false);

            fps = Ui.Label(Root, "", 22, Ui.Green, TextAnchor.LowerLeft, false);
            Ui.Place(fps.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 24), new Vector2(300, 30));
            rec = Ui.Label(Root, "", 28, Ui.Red, TextAnchor.UpperRight);
            Ui.Place(rec.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -60), new Vector2(400, 40));
        }

        public void SetScore(int s) { score.text = s.ToString(CultureInfo.InvariantCulture); pop = 1; }
        public void SetCoins(int c) { coins.text = c.ToString(CultureInfo.InvariantCulture); }
        public void ShowPauseButton(bool on) { pauseButton.SetActive(on); }

        /// <summary>Multiplayer: the standings (rich text), the mode line, a big centre message, the host's END button.</summary>
        public void SetMatch(string table, string modeLine, string middle, bool canEnd)
        {
            standings.text = table ?? "";
            mode.text = modeLine ?? "";
            centre.text = middle ?? "";
            if (endButton.activeSelf != canEnd) endButton.SetActive(canEnd);
        }

        public override void Tick(float dt)
        {
            pop = Mathf.MoveTowards(pop, 0, dt * 5);
            float s = 1 + pop * 0.25f;
            score.rectTransform.localScale = new Vector3(s, s, 1);
            score.color = Color.Lerp(Color.white, Ui.Gold, pop);
            var c = hint.color; c.a = HintAlpha * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3))); hint.color = c;
            fpsAcc += Time.unscaledDeltaTime; fpsFrames++;
            if (Time.unscaledTime > fpsAt) { fps.text = Settings.ShowFps && fpsAcc > 0 ? Mathf.RoundToInt(fpsFrames / fpsAcc) + " FPS" : ""; fpsAt = Time.unscaledTime + 0.5f; fpsAcc = 0; fpsFrames = 0; }
            rec.text = ClipRecorder.Recording ? "● REC " + FormatTime(ClipRecorder.Seconds) : "";
        }

        static string FormatTime(float s) { int t = (int)s; return (t / 60).ToString("00") + ":" + (t % 60).ToString("00"); }
    }

    public sealed class PauseScreen : Panel
    {
        public PauseScreen(Transform canvas, Action resume, Action settings, Action menu) : base(canvas, "Pause")
        {
            FadeSpeed = 10;
            Shade(Root, 0.7f);
            pauseTitle = GlitchText.Make(Root, "PAUSED", 120, Color.white, TextAnchor.MiddleLeft);
            var list = Ui.Rect(Root, "Buttons");
            pauseList = list;
            Ui.Place(list, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -40), new Vector2(640, 360));
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.childControlHeight = false; v.childControlWidth = false; v.childForceExpandHeight = false; v.childForceExpandWidth = false;
            var a = Ui.MenuButton(list, "RESUME", null, resume, 560, 84, "25b6", Ui.Look.Primary);
            var b = Ui.MenuButton(list, "SETTINGS", null, settings, 560, 84, "2699");
            var c = Ui.MenuButton(list, "MAIN MENU", "Ends this run", menu, 560, 84, "1f6aa");
            Ui.Chain(new Selectable[] { a, b, c });
            FirstSelect = a;
        }

        readonly GlitchText pauseTitle;
        readonly RectTransform pauseList;

        protected override void Layout(Ui.Form form)
        {
            if (form == Ui.Form.Portrait)
            {
                pauseTitle.Main.alignment = TextAnchor.MiddleCenter;
                Ui.Place((RectTransform)pauseTitle.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1000, 160));
                Ui.Place(pauseList, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, -60), new Vector2(640, 360));
            }
            else
            {
                pauseTitle.Main.alignment = TextAnchor.MiddleLeft;
                Ui.Place((RectTransform)pauseTitle.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 260), new Vector2(1000, 160));
                Ui.Place(pauseList, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -40), new Vector2(640, 360));
            }
        }
    }

    public sealed class GameOverScreen : Panel
    {
        readonly Text score, best, coins, badge, xp;
        readonly GlitchText title;
        float age;
        readonly Selectable retry;

        public GameOverScreen(Transform canvas, Action again, Action menu) : base(canvas, "GameOver")
        {
            FadeSpeed = 4;
            Shade(Root, 0.55f);
            var card = Ui.Card(Root, "Card", Ui.Red);
            Ui.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(720, 640));
            title = GlitchText.Make(card, "GAME OVER", 80, Color.white, TextAnchor.MiddleCenter);
            Ui.Place((RectTransform)title.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(680, 110));
            badge = Ui.Label(card, "NEW BEST!", 30, Ui.Gold, TextAnchor.MiddleCenter);
            Ui.Place(badge.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -144), new Vector2(600, 44));
            score = Box(card, "SCORE", -200, -220);
            best = Box(card, "BEST", -200, 0);
            coins = Box(card, "COINS", -200, 220);
            xp = Ui.Label(card, "", 24, Ui.Dim, TextAnchor.MiddleCenter, false);
            Ui.Place(xp.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(640, 36));
            var r = Ui.MenuButton(card, "RETRY", null, again, 600, 88, "25b6", Ui.Look.Primary);
            Ui.Place((RectTransform)r.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(600, 88));
            var m = Ui.MenuButton(card, "MAIN MENU", null, menu, 600, 80, "1f6aa");
            Ui.Place((RectTransform)m.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(600, 80));
            var n1 = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = m, selectOnUp = m };
            var n2 = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = r, selectOnUp = r };
            r.navigation = n1; m.navigation = n2;
            retry = r;
            FirstSelect = r;
        }

        /// <summary>One of the site's three stat boxes: a small green label over a big number.</summary>
        static Text Box(RectTransform card, string name, float y, float x)
        {
            var b = Ui.Img(card, name, Res.Rounded(16), new Color(0.04f, 0.04f, 0.06f, 1));
            Ui.Place(b.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y), new Vector2(200, 120));
            var e = Ui.Img(b.rectTransform, "Edge", Res.Rounded(16, 2), new Color(1, 1, 1, 0.08f));
            Ui.Fill(e.rectTransform);
            var l = Ui.Label(b.rectTransform, name, 20, Ui.Green, TextAnchor.MiddleCenter, false);
            Ui.Place(l.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(190, 30));
            var v = Ui.Label(b.rectTransform, "0", 50, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(v.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(190, 64));
            return v;
        }

        public void Set(int s, int bestScore, bool newBest, int runCoins, int levelGained, int level)
        {
            var inv = CultureInfo.InvariantCulture;
            score.text = s.ToString(inv);
            badge.gameObject.SetActive(newBest);
            best.text = bestScore.ToString(inv);
            coins.text = runCoins.ToString(inv);
            xp.text = levelGained > 0 ? "LEVEL UP!  NOW LEVEL " + level : "+" + s + " XP";
            title.SetText("GAME OVER");
            age = 0;
        }

        /// <summary>Retry only works after a short moment (so a panicked flap doesn't skip the screen).</summary>
        public bool Ready => age > 0.45f;

        public override void Tick(float dt)
        {
            age += dt;
            float s = age < 0.25f ? Mathf.Lerp(1.4f, 1f, age / 0.25f) : 1;
            score.rectTransform.localScale = new Vector3(s, s, 1);
            badge.color = Color.Lerp(Ui.Gold, Color.white, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5)));
            if (retry != null) retry.interactable = Ready;
        }
    }

    /// <summary>Short messages at the top of the screen (level up, clip saved...).</summary>
    public sealed class Toasts
    {
        readonly RectTransform root;
        readonly List<KeyValuePair<RectTransform, float>> live = new List<KeyValuePair<RectTransform, float>>();

        public Toasts(Transform canvas)
        {
            root = Ui.Rect(canvas, "Toasts");
            Ui.Place(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(1000, 400));
        }

        public void Show(string text, Color colour)
        {
            var bg = Ui.Img(root, "Toast", Res.Rounded(18), new Color(0.07f, 0.07f, 0.1f, 0.97f));
            Ui.Place(bg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -live.Count * 86), new Vector2(900, 74));
            var edge = Ui.Img(bg.rectTransform, "Edge", Res.Rounded(18, 3), colour);
            Ui.Fill(edge.rectTransform);
            var t = Ui.Label(bg.rectTransform, text, 30, Color.white, TextAnchor.MiddleCenter);
            Ui.Fill(t.rectTransform);
            live.Add(new KeyValuePair<RectTransform, float>(bg.rectTransform, Time.unscaledTime + 3.2f));
            root.SetAsLastSibling();
        }

        public void Tick()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var k = live[i];
                float left = k.Value - Time.unscaledTime;
                var g = Ui.Group(k.Key.gameObject);
                g.alpha = Mathf.Clamp01(left * 2.5f);
                if (left <= 0) { UnityEngine.Object.Destroy(k.Key.gameObject); live.RemoveAt(i); }
            }
        }
    }
}
