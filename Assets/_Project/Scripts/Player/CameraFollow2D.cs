using UnityEngine;

namespace ProvidenceKnight.Player
{
    /// <summary>플레이어를 부드럽게 따라가는 2D 카메라. 바라보는 쪽을 조금 앞서 보고, 영역(Bounds) 밖은 비추지 않는다.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerController2D player;

        [Header("추적")]
        [SerializeField] Vector2 offset = new(0f, 1f);
        [Tooltip("작을수록 빠르게 따라붙는다")]
        [SerializeField] Vector2 smoothTime = new(0.15f, 0.2f);
        [Tooltip("바라보는 방향으로 미리 보는 거리")]
        [SerializeField] float lookAhead = 2f;
        [SerializeField] float lookAheadSmooth = 0.3f;

        [Header("영역 제한 (월드 좌표)")]
        [SerializeField] bool useBounds = true;
        [SerializeField] Rect bounds = new(-18f, -5f, 36f, 16f);

        Camera _cam;
        Vector2 _vel;
        float _ahead, _aheadVel;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (player == null) player = FindFirstObjectByType<PlayerController2D>();
            if (target == null && player != null) target = player.transform;
        }

        void Start() => Snap();

        /// <summary>부드러움 없이 즉시 목표 위치로.</summary>
        public void Snap()
        {
            if (target == null) return;
            _ahead = 0f;
            var p = Clamp(Desired());
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            float aheadTarget = player != null ? player.Facing * lookAhead : 0f;
            _ahead = Mathf.SmoothDamp(_ahead, aheadTarget, ref _aheadVel, lookAheadSmooth, Mathf.Infinity, dt);

            var cur = (Vector2)transform.position;
            var want = Desired();
            var next = new Vector2(
                Mathf.SmoothDamp(cur.x, want.x, ref _vel.x, smoothTime.x, Mathf.Infinity, dt),
                Mathf.SmoothDamp(cur.y, want.y, ref _vel.y, smoothTime.y, Mathf.Infinity, dt));
            next = Clamp(next);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        Vector2 Desired() => (Vector2)target.position + offset + new Vector2(_ahead, 0f);

        Vector2 Clamp(Vector2 p)
        {
            if (!useBounds) return p;
            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
            // 영역이 화면보다 작으면 가운데에 고정
            p.x = bounds.width <= halfW * 2f ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW);
            p.y = bounds.height <= halfH * 2f ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH);
            return p;
        }

        void OnDrawGizmosSelected()
        {
            if (!useBounds) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
