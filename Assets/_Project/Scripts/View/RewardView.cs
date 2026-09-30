using System;
using System.Collections.Generic;
using ProvidenceKnight.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>스테이지 클리어 후 보상 카드 1장을 고르는 전체화면 오버레이.</summary>
    public class RewardView : MonoBehaviour
    {
        const float RefHeight = 1080f;

        GameObject _canvasGo;
        RectTransform _cardRow;
        Text _title;
        Text _skipLabel;
        Button _skipBtn;
        readonly List<CardView> _cardViews = new();
        Action<CardData> _onChosen;
        bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;

            UI.EnsureEventSystem();

            var canvasGo = new GameObject("Reward Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo = canvasGo;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // 전투 HUD 위에 뜨도록
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, RefHeight);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            UI.AddImage(UI.Stretch("Dim", root), new Color(0.02f, 0.02f, 0.03f, 0.85f));

            _title = UI.AddText(UI.Anchored("Title", root, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1200, 90)), 46, TextAnchor.MiddleCenter);
            _title.text = "스테이지 클리어! 보상 카드를 하나 고르세요";

            _cardRow = UI.Rect("Cards", root);
            _cardRow.anchorMin = _cardRow.anchorMax = new Vector2(0.5f, 0.5f);
            _cardRow.anchoredPosition = Vector2.zero;
            _cardRow.sizeDelta = new Vector2(1000, 320);
            var layout = _cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 34;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var skipRect = UI.Anchored("Skip", root, new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(260, 70));
            var skipImg = UI.AddImage(skipRect, new Color(0.28f, 0.3f, 0.34f));
            _skipBtn = skipRect.gameObject.AddComponent<Button>();
            _skipBtn.targetGraphic = skipImg;
            _skipLabel = UI.AddText(UI.Stretch("Label", skipRect), 30, TextAnchor.MiddleCenter);
            _skipLabel.text = "건너뛰기";

            _canvasGo.SetActive(false);
        }

        /// <summary>options 중 하나를 클릭하면 onChosen(그 카드), 건너뛰면 onChosen(null) 이 호출된다.</summary>
        public void Show(IReadOnlyList<CardData> options, Action<CardData> onChosen)
        {
            Build();
            _title.text = "스테이지 클리어! 보상 카드를 하나 고르세요";
            _skipLabel.text = "건너뛰기";
            _onChosen = onChosen;

            _skipBtn.onClick.RemoveAllListeners();
            _skipBtn.onClick.AddListener(() => Choose(null));

            foreach (var v in _cardViews) Destroy(v.gameObject);
            _cardViews.Clear();

            foreach (var card in options)
            {
                var view = CardView.Create(_cardRow, new Vector2(230, 300));
                view.Set(card, affordable: true, selected: false);
                var captured = card;
                view.Button.onClick.AddListener(() => Choose(captured));
                _cardViews.Add(view);
            }

            _canvasGo.SetActive(true);
        }

        /// <summary>런이 끝났을 때(전체 클리어 또는 패배) 카드 없이 안내만 보여준다.</summary>
        public void ShowEndOfRun(bool cleared, Action onAcknowledge)
        {
            Build();
            _title.text = cleared ? "모든 스테이지 클리어! 런 성공!" : "런 종료 (패배)";
            _skipLabel.text = "확인";
            _onChosen = _ => onAcknowledge?.Invoke();

            _skipBtn.onClick.RemoveAllListeners();
            _skipBtn.onClick.AddListener(() => Choose(null));

            foreach (var v in _cardViews) Destroy(v.gameObject);
            _cardViews.Clear();

            _canvasGo.SetActive(true);
        }

        void Choose(CardData card)
        {
            _canvasGo.SetActive(false);
            var callback = _onChosen;
            _onChosen = null;
            callback?.Invoke(card);
        }
    }
}
