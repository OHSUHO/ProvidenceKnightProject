using System;
using ProvidenceKnight.Battle.Effects;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>몬스터 턴 시작 효과 하나. turns 가 0 보다 크면 전투의 그 턴까지만 걸리고 그 뒤로는 사라진다.</summary>
    [Serializable]
    public sealed class TurnStartEntry
    {
        [Tooltip("이 효과가 유지되는 전투 턴 수. 1 이면 첫 턴에만, 3 이면 3턴째까지. 0 = 전투 내내")]
        [Min(0)] public int turns;

        [SerializeReference] public CardEffect effect;

        public TurnStartEntry() { }

        public TurnStartEntry(CardEffect effect, int turns = 0)
        {
            this.effect = effect;
            this.turns = turns;
        }

        /// <summary>battleTurn(1부터) 번째 턴에 이 효과가 걸리는가.</summary>
        public bool IsActiveOnTurn(int battleTurn) => turns <= 0 || battleTurn <= turns;

        public string Describe() =>
            effect == null ? "" : effect.Describe(null) + (turns > 0 ? $" ({turns}턴까지)" : "");
    }
}
