// Colours as packed RGBA (0xAABBGGRR), as the website's palette is written.
// Part of Flappy Crix (Unity), from Flappy Crix for Gorilla Tag - made with AI (Claude by Anthropic).
using System;

namespace FlappyCrix.Native
{
    public static class Col
    {
        public static uint Hex(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16), g = Convert.ToByte(hex.Substring(2, 2), 16), b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Rgb(r, g, b);
        }
        public static uint Rgb(int r, int g, int b) => (uint)(Clamp(r) | (Clamp(g) << 8) | (Clamp(b) << 16) | (255u << 24));
        public static int R(uint c) => (int)(c & 0xFF);
        public static int G(uint c) => (int)((c >> 8) & 0xFF);
        public static int B(uint c) => (int)((c >> 16) & 0xFF);
        static uint Clamp(int v) => (uint)(v < 0 ? 0 : v > 255 ? 255 : v);
    }
}
