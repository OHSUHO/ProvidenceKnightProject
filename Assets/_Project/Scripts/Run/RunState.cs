using System.Collections.Generic;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Data;

namespace ProvidenceKnight.Run
{
    /// <summary>
    /// 여러 스테이지에 걸친 진행 상태(런). 순수 C#, MonoBehaviour 에 의존하지 않는다.
    /// 체력은 스테이지가 끝난 그대로 이어지고(자동 회복 없음), 덱은 보상 카드가 쌓이며 커진다.
    /// </summary>
    public class RunState
    {
        public IReadOnlyList<StageData> Stages { get; }
        public int StageIndex { get; private set; }
        public List<CardData> Deck { get; }
        public int HandSize { get; }
        public int MaxEnergy { get; private set; }

        /// <summary>다음 전투를 시작할 플레이어 체력. null 이면 풀피 (첫 스테이지).</summary>
        public int? PlayerHp { get; private set; }

        public bool HasCurrentStage => StageIndex < Stages.Count;
        public bool IsLastStage => StageIndex >= Stages.Count - 1;
        public StageData CurrentStage => Stages[StageIndex];

        public RunState(IReadOnlyList<StageData> stages, IEnumerable<CardData> startingDeck, int handSize, int maxEnergy)
        {
            Stages = stages;
            Deck = new List<CardData>(startingDeck);
            HandSize = handSize;
            MaxEnergy = maxEnergy;
        }

        /// <summary>전투 직후 호출: 생존 체력과 (영구 증가했을 수 있는) 최대 에너지를 다음 스테이지로 이어간다.</summary>
        public void CaptureResult(BattleState battle)
        {
            PlayerHp = battle.Player.Hp;
            MaxEnergy = battle.MaxEnergy;
        }

        /// <summary>보상으로 고른 카드를 덱 맨 뒤에 추가한다.</summary>
        public void AddReward(CardData card)
        {
            if (card != null) Deck.Add(card);
        }

        public void AdvanceStage() => StageIndex++;
    }
}
