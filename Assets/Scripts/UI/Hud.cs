using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hug
{
    /// <summary>All 2D UI: score, leaderboard, hug prompt, toasts and the name card.</summary>
    public class Hud : MonoBehaviour
    {
        public struct Entry
        {
            public string Name;
            public int Score;
            public Color Color;
            public bool IsMe;
        }

        public event Action<string> PlayRequested;
        public event Action RetryRequested;

        Canvas _canvas;
        RectTransform _root;

        RectTransform _scoreCard;
        Text _scoreValue;
        Text _hint;
        Text _ping;

        RectTransform _board;
        readonly List<Row> _rows = new List<Row>();

        RectTransform _prompt;
        Text _promptText;

        Text _toast;
        float _toastUntil;
        Color _toastColor;

        RectTransform _entry;
        CanvasGroup _entryGroup;
        Text _entryValue;
        Text _entryPlaceholder;
        RectTransform _caret;
        Text _entryStatus;
        UiButton _playButton;
        Text _playLabel;
        string _typed = "";
        bool _entryOpen;

        public bool EntryOpen => _entryOpen;
        public string TypedName => _typed;
        public TouchControls Touch { get; private set; }

        class Row
        {
            public RectTransform Root;
            public Image Background;
            public Text Rank;
            public Text Name;
            public Text Score;
        }

        static readonly string[] DefaultNames =
        {
            "Sunny", "Bubbles", "Mochi", "Peach", "Cuddles", "Puff", "Noodle", "Sprout",
        };

        // ---------------------------------------------------------------- build

        public void Build()
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _root = canvasGo.GetComponent<RectTransform>();

            BuildScoreCard();
            BuildLeaderboard();
            BuildPrompt();
            BuildHint();
            BuildToast();
            BuildPing();
            BuildTouch();
            BuildEntry();
        }

        void BuildTouch()
        {
            var host = UiKit.NewRect("TouchHost", _root);
            UiKit.Stretch(host);
            Touch = host.gameObject.AddComponent<TouchControls>();
            Touch.Build(host);
        }

        void BuildScoreCard()
        {
            var card = UiKit.PanelImage("ScoreCard", _root, UiKit.Panel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(300f, 112f));
            _scoreCard = card.rectTransform;

            UiKit.Icon("Heart", card.transform, UiKit.HeartSprite, UiKit.Pink,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -24f), new Vector2(54f, 50f));

            _scoreValue = UiKit.Label("Score", card.transform, "0", 56, UiKit.Text, TextAnchor.MiddleLeft);
            UiKit.Place((RectTransform)_scoreValue.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(86f, -12f), new Vector2(190f, 66f));

            var label = UiKit.Label("ScoreLabel", card.transform, "HUGS", 18, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.Place((RectTransform)label.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(88f, -74f), new Vector2(190f, 26f));
        }

        void BuildLeaderboard()
        {
            const int rowCount = 6;
            float height = 54f + rowCount * 38f;

            _board = UiKit.PanelImage("Leaderboard", _root, UiKit.Panel,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(340f, height)).rectTransform;

            var title = UiKit.Label("Title", _board, "TOP HUGGERS", 17, UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -12f), new Vector2(240f, 28f));

            for (int i = 0; i < rowCount; i++)
            {
                var rowRt = UiKit.NewRect("Row" + i, _board);
                UiKit.Place(rowRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -44f - i * 38f), new Vector2(320f, 34f));

                var row = new Row { Root = rowRt };
                row.Background = UiKit.AddImage(rowRt, UiKit.PanelSprite, new Color(1f, 1f, 1f, 0f));
                row.Background.pixelsPerUnitMultiplier = 2.4f;

                row.Rank = UiKit.Label("Rank", rowRt, "", 18, UiKit.Muted, TextAnchor.MiddleCenter);
                UiKit.Place((RectTransform)row.Rank.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(12f, 0f), new Vector2(30f, 30f));

                row.Name = UiKit.Label("Name", rowRt, "", 22, UiKit.Text, TextAnchor.MiddleLeft);
                UiKit.Place((RectTransform)row.Name.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(50f, 0f), new Vector2(190f, 30f));

                row.Score = UiKit.Label("Score", rowRt, "", 22, UiKit.PinkSoft, TextAnchor.MiddleRight);
                UiKit.Place((RectTransform)row.Score.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-14f, 0f), new Vector2(70f, 30f));

                rowRt.gameObject.SetActive(false);
                _rows.Add(row);
            }
        }

        void BuildPrompt()
        {
            _prompt = UiKit.PanelImage("Prompt", _root, new Color(1f, 0.42f, 0.62f, 0.95f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(460f, 56f), 28).rectTransform;

            _promptText = UiKit.Label("Text", _prompt, "Press E to hug", 24, UiKit.Dark);
            UiKit.Stretch((RectTransform)_promptText.transform, 12f, 12f, 4f, 4f);

            _prompt.gameObject.SetActive(false);
        }

        void BuildHint()
        {
            _hint = UiKit.LabelShadow("Hint", _root, "", 19, new Color(1f, 1f, 1f, 0.85f));
            var rt = (RectTransform)_hint.transform;
            UiKit.Place(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 30f));
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
        }

        void BuildToast()
        {
            _toast = UiKit.LabelShadow("Toast", _root, "", 28, Color.white);
            UiKit.Place((RectTransform)_toast.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -104f), new Vector2(900f, 40f));
            UiKit.SetAlpha(_toast, 0f);
        }

        void BuildPing()
        {
            _ping = UiKit.Label("Ping", _root, "", 15, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleRight);
            UiKit.Place((RectTransform)_ping.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-26f, 20f), new Vector2(220f, 24f));
        }

        void BuildEntry()
        {
            _entry = UiKit.NewRect("Entry", _root);
            UiKit.Stretch(_entry);
            UiKit.AddImage(_entry, null, new Color(0.04f, 0.02f, 0.08f, 0.82f), false);
            _entryGroup = _entry.gameObject.AddComponent<CanvasGroup>();

            var card = UiKit.PanelImage("Card", _entry, UiKit.PanelSolid,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 500f), 44);

            var title = UiKit.Label("Title", card.transform, "HUG.IO", 88, UiKit.Pink, TextAnchor.MiddleCenter);
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(600f, 96f));

            var subtitle = UiKit.Label("Subtitle", card.transform,
                "hug everyone. get hugged. climb the board.", 20, UiKit.Muted);
            UiKit.Place((RectTransform)subtitle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -114f), new Vector2(620f, 26f));

            var rule = UiKit.Label("Rule", card.transform,
                "close scores: +1 each   ·   far apart: you swap 10% of your hugs", 15,
                new Color(0.72f, 0.68f, 0.86f, 0.85f));
            UiKit.Place((RectTransform)rule.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -146f), new Vector2(620f, 24f));

            var field = UiKit.PanelImage("NameField", card.transform, new Color(1f, 1f, 1f, 0.08f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(430f, 78f), 22);

            _entryPlaceholder = UiKit.Label("Placeholder", field.transform, "your name", 32,
                new Color(1f, 1f, 1f, 0.28f));
            UiKit.Stretch((RectTransform)_entryPlaceholder.transform, 10f, 10f, 6f, 6f);

            _entryValue = UiKit.Label("Value", field.transform, "", 32, UiKit.Text);
            UiKit.Stretch((RectTransform)_entryValue.transform, 10f, 10f, 6f, 6f);

            var caretRt = UiKit.NewRect("Caret", field.transform);
            UiKit.Place(caretRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(3f, 38f));
            var caretImage = UiKit.AddImage(caretRt, null, UiKit.Pink, false);
            caretImage.raycastTarget = false;
            _caret = caretRt;

            var buttonRt = UiKit.NewRect("Play", card.transform);
            UiKit.Place(buttonRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(290f, 86f));
            var buttonImage = UiKit.AddImage(buttonRt, UiKit.PanelSprite, UiKit.Pink);
            buttonImage.pixelsPerUnitMultiplier = 1.1f;

            _playLabel = UiKit.Label("Label", buttonRt, "PLAY", 38, UiKit.Dark);
            UiKit.Stretch((RectTransform)_playLabel.transform);

            _playButton = buttonRt.gameObject.AddComponent<UiButton>();
            _playButton.Background = buttonImage;
            _playButton.NormalColor = UiKit.Pink;
            _playButton.HoverColor = UiKit.PinkSoft;
            _playButton.PressColor = new Color(0.9f, 0.32f, 0.52f);
            _playButton.Clicked = Submit;

            _entryStatus = UiKit.Label("Status", card.transform, "", 17, UiKit.Muted);
            UiKit.Place((RectTransform)_entryStatus.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -392f), new Vector2(560f, 26f));

            var footer = UiKit.Label("Footer", card.transform,
                "WASD move   ·   E hug   ·   SPACE dash   ·   drag to look", 16,
                new Color(1f, 1f, 1f, 0.35f));
            UiKit.Place((RectTransform)footer.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(620f, 24f));

            _entry.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- entry

        public void SetGameplayVisible(bool visible)
        {
            if (_scoreCard != null) _scoreCard.gameObject.SetActive(visible);
            if (_board != null) _board.gameObject.SetActive(visible);
            if (_hint != null) _hint.gameObject.SetActive(visible);
            if (_ping != null) _ping.gameObject.SetActive(visible);
            if (!visible) SetPrompt(null);
        }

        public void ShowEntry(string prefill, string status = "")
        {
            SetGameplayVisible(false);
            _entryOpen = true;
            _entry.gameObject.SetActive(true);
            _entryGroup.alpha = 1f;
            _typed = prefill ?? "";
            _entryStatus.text = status ?? "";
            _entryStatus.color = UiKit.Muted;
            _playLabel.text = "PLAY";
            _playButton.gameObject.SetActive(true);
            RefreshEntry();
            Sound.Play(Sound.Kind.Pop);

            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        }

        public void HideEntry()
        {
            if (!_entryOpen) return;
            _entryOpen = false;
            SetGameplayVisible(true);
            _entry.gameObject.SetActive(false);
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }

        public void ShowEntryError(string message)
        {
            if (_entryStatus == null) return;
            _entryStatus.text = message;
            _entryStatus.color = new Color(1f, 0.55f, 0.6f);
        }

        public void SetEntryBusy(bool busy)
        {
            if (_playLabel == null) return;
            _playLabel.text = busy ? "..." : "PLAY";
            _playButton.gameObject.SetActive(!busy);
        }

        void OnTextInput(char c)
        {
            if (!_entryOpen) return;

            if (c == '\b')
            {
                if (_typed.Length > 0) _typed = _typed.Substring(0, _typed.Length - 1);
            }
            else if (c == '\n' || c == '\r')
            {
                Submit();
                return;
            }
            else if (c >= ' ' && c != '\u007f' && _typed.Length < 14)
            {
                _typed += c;
            }

            RefreshEntry();
        }

        void RefreshEntry()
        {
            if (_entryValue == null) return;
            _entryValue.text = _typed;
            _entryPlaceholder.gameObject.SetActive(_typed.Length == 0);

            // keep the caret just after the text
            float width = _entryValue.preferredWidth;
            _caret.anchoredPosition = new Vector2(width * 0.5f + 4f, 0f);
            _caret.gameObject.SetActive(true);
        }

        void Submit()
        {
            if (!_entryOpen) return;
            string name = string.IsNullOrWhiteSpace(_typed) ? DefaultNames[UnityEngine.Random.Range(0, DefaultNames.Length)] : _typed.Trim();
            if (name.Length > 14) name = name.Substring(0, 14);
            _typed = name;
            RefreshEntry();
            PlayRequested?.Invoke(name);
        }

        // ----------------------------------------------------------------- hud

        public void SetScore(int score)
        {
            if (_scoreValue != null) _scoreValue.text = score.ToString();
        }

        public void SetHint(string text)
        {
            if (_hint != null) _hint.text = text ?? "";
        }

        public void SetPing(int ms)
        {
            if (_ping == null) return;
            _ping.text = ms >= 0 ? $"{ms} ms" : "";
        }

        public void SetPrompt(string text)
        {
            if (_prompt == null) return;
            bool show = !string.IsNullOrEmpty(text);
            _prompt.gameObject.SetActive(show);
            if (show) _promptText.text = text;
        }

        public void Toast(string text, Color color)
        {
            if (_toast == null) return;
            _toast.text = text;
            _toastColor = color;
            _toastUntil = Time.unscaledTime + 2.4f;
        }

        public void SetLeaderboard(List<Entry> entries)
        {
            if (_rows.Count == 0) return;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i >= entries.Count)
                {
                    row.Root.gameObject.SetActive(false);
                    continue;
                }

                var e = entries[i];
                row.Root.gameObject.SetActive(true);
                row.Rank.text = (i + 1).ToString();
                row.Rank.color = i == 0 ? UiKit.Gold : UiKit.Muted;
                row.Name.text = e.Name;
                row.Name.color = e.IsMe ? UiKit.Pink : e.Color;
                row.Score.text = e.Score.ToString();
                row.Background.color = e.IsMe ? new Color(1f, 0.42f, 0.62f, 0.16f) : new Color(1f, 1f, 1f, 0f);
            }
        }

        // --------------------------------------------------------------- update

        void Update()
        {
            float now = Time.unscaledTime;

            if (_entryOpen)
            {
                _caret.gameObject.SetActive(Mathf.Repeat(now, 1f) < 0.55f);
                if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                {
                    Submit();
                }
            }

            if (_toast != null)
            {
                float remaining = _toastUntil - now;
                float alpha = Mathf.Clamp01(remaining / 0.5f) * Mathf.Clamp01((2.4f - remaining) / 0.18f);
                var c = _toastColor;
                c.a = alpha;
                _toast.color = c;
            }
        }

        void OnDestroy()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }
    }
}
