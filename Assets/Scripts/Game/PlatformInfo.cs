using System.Runtime.InteropServices;
using UnityEngine;

namespace Hug
{
    /// <summary>Small platform probes that behave the same in editor and WebGL.</summary>
    public static class PlatformInfo
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int HugIsCoarsePointer();
#endif

        /// <summary>True when the primary pointer is a finger (phone / tablet).</summary>
        public static bool TouchPrimary
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return HugIsCoarsePointer() != 0;
#else
                return Application.isMobilePlatform && UnityEngine.InputSystem.Touchscreen.current != null;
#endif
            }
        }
    }
}
