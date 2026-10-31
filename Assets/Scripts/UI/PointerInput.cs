using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hug
{
    /// <summary>
    /// One frame's worth of pointer state in Unity screen coordinates.
    ///
    /// In WebGL the browser's own pointer events are the source of truth (Unity's
    /// Input System mouse device is unreliable in some browsers and can be
    /// shadowed by a phantom touchscreen). Everywhere else Mouse.current is used.
    /// Values are refreshed once per frame, on first access, so script execution
    /// order never matters.
    /// </summary>
    public static class PointerInput
    {
        public static Vector2 Position { get; private set; }
        public static Vector2 Delta { get; private set; }
        public static bool Down { get; private set; }
        public static bool Pressed { get; private set; }
        public static bool Released { get; private set; }
        public static bool Available { get; private set; }

        static int _frame = -1;
        static bool _initialised;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HugPointer_Init();
        [DllImport("__Internal")] static extern float HugPointer_Nx();
        [DllImport("__Internal")] static extern float HugPointer_Ny();
        [DllImport("__Internal")] static extern float HugPointer_Dx();
        [DllImport("__Internal")] static extern float HugPointer_Dy();
        [DllImport("__Internal")] static extern int HugPointer_Down();
        [DllImport("__Internal")] static extern int HugPointer_Pressed();
        [DllImport("__Internal")] static extern int HugPointer_Released();
        [DllImport("__Internal")] static extern void HugPointer_ClearEdges();
#endif

        public static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_initialised)
            {
                _initialised = true;
                HugPointer_Init();
            }

            float nx = HugPointer_Nx();
            float ny = HugPointer_Ny();
            Position = new Vector2(nx * Screen.width, (1f - ny) * Screen.height);
            Delta = new Vector2(HugPointer_Dx() * Screen.width, -HugPointer_Dy() * Screen.height);
            Down = HugPointer_Down() != 0;
            Pressed = HugPointer_Pressed() != 0;
            Released = HugPointer_Released() != 0;
            Available = true;
            HugPointer_ClearEdges();
#else
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Position = mouse.position.ReadValue();
                Delta = mouse.delta.ReadValue();
                Down = mouse.leftButton.isPressed;
                Pressed = mouse.leftButton.wasPressedThisFrame;
                Released = mouse.leftButton.wasReleasedThisFrame;
                Available = true;
            }
            else
            {
                var touch = Touchscreen.current;
                if (touch != null)
                {
                    Position = touch.primaryTouch.position.ReadValue();
                    Down = touch.primaryTouch.press.isPressed;
                    Pressed = touch.primaryTouch.press.wasPressedThisFrame;
                    Released = touch.primaryTouch.press.wasReleasedThisFrame;
                    Available = true;
                }
            }
#endif
        }
    }
}
