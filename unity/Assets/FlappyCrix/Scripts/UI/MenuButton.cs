// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    /// <summary>The big menu button: when it's the one you're on it lifts, slides a little and gets the site's green edge.</summary>
    public sealed class MenuButton : Button
    {
        public Image Background, Edge;
        public RectTransform Body, Content;
        public Text Label, Sub;
        public Color EdgeColour = new Color(1, 1, 1, 0.16f), LabelColour = Color.white;
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
            on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * 8);
            float k = Mathf.SmoothStep(0, 1, on);
            if (Body != null) Body.anchoredPosition = new Vector2(0, k * 3);
            if (Content != null) Content.anchoredPosition = new Vector2(k * 14, 0);
            if (Edge != null) Edge.color = Color.Lerp(EdgeColour, Ui.Green, k);
            if (Label != null) Label.color = Locked ? Ui.Dim : LabelColour;
        }
    }
}
