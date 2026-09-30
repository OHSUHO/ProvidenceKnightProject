using System;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Battle.Effects
{
    /// <summary>대상 칸 하이라이트 색 등 뷰가 효과를 대략 분류할 때 쓴다.</summary>
    public enum EffectKind
    {
        Other,
        Move,
        Attack
    }

    /// <summary>효과 하나가 실행될 때 받는 정보.</summary>
    public readonly struct EffectContext
    {
        public BattleState State { get; }
        public Unit Caster { get; }
        public Vector2Int Target { get; }
        public CardData Card { get; }

        public EffectContext(BattleState state, Unit caster, Vector2Int target, CardData card)
        {
            State = state;
            Caster = caster;
            Target = target;
            Card = card;
        }
    }

    /// <summary>
    /// 카드 효과 하나. 카드는 [SerializeReference] 리스트로 효과를 조합한다 (위에서부터 순서대로 실행).
    /// 새 효과 = 이 클래스를 상속한 파일 하나 추가. BattleController/BattleState 는 수정하지 않는다.
    /// 상태 변경은 반드시 BattleRules 를 거친다 (이벤트·Revision 이 함께 처리되도록).
    /// </summary>
    [Serializable]
    public abstract class CardEffect
    {
        public abstract void Resolve(in EffectContext ctx);

        /// <summary>카드 설명 자동 생성용 한 줄.</summary>
        public abstract string Describe(CardData card);

        public virtual EffectKind Kind => EffectKind.Other;
    }
}
