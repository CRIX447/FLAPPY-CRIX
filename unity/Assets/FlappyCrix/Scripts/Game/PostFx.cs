// Runs the screen effects shader on the camera. Settings > Effects: High (all of it),
// Low (lighter glow, no grain) or Off. Other scripts nudge it: Glitch(), Flash(), Desaturate.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;

namespace FlappyCrix
{
    [RequireComponent(typeof(Camera))]
    public sealed class PostFx : MonoBehaviour
    {
        public static PostFx Inst;

        public float Desaturate;           // 0..1, game over fades to grey
        public float ChromaBase = 0f;
        float glitch, flash, chromaKick;
        Color flashColor = Color.white;
        readonly RenderTexture[] chain = new RenderTexture[8];

        void Awake() { Inst = this; }

        /// <summary>A burst of glitch (0..1) that fades quickly.</summary>
        public static void Glitch(float amount) { if (Inst != null) Inst.glitch = Mathf.Max(Inst.glitch, amount); }
        public static void Flash(Color c, float amount) { if (Inst != null) { Inst.flashColor = c; Inst.flash = Mathf.Max(Inst.flash, amount); } }
        public static void Kick(float amount) { if (Inst != null) Inst.chromaKick = Mathf.Max(Inst.chromaKick, amount); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            glitch = Mathf.MoveTowards(glitch, 0, dt * 2.5f);
            flash = Mathf.MoveTowards(flash, 0, dt * 2.2f);
            chromaKick = Mathf.MoveTowards(chromaKick, 0, dt * 3f);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var m = Res.PostFx;
            if (m == null || Settings.Effects == Settings.Fx.Off)
            {
                Graphics.Blit(src, dst);
                return;
            }
            bool high = Settings.Effects == Settings.Fx.High;
            int levels = high ? 5 : 3;
            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            m.SetFloat("_Threshold", 0.8f);
            chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, chain[0], m, 0);
            int used = 1;
            for (; used < levels; used++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                chain[used] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(chain[used - 1], chain[used], m, 1);
            }
            for (int i = used - 2; i >= 0; i--) Graphics.Blit(chain[i + 1], chain[i], m, 2);

            m.SetTexture("_BloomTex", chain[0]);
            // the site's look: clean, just a gentle glow on bright bits and a light vignette
            m.SetFloat("_Bloom", high ? 0.35f : 0.22f);
            m.SetFloat("_Chroma", ChromaBase);
            m.SetFloat("_Vignette", 0.25f);
            m.SetFloat("_Grain", 0);
            m.SetFloat("_Scan", 0);
            m.SetFloat("_Seed", Time.unscaledTime % 97f);
            m.SetFloat("_Desat", Desaturate);
            m.SetFloat("_Flash", flash * 0.55f);
            m.SetFloat("_Glitch", 0);
            m.SetColor("_FlashColor", flashColor);
            Graphics.Blit(src, dst, m, 3);
            for (int i = 0; i < used; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        }
    }
}
