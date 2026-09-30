using ProvidenceKnight.Run;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>화면 구석에 레벨·경험치·골드를 표시하고 레벨 업을 알린다. 씬에 하나 두면 스스로 UI 를 만든다.</summary>
    public class ProfileHud : MonoBehaviour
    {
        [SerializeField] Vector2 margin = new(16f, 16f);
        [SerializeField] float toastSeconds = 2f;

        TextMeshProUGUI _line, _toast;
        PlayerProfile _profile;
        float _toastUntil;

        void Start()
        {
            var canvasGo = new GameObject("ProfileHudCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            _line = MakeText(canvasGo.transform, "Line", 28, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-margin.x, -margin.y));
            _toast = MakeText(canvasGo.transform, "Toast", 48, TextAlignmentOptions.Center, new Vector2(0.5f, 0.75f), Vector2.zero);
            _toast.color = new Color(1f, 0.9f, 0.3f);
            _toast.gameObject.SetActive(false);

            _profile = ProfileSession.Current;
            _profile.Changed += Refresh;
            _profile.LeveledUp += OnLevelUp;
            Refresh();
        }

        void OnDestroy()
        {
            if (_profile == null) return;
            _profile.Changed -= Refresh;
            _profile.LeveledUp -= OnLevelUp;
        }

        void Update()
        {
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                _toast.gameObject.SetActive(false);
        }

        void OnLevelUp(int level)
        {
            _toast.text = $"LEVEL UP!  Lv {level}";
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + toastSeconds;
        }

        void Refresh()
        {
            _line.text = _profile.IsMaxLevel
                ? $"Lv {_profile.Level} (MAX)   Gold {_profile.Gold}"
                : $"Lv {_profile.Level}   EXP {_profile.Exp}/{_profile.ExpToNext}   Gold {_profile.Gold}";
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, float size, TextAlignmentOptions align, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900f, 80f);
            return t;
        }
    }
}
