using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>
    /// 상태이상 부여. 기본은 대상 칸의 적대 유닛에게 건다 (피해 효과 뒤에 붙여 "피해 + 독" 같은 카드를 만든다).
    /// onSelf 를 켜면 시전자 자신에게 건다 (턴 시작 효과에도 쓸 수 있다).
    /// 공격 무효화는 피해만 막는다 — 같은 카드의 상태이상은 그대로 걸린다.
    /// </summary>
    [Serializable]
    public sealed class ApplyStatusEffect : CardEffect
    {
        public StatusType status = StatusType.Poison;
        [Tooltip("피해형(출혈·화상·독)의 턴당 피해량. 기절·빙결·암흑에서는 쓰지 않음")]
        [Min(0)] public int amount = 2;
        [Tooltip("지속 턴 (그 유닛의 턴 기준). 독은 강도가 줄다 0이 되면 끝나므로 쓰지 않음")]
        [Min(0)] public int turns = 2;
        [Tooltip("켜면 시전자 자신에게 건다")]
        public bool onSelf;

        // 적에게 거는 효과는 공격처럼 취급한다 (몬스터 AI 가 공격 카드로 고르고, 대상 칸을 위험 지역으로 표시)
        public override EffectKind Kind => onSelf ? EffectKind.Other : EffectKind.Attack;

        public override void Resolve(in EffectContext ctx)
        {
            var target = onSelf ? ctx.Caster : ctx.State.Grid.GetUnit(ctx.Target);
            if (!onSelf && !Targeting.IsEnemyOf(target, ctx.Caster)) return;
            BattleRules.ApplyStatus(ctx.State, target, status, amount, turns);
        }

        public override string Describe(CardData card)
        {
            var text = StatusRules.Describe(status, amount, turns);
            return onSelf || card == null ? $"자신에게 {text}" : $"{text} 부여";
        }
    }
}
