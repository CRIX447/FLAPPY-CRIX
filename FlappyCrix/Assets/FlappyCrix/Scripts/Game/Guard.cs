// Anti-cheat, part 2: watches for the common ways people cheat in a Unity game on a computer.
//   - Speed hacks (Cheat Engine's speedhack and similar): the game's clock is compared with the
//     computer's real clock every few seconds; a game running much faster than real time is flagged.
//   - Mod loaders (BepInEx, MelonLoader, Unity Doorstop) installed next to the game.
//   - Values edited in memory and edited save files are caught by Guarded.cs.
// Once something is flagged, that session can't play online or save to the cloud (your local save
// is kept). Restarting the game without the cheat clears it.
// This stops casual cheating. The final say is always the server's: the website checks scores
// and bans (api/anticheat.js), which a modified game can't skip.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.IO;
using FlappyCrix.Native;
using UnityEngine;

namespace FlappyCrix
{
    public sealed class Guard : MonoBehaviour
    {
        double realStart, gameStart;
        int strikes;
        const float CheckEvery = 3f;

        void Start()
        {
            Mark();
            InvokeRepeating(nameof(CheckClock), CheckEvery, CheckEvery);
            CheckModLoaders();
        }

        void Mark()
        {
            realStart = (DateTime.UtcNow - DateTime.MinValue).TotalSeconds;
            gameStart = Time.realtimeSinceStartupAsDouble;
        }

        void CheckClock()
        {
            double real = (DateTime.UtcNow - DateTime.MinValue).TotalSeconds - realStart;
            double game = Time.realtimeSinceStartupAsDouble - gameStart;
            Mark();
            // the wall clock can jump (sleep, clock sync); only a steady, large speed-up counts
            if (real <= 0.5 || real > CheckEvery * 4) { strikes = 0; return; }
            double ratio = game / real;
            if (ratio > 1.25) { if (++strikes >= 3) GuardState.Flag("the game was running faster than real time"); }
            else strikes = 0;
        }

        static void CheckModLoaders()
        {
            if (!Settings.IsDesktop || Application.isEditor) return;
            try
            {
                string dir = Path.GetDirectoryName(Application.dataPath);
                foreach (var name in new[] { "BepInEx", "MelonLoader", "doorstop_config.ini", "winhttp.dll", "version.dll", "doorstop_libs" })
                    if (Directory.Exists(Path.Combine(dir, name)) || File.Exists(Path.Combine(dir, name)))
                    { GuardState.Flag("a mod loader is installed (" + name + ")"); return; }
            }
            catch (Exception) { }
        }
    }
}
