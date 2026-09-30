using ProvidenceKnight.Battle;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    public class TurnInfoView : MonoBehaviour
    {
        [SerializeField] TMP_Text turn;
        [SerializeField] TMP_Text phase;

        public void Set(int turnNumber, BattlePhase battlePhase)
        {
            turn.text = $"턴 {turnNumber}";
            if (phase != null)
                phase.text = battlePhase switch
                {
                    BattlePhase.PlayerTurn => "플레이어 턴",
                    BattlePhase.EnemyTurn => "적 턴",
                    BattlePhase.Victory => "승리",
                    BattlePhase.Defeat => "패배",
                    _ => "",
                };
        }
    }
}
