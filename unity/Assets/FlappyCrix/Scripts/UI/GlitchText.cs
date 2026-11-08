// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    /// <summary>Glitchy title text: red and cyan copies jitter behind it now and then.</summary>
    public sealed class GlitchText : MonoBehaviour
    {
        public Text Main, RedCopy, CyanCopy;
        public float Intensity = 1;
        float next, until;
        Vector2 offR, offC;

        public static GlitchText Make(Transform parent, string text, int size, Color colour, TextAnchor align)
        {
            var holder = Ui.Rect(parent, "Glitch " + text);
            var g = holder.gameObject.AddComponent<GlitchText>();
            g.CyanCopy = Ui.Label(holder, text, size, new Color(0.1f, 0.95f, 1f, 0.7f), align, false);
            g.RedCopy = Ui.Label(holder, text, size, new Color(1f, 0.15f, 0.25f, 0.8f), align, false);
            g.Main = Ui.Label(holder, text, size, colour, align, true);
            foreach (var t in new[] { g.CyanCopy, g.RedCopy, g.Main }) Ui.Fill(t.rectTransform);
            return g;
        }

        public void SetText(string s) { Main.text = RedCopy.text = CyanCopy.text = s; }

        void Update()
        {
            float t = Time.unscaledTime;
            if (t > next)
            {
                next = t + UnityEngine.Random.Range(0.6f, 2.6f) / Mathf.Max(0.2f, Intensity);
                until = t + UnityEngine.Random.Range(0.06f, 0.18f);
                offR = new Vector2(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-5f, 5f)) * Intensity;
                offC = new Vector2(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-5f, 5f)) * Intensity;
            }
            bool active = t < until;
            Vector2 baseR = new Vector2(-3, 1), baseC = new Vector2(3, -1);
            RedCopy.rectTransform.anchoredPosition = active ? offR : baseR;
            CyanCopy.rectTransform.anchoredPosition = active ? offC : baseC;
            Main.rectTransform.anchoredPosition = active ? new Vector2(UnityEngine.Random.Range(-3f, 3f), 0) : Vector2.zero;
        }
    }
}
