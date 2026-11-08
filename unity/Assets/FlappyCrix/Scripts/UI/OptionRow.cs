// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyCrix
{
    /// <summary>A settings row: left / right (keys, gamepad, or the arrows) changes the value.</summary>
    public sealed class OptionRow : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public Image Background;
        public Text Label, Value, Left, Right;
        public Func<string> ValueOf;
        public Action<int> Change;
        float on, target;

        public void Refresh() { if (Value != null && ValueOf != null) Value.text = ValueOf(); }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            bool hot = state != SelectionState.Normal && state != SelectionState.Disabled;
            if (hot && target < 1 && Application.isPlaying && Res.Loaded) Sfx.Play("hover", 0.3f);
            target = hot ? 1 : 0;
            if (instant) on = target;
        }

        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); Select(); }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { Step(-1); return; }
            if (e.moveDir == MoveDirection.Right) { Step(1); return; }
            base.OnMove(e);
        }

        public void OnPointerClick(PointerEventData e)
        {
            // the "<" arrow goes back, anywhere else goes forward
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out local);
            var r = ((RectTransform)transform).rect;
            bool back = local.x >= r.xMax - 450 && local.x <= r.xMax - 360;
            Step(back ? -1 : 1);
        }

        public void OnSubmit(BaseEventData e) { Step(1); }

        void Step(int d)
        {
            if (Change == null) return;
            Change(d);
            Sfx.Play("click", 0.6f);
            Refresh();
        }

        void Update()
        {
            on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * 7);
            if (Background != null)
                Background.color = Color.Lerp(new Color(1, 1, 1, 0.05f), new Color(Ui.Red.r, Ui.Red.g, Ui.Red.b, 0.55f), on);
        }
    }
}
