// The bird's hat and trail (from the Locker), and the other players' birds in a match, each with
// their colour and name (where the website would draw them).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System.Collections.Generic;
using FlappyCrix.Native;
using UnityEngine;

namespace FlappyCrix
{
    public sealed partial class World
    {
        string shownHat = "-", shownTrail = "-";
        SpriteRenderer hat;
        Transform hatPivot;

        // hats the website draws as shapes get the nearest emoji here
        static readonly Dictionary<string, string> ShapeEmoji = new Dictionary<string, string>
        {
            { "witch", "1f383" }, { "skull", "1f480" }, { "pumpkin", "1f383" }, { "reindeer", "1f384" },
            { "santa", "1f384" }, { "bunny", "1f430" }, { "party", "1f973" },
        };

        void Dress(float t)
        {
            var save = SaveRef != null ? SaveRef() : null;
            string hatId = save != null ? save.Hat : "", trailId = save != null ? save.Trail : "";
            if (hatId != shownHat)
            {
                shownHat = hatId;
                var c = Catalog.CosmeticById(hatId);
                bird.sprite = Res.Get(c != null ? "bird-bare" : "bird") ?? Res.Get("bird");
                if (hatPivot == null)
                {
                    hatPivot = new GameObject("HatPivot").transform;
                    hatPivot.SetParent(bird.transform, false);
                    hatPivot.localPosition = new Vector3(0.01f, 0.318f, 0);   // the top of the head (the site's brim point)
                    hat = Child(hatPivot, "Hat", null, 51);
                }
                Sprite img = null; float w = 0.6f, fitY = 0.1f;
                if (c != null && c.Image != null) { img = Res.Get(c.Image); w = c.FitW; fitY = c.FitY; }
                else if (c != null && c.Shape != null && ShapeEmoji.ContainsKey(c.Shape)) { img = Res.Emoji(ShapeEmoji[c.Shape]); w = 0.62f; fitY = 0.12f; }
                hat.sprite = img;
                hat.enabled = img != null;
                if (img != null)
                {
                    float h = w * img.bounds.size.y / img.bounds.size.x;
                    hat.transform.localScale = Vector3.one * (w / img.bounds.size.x);
                    hat.transform.localPosition = new Vector3(0, -fitY + h / 2, 0);
                }
            }
            if (hatPivot != null) hatPivot.localRotation = Quaternion.Euler(0, 0, -Sim.HatAngle * 1.1f * Mathf.Rad2Deg);

            if (trailId != shownTrail)
            {
                shownTrail = trailId;
                var c = Catalog.CosmeticById(trailId);
                var g = new Gradient();
                if (c != null && c.Colours != null && c.Colours.Length > 0)
                {
                    int n = Mathf.Min(8, c.Colours.Length);
                    var keys = new GradientColorKey[n];
                    for (int i = 0; i < n; i++) keys[i] = new GradientColorKey(Res.C(c.Colours[n - 1 - i]), n == 1 ? 0 : i / (float)(n - 1));
                    g.SetKeys(keys, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 1) });
                    trail.widthCurve = new AnimationCurve(new Keyframe(0, 0f), new Keyframe(1, 0.22f));
                }
                else
                {
                    g.SetKeys(new[] { new GradientColorKey(accent, 0), new GradientColorKey(Color.Lerp(accent, Color.yellow, 0.5f), 1) },
                              new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.75f, 1) });
                    trail.widthCurve = new AnimationCurve(new Keyframe(0, 0f), new Keyframe(1, 0.16f));
                }
                trail.colorGradient = g;
            }
        }

        /// <summary>A multiplayer match began (the match already started the run from its shared seed).</summary>
        public void EnterMatch()
        {
            Attract = false;
            Sim.Immortal = false;
            Frozen = false;
            SimSpeed = 1;
            acc = 0;
            ClearViews();
        }

        // ------------------------------------------------------------------ other players

        sealed class RemoteView { public Transform Root, Bird; public SpriteRenderer Body, Glow; public TextMesh Name; }
        readonly List<RemoteView> remotes = new List<RemoteView>();

        void DrawRemotes()
        {
            int ri = 0;
            if (Mp != null && Mp.MatchActive)
            {
                foreach (var p in Mp.Players.Values)
                {
                    if (!Mp.Visible(p)) continue;
                    var v = RemoteAt(ri++);
                    v.Root.localPosition = new Vector3(Mp.ScreenX(p) * U, (NativeSim.H - p.DrawY) * U, -0.01f);
                    Color col = Res.Hex(p.Colour);
                    float a = p.Dead ? 0.25f : 0.8f;
                    v.Body.color = new Color(1, 1, 1, a);
                    v.Glow.color = new Color(col.r, col.g, col.b, a * 0.55f);
                    v.Bird.localRotation = Quaternion.Euler(0, 0, -Mathf.Clamp(p.SimVel * 0.035f, -0.3f, 0.4f) * Mathf.Rad2Deg);
                    v.Name.text = p.Name;
                    v.Name.color = new Color(col.r, col.g, col.b, p.Dead ? 0.4f : 1);
                }
            }
            for (int i = ri; i < remotes.Count; i++) if (remotes[i].Root.gameObject.activeSelf) remotes[i].Root.gameObject.SetActive(false);
        }

        RemoteView RemoteAt(int i)
        {
            while (remotes.Count <= i)
            {
                var v = new RemoteView { Root = new GameObject("Player").transform };
                v.Root.SetParent(root, false);
                v.Glow = Child(v.Root, "Glow", Res.Glow, 47, true);
                v.Glow.transform.localScale = Vector3.one * 0.8f;
                v.Body = Child(v.Root, "Bird", Res.Get("bird"), 48);
                v.Bird = v.Body.transform;
                if (v.Body.sprite != null) v.Bird.localScale = Vector3.one * (BirdDraw * U / v.Body.sprite.bounds.size.y);
                var tgo = new GameObject("Name");
                tgo.transform.SetParent(v.Root, false);
                tgo.transform.localPosition = new Vector3(0, 0.36f, 0);
                v.Name = tgo.AddComponent<TextMesh>();
                v.Name.font = Res.Font;
                v.Name.fontSize = 48;
                v.Name.characterSize = 0.035f;
                v.Name.anchor = TextAnchor.MiddleCenter;
                v.Name.alignment = TextAlignment.Center;
                var r = tgo.GetComponent<MeshRenderer>();
                if (Res.Font != null) r.sharedMaterial = Res.Font.material;
                r.sortingOrder = 70;
                remotes.Add(v);
            }
            var rv = remotes[i];
            if (!rv.Root.gameObject.activeSelf) rv.Root.gameObject.SetActive(true);
            return rv;
        }
    }
}
