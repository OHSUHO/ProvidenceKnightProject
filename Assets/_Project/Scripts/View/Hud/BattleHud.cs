using System;
using ProvidenceKnight.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 전투 HUD 캔버스의 입구. 씬에 배치된 하위 뷰(손패·에너지·덱·턴·토스트·툴팁·결과)를 묶어서 다룬다.
    /// 레이아웃은 씬에서 직접 편집한다.
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        [SerializeField] HandView hand;
        [SerializeField] EnergyView energy;
        [SerializeField] DeckPreviewView deckPreview;
        [SerializeField] TurnInfoView turnInfo;
        [SerializeField] ToastView toast;
        [SerializeField] UnitTooltipView tooltip;
        [SerializeField] ResultBannerView resultBanner;
        [SerializeField] Button endTurnButton;
        [Tooltip("연출 중·전투 종료 후 입력을 잠글 하단 패널")]
        [SerializeField] CanvasGroup bottomPanel;
        [SerializeField, Range(0f, 1f)] float lockedAlpha = 0.5f;

        public event Action<int> CardClicked;
        public event Action EndTurnClicked;

        bool _resultShown;

        void Awake()
        {
            hand.CardClicked += i => CardClicked?.Invoke(i);
            endTurnButton.onClick.AddListener(() => EndTurnClicked?.Invoke());
        }

        public void ResetForNewBattle(int handSize)
        {
            hand.EnsureSlots(handSize);
            deckPreview.Count = handSize;
            _resultShown = false;
            resultBanner.Hide();
            tooltip.Hide();
            toast.Clear();
            SetInteractable(true);
        }

        public void Refresh(BattleState state, int selectedCard)
        {
            hand.Refresh(state.Cards.Hand, state.Energy, selectedCard);
            energy.Set(state.Energy, state.MaxEnergy);
            turnInfo.Set(state.Turn, state.Phase);
            deckPreview.Set(state.Cards);
        }

        public void ShowToast(string message) => toast.Show(message);

        public void ShowTooltip(string header, string body) => tooltip.Show(header, body);
        public void HideTooltip() => tooltip.Hide();

        /// <summary>승패 결과를 띄우고 손패·턴 종료 입력을 계속 잠근다.</summary>
        public void ShowResult(bool playerWon)
        {
            _resultShown = true;
            resultBanner.Show(playerWon);
            SetInteractable(false);
        }

        public void SetInteractable(bool value)
        {
            if (_resultShown) value = false;
            if (bottomPanel.interactable == value) return;
            bottomPanel.interactable = value;
            bottomPanel.blocksRaycasts = value;
            bottomPanel.alpha = value ? 1f : lockedAlpha;
        }
    }
}
