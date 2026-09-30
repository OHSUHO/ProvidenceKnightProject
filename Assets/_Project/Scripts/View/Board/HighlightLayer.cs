using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Battle.Effects;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>칸 하이라이트(카드 대상 / 위험 지역 / 전체 위험 지역)와 마우스 호버 표시. 색은 인스펙터에서 조정.</summary>
    public class HighlightLayer : MonoBehaviour
    {
        [SerializeField] BoardView board;
        [SerializeField] SpriteRenderer hover;

        [Header("카드 대상")]
        [SerializeField] Color moveTarget = new(0.3f, 0.6f, 1f, 0.45f);
        [SerializeField] Color attackTarget = new(1f, 0.3f, 0.25f, 0.5f);
        [SerializeField] Color selfTarget = new(0.4f, 1f, 0.5f, 0.4f);

        [Header("위험 지역")]
        [SerializeField] Color dangerMove = new(0.3f, 0.55f, 1f, 0.3f);
        [SerializeField] Color dangerAttack = new(1f, 0.25f, 0.6f, 0.45f);
        [SerializeField] Color allThreats = new(1f, 0.25f, 0.6f, 0.3f);

        void Awake() => SetHover(null);

        public void ShowCardTargets(IEnumerable<Vector2Int> cells, EffectKind kind)
        {
            var color = kind switch
            {
                EffectKind.Attack => attackTarget,
                EffectKind.Move => moveTarget,
                _ => selfTarget,
            };
            Show(cells, color);
        }

        /// <summary>파랑 = 걸어갈 수 있는 칸, 분홍 = 그 너머로 공격만 닿는 칸 (겹치면 파랑).</summary>
        public void ShowDangerZone(ICollection<Vector2Int> moveCells, IEnumerable<Vector2Int> attackCells)
        {
            Show(moveCells, dangerMove);
            Add(attackCells.Where(c => !moveCells.Contains(c)), dangerAttack);
        }

        public void ShowAllThreats(IEnumerable<Vector2Int> cells) => Show(cells, allThreats);

        public void Clear()
        {
            foreach (var t in board.Tiles) t.SetHighlight(null);
        }

        public void SetHover(Vector2Int? cell)
        {
            if (hover == null) return;
            hover.enabled = cell.HasValue;
            if (cell.HasValue)
            {
                hover.transform.position = board.GridToWorld(cell.Value);
                hover.transform.localScale = Vector3.one * board.CellSize;
            }
        }

        void Show(IEnumerable<Vector2Int> cells, Color color)
        {
            Clear();
            Add(cells, color);
        }

        void Add(IEnumerable<Vector2Int> cells, Color color)
        {
            foreach (var c in cells) board.TileAt(c)?.SetHighlight(color);
        }
    }
}
