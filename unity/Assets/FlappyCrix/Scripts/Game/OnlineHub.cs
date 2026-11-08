// Everything online: the crixgamingvr.com account (linked with a code / QR code, the site's
// /link page), cloud saves shared with the website, and multiplayer with website players
// through the same Photon rooms (Free Play, Race, Last One Standing, Coin Rush).
// The account and Photon run on their own threads; Update() is called every frame.
// A dropped connection is retried by itself, with a short wait that grows each time.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using FlappyCrix.Native;
using FlappyCrix.Online;
using UnityEngine;

namespace FlappyCrix
{
    public sealed class OnlineHub : IDisposable
    {
        public readonly SiteConfig Config = new SiteConfig();
        public readonly Account Account;
        public readonly PhotonClient Photon;
        public readonly Multiplayer Mp;
        public readonly string DataRoot;
        readonly Progress progress;

        public bool WantOnline;          // the player is in the online screens (stay connected, come back if dropped)
        Account.State lastState = Account.State.SignedOut;
        float cloudTimer, retryAt, retryWait = 2;
        bool wasInRoom, wasMatch;

        public event Action MatchStarted, MatchEnded, LeftRoom;

        public OnlineHub(string dataRoot, NativeSim sim, Progress progress)
        {
            DataRoot = dataRoot;
            this.progress = progress;
            Action<string> log = m => Debug.Log("[FlappyCrix] " + m);
            ChatFilter.Source = () => { var t = Resources.Load<TextAsset>(Res.Root + "filter"); return t != null ? t.text : ""; };
            ChatFilter.Embedded();
            Multiplayer.Platform = PlatformName();
            Account = new Account(Config, dataRoot) { Log = log };
            Photon = new PhotonClient(Config) { Log = log };
            Mp = new Multiplayer(Photon, sim);
            Mp.Identity = () => new System.Collections.Generic.Dictionary<string, object>
            {
                { "level", progress.Save.Level }, { "hat", progress.Save.Hat == "" ? null : progress.Save.Hat },
                { "trail", progress.Save.Trail == "" ? null : progress.Save.Trail },
            };
            Mp.PassFor = (room, actor) => Account.Status == Account.State.SignedIn ? Account.RoomPass(room, actor) : null;
            Mp.Sound += (n, v) => Sfx.Play(n, v);
            Mp.Reward += (coins, xp) =>
            {
                if (coins > 0) progress.OnCoins(coins);
                if (xp > 0) progress.GiveXp(xp);
            };
            Account.Start();
        }

        static string PlatformName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXPlayer: case RuntimePlatform.OSXEditor: return "mac";
                case RuntimePlatform.LinuxPlayer: case RuntimePlatform.LinuxEditor: return "linux";
                case RuntimePlatform.Android: return "android";
                case RuntimePlatform.IPhonePlayer: return "ios";
                default: return "windows";
            }
        }

        public bool SignedIn => Account.Status == Account.State.SignedIn;
        public string PlayerName => SignedIn && Account.Name != null ? Account.Name : "Guest " + progress.Guest.GuestNo;
        public bool InRoom => Mp.InRoom;
        public bool InMatch => Mp.InRoom && Mp.MatchActive;

        public void Connect()
        {
            WantOnline = true;
            if (Mp.State == Multiplayer.Phase.Off) { Mp.Nickname = PlayerName; Mp.Connect(); }
        }

        public void Disconnect()
        {
            WantOnline = false;
            Mp.Disconnect();
        }

        public void Update(float dt, Action<string, string> notice)
        {
            // the account
            var loaded = Account.TakeLoaded();
            if (loaded != null) progress.Adopt(loaded, DataRoot);
            if (Account.Status != lastState)
            {
                if (Account.Status == Account.State.SignedOut && progress.OnAccount)
                {
                    progress.BackToGuest();
                    notice("SIGNED OUT", "Playing as " + PlayerName + ". Your account progress is safe.");
                }
                lastState = Account.Status;
            }
            cloudTimer += dt;
            if (SignedIn && progress.OnAccount && cloudTimer > 20) { cloudTimer = 0; CloudSaveNow(false); }

            // multiplayer
            Mp.Nickname = PlayerName;
            Mp.Update(dt);
            if (InRoom && Account.Banned) { Mp.Leave(); notice("MULTIPLAYER IS OFF", "This account is banned (" + Account.BanReason + ")."); }
            if (Mp.MatchActive && !wasMatch && MatchStarted != null) MatchStarted();
            if (!Mp.MatchActive && wasMatch && MatchEnded != null) MatchEnded();
            if (!InRoom && wasInRoom && LeftRoom != null) LeftRoom();
            wasMatch = Mp.MatchActive;
            wasInRoom = InRoom;

            // dropped? try again, waiting a little longer each time (2 s, 4 s ... 30 s)
            if (WantOnline && Mp.State == Multiplayer.Phase.Off)
            {
                if (retryAt <= 0) retryAt = Time.unscaledTime + retryWait;
                else if (Time.unscaledTime >= retryAt)
                {
                    retryAt = 0;
                    retryWait = Mathf.Min(30, retryWait * 2);
                    Mp.Nickname = PlayerName;
                    Mp.Connect();
                }
            }
            else if (Mp.State == Multiplayer.Phase.Lobby || InRoom) { retryWait = 2; retryAt = 0; }
        }

        public void CloudSaveNow(bool now)
        {
            if (!SignedIn || !progress.OnAccount) return;
            Account.QueueSave(progress.CloudFields(), now);
        }

        public void Dispose()
        {
            try { CloudSaveNow(true); } catch (Exception) { }
            try { Mp.Disconnect(); } catch (Exception) { }
            try { Photon.Dispose(); } catch (Exception) { }
            try { Account.Dispose(); } catch (Exception) { }
        }
    }
}
