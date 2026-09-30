using System;
using System.Collections.Generic;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 적 계획 중 "누가 어디로 가서 누구를 치는지"를 그린다 (P1 임시 코드 생성 버전, P3 에서 프리팹으로 교체).
    /// - 경로: 적 → 도착 칸까지 그 적의 색으로 된 선 + 화살촉
    /// - 도착 칸: 그 적의 반투명 잔상 (글자 + 행동 순서 번호)
    /// - 공격: 공격 위치(도착 칸 또는 제자리) → 공격 칸까지 빨간 굵은 화살표
    /// SetFocus 로 한 적만 선명하게, 나머지는 흐리게 할 수 있다.
    /// </summary>
    public class PlanOverlay
    {
        // 경로·잔상 채움은 유닛 아래. 잔상 테두리는 유닛 몸체(10) 위·HP 바/의도 배지(12~) 아래,
        // 순서 배지는 맨 위 → 지금 그 칸에 서 있는 유닛에 가려지지 않으면서 그 유닛의 HP 바도 가리지 않음
        const int OrderPath = 3, OrderGhost = 5, OrderGhostText = 6, OrderStrike = 7,
            OrderGhostFrame = 11, OrderBadge = 19, OrderBadgeText = 20;
        const float FrameSize = 0.86f;
        const float FrameWidth = 0.05f;
        const float BadgeSize = 0.2f;
        const float LineWidth = 0.07f;       // 셀 크기 대비
        const float StrikeWidth = 0.1f;
        const float ArrowSize = 0.26f;
        const float GhostSize = 0.5f;
        const float BodyHalf = 0.31f;        // UnitView 몸체 절반 크기
        const float UnfocusedAlpha = 0.18f;

        static readonly Color StrikeColor = new(1f, 0.25f, 0.2f, 0.95f);

        readonly GridView _grid;
        readonly Transform _root;
        readonly float _cell;

        readonly List<SpriteRenderer> _spritePool = new();
        readonly List<TextMesh> _textPool = new();
        int _spriteUsed, _textUsed;

        // 호버 강조용: 그린 요소마다 주인(적 Id)과 원래 색
        readonly List<(int actorId, SpriteRenderer sr, TextMesh tm, Color color)> _drawn = new();

        public PlanOverlay(GridView grid, Transform root)
        {
            _grid = grid;
            _root = root;
            _cell = grid.CellSize;
        }

        public void Show(EnemyTurnPlan plan, Func<int, Unit> unitLookup, float ghostAlpha)
        {
            Clear();
            if (plan == null) return;

            foreach (var action in plan.Actions)
            {
                var unit = unitLookup?.Invoke(action.ActorId);
                if (unit == null) continue;

                var c = unit.Data.color;
                var pathColor = Color.Lerp(c, Color.white, 0.3f);
                pathColor.a = 0.9f;

                if (action.Path.Count > 0)
                {
                    DrawPath(action, pathColor);
                    DrawGhost(action, unit, ghostAlpha);
                }

                if (action.Type == IntentType.Attack)
                {
                    float startInset = action.Path.Count > 0 ? GhostSize * 0.5f : BodyHalf;
                    foreach (var cell in action.AttackCells)
                        DrawStrike(action.ActorId, action.Destination, cell, startInset);
                }
            }
        }

        /// <summary>null 이면 전부 원래대로, 값이 있으면 그 적의 표시만 선명하게.</summary>
        public void SetFocus(int? actorId)
        {
            foreach (var (id, sr, tm, color) in _drawn)
            {
                var col = color;
                if (actorId.HasValue && id != actorId.Value) col.a *= UnfocusedAlpha;
                if (sr != null) sr.color = col;
                if (tm != null) tm.color = col;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _spriteUsed; i++) _spritePool[i].gameObject.SetActive(false);
            for (int i = 0; i < _textUsed; i++) _textPool[i].gameObject.SetActive(false);
            _spriteUsed = _textUsed = 0;
            _drawn.Clear();
        }

        // ---------------- 그리기 ----------------

        void DrawPath(EnemyAction action, Color color)
        {
            // 같은 방향으로 이어지는 칸은 한 선분으로 합친다
            var corners = new List<Vector3> { _grid.GridToWorld(action.From) };
            var prevDir = Vector2Int.zero;
            var prev = action.From;
            foreach (var cell in action.Path)
            {
                var dir = cell - prev;
                if (dir != prevDir && prevDir != Vector2Int.zero) corners.Add(_grid.GridToWorld(prev));
                prevDir = dir;
                prev = cell;
            }

            var dest = _grid.GridToWorld(action.Destination);
            var tip = dest - (dest - corners[corners.Count - 1]).normalized * (GhostSize * 0.5f * _cell);
            DrawPolylineArrow(action.ActorId, corners, tip, LineWidth, color, OrderPath);
        }

        void DrawStrike(int actorId, Vector2Int from, Vector2Int target, float startInset)
        {
            var a = _grid.GridToWorld(from);
            var b = _grid.GridToWorld(target);
            var dir = (b - a).normalized;
            var start = a + dir * (startInset * _cell);
            var tip = b - dir * (BodyHalf * _cell);
            DrawPolylineArrow(actorId, new List<Vector3> { start }, tip, StrikeWidth, StrikeColor, OrderStrike);
        }

        /// <summary>points 를 차례로 잇고 마지막 점 → tip 방향으로 화살촉을 붙인다 (tip 이 화살 끝).</summary>
        void DrawPolylineArrow(int actorId, List<Vector3> points, Vector3 tip, float width, Color color, int order)
        {
            var last = points[points.Count - 1];
            var dir = (tip - last).normalized;
            float arrowLen = ArrowSize * _cell;
            var arrowCenter = tip - dir * (arrowLen * 0.5f);

            var pts = new List<Vector3>(points) { arrowCenter };   // 선은 화살촉 가운데까지 (밑동과 겹쳐 이음매가 안 보임)
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var delta = pts[i + 1] - pts[i];
                if (delta.sqrMagnitude < 1e-6f) continue;
                var seg = TakeSprite(SpriteFactory.Square, order, actorId, color);
                seg.transform.position = (pts[i] + pts[i + 1]) * 0.5f;
                seg.transform.rotation = Quaternion.Euler(0, 0, Angle(delta));
                seg.transform.localScale = new Vector3(delta.magnitude + width * _cell, width * _cell, 1f);   // 모서리가 비지 않게 폭만큼 연장
            }

            var arrow = TakeSprite(SpriteFactory.Triangle, order, actorId, color);
            arrow.transform.position = arrowCenter;
            arrow.transform.rotation = Quaternion.Euler(0, 0, Angle(dir));
            arrow.transform.localScale = Vector3.one * arrowLen;
        }

        void DrawGhost(EnemyAction action, Unit unit, float alpha)
        {
            var pos = _grid.GridToWorld(action.Destination);
            var c = unit.Data.color;

            var body = TakeSprite(SpriteFactory.Square, OrderGhost, action.ActorId, new Color(c.r, c.g, c.b, alpha));
            body.transform.position = pos;
            body.transform.rotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * (GhostSize * _cell);

            var name = unit.Data.displayName;
            var letter = TakeText(action.ActorId, name.Length > 0 ? name.Substring(0, 1) : "?", new Color(0f, 0f, 0f, Mathf.Min(1f, alpha + 0.25f)), OrderGhostText);
            letter.transform.position = pos;
            letter.characterSize = 0.04f * _cell;

            // 테두리: 칸 가장자리를 따라 그 적의 색으로 (유닛 몸체 바깥이라 서 있는 유닛을 가리지 않음)
            var frameColor = new Color(c.r, c.g, c.b, 0.9f);
            float h = FrameSize * 0.5f * _cell;
            var corners = new[] { new Vector3(-h, -h), new Vector3(h, -h), new Vector3(h, h), new Vector3(-h, h) };
            for (int i = 0; i < 4; i++)
            {
                var a = pos + corners[i];
                var b = pos + corners[(i + 1) % 4];
                var seg = TakeSprite(SpriteFactory.Square, OrderGhostFrame, action.ActorId, frameColor);
                seg.transform.position = (a + b) * 0.5f;
                seg.transform.rotation = Quaternion.Euler(0, 0, Angle(b - a));
                seg.transform.localScale = new Vector3((b - a).magnitude + FrameWidth * _cell, FrameWidth * _cell, 1f);
            }

            // 순서 배지: 왼쪽 아래 모서리 (위쪽은 서 있는 유닛의 의도 배지와 겹치므로)
            var badgePos = pos + new Vector3(-h, -h, 0f);
            var badge = TakeSprite(SpriteFactory.Square, OrderBadge, action.ActorId, new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.95f));
            badge.transform.position = badgePos;
            badge.transform.rotation = Quaternion.identity;
            badge.transform.localScale = Vector3.one * (BadgeSize * _cell);

            var order = TakeText(action.ActorId, UnitView.OrderMark(action.Order), Color.white, OrderBadgeText);
            order.transform.position = badgePos;
            order.characterSize = 0.024f * _cell;
        }

        static float Angle(Vector3 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        // ---------------- 풀 ----------------

        SpriteRenderer TakeSprite(Sprite sprite, int order, int actorId, Color color)
        {
            if (_spriteUsed == _spritePool.Count)
            {
                var go = new GameObject("PlanSprite");
                go.transform.SetParent(_root, false);
                _spritePool.Add(go.AddComponent<SpriteRenderer>());
            }
            var sr = _spritePool[_spriteUsed++];
            sr.gameObject.SetActive(true);
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;
            _drawn.Add((actorId, sr, null, color));
            return sr;
        }

        TextMesh TakeText(int actorId, string text, Color color, int order)
        {
            if (_textUsed == _textPool.Count)
            {
                var go = new GameObject("PlanText");
                go.transform.SetParent(_root, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = SpriteFactory.Font;
                tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 64;
                _textPool.Add(tm);
            }
            var t = _textPool[_textUsed++];
            t.gameObject.SetActive(true);
            t.GetComponent<MeshRenderer>().sortingOrder = order;
            t.text = text;
            t.color = color;
            _drawn.Add((actorId, null, t, color));
            return t;
        }
    }
}
