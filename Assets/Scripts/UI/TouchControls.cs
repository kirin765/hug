using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hug
{
    /// <summary>On-screen stick + hug/dash buttons for phones and tablets.</summary>
    public class TouchControls : MonoBehaviour
    {
        public Vector2 Stick { get; private set; }
        public bool HugQueued { get; private set; }
        public bool DashQueued { get; private set; }
        public bool Visible { get; private set; }

        RectTransform _root;
        RectTransform _knob;
        RectTransform _hugButton;
        Image _hugImage;
        RectTransform _hugInner;
        RectTransform _dashButton;
        Image _dashImage;
        RectTransform _dashInner;
        Text _dashLabel;

        int _stickTouch = -1;
        Vector2 _stickOrigin;
        float _stickRadius = 68f;
        float _dashCharge = 1f;

        public void Build(RectTransform parent)
        {
            _root = UiKit.NewRect("TouchControls", parent);
            UiKit.Stretch(_root);
            _root.SetAsLastSibling();

            // stick
            var baseRt = UiKit.NewRect("Stick", _root);
            UiKit.Place(baseRt, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(170f, 170f), new Vector2(220f, 220f));
            UiKit.AddImage(baseRt, UiKit.CircleSprite, new Color(1f, 1f, 1f, 0.12f), false);

            _knob = UiKit.NewRect("Knob", baseRt);
            UiKit.Place(_knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            UiKit.AddImage(_knob, UiKit.CircleSprite, new Color(1f, 1f, 1f, 0.55f), false);

            // dash button
            _dashButton = UiKit.NewRect("DashButton", _root);
            UiKit.Place(_dashButton, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-316f, 276f), new Vector2(128f, 128f));
            _dashImage = UiKit.AddImage(_dashButton, UiKit.CircleSprite, new Color(0.42f, 0.86f, 1f, 0.85f), false);

            _dashInner = UiKit.NewRect("Arrow", _dashButton);
            UiKit.Place(_dashInner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(64f, 64f));
            var arrow = UiKit.Label("Glyph", _dashInner, "»", 54, Color.white);
            UiKit.Stretch((RectTransform)arrow.transform);

            _dashLabel = UiKit.Label("Label", _dashButton, "DASH", 18, new Color(1f, 1f, 1f, 0.95f));
            UiKit.Place((RectTransform)_dashLabel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(110f, 22f));

            // hug button
            _hugButton = UiKit.NewRect("HugButton", _root);
            UiKit.Place(_hugButton, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-160f, 160f), new Vector2(180f, 180f));
            _hugImage = UiKit.AddImage(_hugButton, UiKit.CircleSprite, new Color(1f, 0.42f, 0.62f, 0.85f), false);

            _hugInner = UiKit.NewRect("Heart", _hugButton);
            UiKit.Place(_hugInner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(86f, 78f));
            UiKit.AddImage(_hugInner, UiKit.HeartSprite, Color.white, false);

            var label = UiKit.Label("Label", _hugButton, "HUG", 20, new Color(1f, 1f, 1f, 0.9f));
            UiKit.Place((RectTransform)label.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 24f));

            Visible = PlatformInfo.TouchPrimary;
            _root.gameObject.SetActive(Visible);
        }

        void Update()
        {
            HugQueued = false;
            DashQueued = false;

            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                Stick = Vector2.zero;
                return;
            }

            // A mouse-first device can still have a touchscreen: reveal the
            // controls the moment someone actually touches the screen.
            if (!Visible)
            {
                if (!touchscreen.primaryTouch.press.isPressed) return;
                Visible = true;
                _root.gameObject.SetActive(true);
            }

            bool stickActive = false;
            bool hugDown = false;
            bool dashDown = false;

            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                int id = touch.touchId.ReadValue();
                Vector2 pos = touch.position.ReadValue();

                if (!touch.press.isPressed)
                {
                    if (id == _stickTouch) _stickTouch = -1;
                    continue;
                }

                if (id == _stickTouch)
                {
                    stickActive = true;
                    Vector2 delta = (pos - _stickOrigin) / _stickRadius;
                    Stick = Vector2.ClampMagnitude(delta, 1f);
                }
                else if (_stickTouch < 0 && touch.press.wasPressedThisFrame && pos.x < Screen.width * 0.5f)
                {
                    _stickTouch = id;
                    _stickOrigin = pos;
                    stickActive = true;
                    Stick = Vector2.zero;
                }
                else if (touch.press.wasPressedThisFrame &&
                         RectTransformUtility.RectangleContainsScreenPoint(_hugButton, pos, null))
                {
                    hugDown = true;
                }
                else if (touch.press.wasPressedThisFrame &&
                         RectTransformUtility.RectangleContainsScreenPoint(_dashButton, pos, null))
                {
                    dashDown = true;
                }
            }

            if (!stickActive)
            {
                Stick = Vector2.zero;
                _stickTouch = -1;
            }

            _knob.anchoredPosition = Stick * _stickRadius;

            // Mouse fallback so the buttons are testable on desktop.
            PointerInput.Refresh();
            if (PointerInput.Available && PointerInput.Pressed)
            {
                Vector2 pos = PointerInput.Position;
                if (RectTransformUtility.RectangleContainsScreenPoint(_hugButton, pos, null)) hugDown = true;
                if (RectTransformUtility.RectangleContainsScreenPoint(_dashButton, pos, null)) dashDown = true;
            }

            if (hugDown)
            {
                HugQueued = true;
                Sound.Play(Sound.Kind.Click);
            }

            if (dashDown && _dashCharge >= 1f)
            {
                DashQueued = true;
                Sound.Play(Sound.Kind.Click);
            }

            float hugTarget = hugDown ? 0.86f : 1f;
            _hugInner.localScale = Vector3.one * Mathf.Lerp(_hugInner.localScale.x, hugTarget, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            _hugImage.color = new Color(1f, 0.42f, 0.62f, hugDown ? 1f : 0.85f);

            bool dashReady = _dashCharge >= 1f;
            float dashTarget = dashDown && dashReady ? 0.86f : 1f;
            _dashInner.localScale = Vector3.one * Mathf.Lerp(_dashInner.localScale.x, dashTarget, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            _dashImage.color = new Color(0.42f, 0.86f, 1f, dashReady ? 0.85f : 0.28f);
            _dashLabel.text = dashReady ? "DASH" : "WAIT";
            _dashLabel.color = new Color(1f, 1f, 1f, dashReady ? 0.95f : 0.45f);
        }

        public void SetDashCharge(float charge)
        {
            _dashCharge = Mathf.Clamp01(charge);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (_root != null) _root.gameObject.SetActive(visible);
        }
    }
}
