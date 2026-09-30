using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 유닛 머리 위 말풍선. 적은 행동 의도(순서 배지 + "이동 2 / 공격 7"), 플레이어는 받을 피해 예고.
    /// 배경은 글자 크기에 맞춰 늘어난다 (Sliced SpriteRenderer).
    /// </summary>
    public class IntentBadgeView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer background;
        [SerializeField] TMP_Text text;
        [SerializeField] SpriteRenderer orderBadge;
        [SerializeField] TMP_Text orderText;
        [SerializeField] Vector2 padding = new(0.1f, 0.05f);
        [SerializeField] Vector2 minSize = new(0.4f, 0.26f);

        // 프리팹에서 기본 비활성. (Awake 에서 숨기면 Show 의 SetActive(true) 직후 Awake 가 돌아 다시 꺼진다)

        /// <param name="order">행동 순서 배지 번호 (0 이하면 숨김)</param>
        public void Show(string message, Color color, int order = 0)
        {
            gameObject.SetActive(true);
            text.text = message;
            text.color = color;

            bool hasOrder = order > 0;
            float badgeRadius = hasOrder ? orderBadge.transform.localScale.x * 0.5f : 0f;   // 원 스프라이트 = 1 유닛

            // 순서 배지는 말풍선 왼쪽 가장자리에 걸치므로, 그 반지름만큼 넓히고 글자를 오른쪽으로 민다
            var pref = text.GetPreferredValues(message);
            var size = Vector2.Max(minSize, pref + padding * 2f + new Vector2(badgeRadius, 0f));
            background.size = size;
            var tp = text.transform.localPosition;
            text.transform.localPosition = new Vector3(badgeRadius * 0.5f, tp.y, tp.z);

            orderBadge.gameObject.SetActive(hasOrder);
            if (hasOrder)
            {
                orderText.text = order.ToString();
                var p = orderBadge.transform.localPosition;
                orderBadge.transform.localPosition = new Vector3(background.transform.localPosition.x - size.x * 0.5f, p.y, p.z);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
