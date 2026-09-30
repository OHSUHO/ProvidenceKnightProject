using UnityEngine;

namespace ProvidenceKnight.Data
{
    public enum EquipSlot
    {
        Weapon,
        Head,
        Body,
        Accessory,
    }

    /// <summary>
    /// 장비 정의. 스탯 보너스를 주고, 착용에는 레벨이 필요하다.
    /// (스킬 효과·카드 특수효과 부여 옵션은 다음 작업에서 이 에셋에 추가한다)
    /// </summary>
    [CreateAssetMenu(fileName = "Equip_", menuName = "ProvidenceKnight/Equipment Data")]
    public class EquipmentData : GameDataAsset
    {
        public string displayName = "Equipment";
        public EquipSlot slot;
        [Min(1), Tooltip("착용에 필요한 레벨")] public int requiredLevel = 1;
        public StatBlock stats;
        [Min(0), Tooltip("상점 구매 가격 (골드)")] public int price;
        [TextArea] public string description;
        public Sprite icon;
    }
}
