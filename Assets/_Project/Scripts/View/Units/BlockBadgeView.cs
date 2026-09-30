using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>방어도 배지. 0 이면 숨긴다.</summary>
    public class BlockBadgeView : MonoBehaviour
    {
        [SerializeField] TMP_Text value;
        [SerializeField] float punchScale = 0.4f;
        [SerializeField] float shakeStrength = 0.05f;

        public void Set(int block)
        {
            gameObject.SetActive(block > 0);
            if (block > 0) value.text = block.ToString();
        }

        public Tween Punch() => transform.DOPunchScale(Vector3.one * punchScale, 0.25f, 6);

        /// <summary>방어도로 피해를 전부 막았을 때 흔들기.</summary>
        public Tween Shake() => transform.DOShakePosition(0.2f, shakeStrength, 14, 90);
    }
}
