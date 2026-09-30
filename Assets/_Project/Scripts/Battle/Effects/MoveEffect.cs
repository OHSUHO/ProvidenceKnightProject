using System;
using ProvidenceKnight.Data;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>시전자가 대상 칸으로 걸어서 이동 (최단 경로). 이동 가능 거리는 카드의 targeting.range.</summary>
    [Serializable]
    public sealed class MoveEffect : CardEffect
    {
        public override EffectKind Kind => EffectKind.Move;

        public override void Resolve(in EffectContext ctx)
        {
            if (ctx.Caster.Position == ctx.Target) return;
            var path = ctx.State.Grid.FindPath(ctx.Caster.Position, ctx.Target);
            if (path != null) BattleRules.TryMoveAlongPath(ctx.State, ctx.Caster, path);
        }

        public override string Describe(CardData card) => $"{card.targeting.range}칸 이동";
    }
}
