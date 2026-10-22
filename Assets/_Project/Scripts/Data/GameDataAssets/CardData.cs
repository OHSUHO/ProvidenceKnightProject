using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProvidenceKnight.Battle.Effects;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    [CreateAssetMenu(fileName = "Card_", menuName = "ProvidenceKnight/Card Data")]
    public class CardData : GameDataAsset
    {
        public string cardName = "Card";
        [Min(0)] public int cost = 1;

        [Header("Targeting")]
        public TargetPattern targeting = new(TargetShape.Self, 1, false);

        [Tooltip("위에서부터 순서대로 실행. 인스펙터의 '＋ 효과 추가' 버튼으로 종류를 골라 추가")]
        [SerializeReference] public List<CardEffect> effects = new();

        [Header("Run (보상 카드 등)")]
        [Tooltip("켜면 이 카드는 사용한 뒤 덱 순환에서 빠진다 (이번 스테이지에서만 소멸, 다음 스테이지엔 다시 등장)")]
        public bool exhaust;

        [Header("View")]
        [TextArea] public string description;   // 비어 있으면 효과로 자동 생성
        public Color color = new(0.9f, 0.9f, 0.9f);

        public bool Has(EffectKind kind) => effects != null && effects.Any(e => e != null && e.Kind == kind);

        /// <summary>효과 중 피해 합계 (몬스터 의도 표시용).</summary>
        public int TotalDamage => effects == null ? 0 : effects.OfType<DamageEffect>().Sum(e => e.amount);

        public string GetDescription()
        {
            if (!string.IsNullOrWhiteSpace(description)) return description;

            var sb = new StringBuilder();
            foreach (var e in effects)
            {
                if (e == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(e.Describe(this));
            }
            if (exhaust) sb.Append(sb.Length > 0 ? "\n(이번 전투 1회용)" : "(이번 전투 1회용)");
            return sb.ToString();
        }
    }
}
