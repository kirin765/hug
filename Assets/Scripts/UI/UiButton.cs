using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hug
{
    /// <summary>
    /// Pointer-driven button. The HUD is built in code and never adds an
    /// EventSystem, so this polls mouse/touch devices directly.
    ///
    /// Devices are read explicitly instead of through Pointer.current: on WebGL
    /// the browser also exposes a Touchscreen, and Pointer.current can resolve
    /// to it, which made mouse clicks land on the wrong device.
    /// </summary>
    public class UiButton : MonoBehaviour
    {
        public Action Clicked;
        public Image Background;

        public Color NormalColor = Color.white;
        public Color HoverColor = Color.white;
        public Color PressColor = Color.white;
        public float HoverScale = 1.04f;
        public float PressScale = 0.95f;

        RectTransform _rect;
        bool _pointerDown;
        float _scale = 1f;

        void Awake()
        {
            _rect = (RectTransform)transform;
        }

        void Update()
        {
            PointerInput.Refresh();

            bool over = PointerInput.Available &&
                        RectTransformUtility.RectangleContainsScreenPoint(_rect, PointerInput.Position, null);

            if (over && PointerInput.Pressed)
            {
                _pointerDown = true;
                Sound.Play(Sound.Kind.Click);
            }

            if (_pointerDown && PointerInput.Released)
            {
                _pointerDown = false;
                if (over) Clicked?.Invoke();
            }
            else if (_pointerDown && !PointerInput.Down && !PointerInput.Pressed)
            {
                // Released outside, or the release was missed.
                _pointerDown = false;
            }

            if (Background != null)
            {
                Background.color = _pointerDown && over ? PressColor : (over ? HoverColor : NormalColor);
            }

            float targetScale = _pointerDown && over ? PressScale : (over ? HoverScale : 1f);
            _scale = Mathf.Lerp(_scale, targetScale, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }
}
