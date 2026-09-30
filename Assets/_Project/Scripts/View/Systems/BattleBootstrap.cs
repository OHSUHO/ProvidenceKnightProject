using System;
using System.Collections.Generic;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Data;
using ProvidenceKnight.Input;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 전투 씬 조립. BeginBattle 마다 BattleController 를 만들고 씬의 시스템(연출·계획 표시·입력·HUD)에 연결한다.
    /// RunController 가 스테이지마다 호출한다. stage/deck 을 지정하면 단독 테스트용으로 Start 에서 한 판을 시작한다.
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
        [Header("단독 테스트용 (RunController 가 있으면 비워 둠)")]
        [SerializeField] StageData stage;
        [SerializeField] DeckData deck;
        [SerializeField, Min(1)] int handSize = 5;
        [SerializeField, Min(0)] int maxEnergy = 3;

        [Header("Scene")]
        [SerializeField] BoardView board;
        [SerializeField] BattleEventPlayer eventPlayer;
        [SerializeField] EnemyPlanPresenter planPresenter;
        [SerializeField] PlayerTurnInput input;
        [SerializeField] BattleHud hud;

        public BattleController Battle { get; private set; }
        public BattleState State => Battle?.State;

        /// <summary>이 판이 끝났을 때 (마지막 연출이 끝난 뒤). RunController 가 다음 흐름을 진행한다.</summary>
        public event Action<bool> BattleFinished;

        void Awake() => eventPlayer.BattleEnded += OnBattleEnded;

        void OnDestroy()
        {
            if (eventPlayer != null) eventPlayer.BattleEnded -= OnBattleEnded;
        }

        void Start()
        {
            if (stage != null && deck != null)
                BeginBattle(stage, deck.cards, handSize, maxEnergy, null);
        }

        public void BeginBattle(StageData stageData, IReadOnlyList<CardData> deckCards, int handSizeParam, int maxEnergyParam, int? playerHp)
        {
            Battle = BattleController.Create(stageData, deckCards, handSizeParam, maxEnergyParam);

            eventPlayer.Unbind();              // 이전 판의 유닛·남은 연출 정리
            board.Build(State.Grid);
            eventPlayer.Bind(Battle);
            planPresenter.Bind(Battle);
            input.Bind(Battle);
            hud.ResetForNewBattle(handSizeParam);

            Battle.SpawnFromStage(stageData, playerHp);
            Battle.StartPlayerTurn();
            Debug.Log($"[BattleBootstrap] '{stageData.name}' {stageData.width}x{stageData.height}, 몬스터 {stageData.monsters.Count}마리, 덱 {deckCards.Count}장, 시작체력={(playerHp?.ToString() ?? "풀피")}");
        }

        void OnBattleEnded(bool playerWon)
        {
            Debug.Log(playerWon ? "[BattleBootstrap] 승리" : "[BattleBootstrap] 패배");
            hud.ShowResult(playerWon);
            BattleFinished?.Invoke(playerWon);
        }
    }
}
