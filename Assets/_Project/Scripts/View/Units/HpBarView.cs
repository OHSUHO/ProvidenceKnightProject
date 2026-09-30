using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>유닛 아래 HP 바. 채움은 배경 폭 기준으로 왼쪽에서부터 줄어든다 (둘 다 Sliced SpriteRenderer).</summary>
    public class HpBarView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer fill;
        [SerializeField] TMP_Text label;
        [SerializeField] Color high = new(0.35f, 0.85f, 0.4f);
        [SerializeField] Color mid = new(0.9f, 0.85f, 0.25f);
        [SerializeField] Color low = new(0.9f, 0.25f, 0.25f);

        float _shown = 1f;

        public void SetInstant(int hp, int maxHp)
        {
            ApplyRatio(Ratio(hp, maxHp));
            label.text = $"{hp}/{maxHp}";
        }

        /// <summary>UnitView 의 시퀀스 안에 넣어 쓰므로 타깃을 따로 두지 않는다 (중첩 트윈은 개별 Kill 불가).</summary>
        public Tween AnimateTo(int hp, int maxHp, float duration)
        {
            label.text = $"{hp}/{maxHp}";
            return DOTween.To(() => _shown, ApplyRatio, Ratio(hp, maxHp), duration).SetEase(Ease.OutQuad);
        }

        static float Ratio(int hp, int maxHp) => maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;

        void ApplyRatio(float ratio)
        {
            _shown = ratio;
            var full = background.size;
            float w = full.x * ratio;
            fill.size = new Vector2(Mathf.Max(w, 0.0001f), fill.size.y);
            var p = fill.transform.localPosition;
            fill.transform.localPosition = new Vector3(background.transform.localPosition.x - full.x * 0.5f + w * 0.5f, p.y, p.z);
            fill.color = ratio > 0.5f ? Color.Lerp(mid, high, (ratio - 0.5f) * 2f) : Color.Lerp(low, mid, ratio * 2f);
        }
    }
}
