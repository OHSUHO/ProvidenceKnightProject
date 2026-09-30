using UnityEngine;
using UnityEngine.InputSystem;

namespace ProvidenceKnight.Player
{
    /// <summary>
    /// 메트로배니아식 횡스크롤 이동. Rigidbody2D(Dynamic) + BoxCollider2D 필요. 중력은 직접 계산한다(gravityScale 은 0 으로 강제).
    /// 이동 능력은 <see cref="Abilities"/> 로 개별 잠금/해제.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerController2D : MonoBehaviour
    {
        public enum MoveState { Idle, Walk, Run, Crouch, CrouchWalk, Rise, Fall, Glide, WallSlide, WallClimb }

        [Header("능력 잠금/해제")]
        public PlayerAbilities Abilities = new();

        [Header("입력 (이동: A/D·←/→, 앉기: S/↓, 벽 오르기: W/↑)")]
        [SerializeField] Key jumpKey = Key.Space;
        [SerializeField] Key runKey = Key.LeftShift;

        [Header("이동")]
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float runSpeed = 8f;
        [SerializeField] float crouchSpeed = 2f;
        [SerializeField] float groundAccel = 60f;
        [SerializeField] float airAccel = 40f;

        [Header("점프 / 중력")]
        [SerializeField] float jumpSpeed = 12f;
        [SerializeField] float gravity = 35f;
        [SerializeField] float maxFallSpeed = 20f;
        [Tooltip("점프 키를 일찍 떼면 상승 속도를 이 비율로 줄인다")]
        [SerializeField, Range(0f, 1f)] float jumpCutFactor = 0.45f;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBuffer = 0.1f;
        [Tooltip("지상 점프 외에 공중에서 더 뛸 수 있는 횟수 (이단 점프 = 1)")]
        [SerializeField] int airJumps = 1;
        [SerializeField] float airJumpSpeed = 11f;

        [Header("벽")]
        [SerializeField] float wallSlideSpeed = 2.5f;
        [SerializeField] float wallClimbSpeed = 3.5f;
        [SerializeField] Vector2 wallJumpVelocity = new(7f, 12f);
        [Tooltip("벽 점프 직후 좌우 조작이 잠기는 시간")]
        [SerializeField] float wallJumpLock = 0.15f;
        [SerializeField] float wallCoyoteTime = 0.08f;

        [Header("활강")]
        [SerializeField] float glideFallSpeed = 2f;

        [Header("앉기")]
        [Tooltip("앉았을 때 콜라이더 높이 비율")]
        [SerializeField, Range(0.3f, 0.9f)] float crouchHeightRatio = 0.55f;

        [Header("충돌 검사")]
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] float probeDistance = 0.05f;

        [Header("표시 (임시 스프라이트)")]
        [SerializeField] SpriteRenderer sprite;

        public MoveState State { get; private set; }
        public bool Grounded { get; private set; }
        public int WallSide { get; private set; }
        public int Facing { get; private set; } = 1;
        public Vector2 Velocity => _rb.linearVelocity;

        Rigidbody2D _rb;
        BoxCollider2D _box;
        Vector2 _standSize, _standOffset;
        readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        readonly Collider2D[] _overlaps = new Collider2D[8];
        ContactFilter2D _filter;

