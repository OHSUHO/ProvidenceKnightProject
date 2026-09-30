using System;
using ProvidenceKnight.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProvidenceKnight.Input
{
    /// <summary>마우스 위치를 칸 좌표로 바꿔 호버 / 클릭 / 취소(우클릭) 이벤트를 낸다. UI 위에서는 칸을 가리키지 않는다.</summary>
    public class BoardPointer : MonoBehaviour
    {
        [SerializeField] BoardView board;
        [SerializeField] Camera cam;

        public event Action<Vector2Int?> Hovered;
        public event Action<Vector2Int> Clicked;
        public event Action Cancelled;

        public Vector2Int? Current { get; private set; }

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || board == null || cam == null) return;

            var screen = mouse.position.ReadValue();
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2Int? cell = !overUI && board.TryWorldToGrid(world, out var c) ? c : null;

            if (cell != Current)
            {
                Current = cell;
                Hovered?.Invoke(cell);
            }

            if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
                Clicked?.Invoke(cell.Value);

            if (mouse.rightButton.wasPressedThisFrame)
                Cancelled?.Invoke();
        }
    }
}
