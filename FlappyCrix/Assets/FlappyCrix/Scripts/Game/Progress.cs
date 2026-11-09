// Your progress and the website's rules for it: XP and levels (1 XP a pipe), awards, the shop
// (power-ups, hats, trails), the locker, the daily reward streak, seasonal calendar doors, and
// switching between the guest save and a linked account's save (cloud saved, shared with the site).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using System.IO;
using FlappyCrix.Native;
using FlappyCrix.Online;
using UnityEngine;

namespace FlappyCrix
{
    public sealed class Progress
    {
        public SaveData Save { get; private set; }
        public readonly SaveData Guest;
        public Season Season;
        /// <summary>A message for the player: emoji (or "coin"), title, text, colour.</summary>
        public event Action<string, string, string, Color> Toast;
        public event Action Changed;
        bool has67, has1000;
        string pendingDisplayName;

        public Progress(SaveData guest)
        {
            Guest = guest;
            Save = guest;
            if (Guest.GuestNo < 1000) { Guest.GuestNo = new System.Random().Next(1000, 10000); Guest.Dirty = true; }
        }

        public bool OnAccount => Save != Guest;

        void Say(string icon, string title, string body, Color c) { if (Toast != null) Toast(icon, title, body, c); }
        void Bump() { Save.Dirty = true; if (Changed != null) Changed(); }

        static DateTime LocalNow => DateTime.Now;
        Season.Day Today => Season.SydneyToday(DateTime.UtcNow);

        // ------------------------------------------------------------------ runs

        public void OnRunStart()
        {
            Save.Games++;
            has67 = has1000 = false;
            if (Season.IsPartyHatDay(Today) && Save.Owned.Add("hat_party"))
            {
                Say("1f382", "GOLDEN PARTY HAT", "Happy birthday Flappy Crix! It's in your Locker.", Ui.Gold);
                Sfx.Play("unlock");
            }
            Bump();
            CheckAchievements();
        }

        /// <summary>One pipe passed. True = levelled up.</summary>
        public bool OnScored(int score)
        {
            bool up = GiveXp(1);
            if (score == 67 && !has67) { has67 = true; Sfx.Play("67"); Unlock("mystery"); }
            if (score == 1000 && !has1000) { has1000 = true; Sfx.Play("boom"); Sfx.Play("victory"); Unlock("legend"); }
            CheckAchievements(score);
            return up;
        }

        public bool GiveXp(int n)
        {
            bool up = false;
            for (int k = 0; k < n && Save.Level < Catalog.MaxLevel; k++)
            {
                Save.Xp++;
                while (Save.Level < Catalog.MaxLevel && Save.Xp >= Catalog.XpForLevel(Save.Level))
                {
                    Save.Xp -= Catalog.XpForLevel(Save.Level);
                    Save.Level++;
                    up = true;
                    Say("2b50", "LEVEL " + Save.Level, "Keep flying!", Ui.Gold);
                    Sfx.Play("levelup", 0.8f);
                }
            }
            if (Save.Level >= Catalog.MaxLevel) Save.Xp = 0;
            Bump();
            return up;
        }

        public void OnCoins(int n) { Save.Coins += n; Bump(); CheckAchievements(); }

        /// <summary>A solo run ended. True = a new best.</summary>
        public bool OnDied(int score)
        {
            bool best = score > Save.Best;
            if (best) Save.Best = score;
            Bump();
            CheckAchievements(score);
            return best;
        }

        // ------------------------------------------------------------------ awards

        public int AchievementProgress(Catalog.Achievement a)
        {
            switch (a.Id)
            {
                case "first": case "games25": case "games100": return Save.Games;
                case "collector": case "coins1k": case "coins10k": return (int)Math.Min(int.MaxValue, Save.Coins);
                case "score25": case "score50": case "score100": case "score250": return Save.Best;
                case "dripped": return Save.Hat != "" && Save.Trail != "" ? 1 : 0;
                case "lvl10": case "lvl25": case "lvlmax": return Save.Level;
                default: return Save.Achievements.Contains(a.Id) ? 1 : 0;
            }
        }

