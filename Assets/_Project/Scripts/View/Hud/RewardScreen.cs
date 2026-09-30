using System;
using System.Collections.Generic;
using ProvidenceKnight.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>스테이지 클리어 후 보상 카드 1장을 고르는 화면 (씬의 Reward Canvas, 기본 비활성). Card 프리팹을 재사용한다.</summary>
    public class RewardScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] RectTransform cardRow;
        [SerializeField] CardView cardPrefab;
        [SerializeField] Button skipButton;
        [SerializeField] TMP_Text skipLabel;

        [Header("Text")]
        [SerializeField] string rewardTitle = "스테이지 클리어! 보상 카드를 하나 고르세요";
        [SerializeField] string skipText = "건너뛰기";
        [SerializeField] string clearedTitle = "모든 스테이지 클리어! 런 성공!";
        [SerializeField] string defeatedTitle = "런 종료 (패배)";
        [SerializeField] string confirmText = "확인";

        readonly List<CardView> _cards = new();
        Action<CardData> _onChosen;

        void Awake()
        {
            skipButton.onClick.AddListener(() => Choose(null));
            panel.SetActive(false);
        }

        /// <summary>options 중 하나를 클릭하면 onChosen(그 카드), 건너뛰면 onChosen(null).</summary>
        public void Show(IReadOnlyList<CardData> options, Action<CardData> onChosen)
        {
            Open(rewardTitle, skipText, onChosen);
            foreach (var card in options)
            {
                var view = Instantiate(cardPrefab, cardRow);
                view.Set(card, affordable: true, selected: false);
                var captured = card;
                view.Button.onClick.AddListener(() => Choose(captured));
                _cards.Add(view);
            }
        }

        /// <summary>런이 끝났을 때(전체 클리어 또는 패배) 카드 없이 안내만.</summary>
        public void ShowEndOfRun(bool cleared, Action onAcknowledge) =>
            Open(cleared ? clearedTitle : defeatedTitle, confirmText, _ => onAcknowledge?.Invoke());

        void Open(string titleText, string buttonText, Action<CardData> onChosen)
        {
            title.text = titleText;
            skipLabel.text = buttonText;
            _onChosen = onChosen;
            foreach (var v in _cards) Destroy(v.gameObject);
            _cards.Clear();
            panel.SetActive(true);
        }

        void Choose(CardData card)
        {
            panel.SetActive(false);
            var callback = _onChosen;
            _onChosen = null;
            callback?.Invoke(card);
        }
    }
}
