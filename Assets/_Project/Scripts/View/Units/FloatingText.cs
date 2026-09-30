using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>위로 떠오르며 사라지는 숫자 팝업 (피해 / 방어도 획득). FloatingTextPool 이 재사용한다.</summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] float rise = 0.55f;
        [SerializeField] float duration = 0.7f;
        [SerializeField] float fadeDelay = 0.15f;

        public void Play(Vector3 worldPos, string message, Color color, System.Action onFinished)
        {
            DOTween.Kill(this);
            transform.position = worldPos;
            text.text = message;
            text.color = color;

            var seq = DOTween.Sequence().SetTarget(this);
            seq.Append(transform.DOMoveY(worldPos.y + rise, duration).SetEase(Ease.OutCubic));
            seq.Join(DOTween.To(() => text.color, c => text.color = c, new Color(color.r, color.g, color.b, 0f), duration - fadeDelay)
                .SetEase(Ease.InQuad).SetDelay(fadeDelay));
            seq.OnComplete(() => onFinished?.Invoke());
        }

        void OnDestroy() => DOTween.Kill(this);
    }
}
