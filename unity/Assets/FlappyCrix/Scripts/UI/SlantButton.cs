// A small slanted button that brightens and grows a little when selected (mouse, keys, gamepad, touch).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    public sealed class SlantButton : Button
    {
        public Image Background, Edge, Icon;
        public Text Text;
        public Color Base = Color.white, EdgeBase = new Color(1, 1, 1, 0.15f);
        public float Grow = 0.04f;
        float on, target;
        bool disabled;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            disabled = state == SelectionState.Disabled;
            target = state != SelectionState.Normal && !disabled ? 1 : 0;
            if (instant) on = target;
        }

        public override void OnPointerEnter(PointerEventData e)
        {
            base.OnPointerEnter(e);
            if (IsInteractable()) { if (target < 1 && Res.Loaded) Sfx.Play("hover", 0.25f); Select(); }
        }

        void Update()
        {
            on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * 8);
            if (Background != null)
            {
                Color c = disabled ? new Color(Base.r * 0.4f, Base.g * 0.4f, Base.b * 0.45f, Base.a * 0.6f) : Color.Lerp(Base, Color.Lerp(Base, Color.white, 0.35f), on);
                Background.color = c;
            }
            if (Edge != null) Edge.color = Color.Lerp(EdgeBase, Ui.Green, disabled ? 0 : on);
            float s = 1 + on * Grow;
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
