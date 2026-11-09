// Music and sound effects, with the volumes from Settings. Sounds that restart instead of
// stacking (as on the website) get their own source.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System.Collections.Generic;
using UnityEngine;

namespace FlappyCrix
{
    public sealed class Sfx : MonoBehaviour
    {
        static Sfx inst;
        static readonly HashSet<string> Mono = new HashSet<string> { "jump", "coin", "death", "select", "hover", "error", "click" };

        AudioSource music, oneShots;
        readonly Dictionary<string, AudioSource> mono = new Dictionary<string, AudioSource>();
        string wantTrack;
        float duck = 1, duckTarget = 1, fade = 1;

        public static void Create(GameObject host)
        {
            inst = host.AddComponent<Sfx>();
            inst.music = host.AddComponent<AudioSource>();
            inst.music.loop = true; inst.music.playOnAwake = false; inst.music.spatialBlend = 0; inst.music.priority = 0;
            inst.oneShots = host.AddComponent<AudioSource>();
            inst.oneShots.playOnAwake = false; inst.oneShots.spatialBlend = 0;
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (inst == null) return;
            var clip = Res.Clip(name);
            if (clip == null) return;
            float v = volume * Settings.Sfx;
            if (v <= 0.001f) return;
            if (Mono.Contains(name))
            {
                AudioSource s;
                if (!inst.mono.TryGetValue(name, out s))
                {
                    s = inst.gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false; s.spatialBlend = 0; s.clip = clip;
                    inst.mono[name] = s;
                }
                s.Stop(); s.volume = v; s.pitch = pitch; s.Play();
            }
            else
            {
                inst.oneShots.pitch = pitch;
                inst.oneShots.PlayOneShot(clip, v);
            }
        }

        /// <summary>Which track should play (null = silence). It changes with a short fade.</summary>
        public static void Music(string track) { if (inst != null) inst.wantTrack = track; }

        /// <summary>Starts the current track again from the top (a new run, as on the site).</summary>
        public static void RestartMusic() { if (inst != null && inst.music.clip != null) { inst.music.Stop(); inst.music.Play(); } }

        /// <summary>Music quieter (pause, game over) or back to normal.</summary>
        public static void Duck(bool on) { if (inst != null) inst.duckTarget = on ? 0.35f : 1; }

        void Update()
        {
            var want = Res.Clip(wantTrack);
            if (want != music.clip)
            {
                fade = Mathf.MoveTowards(fade, 0, Time.unscaledDeltaTime * 3);
                if (fade <= 0 || music.clip == null)
                {
                    music.clip = want;
                    if (want != null) music.Play();
                }
            }
            else fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime * 1.5f);
            duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * 2);
            music.volume = Settings.Music * 0.7f * duck * fade;
            if (music.clip != null && !music.isPlaying && want == music.clip && music.volume > 0) music.Play();
        }
    }
}
