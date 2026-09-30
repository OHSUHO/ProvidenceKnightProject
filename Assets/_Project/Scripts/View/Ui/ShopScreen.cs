using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>상점 화면. <see cref="Open(IReadOnlyList{EquipmentData}, string)"/> 로 진열 목록을 넘겨 연다 (ShopNpc, 나중에는 Yarn 커맨드).</summary>
    public class ShopScreen : ItemScreenBase
    {
        static readonly string[] SlotNames = { "무기", "머리", "몸", "장신구" };

        IReadOnlyList<EquipmentData> _stock = System.Array.Empty<EquipmentData>();
        string _shopName = "상점";
        TextMeshProUGUI _intro;

        protected override string TitleText => _shopName;

        public void Open(IReadOnlyList<EquipmentData> stock, string shopName)
        {
            if (!CanOpen) return;
            _stock = stock ?? System.Array.Empty<EquipmentData>();
            _shopName = string.IsNullOrEmpty(shopName) ? "상점" : shopName;
            OpenScreen();
        }

        protected override void BuildExtra(RectTransform extra)
        {
            _intro = UiKit.Text("Intro", extra, "", 26f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, UiKit.Muted,
                new Vector2(18f, 12f), new Vector2(-18f, -12f));
        }

        protected override void OnRefresh() =>
            _intro.text = $"보유 골드  <color=#FFD94D>{Profile.Gold} G</color>\n<size=80%>구매한 장비는 I 키 장비 화면에서 착용합니다.</size>";

        protected override IEnumerable<EquipmentData> Items() => _stock.Where(e => e != null);

        protected override string RowText(EquipmentData item)
        {
            string price = Profile.Owns(item)
                ? "<color=#7A8093>보유 중</color>"
                : Profile.Gold >= item.price ? $"<color=#FFD94D>{item.price} G</color>" : $"<color=#E06060>{item.price} G</color>";
            return $"{item.displayName}   {price}\n<size=70%><color=#A6ADBF>{SlotNames[(int)item.slot]} · Lv {item.requiredLevel} · {item.stats}</color></size>";
        }

        protected override string DetailText(EquipmentData item)
        {
            string text = $"<size=120%><b>{item.displayName}</b></size>\n<color=#A6ADBF>{SlotNames[(int)item.slot]} · 필요 레벨 {item.requiredLevel}</color>\n\n{item.stats}";
            var current = Profile.GetEquipped(item.slot);
            if (current != null && current != item)
                text += $"\n\n<color=#FFD94D>현재 착용 대비</color>\n{item.stats - current.stats}";
            if (!string.IsNullOrWhiteSpace(item.description)) text += $"\n\n<color=#A6ADBF>{item.description}</color>";
            return text;
        }

        protected override (string label, bool enabled) ActionState(EquipmentData item)
        {
            if (Profile.Owns(item)) return ("보유 중", false);
            if (Profile.Gold < item.price) return ($"골드 부족 ({item.price} G)", false);
            return ($"구매  {item.price} G", true);
        }

        protected override void OnAction(EquipmentData item)
        {
            if (Profile.Owns(item) || !Profile.TrySpendGold(item.price)) return;
            Profile.AddOwned(item);
            ProfileSession.Save();
            Message.text = $"{item.displayName} 구매 완료!";
        }
    }
}
