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
        public static readonly Color Gold = new Color32(0xFF, 0xD5, 0x4A, 255);
        public static readonly Color Green = new Color32(0x2B, 0xFF, 0xA3, 255);
        public static readonly Color Ink = new Color32(0x07, 0x06, 0x0C, 255);
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
                s.effectColor = new Color(0, 0, 0, 0.75f); s.effectDistance = new Vector2(3, -3);
            }
            return t;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        /// <summary>A big slanted menu button. sub = small text under the label (or null).</summary>
        public static MenuButton MenuButton(Transform parent, string label, string sub, Action onClick, float width = 560, float height = 92)
        {
            var r = Rect(parent, "Button " + label);
            r.sizeDelta = new Vector2(width, height);
            var le = r.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = Res.Slant; bg.type = Image.Type.Sliced; bg.color = new Color(1, 1, 1, 0.06f);
            var b = r.gameObject.AddComponent<MenuButton>();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.None;
            b.Background = bg;
            var bar = Img(r, "Bar", Res.White, Red);
            Place(bar.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(8, height * 0.62f));
            b.Bar = bar;
            var holder = Rect(r, "Content");
            Fill(holder);
            b.Content = holder;
            var t = Label(holder, label, sub != null ? 44 : 48, Color.white, sub != null ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            Fill(t.rectTransform, 48, sub != null ? 0 : 0, 20, sub != null ? 10 : 0);
            b.Label = t;
            if (sub != null)
            {
                var s = Label(holder, sub, 20, Dim, TextAnchor.LowerLeft, false);
                Fill(s.rectTransform, 50, 12, 20, 0);
                b.Sub = s;
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
            bg.sprite = Res.Slant; bg.type = Image.Type.Sliced; bg.color = new Color(1, 1, 1, 0.05f);
            var o = r.gameObject.AddComponent<OptionRow>();
            o.targetGraphic = bg; o.transition = Selectable.Transition.None;
            o.Background = bg; o.ValueOf = value; o.Change = change;
            var t = Label(r, label, 32, Color.white);
            Fill(t.rectTransform, 44, 0, 0, 0);
            o.Label = t;
            var left = Label(r, "<", 36, Red, TextAnchor.MiddleCenter);
            Place(left.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-370, 0), new Vector2(60, 60));
            var val = Label(r, "", 32, Gold, TextAnchor.MiddleCenter);
            Place(val.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(280, 60));
            var right = Label(r, ">", 36, Red, TextAnchor.MiddleCenter);
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
    }
}
