// The main menu, laid out like the Flappy Crix website, with the live game playing behind it.
//   Desktop  - three columns like the site on a computer: you (level, stats, today) on the left,
//              the big menu card in the middle, score / next milestone / controls on the right.
//   Compact  - phones held sideways and squarer screens: one wide card split in two halves,
//              with bigger buttons.
//   Portrait - phones and tablets held upright: like the site on a phone. Account and coins on top,
//              the menu card, then your level and stats underneath.
// Everything is built once; Layout() only moves and resizes it for the screen shape.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections.Generic;
using System.Globalization;
using FlappyCrix.Native;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class MenuScreen : Panel
    {
        // the site's colours for the menu pieces
        static readonly Color PanelBg = new Color(0.035f, 0.035f, 0.05f, 0.94f);
        static readonly Color ColumnBg = new Color(0.055f, 0.055f, 0.075f, 0.92f);
        static readonly Color SoftEdge = new Color(1, 1, 1, 0.07f);
        static readonly Color TileEdge = new Color(1f, 0.42f, 0.21f, 0.32f);
        static readonly Color Maroon = new Color(0.17f, 0.05f, 0.06f, 1);

        readonly Func<SaveData> saveOf;
        SaveData save => saveOf();
        public Func<bool> DailyBadge = () => false;
        public Func<string> AccountLine = () => "Guest";
        public Func<bool> SignedIn = () => false;
        public int LastScore;
        /// <summary>Opens the locker (the wardrobe), where you wear what you own.</summary>
        public Action OpenLocker;

        // everything sits in here, so it can shrink to fit any screen
        readonly RectTransform holder;

        // centre card
        readonly RectTransform panel, panelLine;
        readonly SlantButton online, play, music, account, website, quit;
        readonly Text accountName, accountChip, accountActionText, avatarLetter;
        readonly RectTransform accountChipBox, accountActionBox, avatar;
        readonly GlitchText title;
        readonly Text tagline;
        readonly RectTransform birdFrame;
        readonly Image bird;
        readonly RectTransform chipsRow;
        readonly Text chipCoins, chipBest, chipAwards;
        readonly List<Tile> tiles = new List<Tile>();
        readonly Image dailyDot;

        // left column
        readonly RectTransform leftCol, brandBox, levelCard, statsCard, todayCard;
        readonly Text brand, brandSub, levelNum, levelText, xpText, statCoins, statBest, statAwards, statGames, todayText;
        readonly Image xpFill;
        readonly RectTransform xpBack;

        // right column
        readonly RectTransform rightCol, scoreCard, mileCard, controlsCard;
        readonly Text scoreValue, scoreBest, mileValue;
        readonly Image mileFill;

        // portrait-only coin pill
        readonly RectTransform coinPill;
        readonly Text coinPillText;

        float age;
        readonly bool canQuit;

        // While you play on a computer (Settings > Game view: Website) the menu stays up as the frame:
        // the centre card empties and the game shows through it; the side columns stay, with the live score.
        readonly Image dimImg, gridImg, panelBg;
        readonly Text scoreHead;
        public bool Playing { get; private set; }
        int liveScore;

        sealed class Tile
        {
            public SlantButton Button;
            public RectTransform Rt;
            public Image Icon;
            public Text Label;
        }

        public MenuScreen(Transform canvas, Func<SaveData> saveOf, Action playAction, Action onlineAction, Action shopAction, Action dailyAction,
                          Action awardsAction, Action accountAction, Action settingsAction, Action creditsAction, Action quitAction) : base(canvas, "Menu")
        {
            this.saveOf = saveOf;

            // the live game shows through, dimmed, behind the site's grid
            var dim = Ui.Img(Root, "Dim", Res.White, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.55f));
            Ui.Fill(dim.rectTransform);
            var grid = Ui.Img(Root, "Grid", Res.Grid(), new Color(1, 1, 1, 0.6f));
            grid.type = Image.Type.Tiled;
            Ui.Fill(grid.rectTransform);
            dimImg = dim; gridImg = grid;

            holder = Ui.Rect(Root, "Holder");
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);

            // ---------------------------------------------------------------- centre card
            panel = Ui.Box(holder, "MenuCard", PanelBg, Ui.Red, 30, 4, 7, new Color(0.55f, 0.08f, 0.14f, 0.9f));
            panelBg = panel.Find("Bg").GetComponent<Image>();

            online = Ui.Pill(panel, "PLAY ONLINE", "1f3ae", onlineAction, Maroon, Maroon, new Color(1f, 0.42f, 0.21f, 0.75f), Ui.Orange, 28,
                             new Color(0.35f, 0.08f, 0.05f, 0.9f), 26);
            var dot = Ui.Label((RectTransform)online.transform.Find("Content"), "●", 22, Ui.Gold, TextAnchor.MiddleCenter, false);
            dot.name = "Dot";

            // account bar: avatar, name, LOCAL / ONLINE chip, SIGN IN
            account = Ui.Pill(panel, "", null, accountAction, new Color(0.06f, 0.06f, 0.08f, 1), new Color(0.06f, 0.06f, 0.08f, 1),
                              new Color(1f, 0.27f, 0.33f, 0.7f), Color.white, 10, new Color(0.3f, 0.05f, 0.08f, 0.9f), 26);
            account.Grow = 0.015f;
            account.transform.Find("Content").gameObject.SetActive(false);
            avatar = Ui.Img(account.transform, "Avatar", Res.Rounded(40, 0, Ui.Red, Ui.Orange), Color.white).rectTransform;
            avatarLetter = Ui.Label(avatar, "G", 26, Color.white, TextAnchor.MiddleCenter);
            Ui.Fill(avatarLetter.rectTransform);
            accountName = Ui.Label(account.transform, "GUEST", 24, Color.white, TextAnchor.MiddleLeft);
            accountChipBox = Ui.Box(account.transform, "Chip", new Color(0, 0.8f, 0.48f, 0.08f), new Color(0, 0.8f, 0.48f, 0.45f), 16, 2);
            accountChip = Ui.Label(accountChipBox, "LOCAL", 16, Ui.Green, TextAnchor.MiddleCenter, false);
            Ui.Fill(accountChip.rectTransform);
            accountActionBox = Ui.Box(account.transform, "Action", Ui.Red, new Color(0, 0, 0, 0), 22, 2, 4, new Color(0.6f, 0.1f, 0.15f, 1));
            var actImg = accountActionBox.Find("Bg").GetComponent<Image>();
            actImg.sprite = Res.Rounded(22, 0, Ui.Red, Ui.Orange); actImg.color = Color.white;
            accountActionText = Ui.Label(accountActionBox, "SIGN IN", 18, Color.white, TextAnchor.MiddleCenter);
            Ui.Fill(accountActionText.rectTransform);

            title = GlitchText.Make(panel, "FLAPPY CRIX", 92, Color.white, TextAnchor.MiddleCenter);
            tagline = Ui.Label(panel, "T A P   ·   C O L L E C T   ·   D R I P", 20, Ui.Green, TextAnchor.MiddleCenter, false);
            panelLine = Ui.Img(panel, "Line", Res.White, Ui.Red).rectTransform;

            // the bird in its frame; it bobs and tilts like it's about to flap
            birdFrame = Ui.Box(panel, "BirdFrame", new Color(0.07f, 0.07f, 0.1f, 1), new Color(0, 0, 0, 0), 26, 3, 6, new Color(0.45f, 0.06f, 0.1f, 1));
            var frameEdge = Ui.Img(birdFrame, "Edge", Res.Rounded(26, 4, Ui.Red, Ui.Orange), Color.white);
            Ui.Fill(frameEdge.rectTransform);
            var mask = Ui.Rect(birdFrame, "Mask");
            Ui.Fill(mask, 6, 6, 6, 6);
            mask.gameObject.AddComponent<RectMask2D>();
            bird = Ui.Img(mask, "Bird", Res.Get("bird"), Color.white);
            bird.preserveAspect = true;
            Ui.Fill(bird.rectTransform, 4, 4, 4, 4);

            // three stat chips like the site's: COINS / BEST / ACHIEVEMENTS
            chipsRow = Ui.Rect(panel, "Chips");
            chipCoins = Chip(chipsRow, "COINS", "coin", 0);
            chipBest = Chip(chipsRow, "BEST", null, 1);
            chipAwards = Chip(chipsRow, "ACHIEVEMENTS", "1f3c6", 2);

            play = Ui.Pill(panel, "PLAY", "25b6", playAction, Ui.Red, Ui.Orange, new Color(1, 1, 1, 0.18f), Color.white, 40,
                           new Color(0.62f, 0.1f, 0.16f, 1));
            music = Ui.Pill(panel, "MUSIC", "1f50a", ToggleMusic, new Color32(0x0E, 0x8F, 0x62, 255), Ui.Green, new Color(1, 1, 1, 0.18f), Color.white, 34,
                            new Color(0.02f, 0.35f, 0.22f, 1));

            tiles.Add(MakeTile(panel, "DAILY", "1f381", dailyAction));
            tiles.Add(MakeTile(panel, "STORE", "1f6d2", shopAction));
            tiles.Add(MakeTile(panel, "AWARDS", "1f3c6", awardsAction));
            tiles.Add(MakeTile(panel, "LOCKER", "1f9e2", () => { if (OpenLocker != null) OpenLocker(); }));
            tiles.Add(MakeTile(panel, "SETTINGS", "2699", settingsAction));
            tiles.Add(MakeTile(panel, "CREDITS", "2728", creditsAction));
            dailyDot = Ui.Img(tiles[0].Rt, "Dot", Res.Rounded(10), Ui.Red);
            Ui.Place(dailyDot.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(16, 16));

            website = Ui.Pill(panel, "CRIXGAMING VR", "1f310", () => Application.OpenURL("https://crixgamingvr.com"),
                              new Color(1, 1, 1, 0.04f), new Color(1, 1, 1, 0.04f), SoftEdge, Ui.Dim, 18, new Color(0, 0, 0, 0.3f), 22);
            website.Grow = 0.02f;
            canQuit = !Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;
            quit = Ui.Pill(panel, "QUIT", "1f6aa", quitAction, new Color(1, 1, 1, 0.04f), new Color(1, 1, 1, 0.04f), SoftEdge, Ui.Dim, 18,
                           new Color(0, 0, 0, 0.3f), 22);
            quit.Grow = 0.02f;
            quit.gameObject.SetActive(canQuit);

            // ---------------------------------------------------------------- left column
            leftCol = Ui.Rect(holder, "Left");
            brandBox = Ui.Rect(leftCol, "Brand");
            brand = Ui.Label(brandBox, "FLAPPY <color=#FF6B35>CRIX</color>", 46, Color.white, TextAnchor.UpperLeft);
            brand.supportRichText = true;
            brandSub = Ui.Label(brandBox, "GUEST · SIGN IN TO SAVE", 17, Ui.Dim, TextAnchor.UpperLeft, false);

            levelCard = Ui.Box(leftCol, "Level", ColumnBg, SoftEdge, 20, 2, 5, new Color(0, 0, 0, 0.5f));
            var ring = Ui.Img(levelCard, "Ring", Res.Rounded(36, 4), new Color(1, 1, 1, 0.14f));
            Ui.At(ring.rectTransform, 18, 20, 72, 72);
            var ringIn = Ui.Img(ring.rectTransform, "In", Res.Rounded(32), new Color(0, 0, 0, 0.35f));
            Ui.Fill(ringIn.rectTransform, 4, 4, 4, 4);
            levelNum = Ui.Label(ring.rectTransform, "1", 30, Color.white, TextAnchor.MiddleCenter);
            Ui.Fill(levelNum.rectTransform);
            levelText = Ui.Label(levelCard, "LEVEL 1", 28, Color.white, TextAnchor.MiddleLeft);
            Ui.At(levelText.rectTransform, 106, 20, 230, 36);
            xpBack = Ui.Img(levelCard, "XpBack", Res.Rounded(6), new Color(1, 1, 1, 0.09f)).rectTransform;
            Ui.At(xpBack, 108, 60, 200, 10);
            xpFill = Ui.Img(xpBack, "XpFill", Res.Rounded(6, 0, Ui.Red, Ui.Orange), Color.white);
            xpFill.rectTransform.anchorMin = Vector2.zero; xpFill.rectTransform.anchorMax = new Vector2(0, 1);
            xpFill.rectTransform.pivot = new Vector2(0, 0.5f); xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;
            xpText = Ui.Label(levelCard, "0 / 50 XP", 16, Ui.Dim, TextAnchor.MiddleLeft, false);
            Ui.At(xpText.rectTransform, 108, 74, 220, 24);

            statsCard = Ui.Box(leftCol, "Stats", ColumnBg, SoftEdge, 20, 2, 5, new Color(0, 0, 0, 0.5f));
            statCoins = StatRow(statsCard, "COINS", "coin", 0, Ui.Orange);
            statBest = StatRow(statsCard, "BEST", "1f3c6", 1, Color.white);
            statAwards = StatRow(statsCard, "AWARDS", "1f3c5", 2, Color.white);
            statGames = StatRow(statsCard, "GAMES", "1f579", 3, Color.white);

            todayCard = Ui.Box(leftCol, "Today", ColumnBg, SoftEdge, 20, 2, 5, new Color(0, 0, 0, 0.5f));
            var todayHead = Ui.Label(todayCard, "TODAY", 17, Ui.Dim, TextAnchor.UpperLeft, false);
            Ui.At(todayHead.rectTransform, 20, 16, 280, 24);
            todayText = Ui.Label(todayCard, "", 18, Color.white, TextAnchor.UpperLeft, false);
            todayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.At(todayText.rectTransform, 20, 46, 300, 70);

            // ---------------------------------------------------------------- right column
            rightCol = Ui.Rect(holder, "Right");
            scoreCard = Ui.Box(rightCol, "Score", ColumnBg, SoftEdge, 20, 2, 5, new Color(0, 0, 0, 0.5f));
            var sh = Ui.Label(scoreCard, "LAST SCORE", 17, Ui.Dim, TextAnchor.UpperLeft, false);
            scoreHead = sh;
            Ui.At(sh.rectTransform, 20, 16, 200, 24);
            scoreBest = Ui.Label(scoreCard, "BEST 0", 18, Ui.Gold, TextAnchor.UpperRight, false);
            Ui.Place(scoreBest.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -16), new Vector2(200, 24));
            scoreValue = Ui.Label(scoreCard, "0", 72, Color.white, TextAnchor.UpperLeft);
            Ui.At(scoreValue.rectTransform, 18, 40, 300, 84);

            mileCard = Ui.Box(rightCol, "Milestone", ColumnBg, SoftEdge, 20, 2, 5, new Color(0, 0, 0, 0.5f));
            var mh = Ui.Label(mileCard, "NEXT MILESTONE", 17, Ui.Dim, TextAnchor.UpperLeft, false);
            Ui.At(mh.rectTransform, 20, 16, 300, 24);
            mileValue = Ui.Label(mileCard, "10", 38, Ui.Orange, TextAnchor.MiddleLeft);
            Ui.At(mileValue.rectTransform, 18, 44, 90, 44);
            var mileBack = Ui.Img(mileCard, "Bar", Res.Rounded(6), new Color(1, 1, 1, 0.09f));
            Ui.At(mileBack.rectTransform, 110, 62, 200, 10);
            mileFill = Ui.Img(mileBack.rectTransform, "Fill", Res.Rounded(6, 0, Ui.Red, Ui.Orange), Color.white);
            mileFill.rectTransform.anchorMin = Vector2.zero; mileFill.rectTransform.anchorMax = new Vector2(0, 1);
            mileFill.rectTransform.offsetMin = mileFill.rectTransform.offsetMax = Vector2.zero;

            controlsCard = Ui.Box(rightCol, "Controls", ColumnBg, SoftEdge, 20, 2, 5, new Color(0, 0, 0, 0.5f));
            var ch = Ui.Label(controlsCard, "CONTROLS", 17, Ui.Dim, TextAnchor.UpperLeft, false);
            Ui.At(ch.rectTransform, 20, 16, 300, 24);
            string[,] keys = { { "SPACE", "FLAP / PLAY" }, { "CLICK", "FLAP" }, { "ESC", "PAUSE" }, { "A", "FLAP (PAD)" }, { "F11", "FULLSCREEN" }, { "F9", "RECORD CLIP" } };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var k = Ui.Box(controlsCard, "Key", new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.1f), 10, 2);
                Ui.At(k, 20, 50 + i * 40, 96, 30);
                var kt = Ui.Label(k, keys[i, 0], 16, Color.white, TextAnchor.MiddleCenter, false);
                Ui.Fill(kt.rectTransform);
                var kd = Ui.Label(controlsCard, keys[i, 1], 16, Ui.Dim, TextAnchor.MiddleLeft, false);
                Ui.At(kd.rectTransform, 130, 50 + i * 40, 200, 30);
            }

            // ---------------------------------------------------------------- portrait coin pill
            coinPill = Ui.Box(holder, "CoinPill", new Color(0.06f, 0.06f, 0.08f, 0.95f), SoftEdge, 30, 2);
            var cpi = Ui.Img(coinPill, "Coin", Res.Get("coin"), Color.white);
            cpi.preserveAspect = true;
            Ui.Place(cpi.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(44, 44));
            coinPillText = Ui.Label(coinPill, "0", 30, Ui.Gold, TextAnchor.MiddleRight);
            Ui.Fill(coinPillText.rectTransform, 70, 0, 22, 0);

            FirstSelect = play;
        }

        // ---------------------------------------------------------------- pieces

        static Text Chip(RectTransform row, string name, string icon, int index)
        {
            var box = Ui.Box(row, name, new Color(0.03f, 0.03f, 0.04f, 1), new Color(1f, 0.42f, 0.21f, 0.6f), 18, 2, 5, new Color(0.35f, 0.12f, 0.05f, 0.9f));
            box.name = "Chip" + index;
            var l = Ui.Label(box, name, 15, Ui.Green, TextAnchor.MiddleCenter, false);
            Ui.Place(l.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(240, 22));
            var line = Ui.Rect(box, "Value");
            Ui.Place(line, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(240, 40));
            var h = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter; h.spacing = 8;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            if (icon != null)
            {
                var sp = icon == "coin" ? Res.Get("coin") : Res.Emoji(icon);
                if (sp != null)
                {
                    var ic = Ui.Img(line, "Icon", sp, Color.white);
                    ic.preserveAspect = true;
                    var le = ic.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = le.preferredHeight = 26;
                }
            }
            return Ui.Label(line, "0", 30, Ui.Orange, TextAnchor.MiddleCenter);
        }

        static Text StatRow(RectTransform card, string name, string icon, int i, Color valueColour)
        {
            float y = 18 + i * 44;
            var sp = icon == "coin" ? Res.Get("coin") : Res.Emoji(icon);
            if (sp != null)
            {
                var ic = Ui.Img(card, "Icon", sp, Color.white);
                ic.preserveAspect = true;
                Ui.At(ic.rectTransform, 20, y + 6, 26, 26);
            }
            var l = Ui.Label(card, name, 20, Color.white, TextAnchor.MiddleLeft, false);
            Ui.At(l.rectTransform, 56, y, 160, 38);
            var v = Ui.Label(card, "0", 26, valueColour, TextAnchor.MiddleRight);
            Ui.Place(v.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -y), new Vector2(170, 38));
            return v;
        }

        Tile MakeTile(Transform parent, string label, string icon, Action onClick)
        {
            var t = new Tile();
            t.Rt = Ui.Box(parent, "Tile " + label, new Color(0.04f, 0.04f, 0.055f, 1), TileEdge, 18, 2, 5, new Color(0.3f, 0.1f, 0.04f, 0.9f));
            var hit = t.Rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var b = t.Rt.gameObject.AddComponent<SlantButton>();
            b.targetGraphic = hit; b.transition = Selectable.Transition.None;
            var bg = t.Rt.Find("Bg").GetComponent<Image>();
            b.Background = bg; b.Base = bg.color;
            b.Edge = t.Rt.Find("Edge").GetComponent<Image>(); b.EdgeBase = TileEdge;
            b.Grow = 0.05f;
            b.onClick.AddListener(() => { Sfx.Play("click", 0.6f); if (onClick != null) onClick(); });
            t.Button = b;
            t.Icon = Ui.Img(t.Rt, "Icon", Res.Emoji(icon), Color.white);
            t.Icon.preserveAspect = true;
            t.Label = Ui.Label(t.Rt, label, 16, Ui.Dim, TextAnchor.MiddleCenter, false);
            return t;
        }

        void ToggleMusic()
        {
            if (Settings.Music > 0.001f)
            {
                PlayerPrefs.SetFloat("fc.music.before", Settings.Music);
                Settings.Music = 0;
            }
            else Settings.Music = Mathf.Max(0.1f, PlayerPrefs.GetFloat("fc.music.before", 0.6f));
            Settings.Save();
            RefreshMusic();
        }

        void RefreshMusic()
        {
            bool on = Settings.Music > 0.001f;
            music.Text.text = on ? "MUSIC" : "MUSIC OFF";
            if (music.Icon != null) music.Icon.sprite = Res.Emoji(on ? "1f50a" : "1f507");
        }

        // ---------------------------------------------------------------- layout

        protected override void Layout(Ui.Form form)
        {
            bool desk = form == Ui.Form.Desktop, portrait = form == Ui.Form.Portrait;
            leftCol.gameObject.SetActive(desk || portrait);
            rightCol.gameObject.SetActive(desk);
            coinPill.gameObject.SetActive(portrait);
            brandBox.gameObject.SetActive(desk);
            todayCard.gameObject.SetActive(desk);
            quit.gameObject.SetActive(canQuit && !portrait);
            // upright, the account bar sits above the card; otherwise inside it
            account.transform.SetParent(portrait ? (Transform)holder : panel, false);

            float W, H;
            if (desk) { W = 1608; H = 1000; LayoutDesktop(); }
            else if (portrait) { W = 1020; H = LayoutPortrait(); }
            else { W = 1840; H = 1000; LayoutCompact(); }

            holder.sizeDelta = new Vector2(W, H);
            Fit(W, H);
            if (Playing) { ShowCentre(false); brandBox.gameObject.SetActive(false); }
        }

        void ShowCentre(bool on)
        {
            for (int i = 0; i < panel.childCount; i++)
            {
                var c = panel.GetChild(i);
                if (c.name != "Lip" && c.name != "Bg" && c.name != "Edge") c.gameObject.SetActive(on);
            }
        }

        /// <summary>Shrinks everything evenly if the screen is smaller than the layout.</summary>
        void Fit(float w, float h)
        {
            var c = Ui.CanvasSize;
            float s = Mathf.Min(1f, (c.x - 40) / w, (c.y - 40) / h);
            holder.localScale = new Vector3(s, s, 1);
            holder.anchoredPosition = Vector2.zero;
        }

        void LayoutDesktop()
        {
            // columns: 340 | 24 | 780 | 24 | 340  (1508 wide... plus breathing room)
            Ui.At(leftCol, 0, 0, 380, 1000);
            Ui.At(panel, 414, 0, 780, 1000);
            Ui.At(rightCol, 1228, 0, 380, 1000);

            Ui.At(brandBox, 4, 10, 370, 100);
            Ui.At((RectTransform)brand.transform, 0, 0, 370, 56);
            Ui.At((RectTransform)brandSub.transform, 2, 60, 370, 26);
            Ui.At(levelCard, 0, 118, 380, 112);
            Ui.At(xpBack, 108, 60, 240, 10);
            Ui.At((RectTransform)levelText.transform, 106, 20, 250, 36);
            Ui.At(statsCard, 0, 252, 380, 196);
            Ui.At(todayCard, 0, 470, 380, 130);

            Ui.At(scoreCard, 0, 10, 380, 140);
            Ui.At(mileCard, 0, 172, 380, 104);
            Ui.At(controlsCard, 0, 298, 380, 300);

            float x = 24, w = 732;
            Ui.At((RectTransform)online.transform, x, 26, w, 64);
            online.Text.fontSize = 28;
            LayoutAccount(x, 110, w, 76, true);
            Ui.At((RectTransform)title.transform, x, 206, w, 104);
            title.Main.fontSize = 92;
            Ui.At((RectTransform)tagline.transform, x, 308, w, 28);
            Ui.At(panelLine, x, 346, w, 4);
            Ui.At(birdFrame, (780 - 170) / 2f, 372, 170, 170);
            LayoutChips(x + 50, 566, w - 100, 76);
            Ui.At((RectTransform)play.transform, x, 670, (w - 16) / 2, 76);
            Ui.At((RectTransform)music.transform, x + (w + 16) / 2, 670, (w - 16) / 2, 76);
            play.Text.fontSize = 40; music.Text.fontSize = 34;
            LayoutTiles(x, 774, w, 104, 6, true);
            LayoutLinks(904, 780);
        }

        void LayoutCompact()
        {
            // one wide card, left half: who you are; right half: what to do
            Ui.At(panel, 0, 0, 1840, 1000);
            float lx = 40, lw = 860, rx = 940, rw = 860;
            LayoutAccount(lx, 36, lw, 96, false);
            Ui.At((RectTransform)title.transform, lx, 160, lw, 130);
            title.Main.fontSize = 112;
            Ui.At((RectTransform)tagline.transform, lx, 290, lw, 34);
            tagline.fontSize = 24;
            Ui.At(panelLine, lx, 336, lw, 4);
            Ui.At(birdFrame, lx + (lw - 300) / 2, 370, 300, 300);
            LayoutChips(lx, 712, lw, 110);

            Ui.At((RectTransform)play.transform, rx, 36, rw, 150);
            play.Text.fontSize = 64;
            Ui.At((RectTransform)online.transform, rx, 212, rw, 120);
            online.Text.fontSize = 44;
            LayoutTiles(rx, 360, rw, 130, 3, false);
            Ui.At((RectTransform)music.transform, rx, 660, (rw - 20) / 2, 110);
            music.Text.fontSize = 36;
            // in this layout the links sit beside MUSIC, bottom right
            Ui.At((RectTransform)website.transform, rx + (rw + 20) / 2, 660, (rw - 20) / 2, quit.gameObject.activeSelf ? 50 : 110);
            if (quit.gameObject.activeSelf) Ui.At((RectTransform)quit.transform, rx + (rw + 20) / 2, 720, (rw - 20) / 2, 50);
            website.Text.fontSize = quit.gameObject.activeSelf ? 20 : 28;
        }

        float LayoutPortrait()
        {
            // top bar: account pill + coins, then the menu card, then your level and stats
            float W = 1020;
            LayoutAccountPill(0, 0, W - 290, 96);
            Ui.At(coinPill, W - 270, 0, 270, 96);

            float y = 0;
            float cardTop = 126;
            float x = 40, w = W - 80;
            y = 36;
            Ui.At((RectTransform)title.transform, x, y, w, 130); title.Main.fontSize = 116; y += 128;
            Ui.At((RectTransform)tagline.transform, x, y, w, 34); tagline.fontSize = 26; y += 50;
            Ui.At(panelLine, x, y, w, 4); y += 30;
            Ui.At(birdFrame, (W - 300) / 2, y, 300, 300); y += 340;
            Ui.At((RectTransform)play.transform, x, y, w, 140); play.Text.fontSize = 62; y += 166;
            Ui.At((RectTransform)online.transform, x, y, w, 120); online.Text.fontSize = 46; y += 150;
            // tiles in two columns, then MUSIC and the website link as the last row
            float tw = (w - 20) / 2, th = 116;
            for (int i = 0; i < tiles.Count; i++)
            {
                int col = i % 2, row = i / 2;
                Ui.At(tiles[i].Rt, x + col * (tw + 20), y + row * (th + 18), tw, th);
                TileShape(tiles[i], false, th);
            }
            y += 3 * (th + 18);
            Ui.At((RectTransform)music.transform, x, y, tw, th); music.Text.fontSize = 32;
            Ui.At((RectTransform)website.transform, x + tw + 20, y, tw, th); website.Text.fontSize = 24;
            y += th + 40;
            Ui.At(panel, 0, cardTop, W, y);
            chipsRow.gameObject.SetActive(false);

            // level and stats from the left column, under the card
            float below = cardTop + y + 30;
            Ui.At(leftCol, 0, below, W, 560);
            Ui.At(levelCard, 0, 0, W, 130);
            Ui.At(xpBack, 108, 64, W - 160, 12);
            Ui.At((RectTransform)levelText.transform, 106, 20, W - 160, 36);
            Ui.At(statsCard, 0, 156, W, 200);
            return below + 156 + 200 + 10;
        }

        void LayoutAccount(float x, float y, float w, float h, bool chip)
        {
            Ui.At((RectTransform)account.transform, x, y, w, h);
            float a = h - 24;
            Ui.At(avatar, 14, 12, a, a);
            avatarLetter.fontSize = (int)(a * 0.5f);
            Ui.At((RectTransform)accountName.transform, 24 + a, 0, w * 0.45f, h);
            accountName.fontSize = h > 90 ? 32 : 24;
            accountChipBox.gameObject.SetActive(chip);
            if (chip) Ui.Place(accountChipBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(110, 34));
            accountActionBox.gameObject.SetActive(true);
            Ui.Place(accountActionBox, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 2), new Vector2(h > 90 ? 200 : 150, h - 30));
            accountActionText.fontSize = h > 90 ? 24 : 18;
        }

        void LayoutAccountPill(float x, float y, float w, float h)
        {
            LayoutAccount(x, y, w, h, false);
            accountActionBox.gameObject.SetActive(false);
            Ui.At((RectTransform)accountName.transform, 24 + (h - 24), 0, w - h - 30, h);
            accountName.fontSize = 28;
        }

        void LayoutChips(float x, float y, float w, float h)
        {
            chipsRow.gameObject.SetActive(true);
            Ui.At(chipsRow, x, y, w, h);
            float cw = (w - 32) / 3;
            for (int i = 0; i < 3; i++)
            {
                var c = (RectTransform)chipsRow.GetChild(i);
                Ui.At(c, i * (cw + 16), 0, cw, h);
            }
            int big = h > 90 ? 40 : 30;
            chipCoins.fontSize = chipBest.fontSize = chipAwards.fontSize = big;
        }

        void LayoutTiles(float x, float y, float w, float h, int perRow, bool square)
        {
            float gap = 12;
            float tw = (w - gap * (perRow - 1)) / perRow;
            for (int i = 0; i < tiles.Count; i++)
            {
                int col = i % perRow, row = i / perRow;
                Ui.At(tiles[i].Rt, x + col * (tw + gap), y + row * (h + gap + 4), tw, h);
                TileShape(tiles[i], square, h);
            }
        }

        static void TileShape(Tile t, bool square, float h)
        {
            if (square)
            {
                Ui.Place(t.Icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(42, 42));
                Ui.Place(t.Label.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(200, 26));
                t.Label.alignment = TextAnchor.MiddleCenter;
                t.Label.fontSize = 16;
            }
            else
            {
                float s = h * 0.42f;
                Ui.Place(t.Icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(s, s));
                t.Label.rectTransform.anchorMin = Vector2.zero; t.Label.rectTransform.anchorMax = Vector2.one;
                t.Label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.Label.rectTransform.offsetMin = new Vector2(28 + s + 20, 0); t.Label.rectTransform.offsetMax = new Vector2(-10, 0);
                t.Label.alignment = TextAnchor.MiddleLeft;
                t.Label.fontSize = (int)Mathf.Clamp(h * 0.27f, 22, 34);
                t.Label.color = Color.white;
            }
            if (square) t.Label.color = Ui.Dim;
        }

        void LayoutLinks(float y, float panelW)
        {
            bool q = quit.gameObject.activeSelf;
            float lw = 250, qw = 150, gap = 14;
            float total = lw + (q ? gap + qw : 0);
            float x0 = (panelW - total) / 2;
            Ui.At((RectTransform)website.transform, x0, y, lw, 50);
            website.Text.fontSize = 18;
            if (q) Ui.At((RectTransform)quit.transform, x0 + lw + gap, y, qw, 50);
        }

        // ---------------------------------------------------------------- live bits

        public override void Tick(float dt)
        {
            age += dt;
            // the bird bobs and tilts in its frame
            float t = Time.unscaledTime;
            bird.rectTransform.anchoredPosition = new Vector2(0, Mathf.Sin(t * 2.4f) * 8);
            bird.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.4f + 0.6f) * 7);
            dailyDot.color = new Color(Ui.Red.r, Ui.Red.g, Ui.Red.b, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 3)));
            var dotT = online.transform.Find("Content/Dot");
            if (dotT != null)
            {
                var d = dotT.GetComponent<Text>();
                d.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 2)));
            }
        }

        protected override void OnShow() { Refresh(); }

        /// <summary>Playing: the centre card turns into the game's frame and the columns show the live score.</summary>
        public void SetPlaying(bool on)
        {
            Playing = on;
            ShowCentre(!on);
            brandBox.gameObject.SetActive(!on && Ui.CurrentForm == Ui.Form.Desktop);   // the game's coin counter sits there
            if (!on) Layout(Ui.CurrentForm);      // puts back what the layout hides or moves
            panelBg.enabled = !on;
            dimImg.enabled = !on;
            gridImg.enabled = !on;
            Group.interactable = !on; Group.blocksRaycasts = !on;
            scoreHead.text = on ? "SCORE" : "LAST SCORE";
            liveScore = 0;
            Refresh();
        }

        public void SetLiveScore(int s)
        {
            liveScore = s;
            if (!Playing) return;
            scoreValue.text = s.ToString(CultureInfo.InvariantCulture);
            int next = (s / 10 + 1) * 10;
            mileValue.text = next.ToString(CultureInfo.InvariantCulture);
            mileFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((s % 10) / 10f), 1);
        }

        /// <summary>Where the game shows on screen, as a fraction of the screen (the inside of the centre card).</summary>
        public Rect GameArea()
        {
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float inset = 6 * holder.lossyScale.x;
            float x0 = corners[0].x + inset, y0 = corners[0].y + inset, x1 = corners[2].x - inset, y1 = corners[2].y - inset;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            return Rect.MinMaxRect(Mathf.Clamp01(x0 / w), Mathf.Clamp01(y0 / h), Mathf.Clamp01(x1 / w), Mathf.Clamp01(y1 / h));
        }

        public void Refresh()
        {
            var inv = CultureInfo.InvariantCulture;
            var s = save;
            string who = AccountLine() ?? "Guest";
            bool signed = SignedIn();
            accountName.text = who.ToUpperInvariant();
            avatarLetter.text = who.Length > 0 ? who.Substring(0, 1).ToUpperInvariant() : "?";
            accountChip.text = signed ? "SYNCED" : "LOCAL";
            accountActionText.text = signed ? "ACCOUNT" : "SIGN IN";
            brandSub.text = signed ? who.ToUpperInvariant() : "GUEST · SIGN IN TO SAVE";

            dailyDot.gameObject.SetActive(DailyBadge());
            todayText.text = DailyBadge() ? "Your daily reward is ready. Open DAILY to grab it!" : "Daily reward collected. Come back tomorrow.";

            bool max = s.Level >= Catalog.MaxLevel;
            levelNum.text = s.Level.ToString(inv);
            levelText.text = max ? "LEVEL " + s.Level + " (MAX)" : "LEVEL " + s.Level;
            int need = Catalog.XpForLevel(s.Level);
            float f = max ? 1 : Mathf.Clamp01(s.Xp / (float)Mathf.Max(1, need));
            xpFill.rectTransform.anchorMax = new Vector2(f, 1);
            xpText.text = max ? "MAX LEVEL" : s.Xp + " / " + need + " XP";

            string coins = Catalog.FormatCoins(s.Coins);
            int awards = s.Achievements.Count, allAwards = Catalog.Achievements.Length;
            chipCoins.text = coins; chipBest.text = s.Best.ToString(inv); chipAwards.text = awards + "/" + allAwards;
            statCoins.text = coins; statBest.text = s.Best.ToString(inv); statAwards.text = awards + " / " + allAwards; statGames.text = s.Games.ToString(inv);
            coinPillText.text = coins;

            scoreBest.text = "BEST " + s.Best.ToString(inv);
            if (Playing) SetLiveScore(liveScore);
            else
            {
                scoreValue.text = LastScore.ToString(inv);
                int next = (s.Best / 10 + 1) * 10;
                mileValue.text = next.ToString(inv);
                mileFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((s.Best % 10) / 10f), 1);
            }

            RefreshMusic();
        }
    }
}
