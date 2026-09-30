using System;
using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>장비 착용 화면. I 키로 열고 닫는다. 보유 장비를 골라 장착/해제하고, 착용 중인 장비의 스탯 합을 보여 준다.</summary>
    public class EquipmentScreen : ItemScreenBase
    {
        [SerializeField] Key toggleKey = Key.I;

        static readonly string[] SlotNames = { "무기", "머리", "몸", "장신구" };

        Button[] _slotButtons;
        TextMeshProUGUI _totals;

        protected override string TitleText => "장비";

        public void Open() => OpenScreen();

        protected override void Tick()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb[toggleKey].wasPressedThisFrame) return;
            if (IsOpen) Close();
            else if (!GamePause.IsPaused) OpenScreen();
        }

        protected override void BuildExtra(RectTransform extra)
        {
            var slots = Enum.GetValues(typeof(EquipSlot)).Cast<EquipSlot>().ToArray();
            _slotButtons = new Button[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                int col = i % 2, row = i / 2;
                var aMin = new Vector2(col * 0.5f, 1f - (row + 1) * 0.36f);
                var aMax = new Vector2((col + 1) * 0.5f, 1f - row * 0.36f);
                var b = UiKit.Button($"Slot_{slot}", extra, "", 22f, aMin, aMax, TextAlignmentOptions.MidlineLeft,
                    new Vector2(8f, 6f), new Vector2(-8f, -6f));
                b.onClick.AddListener(() =>
                {
                    var equipped = Profile.GetEquipped(slot);
                    if (equipped != null) Select(equipped);
                });
                _slotButtons[i] = b;
            }
            _totals = UiKit.Text("Totals", extra, "", 24f, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, new Vector2(1f, 0.28f), UiKit.Accent, new Vector2(18f, 4f), new Vector2(-18f, -4f));
        }

        protected override void OnRefresh()
        {
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var e = Profile.GetEquipped(slot);
                var btn = _slotButtons[(int)slot];
                UiKit.SetLabel(btn, e != null
                    ? $"<size=75%><color=#A6ADBF>{SlotNames[(int)slot]}</color></size>\n{e.displayName}"
                    : $"<size=75%><color=#A6ADBF>{SlotNames[(int)slot]}</color></size>\n<color=#7A8093>비어 있음</color>");
                UiKit.SetColor(btn, e != null && e == Selected ? UiKit.ButtonSelected : UiKit.ButtonColor);
                btn.interactable = e != null;
            }
            _totals.text = $"합계  {Profile.Stats}";
        }

        protected override IEnumerable<EquipmentData> Items() =>
            Profile.Owned.OrderBy(e => (int)e.slot).ThenBy(e => e.requiredLevel);

        protected override string RowText(EquipmentData item)
        {
            bool worn = Profile.GetEquipped(item.slot) == item;
            string mark = worn ? "<color=#FFD94D>[착용]</color> " : "";
            string level = Profile.Level < item.requiredLevel ? $"  <color=#E06060>Lv {item.requiredLevel}</color>" : $"  <color=#A6ADBF>Lv {item.requiredLevel}</color>";
            return $"{mark}{item.displayName}{level}\n<size=70%><color=#A6ADBF>{SlotNames[(int)item.slot]} · {item.stats}</color></size>";
        }

        protected override string DetailText(EquipmentData item)
        {
            var current = Profile.GetEquipped(item.slot);
            string text = $"<size=120%><b>{item.displayName}</b></size>\n<color=#A6ADBF>{SlotNames[(int)item.slot]} · 필요 레벨 {item.requiredLevel}</color>\n\n{item.stats}";
            if (current != item)
            {
                var diff = item.stats - (current != null ? current.stats : default);
                text += $"\n\n<color=#FFD94D>장착 시 변화</color>\n{diff}";
            }
            if (!string.IsNullOrWhiteSpace(item.description)) text += $"\n\n<color=#A6ADBF>{item.description}</color>";
            return text;
        }

        protected override (string label, bool enabled) ActionState(EquipmentData item)
        {
            if (Profile.GetEquipped(item.slot) == item) return ("해제", true);
            return Profile.CanEquip(item) switch
            {
                EquipCheck.Ok => ("장착", true),
                EquipCheck.LevelTooLow => ($"레벨 부족 (Lv {item.requiredLevel} 필요)", false),
                _ => ("장착할 수 없음", false),
            };
        }

        protected override void OnAction(EquipmentData item)
        {
            if (Profile.GetEquipped(item.slot) == item) Profile.Unequip(item.slot);
            else if (Profile.TryEquip(item) != EquipCheck.Ok) return;
            ProfileSession.Save();
            Message.text = "";
        }
    }
}
