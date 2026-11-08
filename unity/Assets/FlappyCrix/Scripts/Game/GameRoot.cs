// The whole game: starts itself in any scene (see Boot), then
//   CRIX STUDIOS splash -> loading (art, sounds, world, save) -> title -> main menu -> play.
// The game world keeps playing itself behind the menus, like most console games.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections;
using System.IO;
using FlappyCrix.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class GameRoot : MonoBehaviour
    {
        public const string Version = "2.0.0-unity";

        enum State { Boot, Title, Menu, Playing, Paused, Dead }
        State state = State.Boot;

        Camera cam;
        Canvas canvas;
        CanvasScaler scaler;
        World world;
        SaveData save;
        Season season;
        ClipRecorder recorder;
        Toasts toasts;

        SplashScreen splash;
        LoadingScreen loading;
        TitleScreen title;
        MenuScreen menu;
        SettingsScreen settings;
        CreditsScreen credits;
        HudScreen hud;
        PauseScreen pause;
        GameOverScreen gameOver;
        Panel[] panels;

        int runCoins;
        float deathAt;
        bool settingsFromPause;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Settings.Load();
            Settings.ApplyDisplay();
            Res.LoadBoot();
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = !Application.isMobilePlatform;
            cam.allowMSAA = false;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<PostFx>();
            recorder = camGo.AddComponent<ClipRecorder>();
            Sfx.Create(gameObject);

            canvas = Ui.MakeCanvas(transform, cam, out scaler);
            splash = new SplashScreen(canvas.transform);
            loading = new LoadingScreen(canvas.transform);
            toasts = new Toasts(canvas.transform);
            ClipRecorder.Notice += s => toasts.Show(s, Ui.Red);
            StartCoroutine(Boot());
        }

        // ------------------------------------------------------------------ boot

        IEnumerator Boot()
        {
            // 1. CRIX STUDIOS (any button skips it after a moment)
            splash.Show();
            var load = StartCoroutine(Res.LoadAll((p, what) => loading.SetProgress(p * 0.8f, what)));
            float t = 0;
            while (t < 3.2f && !(t > 0.8f && Inputs.Any())) { t += Time.unscaledDeltaTime; yield return null; }
            splash.Hide();
            loading.Show();
            yield return new WaitForSecondsRealtime(0.35f);

            // 2. everything else
            while (!Res.Loaded) yield return null;
            loading.SetProgress(0.82f, "Loading your progress");
            yield return null;
            save = SaveData.Load(Path.Combine(Application.persistentDataPath, "save.txt"), m => Debug.Log("[FlappyCrix] " + m));
            season = Season.Pick(save.SeasonChoice, Season.SydneyToday(DateTime.UtcNow));
            loading.SetProgress(0.88f, "Building the world");
            yield return null;
            var wgo = new GameObject("Game");
            wgo.transform.SetParent(transform, false);
            world = wgo.AddComponent<World>();
            world.Build(cam, season);
            world.Scored += OnScored;
            world.Died += OnDied;
            world.CoinsGained += n => { runCoins += n; hud.SetCoins(runCoins); world.CoinBurst(); };
            loading.SetProgress(0.94f, "Building menus");
            yield return null;
            BuildScreens();
            loading.SetProgress(1f, "Ready");
            while (!loading.Done) yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            Sfx.Music(season != null && season.Music != null ? season.Music : "music");
            loading.Hide();
            PostFx.Glitch(1);
            Sfx.Play("woosh", 0.7f);
            GoTitle();
        }

        void BuildScreens()
        {
            var root = canvas.transform;
            title = new TitleScreen(root, Version);
            menu = new MenuScreen(root, save, StartRun, () => toasts.Show("ONLINE IS COMING IN THE NEXT UPDATE", Ui.Orange),
                                  () => OpenSettings(false), () => { Hide(menu); credits.Show(); }, Quit);
            settings = new SettingsScreen(root);
            settings.Closed = CloseSettings;
            credits = new CreditsScreen(root, Version);
            credits.Closed = () => { credits.Hide(); menu.Show(); };
            hud = new HudScreen(root, () => { if (state == State.Playing) Pause(); });
            pause = new PauseScreen(root, Resume, () => OpenSettings(true), () => { pause.Hide(); EndRunToMenu(); });
            gameOver = new GameOverScreen(root, () => { if (gameOver.Ready) StartRun(); }, () => { gameOver.Hide(); EndRunToMenu(); });
            panels = new Panel[] { splash, loading, title, menu, settings, credits, hud, pause, gameOver };
            hud.ShowPauseButton(Settings.IsMobile);
        }

        static void Hide(Panel p) { if (p != null) p.Hide(); }

        // ------------------------------------------------------------------ states

        void GoTitle()
        {
            state = State.Title;
            world.StartAttract();
            title.Show();
        }

        void GoMenu()
        {
            state = State.Menu;
            title.Hide();
            menu.Show();
            PostFx.Glitch(0.6f);
            Sfx.Play("select", 0.7f);
        }

        void StartRun()
        {
            if (gameOver.Visible) gameOver.Hide();
            menu.Hide();
            state = State.Playing;
            runCoins = 0;
            world.Attract = false;
            world.NewRun();
            hud.SetScore(0); hud.SetCoins(0);
            hud.HintAlpha = 1;
            hud.Show();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (PostFx.Inst != null) PostFx.Inst.Desaturate = 0;
            Sfx.Duck(false);
            Sfx.RestartMusic();
            Sfx.Play("start", 0.6f);
            PostFx.Glitch(0.5f);
        }

        void Pause()
        {
            state = State.Paused;
            world.Frozen = true;
            Sfx.Duck(true);
            pause.Show();
        }

        void Resume()
        {
            pause.Hide();
            state = State.Playing;
            world.Frozen = false;
            Sfx.Duck(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void EndRunToMenu()
        {
            hud.Hide();
            world.Attract = true;
            world.StartAttract();
            if (PostFx.Inst != null) PostFx.Inst.Desaturate = 0;
            Sfx.Duck(false);
            state = State.Menu;
            menu.Show();
        }

        void OpenSettings(bool fromPause)
        {
            settingsFromPause = fromPause;
            if (fromPause) pause.Hide(); else menu.Hide();
            settings.Show();
        }

        void CloseSettings()
        {
            settings.Hide();
            if (settingsFromPause) pause.Show(); else menu.Show();
        }

        void Quit()
        {
            Flush();
            Application.Quit();
        }

        // ------------------------------------------------------------------ game events

        void OnScored(int s)
        {
            hud.SetScore(s);
            hud.HintAlpha = 0;
            save.Xp++;
            int levels = 0;
            while (save.Level < Catalog.MaxLevel && save.Xp >= Catalog.XpForLevel(save.Level))
            {
                save.Xp -= Catalog.XpForLevel(save.Level);
                save.Level++; levels++;
            }
            if (save.Level >= Catalog.MaxLevel) save.Xp = 0;
            if (levels > 0) { toasts.Show("LEVEL UP!  LEVEL " + save.Level, Ui.Gold); Sfx.Play("levelup", 0.8f); }
            save.Dirty = true;
            if (s % 10 == 0) { PostFx.Glitch(0.45f); PostFx.Kick(0.6f); }
            else PostFx.Kick(0.15f);
            if (s == 67) Sfx.Play("67");
        }

        void OnDied(int score)
        {
            state = State.Dead;
            deathAt = Time.unscaledTime;
            hud.Hide();
            PostFx.Flash(new Color(1f, 0.2f, 0.25f), 1);
            PostFx.Glitch(1);
            PostFx.Kick(1);
            Sfx.Duck(true);
            bool newBest = score > save.Best;
            if (newBest) save.Best = score;
            save.Games++;
            save.Coins += runCoins;
            save.Dirty = true;
            Flush();
            gameOver.Set(score, save.Best, newBest, runCoins, 0, save.Level);
            if (newBest && score > 0) Sfx.Play("victory", 0.7f);
            StartCoroutine(ShowGameOver());
#if UNITY_ANDROID || UNITY_IOS
            if (Settings.Haptics) Handheld.Vibrate();
#endif
        }

        IEnumerator ShowGameOver()
        {
            float t = 0;
            while (t < 0.75f)
            {
                t += Time.unscaledDeltaTime;
                if (PostFx.Inst != null) PostFx.Inst.Desaturate = Mathf.Lerp(0, 0.65f, t / 0.75f);
                yield return null;
            }
            if (state == State.Dead) gameOver.Show();
        }

        void Flush()
        {
            if (save != null && save.Dirty) { try { save.Save(); } catch (Exception e) { Debug.LogWarning("[FlappyCrix] save failed: " + e.Message); } }
        }

        void OnApplicationPause(bool paused) { if (paused) { Flush(); if (state == State.Playing) Pause(); } }
        void OnApplicationFocus(bool focus) { if (!focus && state == State.Playing && Settings.IsMobile) Pause(); }
        void OnApplicationQuit() { Flush(); }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (panels != null) foreach (var p in panels) p.Update(dt);
            else { splash.Update(dt); loading.Update(dt); }
            toasts.Tick();

            // UI scale: landscape by height, portrait by width
            bool portrait = Screen.height > Screen.width;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = portrait ? 0 : 1;

            if (Settings.IsDesktop && (Inputs.ToggleFullscreen() || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.Return))))
                Settings.ToggleFullscreen();

            switch (state)
            {
                case State.Title:
                    if (Inputs.Any()) GoMenu();
                    break;
                case State.Menu:
                    if (credits.Visible && Inputs.Back()) { credits.Hide(); menu.Show(); }
                    else if (settings.Visible && Inputs.Back()) CloseSettings();
                    else if (menu.Visible && Inputs.Back()) { menu.Hide(); GoTitle(); }
                    break;
                case State.Playing:
                    if (Inputs.Pause()) { Pause(); break; }
                    if (Inputs.Flap()) world.Flap();
                    break;
                case State.Paused:
                    if (settings.Visible) { if (Inputs.Back()) CloseSettings(); }
                    else if (Inputs.Pause() || Input.GetKeyDown(KeyCode.JoystickButton1)) Resume();
                    break;
                case State.Dead:
                    if (gameOver.Visible && gameOver.Ready)
                    {
                        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R)) StartRun();
                        else if (Inputs.Back()) { gameOver.Hide(); EndRunToMenu(); }
                    }
                    break;
            }
        }
    }
}
