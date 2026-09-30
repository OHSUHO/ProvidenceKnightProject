using System.Collections.Generic;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 장비 목록 + 상세 + 실행 버튼 하나로 이뤄진 전체 화면 창의 공통 뼈대 (장비 착용 화면, 상점).
    /// 왼쪽은 목록, 오른쪽은 위에서부터 [추가 영역 / 상세 / 실행 버튼]. 열려 있는 동안 게임을 멈춘다.
    /// </summary>
    public abstract class ItemScreenBase : MonoBehaviour
    {
        protected PlayerProfile Profile { get; private set; }
        protected EquipmentData Selected { get; set; }
        protected RectTransform Extra { get; private set; }
        protected TextMeshProUGUI Message { get; private set; }

        public bool IsOpen { get; private set; }

        GameObject _root;
        TextMeshProUGUI _title, _detail, _actionLabel;
        Button _action;
        RectTransform _listContent;
        readonly List<GameObject> _rows = new();

        protected abstract string TitleText { get; }
        protected abstract IEnumerable<EquipmentData> Items();
        protected abstract string RowText(EquipmentData item);
        protected abstract string DetailText(EquipmentData item);
        protected abstract (string label, bool enabled) ActionState(EquipmentData item);
        protected abstract void OnAction(EquipmentData item);

        protected virtual void BuildExtra(RectTransform extra) { }
        protected virtual void OnRefresh() { }
        protected virtual void Tick() { }

        void Start()
        {
            Profile = ProfileSession.Current;
            Profile.Changed += OnProfileChanged;
            BuildUi();
            _root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Profile != null) Profile.Changed -= OnProfileChanged;
            if (IsOpen) GamePause.Pop();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
            Tick();
        }

        // 멈춤 여부는 호출하는 쪽이 정한다 (상점은 대화 도중에도 열려야 하므로)
        protected bool CanOpen => !IsOpen && _root != null;

        protected void OpenScreen()
        {
            if (!CanOpen) return;
            IsOpen = true;
            GamePause.Push();
            Message.text = "";
            Selected = null;
            _root.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _root.SetActive(false);
            GamePause.Pop();
        }

        void OnProfileChanged()
        {
            if (IsOpen) Refresh();
        }

        protected void Refresh()
        {
            _title.text = $"{TitleText}      <size=70%><color=#FFD94D>Lv {Profile.Level}   {Profile.Gold} G</color></size>";

            foreach (var row in _rows)
            {
                row.SetActive(false);   // Destroy 는 프레임 끝에 일어나므로, 그동안 레이아웃에서 빠지게 먼저 끈다
                Destroy(row);
            }
            _rows.Clear();
            foreach (var item in Items())
            {
                var captured = item;
                var row = UiKit.ListRow(_listContent, RowText(item));
                UiKit.SetColor(row, item == Selected ? UiKit.ButtonSelected : UiKit.ButtonColor);
                row.onClick.AddListener(() => Select(captured));
                _rows.Add(row.gameObject);
            }

            if (Selected == null)
            {
                _detail.text = "<color=#A6ADBF>항목을 선택하세요.</color>";
                _actionLabel.text = "-";
                _action.interactable = false;
            }
            else
            {
                _detail.text = DetailText(Selected);
                var (label, enabled) = ActionState(Selected);
                _actionLabel.text = label;
                _action.interactable = enabled;
            }
            OnRefresh();
        }

        protected void Select(EquipmentData item)
        {
            Selected = item;
            Message.text = "";
            Refresh();
        }

        void BuildUi()
        {
            var canvas = UiKit.CreateCanvas(transform, $"{GetType().Name}Canvas", 200);
            _root = canvas.gameObject;

            // 배경 어둡게 (뒤 클릭 차단)
            var dim = UiKit.Panel("Dim", _root.transform, Vector2.zero, Vector2.one, UiKit.Dim);
            dim.raycastTarget = true;

            var panel = UiKit.Panel("Panel", dim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), UiKit.PanelColor);
            var prt = (RectTransform)panel.transform;
            prt.sizeDelta = new Vector2(1400f, 820f);

            _title = UiKit.Text("Title", panel.transform, "", 44f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), null, new Vector2(30f, -90f), new Vector2(-190f, -14f));
            var closeButton = UiKit.Button("Close", panel.transform, "닫기 (Esc)", 24f,
                new Vector2(1f, 1f), new Vector2(1f, 1f), TextAlignmentOptions.Center, new Vector2(-180f, -76f), new Vector2(-24f, -20f));
            closeButton.onClick.AddListener(Close);

            // 왼쪽 목록
            _listContent = UiKit.ScrollList("List", panel.transform, new Vector2(0f, 0f), new Vector2(0.46f, 1f));
            var listRt = (RectTransform)_listContent.parent.parent;
            listRt.offsetMin = new Vector2(24f, 24f);
            listRt.offsetMax = new Vector2(-8f, -100f);

            // 오른쪽: 추가 영역 / 상세 / 실행 버튼
            var right = UiKit.Rect("Right", panel.transform, new Vector2(0.46f, 0f), Vector2.one, new Vector2(8f, 24f), new Vector2(-24f, -100f));
            Extra = UiKit.Panel("Extra", right, new Vector2(0f, 0.56f), Vector2.one, UiKit.BoxColor).rectTransform;
            var detailBox = UiKit.Panel("DetailBox", right, new Vector2(0f, 0.15f), new Vector2(1f, 0.54f), UiKit.BoxColor);
            _detail = UiKit.Text("Detail", detailBox.transform, "", 26f, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, null, new Vector2(18f, 12f), new Vector2(-18f, -12f));
            _detail.textWrappingMode = TextWrappingModes.Normal;
            Message = UiKit.Text("Message", right, "", 24f, TextAlignmentOptions.Center,
                new Vector2(0f, 0.09f), new Vector2(1f, 0.145f), UiKit.Accent);
            _action = UiKit.Button("Action", right, "", 32f, Vector2.zero, new Vector2(1f, 0.085f));
            _actionLabel = _action.GetComponentInChildren<TextMeshProUGUI>();
            _action.onClick.AddListener(() =>
            {
                if (Selected != null) OnAction(Selected);
            });

            BuildExtra(Extra);
        }
    }
}
