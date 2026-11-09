// Player settings (saved with PlayerPrefs): display mode, resolution, frame rate, effects,
// sound. Fullscreen by default on computers; windowed works too (Settings, F11 or Alt+Enter).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlappyCrix
{
    public static class Settings
    {
        public enum Display { Fullscreen = 0, Windowed = 1, Exclusive = 2 }
        public enum Fx { Off = 0, Low = 1, High = 2 }

        public static Display DisplayMode;
        public static int ResW, ResH;
        public static bool VSync;
        public static int FpsCap;          // 0 = unlimited
        public static Fx Effects;
        public static bool Shake, ShowFps, Haptics;
        public static float Music, Sfx;    // 0..1
        /// <summary>How big the menus and buttons are (0.8 .. 1.4).</summary>
        public static float UiScale = 1;
        /// <summary>How big the game itself is on screen (0.6 .. 1): smaller shows more around it.</summary>
        public static float GameSize = 1;

        public static readonly int[] FpsChoices = { 30, 60, 90, 120, 144, 240, 0 };
        public static event Action Changed;

        public static bool IsDesktop =>
            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.LinuxPlayer || Application.isEditor;

        public static bool IsMobile => Application.isMobilePlatform;

        public static void Load()
        {
            DisplayMode = (Display)PlayerPrefs.GetInt("fc.display", 0);
            ResW = PlayerPrefs.GetInt("fc.resW", 0);
            ResH = PlayerPrefs.GetInt("fc.resH", 0);
            VSync = PlayerPrefs.GetInt("fc.vsync", 1) == 1;
            FpsCap = PlayerPrefs.GetInt("fc.fps", IsMobile ? 60 : 144);
            Effects = (Fx)PlayerPrefs.GetInt("fc.fx", IsMobile ? 1 : 2);
            Shake = PlayerPrefs.GetInt("fc.shake", 1) == 1;
            ShowFps = PlayerPrefs.GetInt("fc.showfps", 0) == 1;
            Haptics = PlayerPrefs.GetInt("fc.haptics", 1) == 1;
            Music = PlayerPrefs.GetFloat("fc.music", 0.6f);
            Sfx = PlayerPrefs.GetFloat("fc.sfx", 0.9f);
            UiScale = Mathf.Clamp(PlayerPrefs.GetFloat("fc.uiscale", 1f), 0.8f, 1.4f);
            GameSize = Mathf.Clamp(PlayerPrefs.GetFloat("fc.gamesize", 1f), 0.6f, 1f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("fc.display", (int)DisplayMode);
            PlayerPrefs.SetInt("fc.resW", ResW);
            PlayerPrefs.SetInt("fc.resH", ResH);
            PlayerPrefs.SetInt("fc.vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("fc.fps", FpsCap);
            PlayerPrefs.SetInt("fc.fx", (int)Effects);
            PlayerPrefs.SetInt("fc.shake", Shake ? 1 : 0);
            PlayerPrefs.SetInt("fc.showfps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("fc.haptics", Haptics ? 1 : 0);
            PlayerPrefs.SetFloat("fc.music", Music);
            PlayerPrefs.SetFloat("fc.sfx", Sfx);
            PlayerPrefs.SetFloat("fc.uiscale", UiScale);
            PlayerPrefs.SetFloat("fc.gamesize", GameSize);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        /// <summary>Puts the display settings into effect.</summary>
        public static void ApplyDisplay()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = IsMobile ? (FpsCap == 0 ? 120 : FpsCap) : (VSync ? -1 : (FpsCap == 0 ? -1 : FpsCap));
            if (!IsDesktop || Application.isEditor) return;

            var native = Screen.currentResolution;
            int w = ResW > 0 ? ResW : native.width, h = ResH > 0 ? ResH : native.height;
            FullScreenMode mode = FullScreenMode.FullScreenWindow;
            if (DisplayMode == Display.Windowed)
            {
                mode = FullScreenMode.Windowed;
                if (ResW <= 0) { w = Mathf.Min(1600, native.width * 3 / 4); h = w * 9 / 16; }
            }
            else if (DisplayMode == Display.Exclusive)
                mode = Application.platform == RuntimePlatform.WindowsPlayer ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
            Screen.SetResolution(w, h, mode);
        }

        /// <summary>F11 / Alt+Enter: fullscreen and windowed back and forth.</summary>
        public static void ToggleFullscreen()
        {
            DisplayMode = DisplayMode == Display.Windowed ? Display.Fullscreen : Display.Windowed;
            if (DisplayMode == Display.Windowed) { ResW = 0; ResH = 0; }
            ApplyDisplay();
            Save();
        }

        /// <summary>The resolutions this screen offers (biggest first, no repeats).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v) && r.width >= 800) list.Add(v);
            }
            list.Sort((a, b) => (b.x * b.y).CompareTo(a.x * a.y));
            if (list.Count == 0) list.Add(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
            return list;
        }
    }
}
