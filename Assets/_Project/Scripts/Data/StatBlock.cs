using System;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>장비가 주는 (그리고 합산된) 스탯 보너스. 전부 더하기 방식이며 0 이면 영향 없음.</summary>
    [Serializable]
    public struct StatBlock
    {
        [Tooltip("최대 체력 +")] public int maxHp;
        [Tooltip("카드 피해 +N (피해가 1 이상인 공격에만)")] public int attack;
        [Tooltip("카드로 얻는 방어도 +N (얻는 방어도가 1 이상일 때만)")] public int defense;
        [Tooltip("최대 에너지 +")] public int maxEnergy;
        [Tooltip("손패 수 +")] public int handSize;

        public bool IsZero => maxHp == 0 && attack == 0 && defense == 0 && maxEnergy == 0 && handSize == 0;

        public static StatBlock operator +(StatBlock a, StatBlock b) => new()
        {
            maxHp = a.maxHp + b.maxHp,
            attack = a.attack + b.attack,
            defense = a.defense + b.defense,
            maxEnergy = a.maxEnergy + b.maxEnergy,
            handSize = a.handSize + b.handSize,
        };

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (maxHp != 0) parts.Add($"체력 {maxHp:+#;-#}");
            if (attack != 0) parts.Add($"공격 {attack:+#;-#}");
            if (defense != 0) parts.Add($"방어 {defense:+#;-#}");
            if (maxEnergy != 0) parts.Add($"에너지 {maxEnergy:+#;-#}");
            if (handSize != 0) parts.Add($"손패 {handSize:+#;-#}");
            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }
    }
}
