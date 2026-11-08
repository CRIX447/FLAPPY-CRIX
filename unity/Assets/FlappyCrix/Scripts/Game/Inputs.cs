// One place for the controls: keyboard, mouse, touch and gamepad.
//   Flap: Space / W / Up / click / tap / A (gamepad)
//   Pause: Esc / P / Start      Back: Esc / B / Android back
// Taps and clicks on buttons never flap.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;
using UnityEngine.EventSystems;

namespace FlappyCrix
{
    public static class Inputs
    {
        static bool OverUi(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId);
        }

        public static bool Flap()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.JoystickButton0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && !OverUi(t.fingerId)) return true;
            }
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0) && !OverUi(-1)) return true;
            return false;
        }

        public static bool Pause()
        {
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7) ||
                   Input.GetKeyDown(KeyCode.JoystickButton9);
        }

        public static bool Back()
        {
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1);
        }

        /// <summary>Anything at all (title screen: "press any button").</summary>
        public static bool Any()
        {
            if (Input.anyKeyDown) return true;
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }

        public static bool ToggleFullscreen()
        {
            return Input.GetKeyDown(KeyCode.F11);
        }
    }
}
