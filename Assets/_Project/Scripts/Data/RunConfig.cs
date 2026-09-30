using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>런 한 판의 설정: 플레이어, 스테이지 순서, 시작 덱, 보상, 손패·에너지 수치.</summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "ProvidenceKnight/Run Config")]
    public class RunConfig : ScriptableObject
    {
        [Tooltip("플레이어 유닛 (팀 = Player). 시작 위치는 스테이지마다 StageData 에서 정한다")]
        public UnitData player;

        [Tooltip("위에서부터 순서대로 진행")]
        public List<StageData> stages = new();

        public DeckData startingDeck;

        [Tooltip("스테이지 클리어 보상 후보. 비우면 보상 없이 다음 스테이지로")]
        public RewardPoolData rewardPool;

        [Min(1)] public int handSize = 5;
        [Min(0)] public int startingEnergy = 3;
        [Min(1)] public int rewardChoices = 3;

        /// <summary>설정 오류 목록. 비어 있으면 런을 시작할 수 있다.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (player == null) errors.Add("player 가 비어 있음");
            else if (player.team != Team.Player) errors.Add($"player '{player.name}' 의 팀이 Player 가 아님");

            if (stages == null || stages.Count == 0) errors.Add("stages 가 비어 있음");
            else
                for (int i = 0; i < stages.Count; i++)
                    if (stages[i] == null) errors.Add($"stages[{i}] 가 비어 있음");

            if (startingDeck == null) errors.Add("startingDeck 이 비어 있음");
            else if (startingDeck.cards.Count == 0) errors.Add($"시작 덱 '{startingDeck.name}' 에 카드가 없음");
            return errors;
        }
    }
}
