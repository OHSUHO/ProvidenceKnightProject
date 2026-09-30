using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>최대 에너지 영구 증가 (이번 런 내내 유지). 이번 턴 에너지도 즉시 같은 만큼 늘어난다.</summary>
    [Serializable]
    public sealed class GainMaxEnergyEffect : CardEffect
    {
        [Min(1)] public int amount = 1;

        public override void Resolve(in EffectContext ctx)
        {
            ctx.State.MaxEnergy += amount;
            ctx.State.Energy += amount;   // ResourcesChanged 는 카드 사용이 끝난 뒤 BattleController 가 한 번에 알린다
        }

        public override string Describe(CardData card) => $"최대 에너지 +{amount} (이번 런 내내)";
    }
}
