using System;
using ProvidenceKnight.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProvidenceKnight.Input
{
    /// <summary>마우스 위치를 칸 좌표로 바꿔 hover / click 이벤트를 발행한다.</summary>
    public class BattleInputController : MonoBehaviour
    {
        [SerializeField] GridView gridView;
        [SerializeField] Camera cam;

        public event Action<Vector2Int?> TileHovered;
        public event Action<Vector2Int> TileClicked;
        public event Action Cancelled;

        Vector2Int? _hovered;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || gridView == null || cam == null) return;

            var screen = mouse.position.ReadValue();
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2Int? cell = !overUI && gridView.TryWorldToGrid(world, out var c) ? c : null;

            if (cell != _hovered)
            {
                _hovered = cell;
                TileHovered?.Invoke(cell);
            }

            if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
                TileClicked?.Invoke(cell.Value);

            if (mouse.rightButton.wasPressedThisFrame)
                Cancelled?.Invoke();
        }
    }
}
