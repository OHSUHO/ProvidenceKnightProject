using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 칸 하나. 바닥 + 적 공격 예정(빨강) + 선택/위험 지역 하이라이트를 겹쳐 그린다.
    /// 색은 프리팹에서 조정한다 (하이라이트 색은 용도별로 HighlightLayer 가 정함).
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer floor;
        [SerializeField] SpriteRenderer threat;
        [SerializeField] SpriteRenderer highlight;

        [Header("Colors")]
        [SerializeField] Color colorA = new(0.22f, 0.24f, 0.28f);
        [SerializeField] Color colorB = new(0.26f, 0.28f, 0.33f);
        [SerializeField] Color blockedColor = new(0.08f, 0.08f, 0.1f);

        public Vector2Int Cell { get; private set; }
        public bool HasHighlight => highlight.enabled;

        public void Setup(Vector2Int cell, bool blocked)
        {
            Cell = cell;
            name = $"Tile {cell.x},{cell.y}";
            floor.color = blocked ? blockedColor : ((cell.x + cell.y) % 2 == 0 ? colorA : colorB);
            SetThreat(false);
            SetHighlight(null);
        }

        public void SetThreat(bool on) => threat.enabled = on;

        public void SetHighlight(Color? color)
        {
            highlight.enabled = color.HasValue;
            if (color.HasValue) highlight.color = color.Value;
        }
    }
}
