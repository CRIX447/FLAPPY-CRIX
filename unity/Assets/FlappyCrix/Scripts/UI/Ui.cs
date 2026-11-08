// Builds the interface in code (no prefabs to break): the canvas, text, images, the edgy
// slanted menu buttons and the settings rows. Everything uses the game's font.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    public static class Ui
    {
        public static readonly Color Red = new Color32(0xFF, 0x46, 0x55, 255);
        public static readonly Color Orange = new Color32(0xFF, 0x6B, 0x35, 255);
        public static readonly Color Gold = new Color32(0xFF, 0xD7, 0x00, 255);
        public static readonly Color Green = new Color32(0x00, 0xCC, 0x7A, 255);
        public static readonly Color Ink = new Color32(0x0A, 0x0A, 0x0F, 255);
        public static readonly Color CardBg = new Color(12 / 255f, 12 / 255f, 20 / 255f, 0.94f);
        public static readonly Color Lip = new Color(150 / 255f, 20 / 255f, 35 / 255f, 0.8f);
        public static readonly Color Dim = new Color32(0xA6, 0xA6, 0xB2, 255);

        /// <summary>The interface is drawn by the game camera, so the screen effects (glow, glitch) touch it too.</summary>
        public static Canvas MakeCanvas(Transform parent, Camera cam, out CanvasScaler scaler)
        {
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 1;
            c.sortingOrder = 1000;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }
            return c;
        }

        // ------------------------------------------------------------------ screen shapes
        // Each kind of device gets its own layout:
        //   Desktop  - wide screens (PC, laptop, wide tablet): three columns like the website
        //   Compact  - phones held sideways and squarer screens: one big two-half panel, bigger buttons
        //   Portrait - phones and tablets held upright: one tall column like the website on a phone
        public enum Form { Desktop, Compact, Portrait }
        public static Form CurrentForm = Form.Desktop;
        public static int FormVersion = 1;
        public static Vector2 CanvasSize = new Vector2(1920, 1080);

        /// <summary>Called every frame by GameRoot with the canvas; panels re-lay themselves out when this changes.</summary>
        public static void UpdateForm(RectTransform canvas)
        {
            var size = canvas.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            CanvasSize = size;
            Form f;
            if (size.y > size.x) f = Form.Portrait;
            else if (IsPhone() || size.x < 1700) f = Form.Compact;
            else f = Form.Desktop;
            if (f != CurrentForm) { CurrentForm = f; FormVersion++; }
        }

        /// <summary>A phone (not a tablet): a mobile screen whose short side is under about 4 inches.</summary>
        public static bool IsPhone()
        {
            if (!Application.isMobilePlatform) return false;
            float dpi = Screen.dpi;
            if (dpi <= 0) return true;
            return Mathf.Min(Screen.width, Screen.height) / dpi < 4f;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(RectTransform r, float l = 0, float b = 0, float rr = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
            return r;
        }

        /// <summary>Place from the parent's top-left corner: x right, y down (like a web page).</summary>
        public static RectTransform At(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            return r;
        }

        /// <summary>A rounded box: background, optional edge, optional darker lip underneath (the site's "3D" buttons and cards).</summary>
        public static RectTransform Box(Transform parent, string name, Color bg, Color edge, int radius = 18, int edgeWidth = 2, float lip = 0, Color lipColour = default(Color))
        {
            var r = Rect(parent, name);
            if (lip > 0)
            {
                var l = Img(r, "Lip", Res.Rounded(radius), lipColour);
                Fill(l.rectTransform, 0, -lip, 0, lip);
            }
            var b = Img(r, "Bg", Res.Rounded(radius), bg);
            Fill(b.rectTransform);
            if (edge.a > 0)
            {
                var e = Img(r, "Edge", Res.Rounded(radius, edgeWidth), edge);
                Fill(e.rectTransform);
            }
            return r;
        }

        /// <summary>
        /// A pill button like the site's: a fill (solid or a left-to-right gradient), a darker lip under it,
        /// an edge, and an emoji + label centred. Goes a bit brighter and gets the green edge when it's the one you're on.
        /// </summary>
        public static SlantButton Pill(Transform parent, string label, string icon, Action onClick, Color left, Color right,
                                       Color edge, Color text, int fontSize, Color lipColour, int radius = 40)
        {
            var root = Rect(parent, "Pill " + label);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var lip = Img(root, "Lip", Res.Rounded(radius), lipColour);
            Fill(lip.rectTransform, 0, -6, 0, 6);
            var fill = Img(root, "Fill", left == right ? Res.Rounded(radius) : Res.Rounded(radius, 0, left, right), left == right ? left : Color.white);
            Fill(fill.rectTransform);
            var e = Img(root, "Edge", Res.Rounded(radius, 2), edge);
            Fill(e.rectTransform);
            var b = root.gameObject.AddComponent<SlantButton>();
            b.targetGraphic = hit; b.transition = Selectable.Transition.None;
            b.Background = fill; b.Base = fill.color; b.Edge = e; b.EdgeBase = edge;
            var row = Rect(root, "Content");
            Fill(row, 16, 0, 16, 0);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter; h.spacing = Mathf.Max(8, fontSize * 0.35f);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            if (icon != null && Res.Emoji(icon) != null)
            {
                var ic = Img(row, "Icon", Res.Emoji(icon), Color.white);
                ic.preserveAspect = true;
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = fontSize * 1.1f;
                b.Icon = ic;
            }
            var t = Label(row, label, fontSize, text, TextAnchor.MiddleCenter);
            b.Text = t;
            b.onClick.AddListener(() => { Sfx.Play("click", 0.6f); if (onClick != null) onClick(); });
            return b;
        }

        /// <summary>Place: anchor (0..1), pivot, position, size.</summary>
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        public static Image Img(Transform parent, string name, Sprite s, Color c)
        {
            var r = Rect(parent, name);
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = s; i.color = c; i.raycastTarget = false;
            if (s != null && s.border != Vector4.zero) i.type = Image.Type.Sliced;
            return i;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft, bool shadow = true)
        {
            var r = Rect(parent, "Text");
            var t = r.gameObject.AddComponent<Text>();
            t.font = Res.Font; t.text = text; t.fontSize = size; t.color = c; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow)
            {
                var s = r.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.5f); s.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        /// <summary>A big slanted menu button. sub = small text under the label (or null).</summary>
        public enum Look { Dark, Primary, Online, Green }

        /// <summary>
        /// A big rounded menu button in the website's style: PLAY is the red-to-orange pill, the rest are
        /// dark pills; the one you're on lifts and gets the site's green edge. icon = an emoji code.
        /// </summary>
        public static MenuButton MenuButton(Transform parent, string label, string sub, Action onClick, float width = 560, float height = 92,
                                            string icon = null, Look look = Look.Dark)
        {
            int r = Mathf.Min(48, (int)(height / 2));
            var root = Rect(parent, "Button " + label);
            root.sizeDelta = new Vector2(width, height);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var b = root.gameObject.AddComponent<MenuButton>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;

            var body = Rect(root, "Body");
            Fill(body);
            b.Body = body;
            var lip = Img(body, "Lip", Res.Rounded(r), look == Look.Primary ? new Color(180 / 255f, 30 / 255f, 50 / 255f, 0.85f) : look == Look.Green ? new Color(0, 0.4f, 0.25f, 0.9f) : new Color(0, 0, 0, 0.55f));
            Fill(lip.rectTransform, 0, -5, 0, 5);
            Image bg;
            if (look == Look.Primary) bg = Img(body, "Fill", Res.Rounded(r, 0, Red, Orange), Color.white);
            else if (look == Look.Green) bg = Img(body, "Fill", Res.Rounded(r, 0, new Color32(0x0E, 0x8F, 0x62, 255), Green), Color.white);
            else if (look == Look.Online) bg = Img(body, "Fill", Res.Rounded(r), new Color(0.2f, 0.05f, 0.07f, 0.96f));
            else bg = Img(body, "Fill", Res.Rounded(r), new Color(0.11f, 0.11f, 0.15f, 0.96f));
            Fill(bg.rectTransform);
            b.Background = bg;
            var edge = Img(body, "Edge", Res.Rounded(r, 3), look == Look.Online ? Orange : new Color(1, 1, 1, 0.16f));
            Fill(edge.rectTransform);
            b.Edge = edge; b.EdgeColour = edge.color;
            var holder = Rect(body, "Content");
            Fill(holder);
            b.Content = holder;
            float textLeft = 44;
            if (icon != null && Res.Emoji(icon) != null)
            {
                var ic = Img(holder, "Icon", Res.Emoji(icon), Color.white);
                ic.preserveAspect = true;
                float s = Mathf.Min(height * 0.5f, 50);
                Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(s, s));
                textLeft = 34 + s + 18;
            }
            var t = Label(holder, label, sub != null ? 40 : (height >= 80 ? 46 : 34), look == Look.Online ? Orange : Color.white,
                          sub != null ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            Fill(t.rectTransform, textLeft, 0, 20, sub != null ? 12 : 0);
            b.Label = t; b.LabelColour = t.color;
            if (sub != null)
            {
                var sl = Label(holder, sub, 19, look == Look.Primary ? new Color(1, 1, 1, 0.85f) : Dim, TextAnchor.LowerLeft, false);
                Fill(sl.rectTransform, textLeft + 2, 14, 20, 0);
                sl.horizontalOverflow = HorizontalWrapMode.Wrap;
                b.Sub = sl;
            }
            b.onClick.AddListener(() => { if (onClick != null) onClick(); });
            return b;
        }

        /// <summary>A settings row: label on the left, a value you change with left/right.</summary>
        public static OptionRow Option(Transform parent, string label, Func<string> value, Action<int> change, float width = 980)
        {
            var r = Rect(parent, "Option " + label);
            var le = r.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = 74;
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = Res.Rounded(20); bg.type = Image.Type.Sliced; bg.color = CardBg;
            var edge = Img(r, "Edge", Res.Rounded(20, 2), new Color(1, 1, 1, 0.12f));
            Fill(edge.rectTransform);
            var o = r.gameObject.AddComponent<OptionRow>();
            o.Edge = edge;
            o.targetGraphic = bg; o.transition = Selectable.Transition.None;
            o.Background = bg; o.ValueOf = value; o.Change = change;
            var t = Label(r, label, 32, Color.white);
            Fill(t.rectTransform, 44, 0, 0, 0);
            o.Label = t;
            var left = Label(r, "<", 36, Orange, TextAnchor.MiddleCenter);
            Place(left.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-370, 0), new Vector2(60, 60));
            var val = Label(r, "", 32, Gold, TextAnchor.MiddleCenter);
            Place(val.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(280, 60));
            var right = Label(r, ">", 36, Orange, TextAnchor.MiddleCenter);
            Place(right.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(60, 60));
            o.Value = val; o.Left = left; o.Right = right;
            o.Refresh();
            return o;
        }

        /// <summary>Chains buttons for keyboard / gamepad: up and down.</summary>
        public static void Chain(Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = items[(i + items.Length - 1) % items.Length];
                n.selectOnDown = items[(i + 1) % items.Length];
                items[i].navigation = n;
            }
        }
    
        // ------------------------------------------------------------------ bigger pieces (phase 2 screens)

        /// <summary>A scrolling area; returns the content that items go into (it grows to fit them).</summary>
        public static RectTransform Scroll(RectTransform area, bool grid, Vector2 cell, float spacing = 16)
        {
            var sr = area.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40;
            var view = Fill(Rect(area, "Viewport"));
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var content = Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            if (grid)
            {
                var g = content.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = cell; g.spacing = new Vector2(spacing, spacing); g.childAlignment = TextAnchor.UpperCenter;
                g.constraint = GridLayoutGroup.Constraint.Flexible;
            }
            else
            {
                var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = spacing; v.childControlHeight = false; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            }
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = view; sr.content = content;
            return content;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>A small solid button (shop BUY, JOIN...).</summary>
        public static Button SmallButton(Transform parent, string label, Action onClick, Color colour, float w = 220, float h = 64, int size = 26)
        {
            var r = Rect(parent, "Button " + label);
            r.sizeDelta = new Vector2(w, h);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
            int rad = Mathf.Min(40, (int)(h / 2));
            var lip = Img(r, "Lip", Res.Rounded(rad), new Color(0, 0, 0, 0.5f));
            Fill(lip.rectTransform, 0, -4, 0, 4);
            var img = r.gameObject.AddComponent<Image>();
            bool primary = colour == Red;
            img.sprite = primary ? Res.Rounded(rad, 0, Red, Orange) : Res.Rounded(rad); img.type = Image.Type.Sliced;
            img.color = primary ? Color.white : colour;
            var edge = Img(r, "Edge", Res.Rounded(rad, 2), new Color(1, 1, 1, 0.15f));
            Fill(edge.rectTransform);
            var b = r.gameObject.AddComponent<SlantButton>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            b.Background = img; b.Base = img.color; b.Edge = edge;
            var t = Label(r, label, size, Color.white, TextAnchor.MiddleCenter);
            Fill(t.rectTransform);
            b.onClick.AddListener(() => { Sfx.Play("click", 0.6f); if (onClick != null) onClick(); });
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>A card: a dark slanted panel with a bright edge.</summary>
        /// <summary>A card in the website's style: dark, rounded, a coloured edge and a darker lip under it.</summary>
        public static RectTransform Card(Transform parent, string name, Color edge, float alpha = 0.94f)
        {
            var holder = Rect(parent, name);
            var lip = Img(holder, "Lip", Res.Rounded(22), new Color(edge.r * 0.55f, edge.g * 0.55f, edge.b * 0.55f, Mathf.Min(1, edge.a + 0.2f)));
            Fill(lip.rectTransform, 0, -5, 0, 5);
            var bg = Img(holder, "Bg", Res.Rounded(22), new Color(CardBg.r, CardBg.g, CardBg.b, alpha));
            Fill(bg.rectTransform);
            var e = Img(holder, "Edge", Res.Rounded(22, 3), edge);
            Fill(e.rectTransform);
            return holder;
        }

        public static InputField Input(Transform parent, string placeholder, int max, float w, float h)
        {
            var r = Rect(parent, "Input");
            r.sizeDelta = new Vector2(w, h);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = Res.Rounded(20); bg.type = Image.Type.Sliced; bg.color = CardBg;
            var ie = Img(r, "Edge", Res.Rounded(20, 2), Orange);
            Fill(ie.rectTransform);
            var f = r.gameObject.AddComponent<InputField>();
            var text = Label(r, "", 40, Gold, TextAnchor.MiddleCenter, false);
            Fill(text.rectTransform, 12, 0, 12, 0);
            text.supportRichText = false;
            var ph = Label(r, placeholder, 30, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter, false);
            Fill(ph.rectTransform, 12, 0, 12, 0);
            f.textComponent = text; f.placeholder = ph; f.characterLimit = max;
            f.targetGraphic = bg;
            return f;
        }

        public static Image Bar(Transform parent, float value, Color colour, float w, float h)
        {
            var back = Img(parent, "Bar", Res.Rounded(6), new Color(1, 1, 1, 0.1f));
            back.rectTransform.sizeDelta = new Vector2(w, h);
            var fill = Img(back.rectTransform, "Fill", Res.Rounded(6), colour);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return back;
        }
    }
}
