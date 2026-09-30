using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 적이 이동할 칸에 놓는 잔상: 반투명 몸체 + 글자, 칸 가장자리 테두리(유닛 몸체 위), 왼쪽 아래 순서 배지(맨 위).
    /// 그 칸에 지금 다른 유닛이 서 있어도(먼저 비켜 줄 적) 테두리와 배지는 보인다.
    /// </summary>
    public class PlanGhostView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] TMP_Text letter;
        [SerializeField] SpriteRenderer[] frame;
        [SerializeField] SpriteRenderer badge;
        [SerializeField] TMP_Text badgeText;
        [SerializeField, Range(0f, 1f)] float frameAlpha = 0.9f;

        readonly List<(SpriteRenderer sr, TMP_Text tm, Color color)> _parts = new();

        /// <param name="order">행동 순서 (0 이하면 배지 숨김 — 예: 미리보기의 플레이어 잔상)</param>
        public void Set(Color unitColor, string initial, int order, float alpha)
        {
            _parts.Clear();
            Part(body, new Color(unitColor.r, unitColor.g, unitColor.b, alpha));
            Part(letter, new Color(0f, 0f, 0f, Mathf.Min(1f, alpha + 0.25f)));
            letter.text = initial;
            foreach (var f in frame) Part(f, new Color(unitColor.r, unitColor.g, unitColor.b, frameAlpha));

            bool showBadge = order > 0;
            badge.gameObject.SetActive(showBadge);
            badgeText.gameObject.SetActive(showBadge);
            if (showBadge)
            {
                Part(badge, new Color(unitColor.r * 0.5f, unitColor.g * 0.5f, unitColor.b * 0.5f, 0.95f));
                Part(badgeText, Color.white);
                badgeText.text = order.ToString();
            }
        }

        /// <summary>알파에 곱할 값 (1 = 원래대로). 다른 적에 마우스를 올렸을 때 흐리게.</summary>
        public void SetDim(float alphaMultiplier)
        {
            foreach (var (sr, tm, color) in _parts)
            {
                var c = color;
                c.a *= alphaMultiplier;
                if (sr != null) sr.color = c;
                if (tm != null) tm.color = c;
            }
        }

        void Part(SpriteRenderer sr, Color c)
        {
            sr.color = c;
            _parts.Add((sr, null, c));
        }

        void Part(TMP_Text tm, Color c)
        {
            tm.color = c;
            _parts.Add((null, tm, c));
        }
    }
}
