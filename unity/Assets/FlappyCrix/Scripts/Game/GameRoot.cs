// The whole game: starts itself in any scene (see Boot), then
//   CRIX STUDIOS splash -> loading (art, sounds, world, save, online) -> title -> main menu.
// From the menu: solo runs, online (lobby -> room -> match -> results, with website players),
// the shop, daily rewards, awards, the account, settings, credits.
// The game world keeps playing itself behind the menus, like most console games.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlappyCrix.Native;
using FlappyCrix.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class GameRoot : MonoBehaviour
    {
        public const string Version = "2.1.0-unity";

        enum State { Boot, Title, Menu, Playing, Paused, Dead, Online, Match }
        State state = State.Boot;

        Camera cam;
        Canvas canvas;
        CanvasScaler scaler;
        World world;
        Progress progress;
        OnlineHub online;
        Season season;
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
        ShopScreen shop;
        DailyScreen daily;
        AwardsScreen awards;
        AccountScreen account;
        LobbyScreen lobby;
        RoomScreen room;
        ResultsScreen results;
        Panel[] panels;
        Panel returnTo;                  // where BACK goes from settings / account

        int runCoins;
        bool resultsShown;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Settings.Load();
            Settings.ApplyDisplay();
            Res.LoadBoot();
            Application.runInBackground = true;        // keeps the online connection alive in the background
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
            camGo.AddComponent<ClipRecorder>();
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
            StartCoroutine(Res.LoadAll((p, what) => loading.SetProgress(p * 0.75f, what)));
            float t = 0;
            while (t < 3.2f && !(t > 0.8f && Inputs.Any())) { t += Time.unscaledDeltaTime; yield return null; }
            splash.Hide();
            loading.Show();
            yield return new WaitForSecondsRealtime(0.35f);

            // 2. everything else
            while (!Res.Loaded) yield return null;
            loading.SetProgress(0.8f, "Loading your progress");
            yield return null;
            string data = Application.persistentDataPath;
            var guest = SaveData.Load(Path.Combine(data, "save.txt"), m => Debug.Log("[FlappyCrix] " + m));
            progress = new Progress(guest);
            season = Season.Pick(guest.SeasonChoice, Season.SydneyToday(DateTime.UtcNow));
            progress.Season = season;
            progress.Toast += (icon, head, body, col) => toasts.Show(head + (string.IsNullOrEmpty(body) ? "" : "  ·  " + body), col);

            loading.SetProgress(0.86f, "Building the world");
            yield return null;
            var wgo = new GameObject("Game");
            wgo.transform.SetParent(transform, false);
            world = wgo.AddComponent<World>();
            world.Build(cam, season);
            world.SaveRef = () => progress.Save;
            world.Scored += OnScored;
            world.Died += OnDied;
            world.CoinsGained += n =>
            {
                runCoins += n; hud.SetCoins(runCoins); world.CoinBurst();
                progress.OnCoins(n);
                if (online != null && online.InMatch) online.Mp.OnCoin();
            };

            loading.SetProgress(0.9f, "Connecting");
            yield return null;
            try
            {
                online = new OnlineHub(data, world.Sim, progress);
                world.Mp = online.Mp;
                online.MatchStarted += OnMatchStarted;
                online.LeftRoom += OnLeftRoom;
                online.Mp.Notice += (h, b) => toasts.Show(h + "  ·  " + b, Ui.Orange);
            }
            catch (Exception e) { Debug.LogWarning("[FlappyCrix] online is off: " + e.Message); online = null; }

            loading.SetProgress(0.95f, "Building menus");
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
            menu = new MenuScreen(root, () => progress.Save, StartRun, OpenOnline, () => Open(shop), () => Open(daily), () => Open(awards),
                                  () => OpenAccount(menu), () => OpenSettings(menu), () => Open(credits), Quit);
            menu.DailyBadge = () => progress.DailyReady || progress.CalendarReady;
            menu.AccountLine = () => online == null ? "Guest" : online.PlayerName;
            menu.SignedIn = () => online != null && online.SignedIn;
            settings = new SettingsScreen(root);
            settings.Closed = () => Back(settings);
            credits = new CreditsScreen(root, Version);
            credits.Closed = () => Back(credits);
            hud = new HudScreen(root, () => { if (state == State.Playing || state == State.Match) Pause(); }, () => { if (online != null) online.Mp.EndMatchNow(); });
            pause = new PauseScreen(root, Resume, () => OpenSettings(pause), LeaveFromPause);
            gameOver = new GameOverScreen(root, () => { if (gameOver.Ready) StartRun(); }, () => { gameOver.Hide(); EndRunToMenu(); });
            shop = new ShopScreen(root, progress, () => world.Sim);
            shop.Closed = () => Back(shop);
            daily = new DailyScreen(root, progress);
            daily.Closed = () => Back(daily);
            awards = new AwardsScreen(root, progress);
            awards.Closed = () => Back(awards);
            var list = new List<Panel> { splash, loading, title, menu, settings, credits, hud, pause, gameOver, shop, daily, awards };
            if (online != null)
            {
                account = new AccountScreen(root, online, progress);
                account.Closed = () => Back(account);
                lobby = new LobbyScreen(root, online);
                lobby.Closed = () => { lobby.Hide(); online.Disconnect(); state = State.Menu; menu.Show(); };
                lobby.OpenAccount = () => OpenAccount(lobby);
                room = new RoomScreen(root, online);
                room.Closed = () => online.Mp.Leave();
                results = new ResultsScreen(root, online);
                results.Closed = () => { results.Hide(); online.Mp.CloseResults(); resultsShown = false; };
                list.Add(account); list.Add(lobby); list.Add(room); list.Add(results);
            }
            panels = list.ToArray();
            hud.ShowPauseButton(Settings.IsMobile);
        }

        // ------------------------------------------------------------------ moving between screens

        void Open(Panel p)
        {
            menu.Hide();
            returnTo = menu;
            p.Show();
        }

        void Back(Panel p)
        {
            p.Hide();
            var to = returnTo ?? menu;
            returnTo = null;
            to.Show();
        }

        void OpenSettings(Panel from)
        {
            from.Hide();
            returnTo = from;
            settings.Show();
        }

        void OpenAccount(Panel from)
        {
            if (account == null) { toasts.Show("ONLINE ISN'T AVAILABLE RIGHT NOW", Ui.Red); return; }
            from.Hide();
            returnTo = from;
            account.Show();
        }

        void OpenOnline()
        {
            if (online == null) { toasts.Show("ONLINE ISN'T AVAILABLE RIGHT NOW", Ui.Red); return; }
            menu.Hide();
            state = State.Online;
            lobby.Show();
        }

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

        // ------------------------------------------------------------------ solo runs

        void StartRun()
        {
            if (gameOver.Visible) gameOver.Hide();
            menu.Hide();
            state = State.Playing;
            runCoins = 0;
            world.Attract = false;
            world.NewRun();
            progress.OnRunStart();
            hud.SetScore(0); hud.SetCoins(0);
            hud.SetMatch(null, null, null, false);
            hud.HintAlpha = 1;
            hud.Show();
            Deselect();
            if (PostFx.Inst != null) PostFx.Inst.Desaturate = 0;
            Sfx.Duck(false);
            Sfx.RestartMusic();
            Sfx.Play("start", 0.6f);
            PostFx.Glitch(0.5f);
        }

        void Pause()
        {
            // a match keeps going for everyone else, so it never freezes
            if (state == State.Playing) { state = State.Paused; world.Frozen = true; }
            Sfx.Duck(true);
            pause.Show();
        }

        void Resume()
        {
            pause.Hide();
            if (state == State.Paused) { state = State.Playing; world.Frozen = false; }
            Sfx.Duck(false);
            Deselect();
        }

        void LeaveFromPause()
        {
            pause.Hide();
            if (state == State.Match && online != null) { online.Mp.Leave(); return; }
            EndRunToMenu();
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
            if (online != null) online.CloudSaveNow(false);
        }

        static void Deselect() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }

        // ------------------------------------------------------------------ online matches

        void OnMatchStarted()
        {
            if (results != null && results.Visible) results.Hide();
            room.Hide(); lobby.Hide(); menu.Hide(); pause.Hide();
            if (account != null && account.Visible) account.Hide();
            state = State.Match;
            runCoins = 0;
            resultsShown = false;
            world.EnterMatch();
            hud.SetScore(0); hud.SetCoins(0);
            hud.HintAlpha = 1;
            hud.Show();
            Deselect();
            if (PostFx.Inst != null) PostFx.Inst.Desaturate = 0;
            Sfx.Duck(false);
            Sfx.RestartMusic();
            PostFx.Glitch(0.7f);
        }

        void OnLeftRoom()
        {
            hud.Hide(); room.Hide(); pause.Hide();
            if (results.Visible) results.Hide();
            resultsShown = false;
            world.Attract = true;
            world.StartAttract();
            if (PostFx.Inst != null) PostFx.Inst.Desaturate = 0;
            state = State.Online;
            lobby.Show();
        }

        /// <summary>Which online screen should be up right now (room or results), outside a match.</summary>
        void UpdateOnlineScreens()
        {
            var mp = online.Mp;
            if (mp.State == Multiplayer.Phase.Results && !resultsShown)
            {
                resultsShown = true;
                hud.Hide(); pause.Hide();
                world.Attract = true;
                world.StartAttract();
                if (PostFx.Inst != null) PostFx.Inst.Desaturate = 0;
                state = State.Online;
                room.Hide(); lobby.Hide();
                results.Show();
                online.CloudSaveNow(false);
                return;
            }
            if (state == State.Online && mp.State == Multiplayer.Phase.Room && !room.Visible && !results.Visible &&
                (account == null || !account.Visible) && !settings.Visible)
            {
                lobby.Hide();
                room.Show();
            }
        }

        void UpdateMatchHud()
        {
            var mp = online.Mp;
            var sb = new StringBuilder();
            int place = 1;
            foreach (var r in mp.Standings())
            {
                if (place > 6) break;
                Multiplayer.Player p;
                mp.Players.TryGetValue(r.Actor, out p);
                string col = p != null ? p.Colour : "#FFFFFF";
                bool dead = p != null && p.Dead;
                sb.Append(place).Append(".  <color=").Append(col).Append('>').Append(Clip(r.Name, 14)).Append("</color>  ")
                  .Append(dead ? "<color=#777777>" : "").Append(r.Score).Append(dead ? "</color>" : "").Append('\n');
                place++;
            }
            string modeLine = Multiplayer.ModeName(mp.Mode);
            if (mp.Mode == "race") modeLine += "  ·  FIRST TO " + mp.RaceTarget;
            else if (mp.Mode == "coinrush")
            {
                int left = Mathf.Max(0, (int)(mp.RushSeconds - mp.MatchClock));
                modeLine += "  ·  " + left / 60 + ":" + (left % 60).ToString("00");
            }
            else if (mp.Mode == "lastone")
            {
                int alive = 0; foreach (var p in mp.Players.Values) if (!p.Dead) alive++;
                modeLine += "  ·  " + alive + " LEFT";
            }
            string middle = null;
            if (world.Sim.Screen == "dead") middle = mp.Mode == "lastone" ? "OUT!  WATCHING..." : "RESPAWNING...";
            hud.SetMatch(sb.ToString(), modeLine, middle, mp.IsHost && mp.Mode == "freemode");
            if (mp.Mode == "coinrush") hud.SetScore(world.Sim.CoinsThisMatch);
        }

        static string Clip(string s, int n) { s = s ?? ""; return s.Length > n ? s.Substring(0, n - 1) + "." : s; }

        // ------------------------------------------------------------------ game events

        void OnScored(int s)
        {
            if (state == State.Match) online.Mp.OnScored(s);
            if (state != State.Match || online.Mp.Mode != "coinrush") hud.SetScore(s);
            hud.HintAlpha = 0;
            progress.OnScored(s);
            if (s % 10 == 0) { PostFx.Glitch(0.45f); PostFx.Kick(0.6f); }
            else PostFx.Kick(0.15f);
        }

        void OnDied(int score)
        {
            PostFx.Flash(new Color(1f, 0.2f, 0.25f), 1);
            PostFx.Glitch(1);
            PostFx.Kick(1);
            if (Settings.Haptics) Vibrate();

            if (state == State.Match && online != null)
            {
                online.Mp.OnDied(score);       // Race and Coin Rush bring you back; Last One Standing watches
                return;
            }
            state = State.Dead;
            hud.Hide();
            Sfx.Duck(true);
            bool newBest = progress.OnDied(score);
            progress.Flush();
            if (online != null) online.CloudSaveNow(false);
            gameOver.Set(score, progress.Save.Best, newBest, runCoins, 0, progress.Save.Level);
            menu.LastScore = score;
            if (newBest && score > 0) Sfx.Play("victory", 0.7f);
            StartCoroutine(ShowGameOver());
        }

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
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

        void Quit()
        {
            progress.Flush();
            if (online != null) online.Dispose();
            Application.Quit();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            if (progress != null) progress.Flush();
            if (online != null) online.CloudSaveNow(true);
            if (state == State.Playing) Pause();
        }

        void OnApplicationFocus(bool focus) { if (!focus && state == State.Playing && Settings.IsMobile) Pause(); }

        void OnApplicationQuit()
        {
            if (progress != null) progress.Flush();
            if (online != null) online.Dispose();
        }

        // ------------------------------------------------------------------ every frame

        bool TypingInAField()
        {
            var es = EventSystem.current;
            return es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<InputField>() != null;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (panels != null) foreach (var p in panels) p.Update(dt);
            else { splash.Update(dt); loading.Update(dt); }
            toasts.Tick();

            bool portrait = Screen.height > Screen.width;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = portrait ? 0 : 1;
            Ui.UpdateForm((RectTransform)canvas.transform);

            if (Settings.IsDesktop && (Inputs.ToggleFullscreen() || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.Return))))
                Settings.ToggleFullscreen();

            if (online != null && state != State.Boot)
            {
                online.Update(dt, (h, b) => toasts.Show(h + "  ·  " + b, Ui.Orange));
                if (state == State.Online || state == State.Match) UpdateOnlineScreens();
                if (state == State.Match) UpdateMatchHud();
            }

            bool overlay = settings != null && (settings.Visible || credits.Visible || shop.Visible || daily.Visible || awards.Visible || (account != null && account.Visible));
            switch (state)
            {
                case State.Title:
                    if (Inputs.Any()) GoMenu();
                    break;
                case State.Menu:
                    if (Inputs.Back())
                    {
                        if (overlay) BackFromOverlay();
                        else if (menu.Visible) { menu.Hide(); GoTitle(); }
                    }
                    break;
                case State.Online:
                    if (Inputs.Back() && !TypingInAField())
                    {
                        if (overlay) BackFromOverlay();
                        else if (results.Visible) results.Closed();
                        else if (room.Visible) online.Mp.Leave();
                        else if (lobby.Visible) lobby.Closed();
                    }
                    break;
                case State.Playing:
                    if (Inputs.Pause()) { Pause(); break; }
                    if (Inputs.Flap()) world.Flap();
                    break;
                case State.Match:
                    if (pause.Visible)
                    {
                        if (settings.Visible) { if (Inputs.Back()) Back(settings); }
                        else if (Inputs.Pause() || Input.GetKeyDown(KeyCode.JoystickButton1)) Resume();
                        break;
                    }
                    if (settings.Visible) { if (Inputs.Back()) Back(settings); break; }
                    if (Inputs.Pause()) { Pause(); break; }
                    if (Inputs.Flap() && world.Flap()) online.Mp.OnFlap();
                    break;
                case State.Paused:
                    if (settings.Visible) { if (Inputs.Back()) Back(settings); }
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

        void BackFromOverlay()
        {
            foreach (var p in new Panel[] { settings, credits, shop, daily, awards, account })
                if (p != null && p.Visible) { Back(p); return; }
        }
    }
}
