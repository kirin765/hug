using UnityEngine;
using UnityEngine.UI;

namespace Hug
{
    /// <summary>Small uGUI helpers so the HUD can be built from code.</summary>
    public static class UiKit
    {
        public static readonly Color Panel = new Color(0.08f, 0.05f, 0.14f, 0.74f);
        public static readonly Color PanelSolid = new Color(0.11f, 0.07f, 0.19f, 0.92f);
        public static readonly Color Pink = new Color(1f, 0.42f, 0.62f);
        public static readonly Color PinkSoft = new Color(1f, 0.55f, 0.72f);
        public static readonly Color Text = new Color(1f, 0.98f, 1f);
        public static readonly Color Muted = new Color(0.78f, 0.74f, 0.88f);
        public static readonly Color Dark = new Color(0.13f, 0.06f, 0.17f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.42f);

        static Sprite _panelSprite;
        static Sprite _circleSprite;
        static Sprite _heartSprite;

        public static Sprite PanelSprite => _panelSprite != null ? _panelSprite : (_panelSprite = Art.RoundedSprite(26));
        public static Sprite CircleSprite => _circleSprite != null ? _circleSprite : (_circleSprite = Art.TextureSprite(Art.Circle(96, 1.5f)));
        public static Sprite HeartSprite => _heartSprite != null ? _heartSprite : (_heartSprite = Art.TextureSprite(Art.Heart(96)));

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool sliced = true)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced && sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        public static Image PanelImage(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, int radius = 26)
        {
            var rt = NewRect(name, parent);
            Place(rt, anchor, pivot, position, size);
            var img = AddImage(rt, PanelSprite, color);
            img.pixelsPerUnitMultiplier = 26f / Mathf.Max(1f, radius);
            return img;
        }

        public static Text Label(string name, Transform parent, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Art.Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        /// <summary>Text with a soft drop shadow, for headings over the 3D scene.</summary>
        public static Text LabelShadow(string name, Transform parent, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = Label(name, parent, content, size, color, anchor);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.02f, 0.1f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        public static Image Icon(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = NewRect(name, parent);
            Place(rt, anchor, pivot, position, size);
            var img = AddImage(rt, sprite, color, false);
            img.preserveAspect = true;
            return img;
        }

        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
