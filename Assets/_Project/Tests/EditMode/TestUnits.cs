using System.Collections.Generic;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    /// <summary>테스트용 몬스터 카드. moveRange/attackDamage 로 "이동 카드 + 공격 카드" 세트를 만든다 (0 이면 그 카드는 없음).</summary>
    static class TestUnits
    {
        public static List<CardData> Cards(int moveRange, int attackDamage,
            TargetShape shape = TargetShape.Adjacent, int attackRange = 1)
        {
            var cards = new List<CardData>();
            if (moveRange > 0)
                cards.Add(Make("Move", new TargetPattern(TargetShape.Walk, moveRange, false), new MoveEffect()));
            if (attackDamage > 0)
                cards.Add(Make("Attack", new TargetPattern(shape, attackRange, true), new DamageEffect { amount = attackDamage }));
            return cards;
        }

        static CardData Make(string name, TargetPattern targeting, CardEffect effect)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.cardName = name;
            c.cost = 0;
            c.targeting = targeting;
            c.effects = new List<CardEffect> { effect };
            return c;
        }
    }
}
