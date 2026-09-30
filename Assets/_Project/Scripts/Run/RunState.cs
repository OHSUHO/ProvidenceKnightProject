using System;
using System.Collections.Generic;
using System.Linq;
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
        public UnitData Player { get; }
        public int StageIndex { get; private set; }
        public List<CardData> Deck { get; }
        public int HandSize { get; }
        public int MaxEnergy { get; private set; }

        /// <summary>런 난수 시드. 스테이지마다 여기서 파생한 시드를 쓰므로, 시드와 스테이지 번호만 저장하면 같은 결과가 재현된다.</summary>
        public int Seed { get; }

        /// <summary>다음 전투를 시작할 플레이어 체력. null 이면 풀피 (첫 스테이지).</summary>
        public int? PlayerHp { get; private set; }

        public bool HasCurrentStage => StageIndex < Stages.Count;
        public bool IsLastStage => StageIndex >= Stages.Count - 1;
        public StageData CurrentStage => Stages[StageIndex];

        public RunState(IReadOnlyList<StageData> stages, UnitData player, IEnumerable<CardData> startingDeck, int handSize, int maxEnergy, int seed = 0)
        {
            Stages = stages;
            Player = player;
            Deck = new List<CardData>(startingDeck);
            HandSize = handSize;
            MaxEnergy = maxEnergy;
            Seed = seed;
        }

        public static RunState FromConfig(RunConfig config, int seed) =>
            new(config.stages, config.player, config.startingDeck.cards, config.handSize, config.startingEnergy, seed);

        /// <summary>현재 스테이지 전용 시드. salt 로 용도(전투 동점 처리 / 보상 뽑기)를 구분한다.</summary>
        public int StageSeed(int salt)
        {
            unchecked { return (Seed * 486187739) ^ (StageIndex * 16777619) ^ (salt * 73856093); }
        }

        public const int BattleSalt = 1;
        public const int RewardSalt = 2;

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

        // ---------------- 세이브 ----------------

        public RunSaveData ToSaveData() => new()
        {
            stageIndex = StageIndex,
            hasPlayerHp = PlayerHp.HasValue,
            playerHp = PlayerHp ?? 0,
            maxEnergy = MaxEnergy,
            deckCardIds = Deck.Select(c => c.Id).ToArray(),
            rngSeed = Seed,
        };

        /// <summary>저장 데이터 + 설정(스테이지 순서·플레이어·손패 수) + 데이터베이스(카드 id → 에셋)로 런을 되살린다.</summary>
        public static bool TryFromSaveData(RunSaveData data, RunConfig config, GameDatabase db, out RunState run, out string error)
        {
            run = null;
            error = null;
            if (data == null) { error = "세이브 데이터가 없음"; return false; }
            if (data.version != RunSaveData.CurrentVersion) { error = $"세이브 버전 {data.version} 을 읽을 수 없음 (현재 {RunSaveData.CurrentVersion})"; return false; }
            if (data.stageIndex < 0 || data.stageIndex >= config.stages.Count) { error = $"스테이지 번호 {data.stageIndex} 가 RunConfig 범위 밖"; return false; }

            var deck = new List<CardData>();
            foreach (var id in data.deckCardIds ?? Array.Empty<string>())
            {
                var card = db.Get<CardData>(id);
                if (card == null) { error = $"카드 id '{id}' 를 데이터베이스에서 찾을 수 없음"; return false; }
                deck.Add(card);
            }

            run = new RunState(config.stages, config.player, deck, config.handSize, data.maxEnergy, data.rngSeed)
            {
                StageIndex = data.stageIndex,
                PlayerHp = data.hasPlayerHp ? data.playerHp : null,
            };
            return true;
        }
    }
}
