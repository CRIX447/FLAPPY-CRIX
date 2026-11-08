// Colours text left to right (the site's red-to-orange FLAPPY CRIX title).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class GradientText : BaseMeshEffect
    {
        public Color Left = new Color32(0xFF, 0x46, 0x55, 255), Right = new Color32(0xFF, 0x6B, 0x35, 255);
        readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts) { min = Mathf.Min(min, v.position.x); max = Mathf.Max(max, v.position.x); }
            float w = Mathf.Max(1, max - min);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                Color c = Color.Lerp(Left, Right, (v.position.x - min) / w);
                c.a *= v.color.a / 255f;
                v.color = c;
                verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
