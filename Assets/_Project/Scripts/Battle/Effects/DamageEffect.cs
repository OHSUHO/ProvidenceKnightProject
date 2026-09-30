using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>대상 칸의 적대 유닛에게 피해.</summary>
    [Serializable]
    public sealed class DamageEffect : CardEffect
    {
        [Min(0)] public int amount = 6;

        public override EffectKind Kind => EffectKind.Attack;

        public override void Resolve(in EffectContext ctx) =>
            BattleRules.AttackCell(ctx.State, ctx.Caster, ctx.Target, amount);

        public override string Describe(CardData card)
        {
            var shape = card.targeting.shape;
            return shape is TargetShape.Self or TargetShape.Walk
                ? $"피해 {amount}"
                : $"{card.targeting.Describe()} 피해 {amount}";
        }
    }
}
