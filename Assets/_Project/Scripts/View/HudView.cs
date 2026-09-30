using System;
using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 손패 / 에너지 / 덱 미리보기 / 턴 종료 버튼. 프로토타입이라 전부 코드로 생성한다.
    /// 화면 아래 BottomFraction 만큼을 차지한다 (카메라가 그만큼 피해서 필드를 배치).
    /// </summary>
    public class HudView : MonoBehaviour
    {
        public const float BottomFraction = 0.27f;
        const float RefHeight = 1080f;

        int _deckPreviewCount;

        public event Action<int> CardClicked;
        public event Action EndTurnClicked;

        readonly List<CardView> _cards = new();
        RectTransform _handRoot;
        CanvasGroup _panelGroup;
        Text _energyText, _turnText, _deckText, _messageText, _hoverInfoText;
        float _messageUntil;
        bool _messagePinned;

        public void Build(int handSize)
        {
            _deckPreviewCount = handSize;
            UI.EnsureEventSystem();

            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, RefHeight);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // 하단 패널
            var panel = UI.Rect("Bottom Panel", root);
            panel.anchorMin = new Vector2(0, 0);
            panel.anchorMax = new Vector2(1, 0);
            panel.pivot = new Vector2(0.5f, 0);
            panel.sizeDelta = new Vector2(0, RefHeight * BottomFraction);
            UI.AddImage(panel, new Color(0.07f, 0.08f, 0.1f, 0.95f));
            _panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            // 손패
            _handRoot = UI.Rect("Hand", panel);
            _handRoot.anchorMin = _handRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _handRoot.sizeDelta = new Vector2(1100, 250);
            var layout = _handRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            for (int i = 0; i < handSize; i++)
            {
                int index = i;
                var card = CardView.Create(_handRoot, new Vector2(200, 250));
                card.Button.onClick.AddListener(() => CardClicked?.Invoke(index));
                _cards.Add(card);
            }

            // 에너지 (왼쪽)
            _energyText = UI.AddText(UI.Anchored("Energy", panel, new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(220, 120)), 56, TextAnchor.MiddleCenter);
            _energyText.color = new Color(1f, 0.85f, 0.3f);

            // 덱 미리보기(다음 손패) + 턴 종료 (오른쪽)
            _deckText = UI.AddText(UI.Anchored("Deck Preview", panel, new Vector2(1, 0.5f), new Vector2(-190, 55), new Vector2(320, 175)), 22, TextAnchor.UpperLeft);
            _deckText.color = new Color(0.75f, 0.78f, 0.85f);

            var endBtnRect = UI.Anchored("End Turn", panel, new Vector2(1, 0.5f), new Vector2(-190, -100), new Vector2(280, 80));
            var endImg = UI.AddImage(endBtnRect, new Color(0.75f, 0.35f, 0.25f));
            var endBtn = endBtnRect.gameObject.AddComponent<Button>();
            endBtn.targetGraphic = endImg;
            endBtn.onClick.AddListener(() => EndTurnClicked?.Invoke());
            UI.AddText(UI.Stretch("Label", endBtnRect), 34, TextAnchor.MiddleCenter).text = "턴 종료";

            // 상단 정보
            _turnText = UI.AddText(UI.Anchored("Turn", root, new Vector2(0, 1), new Vector2(140, -50), new Vector2(240, 60)), 34, TextAnchor.MiddleLeft);
            _messageText = UI.AddText(UI.Anchored("Message", root, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 60)), 36, TextAnchor.MiddleCenter);
            _messageText.color = new Color(1f, 0.6f, 0.5f);

            // 적 호버 정보 (오른쪽 위)
            _hoverInfoText = UI.AddText(UI.Anchored("Hover Info", root, new Vector2(1, 1), new Vector2(-260, -70), new Vector2(480, 110)), 28, TextAnchor.UpperRight);
            _hoverInfoText.color = new Color(0.9f, 0.92f, 0.97f);
        }

        /// <summary>마우스를 올린 유닛의 스탯 (비우면 숨김).</summary>
        public void SetHoverInfo(string text)
        {
            if (_hoverInfoText != null) _hoverInfoText.text = text ?? "";
        }

        void Update()
        {
            if (!_messagePinned && _messageText != null && _messageText.text.Length > 0 && Time.time > _messageUntil)
                _messageText.text = "";

            // 연출 재생 중엔 새 입력을 못 받게 살짝 잠근다 (전투 종료 잠금은 건드리지 않음).
            if (!_messagePinned) SetInteractable(!AnimationQueue.Busy);
        }

        public void ShowMessage(string msg, float seconds = 1.5f)
        {
            if (_messagePinned) return;
            _messageText.color = new Color(1f, 0.6f, 0.5f);
            _messageText.text = msg;
            _messageUntil = Time.time + seconds;
        }

        /// <summary>승패 결과를 계속 표시하고 손패/턴 종료 입력을 잠근다.</summary>
        public void ShowResult(bool playerWon)
        {
            _messagePinned = true;
            _messageText.text = playerWon ? "승리!" : "패배...";
            _messageText.color = playerWon ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.4f, 0.4f);
            SetInteractable(false);
        }

        public void SetInteractable(bool value)
        {
            if (_panelGroup == null) return;
            _panelGroup.interactable = value;
            _panelGroup.blocksRaycasts = value;
            _panelGroup.alpha = value ? 1f : 0.5f;
        }

        /// <summary>새 스테이지 시작 전 호출: 이전 판의 승패 잠금/메시지를 지운다.</summary>
        public void ResetForNewBattle()
        {
            _messagePinned = false;
            if (_messageText != null) _messageText.text = "";
            SetInteractable(true);
        }

        public void Refresh(BattleState state, int selectedIndex)
        {
            var hand = state.Cards.Hand;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = i < hand.Count ? hand[i] : null;
                _cards[i].gameObject.SetActive(card != null);
                if (card != null)
                    _cards[i].Set(card, affordable: card.cost <= state.Energy, selected: i == selectedIndex);
            }

            _energyText.text = $"{state.Energy}/{state.MaxEnergy}\n<size=24>에너지</size>";
            _turnText.text = $"턴 {state.Turn}";

            // 턴 종료 시 정확히 이 카드들로 손패가 교체된다 (지금까지 낸 카드가 없다면).
            var next = state.Cards.PeekDeck(_deckPreviewCount).Select((c, i) => $"{i + 1}. {c.cardName}");
            _deckText.text = $"다음 손패 (덱 {state.Cards.DeckCount})\n" + string.Join("\n", next);
        }

    }

    public class CardView : MonoBehaviour
    {
        public Button Button { get; private set; }

        Image _bg, _outline;
        Text _cost, _name, _desc;

        public static CardView Create(RectTransform parent, Vector2 size)
        {
            var rt = UI.Rect("Card", parent);
            rt.sizeDelta = size;
            var view = rt.gameObject.AddComponent<CardView>();

            view._outline = UI.AddImage(rt, Color.white);
            var inner = UI.Stretch("Inner", rt, 5);
            view._bg = UI.AddImage(inner, Color.gray);
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view._bg;

            view._cost = UI.AddText(UI.Anchored("Cost", inner, new Vector2(0, 1), new Vector2(28, -28), new Vector2(48, 48)), 34, TextAnchor.MiddleCenter);
            view._cost.color = new Color(1f, 0.85f, 0.3f);
            view._name = UI.AddText(UI.Anchored("Name", inner, new Vector2(0.5f, 1), new Vector2(12, -30), new Vector2(150, 44)), 30, TextAnchor.MiddleCenter);
            view._desc = UI.AddText(UI.Anchored("Desc", inner, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(175, 140)), 24, TextAnchor.MiddleCenter);
            return view;
        }

        public void Set(CardData card, bool affordable, bool selected)
        {
            _cost.text = card.cost.ToString();
            _name.text = card.cardName;
            _desc.text = card.GetDescription();

            var baseColor = card.color * 0.35f;
            baseColor.a = 1f;
            _bg.color = affordable ? baseColor : new Color(0.15f, 0.15f, 0.15f);
            _outline.color = selected ? new Color(1f, 0.9f, 0.3f) : (affordable ? card.color * 0.8f : new Color(0.3f, 0.3f, 0.3f));
            _name.color = _desc.color = affordable ? Color.white : new Color(0.55f, 0.55f, 0.55f);

            var rt = (RectTransform)transform;
            rt.localScale = selected ? Vector3.one * 1.06f : Vector3.one;
        }
    }

    /// <summary>uGUI 코드 생성용 작은 헬퍼.</summary>
    static class UI
    {
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset;
            rt.offsetMax = -Vector2.one * inset;
            return rt;
        }

        public static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image AddImage(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Square;
            img.color = color;
            return img;
        }

        public static Text AddText(RectTransform rt, int size, TextAnchor align)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = SpriteFactory.Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
