using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>잠깐 떴다 사라지는 안내 문구 ("에너지 부족" 등).</summary>
    public class ToastView : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] float defaultSeconds = 1.5f;

        float _until;

        void Awake() => Clear();

        public void Show(string message, float seconds = -1f)
        {
            text.text = message;
            text.enabled = true;
            _until = Time.unscaledTime + (seconds > 0f ? seconds : defaultSeconds);
        }

        public void Clear()
        {
            text.text = "";
            text.enabled = false;
        }

        void Update()
        {
            if (text.enabled && Time.unscaledTime > _until) Clear();
        }
    }
}
