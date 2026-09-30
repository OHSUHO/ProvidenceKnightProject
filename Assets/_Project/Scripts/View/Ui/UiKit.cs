using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>코드로 uGUI 를 짤 때 쓰는 작은 도우미 (캔버스·패널·글자·버튼·스크롤 목록).</summary>
    public static class UiKit
    {
        public static readonly Color Dim = new(0f, 0f, 0f, 0.65f);
        public static readonly Color PanelColor = new(0.09f, 0.1f, 0.14f, 0.97f);
        public static readonly Color BoxColor = new(0.14f, 0.16f, 0.22f, 1f);
        public static readonly Color ButtonColor = new(0.2f, 0.23f, 0.31f, 1f);
        public static readonly Color ButtonSelected = new(0.3f, 0.42f, 0.62f, 1f);
        public static readonly Color Accent = new(1f, 0.85f, 0.3f);
        public static readonly Color Muted = new(0.65f, 0.68f, 0.75f);

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <summary>앵커 비율(aMin~aMax)에 맞춘 빈 RectTransform. offset 으로 안쪽 여백.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Color color, Vector2 offMin = default, Vector2 offMax = default)
        {
            var rt = Rect(name, parent, aMin, aMax, offMin, offMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, TextAlignmentOptions align,
            Vector2 aMin, Vector2 aMax, Color? color = null, Vector2 offMin = default, Vector2 offMax = default)
        {
            var rt = Rect(name, parent, aMin, aMax, offMin, offMax);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, float size, Vector2 aMin, Vector2 aMax,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Vector2 offMin = default, Vector2 offMax = default)
        {
            var img = Panel(name, parent, aMin, aMax, ButtonColor, offMin, offMax);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            button.colors = colors;
            Text("Label", img.transform, label, size, align, Vector2.zero, Vector2.one, null, new Vector2(14f, 4f), new Vector2(-14f, -4f));
            return button;
        }

        public static void SetLabel(Button button, string text) => button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        public static void SetColor(Button button, Color color) => button.GetComponent<Image>().color = color;

        /// <summary>세로 스크롤 목록. content 아래에 항목(높이는 LayoutElement)을 넣으면 된다.</summary>
        public static RectTransform ScrollList(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var bg = Panel(name, parent, aMin, aMax, BoxColor);
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = Rect("Viewport", bg.transform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>스크롤 목록 안의 버튼 한 줄.</summary>
        public static Button ListRow(Transform content, string label, float height = 68f)
        {
            var button = Button("Row", content, label, 26f, Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft);
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            return button;
        }
    }
}
