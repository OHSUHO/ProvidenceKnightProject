using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    public readonly struct DataIssue
    {
        public readonly Object Asset;
        public readonly string Message;

        public DataIssue(Object asset, string message)
        {
            Asset = asset;
            Message = message;
        }

        public override string ToString() => Asset != null ? $"{Asset.name}: {Message}" : Message;
    }

    /// <summary>
    /// 게임 데이터 전체 검사: id 중복·누락, 빈 참조, 스테이지 배치 오류, 효과 없는 카드, 데이터베이스 누락.
    /// 에디터 메뉴 ProvidenceKnight/Validate All Data 와 EditMode 테스트가 같은 검사를 돌린다.
    /// </summary>
    public static class DataValidator
    {
        /// <param name="projectAssets">프로젝트에 있는 모든 데이터 에셋 (데이터베이스에서 빠진 것 찾기용). null 이면 생략.</param>
        public static List<DataIssue> Validate(GameDatabase db, IEnumerable<RunConfig> configs, IEnumerable<GameDataAsset> projectAssets = null)
        {
            var issues = new List<DataIssue>();
            if (db == null)
            {
                issues.Add(new DataIssue(null, "GameDatabase 가 없음 (메뉴 ProvidenceKnight/Rebuild Database)"));
                return issues;
            }

            CheckNullEntries(db, issues);
            var all = db.All.ToList();

            foreach (var a in all.Where(a => string.IsNullOrEmpty(a.Id)))
                issues.Add(new DataIssue(a, "id 가 비어 있음"));
            foreach (var g in all.Where(a => !string.IsNullOrEmpty(a.Id)).GroupBy(a => a.Id).Where(g => g.Count() > 1))
                foreach (var a in g)
                    issues.Add(new DataIssue(a, $"id '{g.Key}' 중복: {string.Join(", ", g.Select(x => x.name))}"));

            if (projectAssets != null)
            {
                var known = new HashSet<GameDataAsset>(all);
                foreach (var a in projectAssets.Where(a => a != null && !known.Contains(a)))
                    issues.Add(new DataIssue(a, "GameDatabase 에 없음 (메뉴 ProvidenceKnight/Rebuild Database)"));
            }

            foreach (var asset in all)
                CheckAsset(asset, issues);

            foreach (var config in configs ?? Enumerable.Empty<RunConfig>())
            {
                if (config == null) continue;
                foreach (var e in config.Validate()) issues.Add(new DataIssue(config, e));
                if (config.startingDeck != null && config.startingDeck.cards.Count < config.handSize)
                    issues.Add(new DataIssue(config, $"시작 덱 {config.startingDeck.cards.Count}장 < 손패 {config.handSize}장 (손패가 다 차지 않음)"));
            }
            return issues;
        }

        static void CheckNullEntries(GameDatabase db, List<DataIssue> issues)
        {
            void Check<T>(List<T> list, string label) where T : Object
            {
                int empty = list.Count(x => x == null);
                if (empty > 0) issues.Add(new DataIssue(db, $"{label} 목록에 빈 칸 {empty}개 (Rebuild Database)"));
            }
            Check(db.cards, "cards");
            Check(db.units, "units");
            Check(db.stages, "stages");
            Check(db.decks, "decks");
            Check(db.rewardPools, "rewardPools");
        }

        static void CheckAsset(GameDataAsset asset, List<DataIssue> issues)
        {
            switch (asset)
            {
                case CardData card:
                    if (string.IsNullOrWhiteSpace(card.cardName)) issues.Add(new DataIssue(card, "카드 이름이 비어 있음"));
                    if (card.effects == null || card.effects.Count == 0) issues.Add(new DataIssue(card, "효과가 없음"));
                    else if (card.effects.Any(e => e == null)) issues.Add(new DataIssue(card, "비어 있는 효과 칸이 있음"));
                    break;

                case UnitData unit:
                    if (string.IsNullOrWhiteSpace(unit.displayName)) issues.Add(new DataIssue(unit, "이름이 비어 있음"));
                    if (unit.team == Team.Enemy && unit.cards.Count == 0) issues.Add(new DataIssue(unit, "몬스터인데 카드가 없음"));
                    CheckCardList(unit, unit.cards, issues);
                    if (unit.turnStartEffects != null)
                    {
                        if (unit.turnStartEffects.Any(e => e == null)) issues.Add(new DataIssue(unit, "비어 있는 턴 시작 효과 칸이 있음"));
                        if (unit.turnStartEffects.Any(e => e != null && e.Kind != Battle.Effects.EffectKind.Other))
                            issues.Add(new DataIssue(unit, "턴 시작 효과에는 공격·이동 효과를 쓸 수 없음 (대상 칸이 없음)"));
                    }
                    break;

                case StageData stage:
                    foreach (var e in stage.Validate()) issues.Add(new DataIssue(stage, e));
                    break;

                case DeckData deck:
                    if (deck.cards.Count == 0) issues.Add(new DataIssue(deck, "카드가 없음"));
                    CheckCardList(deck, deck.cards, issues);
                    break;

                case RewardPoolData pool:
                    CheckCardList(pool, pool.cards, issues);
                    break;
            }
        }

        static void CheckCardList(Object owner, List<CardData> cards, List<DataIssue> issues)
        {
            int empty = cards.Count(c => c == null);
            if (empty > 0) issues.Add(new DataIssue(owner, $"비어 있는 카드 칸 {empty}개"));
        }
    }
}
