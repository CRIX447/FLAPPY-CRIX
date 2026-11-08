// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    /// <summary>The slanted menu button: slides right and lights up when selected.</summary>
    public sealed class MenuButton : Button
    {
        public Image Background, Bar;
        public RectTransform Content;
        public Text Label, Sub;
        public bool Locked;
        float on, target;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            bool hot = state != SelectionState.Normal && state != SelectionState.Disabled;
            if (hot && target < 1 && Application.isPlaying && Res.Loaded) Sfx.Play("hover", 0.35f);
            target = hot ? 1 : 0;
            if (instant) on = target;
        }

        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); if (IsInteractable()) Select(); }

        void Update()
        {
            on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * 7);
            float k = Mathf.SmoothStep(0, 1, on);
            if (Content != null) Content.anchoredPosition = new Vector2(k * 22, 0);
            if (Background != null)
                Background.color = Color.Lerp(new Color(1, 1, 1, 0.06f), new Color(Ui.Red.r, Ui.Red.g, Ui.Red.b, 0.85f), k);
            if (Bar != null) Bar.color = Color.Lerp(Ui.Red, Color.white, k);
            if (Label != null) Label.color = Locked ? Ui.Dim : Color.white;
            if (Sub != null) Sub.color = Color.Lerp(Ui.Dim, new Color(1, 1, 1, 0.85f), k);
        }
    }
}