        bool _jumpPressed, _jumpHeld;
        float _coyote, _wallCoyote, _buffer, _wallLock;
        int _wallCoyoteSide, _airJumpsLeft;
        bool _crouching, _airRun, _gliding, _jumpedUp;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _box = GetComponent<BoxCollider2D>();
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _box.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
            _standSize = _box.size;
            _standOffset = _box.offset;
            _filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundMask };
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            var jump = kb[jumpKey];
            if (jump.wasPressedThisFrame) _jumpPressed = true;
            _jumpHeld = jump.isPressed;
        }

        void FixedUpdate()
        {
            var kb = Keyboard.current;
            float dt = Time.fixedDeltaTime;
            var a = Abilities;

            // 입력
            int dir = 0;
            bool down = false, up = false, run = false;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1;
                down = kb.sKey.isPressed || kb.downArrowKey.isPressed;
                up = kb.wKey.isPressed || kb.upArrowKey.isPressed;
                run = kb[runKey].isPressed;
            }
            if (!a.move) dir = 0;
            if (_wallLock > 0f) _wallLock -= dt;

            // 접지 / 벽 검사
            var v = _rb.linearVelocity;
            Grounded = v.y <= 0.01f && Probe(Vector2.down);
            int wall = 0;
            if (!Grounded)
            {
                if (Probe(Vector2.left)) wall = -1;
                else if (Probe(Vector2.right)) wall = 1;
            }
            WallSide = wall;

            if (Grounded)
            {
                _coyote = coyoteTime;
                _airJumpsLeft = airJumps;
                _gliding = false;
                _jumpedUp = false;
            }
            else _coyote -= dt;

            if (wall != 0) { _wallCoyote = wallCoyoteTime; _wallCoyoteSide = wall; }
            else _wallCoyote -= dt;

            bool pressed = _jumpPressed;
            if (pressed) _buffer = jumpBuffer;
            else _buffer -= dt;
            _jumpPressed = false;

            // 앉기
            bool wantCrouch = a.crouch && Grounded && down;
            if (wantCrouch && !_crouching) SetCrouch(true);
            else if (_crouching && !wantCrouch && CanStand()) SetCrouch(false);

            // 수평 목표 속도
            float speed;
            if (_crouching) speed = a.crouchMove ? crouchSpeed : 0f;
            else if (Grounded) { _airRun = a.run && run; speed = _airRun ? runSpeed : walkSpeed; }
            else speed = _airRun ? runSpeed : walkSpeed;

            // 벽 붙기: 벽 쪽 키를 누르고 있을 때
            bool clinging = !Grounded && wall != 0 && dir == wall && _wallLock <= 0f
                            && (a.wallClimb || (a.wallSlide && v.y <= 0f));
            bool climbing = clinging && a.wallClimb;
            bool sliding = clinging && !climbing;
            if (clinging) _gliding = false;

            // 점프
            if (_buffer > 0f && a.jump)
            {
                if (_coyote > 0f && !(_crouching && !CanStand()))
                {
                    if (_crouching) SetCrouch(false);
                    v.y = jumpSpeed; _coyote = 0f; _buffer = 0f; _jumpedUp = true;
                }
                else if (_coyote <= 0f && a.wallJump && (wall != 0 || _wallCoyote > 0f))
                {
                    int side = wall != 0 ? wall : _wallCoyoteSide;
                    v = new Vector2(-side * wallJumpVelocity.x, wallJumpVelocity.y);
                    _wallLock = wallJumpLock; _wallCoyote = 0f; _buffer = 0f; _jumpedUp = true;
                    _airJumpsLeft = airJumps; _gliding = false; _airRun = true;
                    Facing = -side; clinging = climbing = sliding = false;
                }
                else if (_coyote <= 0f && a.doubleJump && _airJumpsLeft > 0)
                {
                    v.y = airJumpSpeed; _airJumpsLeft--; _buffer = 0f; _jumpedUp = true; _gliding = false;
                }
            }

            // 가변 점프 높이
            if (_jumpedUp && v.y > 0f && !_jumpHeld) { v.y *= jumpCutFactor; _jumpedUp = false; }
            if (v.y <= 0f) _jumpedUp = false;

            // 활강: 공중 낙하 중 점프 키를 눌러 시작, 떼면 종료
            if (a.glide && !Grounded && !clinging && pressed && _buffer > 0f && v.y < 0f) { _gliding = true; _buffer = 0f; }
            if (_gliding && (!_jumpHeld || Grounded || clinging)) _gliding = false;

            // 수평 속도
            if (_wallLock <= 0f)
            {
                float accel = Grounded ? groundAccel : airAccel;
                v.x = Mathf.MoveTowards(v.x, dir * speed, accel * dt);
                if (dir != 0) Facing = dir;
            }

            // 수직 속도
            if (Grounded && v.y <= 0f) v.y = 0f;
            else if (climbing) v.y = ((up ? 1 : 0) - (down ? 1 : 0)) * wallClimbSpeed;
            else if (sliding) v.y = Mathf.Max(v.y - gravity * dt, -wallSlideSpeed);
            else
            {
                v.y -= gravity * dt;
                float fall = _gliding ? glideFallSpeed : maxFallSpeed;
                if (v.y < -fall) v.y = Mathf.MoveTowards(v.y, -fall, 60f * dt);
            }
            if (clinging) v.x = 0f;

            _rb.linearVelocity = v;

            State = ResolveState(v, dir, climbing, sliding);
            UpdateVisual();
        }

        MoveState ResolveState(Vector2 v, int dir, bool climbing, bool sliding)
        {
            if (climbing) return MoveState.WallClimb;
            if (sliding) return MoveState.WallSlide;
            if (!Grounded)
                return _gliding ? MoveState.Glide : v.y > 0f ? MoveState.Rise : MoveState.Fall;
            if (_crouching) return Mathf.Abs(v.x) > 0.1f ? MoveState.CrouchWalk : MoveState.Crouch;
            if (Mathf.Abs(v.x) < 0.1f && dir == 0) return MoveState.Idle;
            return Mathf.Abs(v.x) > walkSpeed + 0.1f ? MoveState.Run : MoveState.Walk;
        }

        bool Probe(Vector2 direction) => _box.Cast(direction, _filter, _hits, probeDistance) > 0;

        bool CanStand()
        {
            var b = _box.bounds;
            float extra = _standSize.y * transform.lossyScale.y - b.size.y;
            if (extra <= 0.001f) return true;
            var center = new Vector2(b.center.x, b.max.y + extra * 0.5f);
            var size = new Vector2(b.size.x - 0.04f, extra - 0.04f);
            int n = Physics2D.OverlapBox(center, size, 0f, _filter, _overlaps);
            for (int i = 0; i < n; i++) if (_overlaps[i] != _box) return false;
            return true;
        }

        void SetCrouch(bool crouch)
        {
            _crouching = crouch;
            if (crouch)
            {
                float h = _standSize.y * crouchHeightRatio;
                _box.size = new Vector2(_standSize.x, h);
                _box.offset = new Vector2(_standOffset.x, _standOffset.y - (_standSize.y - h) * 0.5f);
            }
            else
            {
                _box.size = _standSize;
                _box.offset = _standOffset;
            }
        }

        // 임시 스프라이트(1x1 흰 사각형)를 콜라이더 크기에 맞춰 늘리고 상태별로 색을 바꾼다.
        void UpdateVisual()
        {
            if (sprite == null) return;
            sprite.flipX = Facing < 0;
            sprite.color = State switch
            {
                MoveState.Run => new Color(1f, 0.85f, 0.4f),
                MoveState.Crouch or MoveState.CrouchWalk => new Color(0.6f, 0.8f, 1f),
                MoveState.Glide => new Color(0.6f, 1f, 0.7f),
                MoveState.WallSlide or MoveState.WallClimb => new Color(1f, 0.6f, 0.9f),
                _ => Color.white,
            };
            var t = sprite.transform;
            t.localPosition = new Vector3(_box.offset.x, _box.offset.y, 0f);
            t.localScale = new Vector3(_box.size.x, _box.size.y, 1f);
        }
    }
}
