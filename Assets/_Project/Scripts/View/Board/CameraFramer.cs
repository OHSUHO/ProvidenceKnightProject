using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// HUD 의 BoardArea 사각형 안에 필드가 꼭 맞게 들어오도록 직교 카메라의 크기·위치를 맞춘다.
    /// 에디터에서도 동작하므로 씬 뷰에서 BoardArea 를 옮기거나 크기를 바꾸면 Game 뷰의 필드가 따라온다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFramer : MonoBehaviour
    {
        [SerializeField] BoardView board;
        [Tooltip("필드가 들어갈 HUD 영역 (Screen Space - Overlay 캔버스 아래의 빈 RectTransform)")]
        [SerializeField] RectTransform boardArea;
        [Tooltip("필드 둘레 여백 (월드 단위)")]
        [SerializeField, Min(0f)] float padding = 0.3f;

        Camera _cam;
        readonly Vector3[] _corners = new Vector3[4];

        void LateUpdate() => Frame();

        public void Frame()
        {
            if (board == null || boardArea == null) return;
            if (_cam == null) _cam = GetComponent<Camera>();
            var canvas = boardArea.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var root = (RectTransform)canvas.rootCanvas.transform;
            var rootRect = root.rect;
            if (rootRect.width <= 0f || rootRect.height <= 0f) return;

            // BoardArea 를 화면 비율(0~1) 사각형으로
            boardArea.GetWorldCorners(_corners);
            Vector2 min = root.InverseTransformPoint(_corners[0]);
            Vector2 max = root.InverseTransformPoint(_corners[2]);
            var vMin = new Vector2((min.x - rootRect.xMin) / rootRect.width, (min.y - rootRect.yMin) / rootRect.height);
            var vMax = new Vector2((max.x - rootRect.xMin) / rootRect.width, (max.y - rootRect.yMin) / rootRect.height);
            var vSize = vMax - vMin;
            if (vSize.x <= 0.01f || vSize.y <= 0.01f) return;

            float aspect = rootRect.width / rootRect.height;
            var target = board.WorldRect;
            var need = target.size + Vector2.one * (padding * 2f);

            // 뷰 높이 = 2·size, 뷰 너비 = 2·size·aspect. 영역 비율만큼만 쓸 수 있으므로 양쪽 모두 들어가는 size
            float size = Mathf.Max(need.y / (2f * vSize.y), need.x / (2f * vSize.x * aspect));
            var viewSize = new Vector2(2f * size * aspect, 2f * size);
            var areaCenter = (vMin + vMax) * 0.5f;
            var offset = Vector2.Scale(areaCenter - Vector2.one * 0.5f, viewSize);   // 화면 중심 → 영역 중심 (월드)

            // 값이 같으면 건드리지 않는다 (에디터에서 매 프레임 씬이 변경됨으로 표시되지 않게)
            var pos = target.center - offset;
            var newPos = new Vector3(pos.x, pos.y, transform.position.z);
            if (!_cam.orthographic) _cam.orthographic = true;
            if (Mathf.Abs(_cam.orthographicSize - size) > 1e-4f) _cam.orthographicSize = size;
            if ((transform.position - newPos).sqrMagnitude > 1e-8f) transform.position = newPos;
        }
    }
}
