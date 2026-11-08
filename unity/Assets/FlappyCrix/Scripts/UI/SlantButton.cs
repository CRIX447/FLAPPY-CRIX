// A small slanted button that brightens and grows a little when selected (mouse, keys, gamepad, touch).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class SlantButton : Button
    {
        public Image Background;
        public Color Base = Color.white;
        float on, target;
        bool disabled;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            disabled = state == SelectionState.Disabled;
            target = state != SelectionState.Normal && !disabled ? 1 : 0;
            if (instant) on = target;
        }

        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); if (IsInteractable()) Select(); }

        void Update()
        {
            on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * 8);
            if (Background != null)
            {
                Color c = disabled ? new Color(Base.r * 0.4f, Base.g * 0.4f, Base.b * 0.45f, Base.a * 0.6f) : Color.Lerp(Base, Color.Lerp(Base, Color.white, 0.35f), on);
                Background.color = c;
            }
            float s = 1 + on * 0.06f;
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
