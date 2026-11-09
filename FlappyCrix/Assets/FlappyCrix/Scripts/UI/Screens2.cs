// Phase 2 screens: Shop (power-ups, hats, trails - buy and wear), Daily (7-day streak and the
// seasonal calendar), Awards, Account (link with a code / QR code), and online: Lobby (rooms,
// join by code, create), Room (players, mode, start, chat) and Results.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using System.Globalization;
using FlappyCrix.Native;
using FlappyCrix.Online;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyCrix
{
    /// <summary>A full screen with a title, a BACK button and a content area that fits any screen shape.</summary>
    public abstract class FramePanel : Panel
    {
        protected readonly RectTransform Content;
        protected readonly Text Corner;
        protected readonly MenuButton BackButton;
        public Action Closed;

        protected FramePanel(Transform canvas, string title) : base(canvas, title)
        {
            Shade(Root, 0.86f);
            var t = GlitchText.Make(Root, title, 84, Color.white, TextAnchor.MiddleLeft);
            Ui.Place((RectTransform)t.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -40), new Vector2(1100, 120));
            Corner = Ui.Label(Root, "", 34, Ui.Gold, TextAnchor.MiddleRight);
            Ui.Place(Corner.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -60), new Vector2(700, 60));
            Content = Ui.Rect(Root, "Content");
            Content.anchorMin = Vector2.zero; Content.anchorMax = Vector2.one;
            Content.offsetMin = new Vector2(90, 150); Content.offsetMax = new Vector2(-90, -180);
            BackButton = Ui.MenuButton(Root, "BACK", null, () => { if (Closed != null) Closed(); }, 300, 72);
            Ui.Place((RectTransform)BackButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(90, 46), new Vector2(300, 72));
        }

        protected override void Layout(Ui.Form form)
        {
            bool tall = form == Ui.Form.Portrait;
            // upright: the coins / status line goes under the title instead of beside it
            if (tall) Ui.Place(Corner.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -150), new Vector2(900, 50));
            else Ui.Place(Corner.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -60), new Vector2(700, 60));
            Corner.alignment = tall ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            Content.offsetMin = new Vector2(tall ? 40 : 90, 150);
            Content.offsetMax = new Vector2(tall ? -40 : -90, tall ? -220 : -180);
        }

        protected void Select(Selectable s)
        {
            if (s != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }

    // ---------------------------------------------------------------------- shop

    public sealed class ShopScreen : FramePanel
    {
        readonly Progress progress;
        readonly Func<NativeSim> sim;
        readonly RectTransform list;
        string tab = "powerups";
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();

        // the STORE sells things; the LOCKER (wardrobe) is where you wear what you own, like the website
        readonly bool locker;

        public ShopScreen(Transform canvas, Progress progress, Func<NativeSim> sim, bool locker = false) : base(canvas, locker ? "LOCKER" : "STORE")
        {
            this.progress = progress; this.sim = sim; this.locker = locker;
            if (locker) tab = "hat";
            var tabRow = Ui.Rect(Content, "Tabs");
            tabRow.anchorMin = new Vector2(0, 1); tabRow.anchorMax = new Vector2(1, 1); tabRow.pivot = new Vector2(0.5f, 1);
            tabRow.sizeDelta = new Vector2(0, 70); tabRow.anchoredPosition = Vector2.zero;
            var h = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14; h.childAlignment = TextAnchor.MiddleLeft; h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false;
            var tabList = locker ? new[] { new[] { "hat", "HATS" }, new[] { "trail", "TRAILS" } }
                                 : new[] { new[] { "powerups", "POWER-UPS" }, new[] { "hat", "HATS" }, new[] { "trail", "TRAILS" } };
            foreach (var pair in tabList)
            {
                string id = pair[0];
                tabs[id] = Ui.SmallButton(tabRow, pair[1], () => { tab = id; Rebuild(); }, new Color(1, 1, 1, 0.1f), 260, 64);
            }
            var area = Ui.Rect(Content, "List");
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = Vector2.zero; area.offsetMax = new Vector2(0, -90);
            list = Ui.Scroll(area, true, new Vector2(400, 300));
            progress.Changed += () => { if (Visible) Rebuild(); };
        }

        protected override void OnShow() { Rebuild(); }

        void Rebuild()
        {
            Corner.text = Catalog.FormatCoins(progress.Save.Coins) + " COINS";
            foreach (var kv in tabs) ((SlantButton)kv.Value).Base = kv.Key == tab ? Ui.Red : new Color(1, 1, 1, 0.1f);
            Ui.Clear(list);
            Selectable first = null;
            if (tab == "powerups")
            {
                foreach (var p in Catalog.Powerups)
                {
                    var pp = p;
                    bool on = sim().PowerupOn(p.Id);
                    var b = Item(Res.Emoji(p.Emoji), p.Name, p.Desc, on ? "ON" : Catalog.FormatCoins(p.Price), on ? Ui.Green : Ui.Red,
                                 () => progress.BuyPowerup(pp, sim()), !on);
                    if (first == null) first = b;
                }
            }
            else if (!locker)
            {
                // the store: only what's for sale. Event prizes come from the calendars and live in the locker
                foreach (var c in Catalog.OfType(tab))
                {
                    if (c.Price <= 0) continue;
                    var cc = c;
                    bool owned = progress.Save.Owned.Contains(c.Id);
                    Sprite icon = c.Image != null ? Res.Get(c.Image) : null;
                    var b = Item(icon, c.Name, owned ? "Yours: wear it in the LOCKER" : "Buy it with coins", owned ? "OWNED" : Catalog.FormatCoins(c.Price),
                                 owned ? Ui.Green : Ui.Red, () => progress.BuyOrEquip(cc), !owned, c.Type == "trail" ? c.Colours : null);
                    if (first == null) first = b;
                }
            }
            else
            {
                // the locker: everything you own, ready to wear; event items you haven't won show where they come from
                bool wearingNone = tab == "hat" ? string.IsNullOrEmpty(progress.Save.Hat) : string.IsNullOrEmpty(progress.Save.Trail);
                var none = Item(null, tab == "hat" ? "NO HAT" : "NO TRAIL", "Just the bird", wearingNone ? "WEARING" : "WEAR",
                                wearingNone ? Ui.Green : Ui.Orange, () => progress.TakeOff(tab), !wearingNone, null);
                first = none;
                var owned = new List<Catalog.Cosmetic>(); var locked = new List<Catalog.Cosmetic>();
                foreach (var c in Catalog.OfType(tab)) (progress.Save.Owned.Contains(c.Id) ? owned : locked).Add(c);
                foreach (var c in owned)
                {
                    var cc = c;
                    bool wearing = progress.Wearing(c);
                    Sprite icon = c.Image != null ? Res.Get(c.Image) : null;
                    Item(icon, c.Name, wearing ? "Tap to take it off" : "Yours", wearing ? "WEARING" : "WEAR", wearing ? Ui.Green : Ui.Orange,
                         () => progress.Equip(cc), true, c.Type == "trail" ? c.Colours : null);
                }
                foreach (var c in locked)
                {
                    Sprite icon = c.Image != null ? Res.Get(c.Image) : null;
                    string where = c.Price > 0 ? "In the STORE for " + Catalog.FormatCoins(c.Price) : (c.Source ?? "Event prize");
                    Item(icon, c.Name, where, "LOCKED", new Color(0.3f, 0.3f, 0.35f), () => { }, false, c.Type == "trail" ? c.Colours : null);
                }
            }
            Select(first);
        }

        Button Item(Sprite icon, string name, string desc, string action, Color colour, Action click, bool enabled, uint[] swatch = null)
        {
            var card = Ui.Card(list, name, new Color(colour.r, colour.g, colour.b, 0.8f));
            if (icon != null)
            {
                var i = Ui.Img(card, "Icon", icon, Color.white);
                i.preserveAspect = true;
                Ui.Place(i.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(110, 90));
            }
            else if (swatch != null)
            {
                for (int k = 0; k < swatch.Length; k++)
                {
                    var bar = Ui.Img(card, "Swatch", Res.White, Res.C(swatch[k]));
                    Ui.Place(bar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30 - k * 64f / swatch.Length), new Vector2(200 - k * 18, 64f / swatch.Length));
                }
            }
            var n = Ui.Label(card, name, 28, Color.white, TextAnchor.MiddleCenter);
            Ui.Place(n.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(380, 40));
            var d = Ui.Label(card, desc, 20, Ui.Dim, TextAnchor.MiddleCenter, false);
            Ui.Place(d.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(380, 30));
            var b = Ui.SmallButton(card, action, () => { click(); Rebuild(); }, colour, 240, 62);
            Ui.Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(240, 62));
            b.interactable = enabled;
            return b;
        }
    }

    // ---------------------------------------------------------------------- daily

    public sealed class DailyScreen : FramePanel
    {
        readonly Progress progress;
        readonly RectTransform list;

        public DailyScreen(Transform canvas, Progress progress) : base(canvas, "DAILY REWARD")
        {
            this.progress = progress;
            list = Ui.Scroll(Content, false, Vector2.zero, 24);
            progress.Changed += () => { if (Visible) Rebuild(); };
        }

        protected override void OnShow() { Rebuild(); }

        void Rebuild()
        {
            Corner.text = Catalog.FormatCoins(progress.Save.Coins) + " COINS";
            Ui.Clear(list);
            bool ready = progress.DailyReady;
            int next = progress.NextStreak, streakNow = ready ? next : progress.Save.DailyStreak;

            var row = Ui.Rect(list, "Days");
            var g = row.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(190, 220); g.spacing = new Vector2(14, 14); g.childAlignment = TextAnchor.MiddleCenter;
            for (int d = 1; d <= 7; d++)
            {
                int coins = Catalog.DailyRewards[d - 1];
                int pos = ((streakNow - 1) % 7) + 1;
                bool today = d == pos && ready, done = ready ? d < pos : d <= pos;
                Color edge = today ? Ui.Gold : done ? Ui.Green : new Color(1, 1, 1, 0.2f);
                var card = Ui.Card(row, "Day " + d, edge);
                var l = Ui.Label(card, "DAY " + d, 28, today ? Ui.Gold : Color.white, TextAnchor.MiddleCenter);
                Ui.Place(l.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(180, 40));
                var i = Ui.Img(card, "Coin", Res.Get("coin"), done ? new Color(1, 1, 1, 0.4f) : Color.white);
                i.preserveAspect = true;
                Ui.Place(i.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(70, 70));
                var c = Ui.Label(card, done ? "CLAIMED" : "+" + coins, 26, done ? Ui.Green : Ui.Gold, TextAnchor.MiddleCenter);
                Ui.Place(c.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(180, 40));
            }

            var claimRow = Ui.Rect(list, "Claim");
            claimRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
            var claim = Ui.SmallButton(claimRow, ready ? "CLAIM DAY " + next + "  +" + Catalog.DailyRewards[(next - 1) % 7] : "COME BACK TOMORROW",
                                       () => { progress.ClaimDaily(); Rebuild(); }, ready ? Ui.Red : new Color(0.3f, 0.3f, 0.35f), 640, 86, 32);
            Ui.Place((RectTransform)claim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 86));
            claim.interactable = ready;
            var note = Ui.Label(list, "Come back every day: the reward grows for 7 days in a row. Miss a day and it starts again.", 22, Ui.Dim, TextAnchor.MiddleCenter, false);
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

            // the season's calendar (Halloween treats, Advent, Easter eggs)
            var season = progress.Season;
            if (season != null && season.CalendarDoors > 0)
            {
                var head = Ui.Label(list, (season.CalendarTitle ?? season.Name).ToUpperInvariant(), 40, Ui.Orange, TextAnchor.MiddleLeft);
                head.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
                int open = progress.CalendarDoorToday;
                var claimed = progress.CalendarClaimed;
                var doors = Ui.Rect(list, "Doors");
                int rows = (season.CalendarDoors + 7) / 8;
                var dg = doors.gameObject.AddComponent<GridLayoutGroup>();
                dg.cellSize = new Vector2(150, 106); dg.spacing = new Vector2(12, 12); dg.childAlignment = TextAnchor.UpperCenter;
                for (int d = 1; d <= season.CalendarDoors; d++)
                {
                    int door = d;
                    bool got = claimed.Contains(d), can = d <= open && !got;
                    bool prize = season.PrizeFor(d) != null;
                    var b = Ui.SmallButton(doors, got ? "✓" : (prize ? "★" : "") + d.ToString(CultureInfo.InvariantCulture), () => { progress.ClaimDoor(door); Rebuild(); },
                                           got ? Ui.Green : can ? Ui.Red : new Color(0.25f, 0.25f, 0.3f), 150, 106, 34);
                    b.interactable = can;
                }
            }
            Select(ready ? claim : (Selectable)BackButton);
        }
    }

    // ---------------------------------------------------------------------- awards

    public sealed class AwardsScreen : FramePanel
    {
        readonly Progress progress;
        readonly RectTransform list;

        public AwardsScreen(Transform canvas, Progress progress) : base(canvas, "AWARDS")
        {
            this.progress = progress;
            list = Ui.Scroll(Content, true, new Vector2(400, 230));
        }

        protected override void OnShow()
        {
            Ui.Clear(list);
            int got = 0;
            foreach (var a in Catalog.Achievements)
            {
                bool done = progress.Save.Achievements.Contains(a.Id);
                if (done) got++;
                bool secret = !done && (a.Id == "mystery" || a.Id == "legend");
                int p = Mathf.Min(a.Target, progress.AchievementProgress(a));
                var card = Ui.Card(list, a.Name, done ? Ui.Gold : new Color(1, 1, 1, 0.18f));
                var i = Ui.Img(card, "Icon", secret ? Res.Emoji("2753") : Res.Emoji(a.Emoji), done ? Color.white : new Color(1, 1, 1, 0.35f));
                i.preserveAspect = true;
                Ui.Place(i.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(76, 76));
                var n = Ui.Label(card, secret ? "???" : a.Name, 26, done ? Ui.Gold : Color.white, TextAnchor.MiddleCenter);
                Ui.Place(n.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(380, 36));
                var d = Ui.Label(card, secret ? "Find the secret" : a.Desc, 20, Ui.Dim, TextAnchor.MiddleCenter, false);
                Ui.Place(d.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(380, 30));
                var bar = Ui.Bar(card, done ? 1 : p / (float)a.Target, done ? Ui.Gold : Ui.Red, 300, 10);
                Ui.Place(bar.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(300, 10));
            }
            Corner.text = got + " / " + Catalog.Achievements.Length;
            Select(BackButton);
        }
    }

    // ---------------------------------------------------------------------- account

    public sealed class AccountScreen : FramePanel
    {
        readonly OnlineHub online;
        readonly Progress progress;
        string shown = "";
        Text timer;
        Texture2D qrTex;
        string qrFor;

        public AccountScreen(Transform canvas, OnlineHub online, Progress progress) : base(canvas, "ACCOUNT")
        {
            this.online = online; this.progress = progress;
        }

        protected override void OnShow() { shown = ""; Build(); }

        public override void Tick(float dt)
        {
            var a = online.Account;
            string key = a.Status + "|" + a.Code + "|" + a.Name + "|" + a.Message + "|" + a.Version;
            if (key != shown) Build();
            if (timer != null && a.Status == Account.State.ShowingCode)
            {
                int left = Math.Max(0, (int)(a.CodeExpiresUtc - DateTime.UtcNow).TotalSeconds);
                timer.text = "Code expires in " + left / 60 + ":" + (left % 60).ToString("00", CultureInfo.InvariantCulture) + "  -  the game links by itself once you've done it";
            }
        }

        void Build()
        {
            var a = online.Account;
            shown = a.Status + "|" + a.Code + "|" + a.Name + "|" + a.Message + "|" + a.Version;
            Ui.Clear(Content);
            timer = null;
            Corner.text = online.PlayerName.ToUpperInvariant();
            Selectable first = BackButton;

            var col = Ui.Rect(Content, "Column");
            Ui.Fill(col);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 22; v.childAlignment = TextAnchor.UpperLeft; v.childControlHeight = false; v.childControlWidth = false; v.childForceExpandHeight = false;

            switch (a.Status)
            {
                case Account.State.SignedIn:
                {
                    Line(col, "SIGNED IN AS", 26, Ui.Dim);
                    Line(col, a.Name ?? "Player", 70, Color.white);
                    Line(col, "LEVEL " + progress.Save.Level + "   ·   " + Catalog.FormatCoins(progress.Save.Coins) + " COINS   ·   BEST " + progress.Save.Best, 30, Ui.Gold);
                    Line(col, "Your coins, best score, hats, trails, awards and level are saved to your account and shared with the website.", 26, Ui.Dim, 1500);
                    if (a.Banned) Line(col, "This account is banned (" + a.BanReason + "), so multiplayer is off.", 28, Ui.Red, 1500);
                    first = Ui.SmallButton(col, "SIGN OUT", () => a.SignOut(), new Color(0.35f, 0.1f, 0.15f), 340, 72, 30);
                    break;
                }
                case Account.State.ShowingCode:
                {
                    var row = Ui.Rect(col, "Row");
                    row.sizeDelta = new Vector2(1600, 520);
                    var text = Ui.Rect(row, "Steps");
                    Ui.Place(text, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1000, 520));
                    var tv = text.gameObject.AddComponent<VerticalLayoutGroup>();
                    tv.spacing = 14; tv.childControlHeight = false; tv.childControlWidth = false; tv.childForceExpandHeight = false;
                    Line(text, "1.  On your phone or computer open", 30, Color.white);
                    Line(text, (online.Config.LinkPage ?? "").Replace("https://", ""), 44, Ui.Orange);
                    Line(text, "     (or scan the QR code)", 24, Ui.Dim);
                    Line(text, "2.  Sign in (Google, or email and password)", 30, Color.white);
                    Line(text, "3.  Enter this code:", 30, Color.white);
                    Line(text, a.Code ?? "", 110, Ui.Gold);
                    timer = Line(text, "", 22, Ui.Dim, 1000);
                    DrawQr(row, a.LinkUrl);
                    var buttons = Ui.Rect(col, "Buttons");
                    buttons.sizeDelta = new Vector2(1200, 80);
                    var bh = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
                    bh.spacing = 18; bh.childControlWidth = false; bh.childControlHeight = false; bh.childForceExpandWidth = false;
                    first = Ui.SmallButton(buttons, Settings.IsMobile ? "OPEN THE LINK PAGE" : "OPEN ON THIS DEVICE", () => Application.OpenURL(a.LinkUrl), Ui.Red, 440, 72, 28);
                    Ui.SmallButton(buttons, "NEW CODE", () => a.LinkDevice(), new Color(1, 1, 1, 0.15f), 260, 72, 28);
                    Ui.SmallButton(buttons, "CANCEL", () => a.Cancel(), new Color(1, 1, 1, 0.15f), 220, 72, 28);
                    break;
                }
                case Account.State.Restoring:
                case Account.State.GettingCode:
                case Account.State.Linking:
                    Line(col, a.Status == Account.State.Linking ? "LINKING..." : a.Status == Account.State.Restoring ? "SIGNING IN..." : "GETTING A CODE...", 60, Color.white);
                    break;
                default:
                {
                    Line(col, "PLAYING AS " + online.PlayerName.ToUpperInvariant(), 40, Color.white);
                    Line(col, "Link your crixgamingvr.com account to keep the same coins, level, hats and best score here and on the website, and to play online under your name.", 28, Ui.Dim, 1500);
                    if (a.Status == Account.State.Error && a.Message != null) Line(col, a.Message, 28, Ui.Red, 1500);
                    first = Ui.SmallButton(col, "LINK MY ACCOUNT", () => a.LinkDevice(), Ui.Red, 480, 80, 32);
                    break;
                }
            }
            Select(first);
        }

        static Text Line(RectTransform parent, string text, int size, Color c, float width = 1500)
        {
            var t = Ui.Label(parent, text, size, c, TextAnchor.MiddleLeft, size >= 40);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            int lines = Mathf.Max(1, Mathf.CeilToInt(text.Length * size * 0.55f / width));
            t.rectTransform.sizeDelta = new Vector2(width, size * 1.35f * lines);
            return t;
        }

        void DrawQr(RectTransform row, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (qrFor != url)
            {
                qrFor = url;
                var q = QrCode.Encode(url);
                if (q == null) return;
                int quiet = 2, n = q.Size + quiet * 2;
                qrTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int qx = x - quiet, qy = y - quiet;
                        bool dark = qx >= 0 && qy >= 0 && qx < q.Size && qy < q.Size && q[qx, qy];
                        px[(n - 1 - y) * n + x] = dark ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
                    }
                qrTex.SetPixels32(px); qrTex.Apply(false);
            }
            var img = Ui.Img(row, "QR", Res.MakeSprite(qrTex, qrTex.height, "qr"), Color.white);
            Ui.Place(img.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, 0), new Vector2(440, 440));
        }
    }

    // ---------------------------------------------------------------------- online lobby

    public sealed class LobbyScreen : FramePanel
    {
        readonly OnlineHub online;
        readonly Text status;
        readonly RectTransform rooms;
        readonly InputField code;
        readonly OptionRow modeRow, privacyRow;
        string createMode = "freemode";
        bool createPublic = true;
        string shownKey = "";
        public Action OpenAccount;

        public LobbyScreen(Transform canvas, OnlineHub online) : base(canvas, "PLAY ONLINE")
        {
            this.online = online;
            status = Ui.Label(Root, "", 26, Ui.Dim, TextAnchor.MiddleLeft, false);
            Ui.Place(status.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -150), new Vector2(1400, 40));

            var left = Ui.Rect(Content, "Rooms");
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.58f, 1); left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(-20, -10);
            rooms = Ui.Scroll(left, false, Vector2.zero, 12);

            var right = Ui.Rect(Content, "Side");
            right.anchorMin = new Vector2(0.58f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(20, 0); right.offsetMax = new Vector2(0, -10);
            var v = right.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.childControlHeight = false; v.childControlWidth = true; v.childForceExpandHeight = false;

            var j = Ui.Label(right, "JOIN WITH A CODE", 30, Color.white);
            j.rectTransform.sizeDelta = new Vector2(0, 44);
            code = Ui.Input(right, "ROOM CODE (4 LETTERS)", 9, 600, 80);
            Ui.SmallButton(right, "JOIN", () => { online.Mp.JoinByCode(code.text); Sfx.Play("select"); }, Ui.Red, 600, 70, 30);

            var c = Ui.Label(right, "CREATE A ROOM", 30, Color.white);
            c.rectTransform.sizeDelta = new Vector2(0, 60);
            c.alignment = TextAnchor.LowerLeft;
            modeRow = Ui.Option(right, "MODE", () => Multiplayer.ModeName(createMode), d =>
            {
                int i = Array.IndexOf(Multiplayer.Modes, createMode);
                createMode = Multiplayer.Modes[(i + d + Multiplayer.Modes.Length) % Multiplayer.Modes.Length];
            }, 600);
            privacyRow = Ui.Option(right, "WHO", () => createPublic ? "ANYONE" : "CODE ONLY", d => createPublic = !createPublic, 600);
            Ui.SmallButton(right, "CREATE", () =>
            {
                online.Mp.CreateRoom(createMode, 8, createPublic, online.PlayerName + "'s room");
                Sfx.Play("select");
            }, Ui.Orange, 600, 70, 30);
            var acct = Ui.SmallButton(right, "ACCOUNT", () => { if (OpenAccount != null) OpenAccount(); }, new Color(1, 1, 1, 0.12f), 600, 64, 26);
            FirstSelect = acct;
        }

        protected override void OnShow() { shownKey = ""; online.Connect(); }

        public override void Tick(float dt)
        {
            var mp = online.Mp;
            string s;
            switch (mp.State)
            {
                case Multiplayer.Phase.Connecting: s = "CONNECTING..."; break;
                case Multiplayer.Phase.Joining: s = "JOINING..."; break;
                case Multiplayer.Phase.Lobby: s = "ONLINE  ·  PLAYING AS " + online.PlayerName.ToUpperInvariant() + (online.SignedIn ? "" : "  ·  LINK YOUR ACCOUNT TO PLAY UNDER YOUR NAME"); break;
                default: s = mp.Error != null ? "OFFLINE: " + mp.Error + "  -  TRYING AGAIN..." : "OFFLINE  -  TRYING AGAIN..."; break;
            }
            status.text = s;
            status.color = mp.State == Multiplayer.Phase.Lobby ? Ui.Green : mp.Error != null ? Ui.Red : Ui.Dim;

            var list = mp.State == Multiplayer.Phase.Lobby ? mp.ListRooms() : new List<PhotonClient.RoomInfo>();
            var key = new System.Text.StringBuilder(mp.State.ToString());
            foreach (var r in list) key.Append(r.Name).Append(r.PlayerCount).Append(r.Str("mode"));
            if (key.ToString() == shownKey) return;
            shownKey = key.ToString();
            Ui.Clear(rooms);
            if (list.Count == 0)
            {
                var none = Ui.Label(rooms, mp.State == Multiplayer.Phase.Lobby ? "NO OPEN ROOMS RIGHT NOW. CREATE ONE!" : "", 28, Ui.Dim, TextAnchor.MiddleCenter, false);
                none.rectTransform.sizeDelta = new Vector2(0, 120);
                return;
            }
            foreach (var r in list)
            {
                var room = r;
                var row = Ui.Card(rooms, r.Name, new Color(1, 1, 1, 0.18f));
                row.sizeDelta = new Vector2(0, 100);
                string mode = r.Str("mode") ?? "freemode";
                var t = Ui.Label(row, r.Str("roomName") ?? r.Name, 30, Color.white);
                Ui.Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 16), new Vector2(620, 40));
                var m = Ui.Label(row, Multiplayer.ModeName(mode) + "   ·   " + r.PlayerCount + " / " + r.MaxPlayers, 22, Ui.Dim, TextAnchor.MiddleLeft, false);
                Ui.Place(m.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(42, -22), new Vector2(620, 30));
                bool can = Multiplayer.CanPlay(r);
                var b = Ui.SmallButton(row, can ? "JOIN" : Multiplayer.SiteOnlyModes.Contains(mode) ? "WEB ONLY" : "FULL", () => online.Mp.JoinRoom(room.Name), can ? Ui.Red : new Color(0.3f, 0.3f, 0.35f), 200, 64, 26);
                Ui.Place((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 64));
                b.interactable = can;
            }
        }
    }

    // ---------------------------------------------------------------------- room

    public sealed class RoomScreen : FramePanel
    {
        readonly OnlineHub online;
        readonly RectTransform players, chat;
        readonly Text info;
        readonly Button start, mode;
        string shownKey = "";

        public RoomScreen(Transform canvas, OnlineHub online) : base(canvas, "ROOM")
        {
            this.online = online;
            info = Ui.Label(Root, "", 30, Ui.Gold, TextAnchor.MiddleLeft);
            Ui.Place(info.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -150), new Vector2(1600, 44));

            var left = Ui.Rect(Content, "Players");
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.5f, 1); left.offsetMin = new Vector2(0, 100); left.offsetMax = new Vector2(-20, -20);
            players = Ui.Scroll(left, false, Vector2.zero, 10);
            var right = Ui.Rect(Content, "Chat");
            right.anchorMin = new Vector2(0.5f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(20, 100); right.offsetMax = new Vector2(0, -20);
            var chatBg = Ui.Img(right, "Bg", Res.White, new Color(0, 0, 0, 0.35f));
            Ui.Fill(chatBg.rectTransform);
            chat = Ui.Scroll(right, false, Vector2.zero, 6);

            var bar = Ui.Rect(Content, "Buttons");
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, 80); bar.anchoredPosition = Vector2.zero;
            var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18; h.childAlignment = TextAnchor.MiddleRight; h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false;
            mode = Ui.SmallButton(bar, "MODE", () =>
            {
                var mp = online.Mp;
                int i = Array.IndexOf(Multiplayer.Modes, mp.Mode);
                mp.SetMode(Multiplayer.Modes[(i + 1 + Multiplayer.Modes.Length) % Multiplayer.Modes.Length]);
            }, new Color(1, 1, 1, 0.15f), 420, 74, 28);
            start = Ui.SmallButton(bar, "START", () => online.Mp.StartMatch(), Ui.Red, 340, 74, 32);
            BackButton.Label.text = "LEAVE";
            FirstSelect = start;
        }

        protected override void OnShow() { shownKey = ""; }

        public override void Tick(float dt)
        {
            var mp = online.Mp;
            info.text = "CODE  " + mp.RoomCode + "   ·   " + Multiplayer.ModeName(mp.Mode) + (mp.Mode == "race" ? " (FIRST TO " + mp.RaceTarget + ")" : mp.Mode == "coinrush" ? " (" + mp.RushSeconds / 60 + " MIN)" : "") +
                        "   ·   PING " + mp.PingMs + " MS";
            Ui.SetButtonText(mode, "MODE: " + Multiplayer.ModeName(mp.Mode));
            mode.interactable = mp.IsHost;
            start.interactable = mp.IsHost;
            Ui.SetButtonText(start, mp.IsHost ? "START" : "HOST STARTS");

            var key = new System.Text.StringBuilder();
            foreach (var p in mp.Players.Values) key.Append(p.Actor).Append(p.Name).Append(p.Level).Append(p.Colour);
            key.Append(mp.Client.Host).Append('|').Append(mp.Chat.Count);
            if (key.ToString() == shownKey) return;
            shownKey = key.ToString();
            Ui.Clear(players);
            foreach (var p in mp.Players.Values)
            {
                var row = Ui.Card(players, p.Name, Res.Hex(p.Colour));
                row.sizeDelta = new Vector2(0, 80);
                var dot = Ui.Img(row, "Dot", Res.Soft, Res.Hex(p.Colour));
                Ui.Place(dot.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(40, 40));
                var n = Ui.Label(row, p.Name + (p.Me ? "  (YOU)" : ""), 30, Color.white);
                Ui.Place(n.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(100, 0), new Vector2(520, 40));
                var l = Ui.Label(row, (p.Actor == mp.Client.Host ? "HOST  ·  " : "") + "LV " + p.Level, 24, Ui.Gold, TextAnchor.MiddleRight, false);
                Ui.Place(l.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(260, 40));
            }
            Ui.Clear(chat);
            int from = Math.Max(0, mp.Chat.Count - 14);
            for (int i = from; i < mp.Chat.Count; i++)
            {
                var c = mp.Chat[i];
                var t = Ui.Label(chat, "<color=" + c.Colour + ">" + Esc(c.Name) + "</color>  " + Esc(c.Text), 24, Color.white, TextAnchor.MiddleLeft, false);
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.rectTransform.sizeDelta = new Vector2(0, 34);
            }
            if (mp.Chat.Count == 0)
            {
                var t = Ui.Label(chat, "Chat from website players shows here.", 22, Ui.Dim, TextAnchor.MiddleLeft, false);
                t.rectTransform.sizeDelta = new Vector2(0, 34);
            }
        }

        static string Esc(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
    }

    // ---------------------------------------------------------------------- results

    public sealed class ResultsScreen : FramePanel
    {
        readonly OnlineHub online;
        readonly RectTransform list;
        readonly Text winner;

        public ResultsScreen(Transform canvas, OnlineHub online) : base(canvas, "RESULTS")
        {
            this.online = online;
            winner = Ui.Label(Root, "", 44, Ui.Gold, TextAnchor.MiddleLeft);
            Ui.Place(winner.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -160), new Vector2(1600, 60));
            list = Ui.Scroll(Content, false, Vector2.zero, 10);
            BackButton.Label.text = "CONTINUE";
            FirstSelect = BackButton;
        }

        protected override void OnShow()
        {
            var mp = online.Mp;
            Ui.Clear(list);
            string w = null;
            int place = 1;
            if (mp.Results != null)
                foreach (var r in mp.Results)
                {
                    if (r.Actor == mp.Winner) w = r.Name;
                    bool me = r.Actor == mp.Me;
                    var row = Ui.Card(list, r.Name, place == 1 ? Ui.Gold : me ? Ui.Red : new Color(1, 1, 1, 0.15f));
                    row.sizeDelta = new Vector2(0, 80);
                    var p = Ui.Label(row, "#" + place, 34, place == 1 ? Ui.Gold : Color.white);
                    Ui.Place(p.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(120, 50));
                    var n = Ui.Label(row, r.Name + (me ? "  (YOU)" : ""), 32, Color.white);
                    Ui.Place(n.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(170, 0), new Vector2(900, 50));
                    var s = Ui.Label(row, r.Score.ToString(CultureInfo.InvariantCulture), 36, Ui.Gold, TextAnchor.MiddleRight);
                    Ui.Place(s.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(240, 50));
                    place++;
                }
            winner.text = mp.Winner == mp.Me && mp.Winner != 0 ? "YOU WIN!  +10 XP" : w != null ? w.ToUpperInvariant() + " WINS" : "MATCH OVER";
        }
    }
}
