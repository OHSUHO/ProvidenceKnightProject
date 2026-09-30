using System;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Input;
using ProvidenceKnight.Run;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 전투 씬 조립. BeginBattle 마다 BattleController 를 만들고 씬의 시스템(연출·계획 표시·입력·HUD)에 연결한다.
    /// RunController 가 스테이지마다 호출한다.
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
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

        /// <summary>런의 현재 스테이지로 전투 한 판을 시작한다.</summary>
        public void BeginBattle(RunState run)
        {
            var stage = run.CurrentStage;
            Battle = BattleController.Create(stage, run.Deck, run.HandSize, run.MaxEnergy, run.StageSeed(RunState.BattleSalt));

            eventPlayer.Unbind();              // 이전 판의 유닛·남은 연출 정리
            board.Build(State.Grid);
            eventPlayer.Bind(Battle);
            planPresenter.Bind(Battle);
            input.Bind(Battle);
            hud.ResetForNewBattle(run.HandSize);

            Battle.SpawnFromStage(stage, run.Player, run.PlayerHp);
            Battle.StartPlayerTurn();
            Debug.Log($"[BattleBootstrap] '{stage.name}' {stage.width}x{stage.height}, 몬스터 {stage.monsters.Count}마리, 덱 {run.Deck.Count}장, 시작체력={(run.PlayerHp?.ToString() ?? "풀피")}");
        }

        void OnBattleEnded(bool playerWon)
        {
            Debug.Log(playerWon ? "[BattleBootstrap] 승리" : "[BattleBootstrap] 패배");
            hud.ShowResult(playerWon);
            BattleFinished?.Invoke(playerWon);
        }
    }
}
