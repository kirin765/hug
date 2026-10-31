using UnityEngine;
using UnityEngine.InputSystem;

namespace Hug
{
    /// <summary>Reads keyboard/mouse/touch once per frame and hands the result over.</summary>
    public class InputController : MonoBehaviour
    {
        public struct State
        {
            public Vector2 Move;
            public bool Hug;
            public bool Dash;
            public Vector2 Look;
            public float Zoom;
        }

        public TouchControls Touch;
        public bool AllowLook = true;

        public State Sample()
        {
            var state = new State();

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = 0f;
                float y = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                state.Move = new Vector2(x, y);
                state.Hug = keyboard.eKey.wasPressedThisFrame;
                state.Dash = keyboard.spaceKey.wasPressedThisFrame ||
                            keyboard.leftShiftKey.wasPressedThisFrame ||
                            keyboard.rightShiftKey.wasPressedThisFrame;
            }

            if (Touch != null)
            {
                if (Touch.Stick.sqrMagnitude > 0.01f) state.Move = Touch.Stick;
                state.Hug |= Touch.HugQueued;
                state.Dash |= Touch.DashQueued;
            }

            PointerInput.Refresh();
            if (AllowLook && PointerInput.Available && PointerInput.Down)
            {
                state.Look = PointerInput.Delta;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                state.Zoom = mouse.scroll.ReadValue().y;
            }

            return state;
        }
    }
}
