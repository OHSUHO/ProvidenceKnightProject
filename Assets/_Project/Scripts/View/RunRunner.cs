using System.Collections.Generic;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 여러 스테이지를 이어서 진행하는 런의 진행자.
    /// 스테이지를 깨면: 체력을 이어받고 → 보상 카드를 고르고 → 다음 스테이지 시작.
    /// 지면 런이 그대로 끝난다 (재시작 없음, v0).
    /// </summary>
    public class RunRunner : MonoBehaviour
    {
        [Header("Run Data")]
        [SerializeField] List<StageData> stages = new();
        [SerializeField] DeckData startingDeck;
        [SerializeField] RewardPoolData rewardPool;
        [SerializeField, Min(1)] int handSize = 5;
        [SerializeField, Min(0)] int startingEnergy = 3;
        [SerializeField, Min(1)] int rewardChoices = 3;

        [Header("Scene")]
        [SerializeField] BattleRunner battleRunner;
        [SerializeField] RewardView rewardView;

        public RunState Run { get; private set; }

        readonly System.Random _rng = new();

        void Start()
        {
            if (stages == null || stages.Count == 0 || startingDeck == null)
            {
                Debug.LogError("[RunRunner] stages / startingDeck 이 지정되지 않았습니다.", this);
                enabled = false;
                return;
            }

            Run = new RunState(stages, startingDeck.cards, handSize, startingEnergy);
            battleRunner.BattleFinished += OnBattleFinished;
            StartCurrentStage();
        }

        void OnDestroy()
        {
            if (battleRunner != null) battleRunner.BattleFinished -= OnBattleFinished;
        }

        void StartCurrentStage()
        {
            Debug.Log($"[RunRunner] 스테이지 {Run.StageIndex + 1}/{Run.Stages.Count} 시작 (덱 {Run.Deck.Count}장, 최대에너지 {Run.MaxEnergy}, 체력 {(Run.PlayerHp?.ToString() ?? "풀피")})");
            battleRunner.BeginBattle(Run.CurrentStage, Run.Deck, Run.HandSize, Run.MaxEnergy, Run.PlayerHp);
        }

        void OnBattleFinished(bool playerWon)
        {
            Run.CaptureResult(battleRunner.State);

            if (!playerWon)
            {
                Debug.Log("[RunRunner] 런 종료: 패배");
                rewardView.ShowEndOfRun(cleared: false, onAcknowledge: null);
                return;
            }

            bool wasLastStage = Run.IsLastStage;
            Run.AdvanceStage();

            if (!Run.HasCurrentStage)
            {
                Debug.Log("[RunRunner] 런 클리어! 모든 스테이지를 이겼습니다.");
                rewardView.ShowEndOfRun(cleared: true, onAcknowledge: null);
                return;
            }

            ShowReward();
        }

        void ShowReward()
        {
            var options = RewardPicker.Pick(rewardPool != null ? rewardPool.cards : null, rewardChoices, _rng);
            if (options.Count == 0) { StartCurrentStage(); return; }

            rewardView.Show(options, chosen =>
            {
                if (chosen != null)
                {
                    Run.AddReward(chosen);
                    Debug.Log($"[RunRunner] 보상으로 '{chosen.cardName}' 획득");
                }
                StartCurrentStage();
            });
        }
    }
}
