using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>
    /// 시전자에게 "공격 무효화" 횟수를 준다. 다음에 받는 공격 amount 번의 피해를 (방어도 소모 없이) 통째로 막는다.
    /// 방어도와 달리 턴이 지나도 사라지지 않는다. maxStacks 를 넘게 쌓이지 않는다 (0 = 제한 없음).
    /// </summary>
    [Serializable]
    public sealed class NegateAttackEffect : CardEffect
    {
        [Min(1)] public int amount = 1;
        [Tooltip("이 횟수까지만 쌓인다. 0 = 제한 없음")]
        [Min(0)] public int maxStacks = 1;

        public override void Resolve(in EffectContext ctx) =>
            BattleRules.GainNegate(ctx.State, ctx.Caster, amount, maxStacks);

        public override string Describe(CardData card) => $"공격 {amount}회 무효화";
    }
}
