// Screen titles in the website's style: the chunky font, red-to-orange left to right, with the
// site's dark drop shadow. (The name stays from the first version, which glitched; it doesn't now.)
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class GlitchText : MonoBehaviour
    {
        public Text Main;
        public float Intensity;        // kept for older callers; nothing glitches any more

        public static GlitchText Make(Transform parent, string text, int size, Color colour, TextAnchor align)
        {
            var holder = Ui.Rect(parent, "Title " + text);
            var g = holder.gameObject.AddComponent<GlitchText>();
            g.Main = Ui.Label(holder, text, size, Color.white, align, false);
            Ui.Fill(g.Main.rectTransform);
            var sh = g.Main.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(Mathf.Max(3, size / 22f), -Mathf.Max(3, size / 22f));
            g.Main.gameObject.AddComponent<GradientText>();
            return g;
        }

        public void SetText(string s) { Main.text = s; }
    }
}
