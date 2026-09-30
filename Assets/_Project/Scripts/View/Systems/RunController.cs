using System.Collections.Generic;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 여러 스테이지를 이어서 진행하는 런의 진행자. 설정은 RunConfig 에셋에서 읽는다.
    /// 스테이지를 깨면: 체력을 이어받고 → 보상 카드를 고르고 → 다음 스테이지 시작.
    /// 지면 런이 그대로 끝난다 (재시작 없음).
    /// </summary>
    public class RunController : MonoBehaviour
    {
        /// <summary>에디터: StageData 인스펙터의 "이 스테이지 플레이" 가 여기에 스테이지 GUID 를 넣고 플레이 모드에 들어간다.</summary>
        public const string TestStageKey = "ProvidenceKnight.TestStageGuid";

        [SerializeField] RunConfig config;

        [Header("Scene")]
        [SerializeField] BattleBootstrap battle;
        [SerializeField] RewardScreen rewardScreen;

        public RunState Run { get; private set; }

        void Start()
        {
            var errors = config != null ? config.Validate() : new List<string> { "RunConfig 가 지정되지 않음" };
            if (errors.Count > 0)
            {
                Debug.LogError("[RunController] 런을 시작할 수 없습니다:\n- " + string.Join("\n- ", errors), config != null ? config : this);
                enabled = false;
                return;
            }

            int seed = System.Environment.TickCount;
            Run = RunState.FromConfig(config, seed);
#if UNITY_EDITOR
            if (TakeTestStage() is { } testStage)
            {
                Run = new RunState(new List<StageData> { testStage }, config.player, config.startingDeck.cards, config.handSize, config.startingEnergy, seed);
                Debug.Log($"[RunController] 테스트 플레이: '{testStage.name}' 한 판만 진행합니다.", testStage);
            }
#endif
            battle.BattleFinished += OnBattleFinished;
            StartCurrentStage();
        }

#if UNITY_EDITOR
        static StageData TakeTestStage()
        {
            var guid = UnityEditor.SessionState.GetString(TestStageKey, "");
            if (guid.Length == 0) return null;
            UnityEditor.SessionState.EraseString(TestStageKey);   // 한 번만 적용 (다음 플레이는 평소 런)
            return UnityEditor.AssetDatabase.LoadAssetAtPath<StageData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        }
#endif

        void OnDestroy()
        {
            if (battle != null) battle.BattleFinished -= OnBattleFinished;
        }

        void StartCurrentStage()
        {
            Debug.Log($"[RunController] 스테이지 {Run.StageIndex + 1}/{Run.Stages.Count} 시작 (덱 {Run.Deck.Count}장, 최대에너지 {Run.MaxEnergy}, 체력 {(Run.PlayerHp?.ToString() ?? "풀피")})");
            battle.BeginBattle(Run);
        }

        void OnBattleFinished(bool playerWon)
        {
            Run.CaptureResult(battle.State);

            if (!playerWon)
            {
                Debug.Log("[RunController] 런 종료: 패배");
                rewardScreen.ShowEndOfRun(cleared: false, onAcknowledge: null);
                return;
            }

            // 보상은 방금 깬 스테이지 기준 시드로 뽑는다 (세이브 후 다시 불러와도 같은 선택지)
            var options = RewardPicker.Pick(config.rewardPool != null ? config.rewardPool.cards : null, config.rewardChoices,
                new System.Random(Run.StageSeed(RunState.RewardSalt)));
            Run.AdvanceStage();

            if (!Run.HasCurrentStage)
            {
                Debug.Log("[RunController] 런 클리어! 모든 스테이지를 이겼습니다.");
                rewardScreen.ShowEndOfRun(cleared: true, onAcknowledge: null);
                return;
            }

            ShowReward(options);
        }

        void ShowReward(List<CardData> options)
        {
            if (options.Count == 0) { StartCurrentStage(); return; }

            rewardScreen.Show(options, chosen =>
            {
                if (chosen != null)
                {
                    Run.AddReward(chosen);
                    Debug.Log($"[RunController] 보상으로 '{chosen.cardName}' 획득");
                }
                StartCurrentStage();
            });
        }
    }
}
