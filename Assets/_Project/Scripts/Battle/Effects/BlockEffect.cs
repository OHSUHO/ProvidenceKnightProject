using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>시전자에게 방어도 (다음 내 턴 시작 시 초기화).</summary>
    [Serializable]
    public sealed class BlockEffect : CardEffect
    {
        [Min(0)] public int amount = 5;

        public override void Resolve(in EffectContext ctx) =>
            BattleRules.GainBlock(ctx.State, ctx.Caster, amount);

        public override string Describe(CardData card) => $"방어도 {amount}";
    }
}
