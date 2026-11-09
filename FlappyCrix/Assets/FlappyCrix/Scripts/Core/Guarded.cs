// Anti-cheat, part 1 (no UnityEngine references): values that memory scanners such as Cheat Engine
// can't find or change. The real number is never stored as itself: it's mixed with a key that
// changes every time it's written, plus a check value. Editing the memory breaks the check, which
// is noticed the next time the game reads it.
// Also: the save file is signed, so editing coins=... in Notepad is noticed too.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FlappyCrix.Native
{
    /// <summary>What the anti-cheat has noticed this session. Once set it stays set until the game restarts.</summary>
    public static class GuardState
    {
        public static volatile bool Tampered;
        public static string Why = "";
        public static event Action<string> Flagged;

        public static void Flag(string why)
        {
            if (Tampered) return;
            Tampered = true; Why = why ?? "";
            Flagged?.Invoke(Why);
        }
    }

    public struct GuardedLong
    {
        static long counter = DateTime.UtcNow.Ticks;
        static readonly long Salt = Mix(Environment.TickCount ^ DateTime.UtcNow.Ticks ^ 0x5DEECE66DL);
        long key, masked, check;

        static long Mix(long z)
        {
            unchecked
            {
                ulong x = (ulong)z + 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return (long)(x ^ (x >> 31));
            }
        }

        public long Value
        {
            get
            {
                if (key == 0 && masked == 0 && check == 0) return 0;      // never written
                long v = masked ^ key;
                if (unchecked(Mix(v ^ Salt) ^ key) != check) GuardState.Flag("a value was changed in memory");
                return v;
            }
            set
            {
                long k = Mix(Interlocked.Increment(ref counter) ^ Salt);
                if (k == 0) k = 1;
                key = k; masked = value ^ k; check = unchecked(Mix(value ^ Salt) ^ k);
            }
        }

        public static implicit operator long(GuardedLong g) => g.Value;
        public static implicit operator GuardedLong(long v) { var g = new GuardedLong(); g.Value = v; return g; }
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Signs the save file. Not unbreakable (the key is in the game), but stops casual editing.</summary>
    public static class SaveSeal
    {
        static readonly byte[] Key = Encoding.UTF8.GetBytes("FlappyCrix/CRIX STUDIOS/save-seal/v1/" + "f7c2a9e14b");

        public static string Sign(string body)
        {
            using (var h = new HMACSHA256(Key))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(body.Replace("\r", ""))));
        }
    }
}