        public void CheckAchievements(int runScore = 0)
        {
            foreach (var a in Catalog.Achievements)
            {
                if (Save.Achievements.Contains(a.Id)) continue;
                int p = AchievementProgress(a);
                if (a.Id.StartsWith("score", StringComparison.Ordinal)) p = Math.Max(p, runScore);
                if (a.Id != "mystery" && a.Id != "legend" && p >= a.Target) Unlock(a.Id);
            }
        }

        void Unlock(string id)
        {
            if (!Save.Achievements.Add(id)) return;
            Bump();
            foreach (var a in Catalog.Achievements)
                if (a.Id == id) Say(a.Emoji, "AWARD UNLOCKED", a.Name, Ui.Gold);
            Sfx.Play("ach");
        }

        // ------------------------------------------------------------------ shop and locker

        public bool BuyPowerup(Catalog.Powerup p, NativeSim sim)
        {
            if (sim.PowerupOn(p.Id)) return Refuse(p.Name + " is already on.");
            if (Save.Coins < p.Price) return Refuse("You need " + Catalog.FormatCoins(p.Price - Save.Coins) + " more coins.");
            Save.Coins -= p.Price;
            sim.GrantPowerup(p.Id, p.Seconds);
            Sfx.Play("purchase"); Sfx.Play("powerup");
            Say(p.Emoji, p.Name + " ON", p.Seconds + " seconds - the clock is running!", Ui.Green);
            Bump();
            return true;
        }

        /// <summary>Buy it, or (if you have it) wear it / take it off.</summary>
        public void BuyOrEquip(Catalog.Cosmetic c)
        {
            if (Save.Owned.Contains(c.Id)) { Equip(c); return; }
            if (c.Price <= 0) { Refuse((c.Source ?? "An event") + " only."); return; }
            if (Save.Coins < c.Price) { Refuse("You need " + Catalog.FormatCoins(c.Price - Save.Coins) + " more coins."); return; }
            Save.Coins -= c.Price; Save.Owned.Add(c.Id);
            Sfx.Play("purchase");
            Say(null, "BOUGHT " + c.Name, "Press it again to wear it.", Ui.Green);
            Bump();
            CheckAchievements();
        }

        public void Equip(Catalog.Cosmetic c)
        {
            if (c.Type == "hat") Save.Hat = Save.Hat == c.Id ? "" : c.Id;
            else Save.Trail = Save.Trail == c.Id ? "" : c.Id;
            Sfx.Play("select");
            Bump();
            CheckAchievements();
        }

        /// <summary>Takes off whatever hat or trail you're wearing.</summary>
        public void TakeOff(string type)
        {
            if (type == "hat") Save.Hat = ""; else Save.Trail = "";
            Sfx.Play("select"); Bump();
        }

        public bool Wearing(Catalog.Cosmetic c) => c.Type == "hat" ? Save.Hat == c.Id : Save.Trail == c.Id;

        bool Refuse(string why) { Sfx.Play("error"); Say(null, "CAN'T DO THAT", why, Ui.Red); return false; }

        // ------------------------------------------------------------------ daily reward and calendar

        public bool DailyReady => Save.DailyLast != SaveData.DayNumber(LocalNow);

        /// <summary>The streak day you'd be on if you claimed today (1..).</summary>
        public int NextStreak
        {
            get { int day = SaveData.DayNumber(LocalNow); return Save.DailyLast == day - 1 ? Save.DailyStreak + 1 : 1; }
        }

        public void ClaimDaily()
        {
            if (!DailyReady) { Refuse("Come back tomorrow for the next reward."); return; }
            int streak = NextStreak;
            int coins = Catalog.DailyRewards[(streak - 1) % 7];
            Save.DailyStreak = streak; Save.DailyLast = SaveData.DayNumber(LocalNow);
            Save.Coins += coins;
            Sfx.Play("purchase");
            Say("1f381", "DAY " + streak + " REWARD", "+" + coins + " coins", Ui.Gold);
            Bump();
            CheckAchievements();
            Flush();
        }

        public int CalendarDoorToday => Season != null ? Season.CalendarToday(Today, false) : 0;
        public HashSet<int> CalendarClaimed => Save.CalendarDoors(Season.Id, LocalNow.Year);

        public bool CalendarReady
        {
            get
            {
                if (Season == null || Season.CalendarDoors == 0) return false;
                int open = CalendarDoorToday;
                var claimed = CalendarClaimed;
                for (int d = 1; d <= open; d++) if (!claimed.Contains(d)) return true;
                return false;
            }
        }

        public void ClaimDoor(int door)
        {
            if (Season == null) return;
            var claimed = CalendarClaimed;
            if (claimed.Contains(door)) { Refuse("Door " + door + " is already open."); return; }
            if (door > CalendarDoorToday) { Refuse("Door " + door + " opens on day " + door + "."); return; }
            int coins = Season.CalendarReward(door, Season.CalendarDoors);
            claimed.Add(door);
            Save.Coins += coins;
            string prize = Season.PrizeFor(door);
            var item = prize != null ? Catalog.CosmeticById(prize) : null;
            if (item != null && Save.Owned.Add(item.Id)) { Sfx.Play("unlock"); Say(Season.Emoji, "DOOR " + door + ": " + item.Name, "+" + coins + " coins, and it's in your Locker!", Ui.Gold); }
            else { Sfx.Play("purchase"); Say(Season.Emoji, "DOOR " + door, "+" + coins + " coins", Ui.Gold); }
            Bump();
            CheckAchievements();
            Flush();
        }

        // ------------------------------------------------------------------ accounts

        /// <summary>A linked account's progress arrived: play on it from now on (the guest's progress moves over the first time, as on the site).</summary>
        public void Adopt(Account.Loaded l, string dataRoot)
        {
            string path = Path.Combine(dataRoot, "save-" + SafeName(l.Uid) + ".txt");
            SaveData local = File.Exists(path) ? SaveData.Load(path, m => Debug.Log("[FlappyCrix] " + m)) : null;
            SaveData acct;
            if (CloudSave.HasSave(l.Doc))
            {
                acct = local ?? new SaveData(path);
                if (local == null || local.SavedAt <= Firestore.Long(l.Doc, "savedAt")) CloudSave.Apply(l.Doc, acct);
            }
            else
            {
                acct = local ?? Guest.CopyTo(path);
                pendingDisplayName = l.Name;
            }
            if (l.PfLevel > 0) CloudSave.TakeLevelIfHigher(acct, l.PfLevel, l.PfXp);
            if (l.PfBest > acct.Best) acct.Best = l.PfBest;
            acct.CopySettingsFrom(Save);
            acct.GuestNo = Guest.GuestNo;
            acct.Dirty = true;
            Switch(acct);
            Say("2705", "SIGNED IN", l.Name + " - your progress is saved to your account.", Ui.Green);
            Sfx.Play("unlock", 0.8f);
            CheckAchievements();
        }

        public void BackToGuest()
        {
            if (!OnAccount) return;
            Flush();
            Guest.CopySettingsFrom(Save);
            Switch(Guest);
        }

        void Switch(SaveData s)
        {
            if (Save.Dirty) { try { Save.Save(); } catch (Exception) { } }
            Save = s;
            Save.Dirty = true;
            Flush();
            if (Changed != null) Changed();
        }

        /// <summary>What goes to the account in the cloud (the site's users/{uid} fields).</summary>
        public Dictionary<string, object> CloudFields()
        {
            var f = CloudSave.Fields(Save, AchievementProgress, pendingDisplayName);
            pendingDisplayName = null;
            return f;
        }

        public void Flush()
        {
            if (Save == null || !Save.Dirty) return;
            Save.SavedAt = SaveData.NowMs();
            try { Save.Save(); } catch (Exception e) { Debug.LogWarning("[FlappyCrix] save failed: " + e.Message); }
        }

        static string SafeName(string s)
        {
            var c = (s ?? "account").ToCharArray();
            for (int i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i]) && c[i] != '-' && c[i] != '_') c[i] = '_';
            return new string(c);
        }
    }
}
