using System;
using System.Collections.Generic;
using DG.Tweening;
using ProvidenceKnight.Data;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 유닛 하나의 표시. 아트가 없으면 색 네모 + 이름 첫 글자.
    /// 이동/공격/피격/사망 연출은 전역 AnimationQueue 에 쌓여 로직 이벤트 순서대로 하나씩 재생된다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public Unit Unit { get; private set; }

        const float MoveStepDuration = 0.13f;
        const float LungeOutDuration = 0.12f;
        const float LungeBackDuration = 0.13f;
        const float BarWidthFactor = 0.66f;
        const float BarHeightFactor = 0.10f;

        GridView _grid;
        float _cellSize;

        SpriteRenderer _body;
        TextMesh _label;

        SpriteRenderer _barBg, _barFill;
        TextMesh _hpText;
        float _barWidth;
        float _hpRatioShown = 1f;

        GameObject _blockBadge;
        SpriteRenderer _blockBadgeBg;
        TextMesh _blockBadgeText;

        SpriteRenderer _intentBg;
        TextMesh _intentText;

        Color _bodyBaseColor;


        public static UnitView Create(Unit unit, GridView grid, Transform parent)
        {
            var go = new GameObject($"Unit {unit.Data.displayName}#{unit.Id}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<UnitView>();
            view.Init(unit, grid);
            return view;
        }

        void Init(Unit unit, GridView grid)
        {
            Unit = unit;
            _grid = grid;
            _cellSize = grid.CellSize;
            transform.position = grid.GridToWorld(unit.Position);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sortingOrder = 10;
            if (unit.Data.sprite != null)
            {
                _body.sprite = unit.Data.sprite;
            }
            else
            {
                _body.sprite = SpriteFactory.Square;
                _body.color = unit.Data.color;
                bodyGo.transform.localScale = Vector3.one * (_cellSize * 0.62f);
                var initial = unit.Data.displayName.Length > 0 ? unit.Data.displayName.Substring(0, 1) : "?";
                _label = CreateText("Label", initial, new Vector3(0, 0.04f, 0) * _cellSize, 0.045f * _cellSize, Color.black, 12);
            }

            _bodyBaseColor = _body.color;
            BuildHpBar();
            BuildBlockBadge();
            BuildIntent();   // 적: 의도 배지 / 플레이어: 받을 피해 예고

            ApplyHpInstant(unit.Hp);
            ApplyBlockInstant(unit.Block);
        }

        // ---------------- 생성 ----------------

        void BuildHpBar()
        {
            _barWidth = BarWidthFactor * _cellSize;
            float height = BarHeightFactor * _cellSize;
            var barPos = new Vector3(0, -0.46f, 0) * _cellSize;

            _barBg = CreatePlate("BarBg", barPos, new Vector2(_barWidth, height), new Color(0.05f, 0.05f, 0.06f, 0.9f), 12);
            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = barPos;
            _barFill = fillGo.AddComponent<SpriteRenderer>();
            _barFill.sprite = SpriteFactory.Square;
            _barFill.sortingOrder = 13;

            _hpText = CreateText("HpText", "", new Vector3(0, -0.38f, 0) * _cellSize, 0.020f * _cellSize, Color.white, 14);
        }

        void BuildBlockBadge()
        {
            _blockBadge = new GameObject("BlockBadge");
            _blockBadge.transform.SetParent(transform, false);
            _blockBadge.transform.localPosition = new Vector3(0.30f, 0.32f, 0) * _cellSize;
            _blockBadgeBg = _blockBadge.AddComponent<SpriteRenderer>();
            _blockBadgeBg.sprite = SpriteFactory.Square;
            _blockBadgeBg.color = new Color(0.25f, 0.55f, 0.95f, 0.95f);
            _blockBadgeBg.transform.localScale = Vector3.one * (0.20f * _cellSize);
            _blockBadgeBg.sortingOrder = 15;
            _blockBadgeText = CreateText("Value", "", Vector3.zero, 0.017f * _cellSize, Color.white, 16);
            _blockBadgeText.transform.SetParent(_blockBadge.transform, false);
            _blockBadge.SetActive(false);
        }

        void BuildIntent()
        {
            var pos = new Vector3(0, 0.46f, 0) * _cellSize;
            float width = Unit.Team == Team.Enemy ? 0.92f : 1.35f;   // 플레이어 피해 예고는 글자가 더 김
            _intentBg = CreatePlate("IntentBg", pos, new Vector2(width * _cellSize, 0.30f * _cellSize), new Color(0.05f, 0.05f, 0.07f, 0.75f), 12);
            _intentBg.gameObject.SetActive(false);
            _intentText = CreateText("IntentText", "", pos, 0.019f * _cellSize, Color.white, 13);
            _intentText.gameObject.SetActive(false);
        }

        SpriteRenderer CreatePlate(string objName, Vector3 localPos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh CreateText(string objName, string text, Vector3 localPos, float charSize, Color color, int order)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = SpriteFactory.Font;
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.GetComponent<MeshRenderer>().sortingOrder = order;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.color = color;
            tm.text = text;
            return tm;
        }

        // ---------------- 상태 반영 ----------------

        static Color HpColor(float ratio) => ratio > 0.5f
            ? Color.Lerp(new Color(0.9f, 0.85f, 0.25f), new Color(0.35f, 0.85f, 0.4f), (ratio - 0.5f) * 2f)
            : Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.9f, 0.85f, 0.25f), ratio * 2f);

        void ApplyBarFill(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            float w = _barWidth * ratio;
            _barFill.transform.localScale = new Vector3(Mathf.Max(w, 0.0001f), BarHeightFactor * _cellSize, 1f);
            _barFill.transform.localPosition = new Vector3(-_barWidth * 0.5f + w * 0.5f, -0.46f * _cellSize, 0);
            _barFill.color = HpColor(ratio);
        }

        void ApplyHpInstant(int hp)
        {
            _hpRatioShown = Unit.MaxHp > 0 ? (float)hp / Unit.MaxHp : 0f;
            ApplyBarFill(_hpRatioShown);
            _hpText.text = $"{hp}/{Unit.MaxHp}";
        }

        void ApplyBlockInstant(int block)
        {
            bool show = block > 0;
            _blockBadge.SetActive(show);
            if (show) _blockBadgeText.text = block.ToString();
        }

        /// <summary>
        /// 피해 연출 (UnitDamaged 이벤트). hp/block 은 이벤트 시점의 값이라,
        /// 연출이 늦게 재생되어도 그 순간의 화면 값이 정확하다.
        /// </summary>
        public void PlayDamaged(int hpLoss, int hp, int block)
        {
            Enqueue(done =>
            {
                var targetRatio = Unit.MaxHp > 0 ? (float)hp / Unit.MaxHp : 0f;
                float startRatio = _hpRatioShown;
                var seq = DOTween.Sequence();

                if (hpLoss > 0)
                {
                    seq.Join(DOTween.To(() => startRatio, r => { _hpRatioShown = r; ApplyBarFill(r); }, targetRatio, 0.25f).SetEase(Ease.OutQuad));
                    seq.Join(_body.transform.DOShakePosition(0.22f, 0.08f * _cellSize, 12, 90));
                    var flash = DOTween.Sequence();
                    flash.Append(DOTween.To(() => _body.color, c => _body.color = c, Color.white, 0.06f));
                    flash.Append(DOTween.To(() => _body.color, c => _body.color = c, _bodyBaseColor, 0.14f));
                    seq.Join(flash);
                    FloatingText.Spawn(transform.position + Vector3.up * (0.1f * _cellSize), $"-{hpLoss}", new Color(1f, 0.35f, 0.3f), transform.parent, _cellSize);
                }
                else
                {
                    ApplyHpInstant(hp);
                    // 방어도로 전부 막았을 때도 맞은 느낌은 준다
                    seq.Join(_blockBadge.transform.DOShakePosition(0.2f, 0.05f * _cellSize, 14, 90));
                }
                _hpText.text = $"{hp}/{Unit.MaxHp}";
                ApplyBlockInstant(block);

                seq.OnComplete(() => done());
                seq.SetTarget(this);
            });
        }

        /// <summary>방어도 변화 연출 (BlockChanged 이벤트). 획득이면 뱃지 펀치 + "+N", 초기화면 조용히 숨김.</summary>
        public void PlayBlockChanged(int delta, int block)
        {
            Enqueue(done =>
            {
                if (delta <= 0)
                {
                    ApplyBlockInstant(block);
                    done();
                    return;
                }

                ApplyBlockInstant(block);
                var seq = DOTween.Sequence();
                seq.Join(_blockBadge.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f, 6));
                FloatingText.Spawn(transform.position + Vector3.right * (0.25f * _cellSize), $"+{delta}", new Color(0.4f, 0.75f, 1f), transform.parent, _cellSize);
                seq.OnComplete(() => done());
                seq.SetTarget(this);
            });
        }

        /// <summary>
        /// 적 의도 배지. 첫 줄 = 행동 순서 + 이동 칸 수, 둘째 줄 = 공격 피해.
        /// null 이면 숨김 (이번 적 턴에 행동하지 않음 — 예: 앞선 적이 플레이어를 쓰러뜨리는 계획).
        /// </summary>
        public void SetIntent(EnemyAction action)
        {
            if (action == null) { SetPlate(null, Color.white); return; }

            var order = OrderMark(action.Order);
            switch (action.Type)
            {
                case IntentType.Wait:
                    SetPlate($"{order} 대기", new Color(0.7f, 0.7f, 0.75f));
                    break;
                case IntentType.Move:
                    SetPlate($"{order} 이동 {action.Steps}", new Color(0.55f, 0.75f, 1f));
                    break;
                default:
                    SetPlate(action.Steps > 0 ? $"{order} 이동 {action.Steps}\n공격 {action.Damage}" : $"{order} 공격 {action.Damage}",
                        new Color(1f, 0.5f, 0.45f));
                    break;
            }
        }

        /// <summary>플레이어 머리 위 피해 예고. 적 계획 시뮬레이션 결과(예상 HP/방어도)로 계산한다.</summary>
        public void SetIncoming(EnemyTurnPlan plan)
        {
            if (plan == null || plan.Actions.Count == 0) { SetPlate(null, Color.white); return; }

            int hpLoss = Unit.Hp - plan.PredictedPlayerHp;
            int blockLoss = Unit.Block - plan.PredictedPlayerBlock;
            if (hpLoss <= 0 && blockLoss <= 0) { SetPlate(null, Color.white); return; }

            var first = blockLoss > 0 ? $"피해 {hpLoss + blockLoss} (방어 {blockLoss})" : $"피해 {hpLoss}";
            SetPlate($"{first}\nHP {Unit.Hp}→{plan.PredictedPlayerHp}", new Color(1f, 0.45f, 0.4f));
        }

        void SetPlate(string text, Color color)
        {
            if (_intentText == null) return;
            bool show = !string.IsNullOrEmpty(text);
            _intentBg.gameObject.SetActive(show);
            _intentText.gameObject.SetActive(show);
            if (!show) return;
            _intentText.text = text;
            _intentText.color = color;
        }

        internal static string OrderMark(int order) =>
            order >= 1 && order <= 20 ? ((char)('①' + order - 1)).ToString() : $"({order})";

        // ---------------- 연출 큐 ----------------

        /// <summary>전역 큐에 쌓는다 → 모든 유닛의 연출이 로직 이벤트 순서대로 하나씩 재생된다.</summary>
        void Enqueue(Action<Action> step) => AnimationQueue.Enqueue(this, step);

        /// <summary>경로를 따라 한 칸씩 이동하는 연출을 큐에 쌓는다.</summary>
        public void PlayMove(IReadOnlyList<Vector2Int> path)
        {
            Enqueue(done =>
            {
                if (path == null || path.Count == 0) { done(); return; }
                var seq = DOTween.Sequence();
                foreach (var cell in path)
                    seq.Append(transform.DOMove(_grid.GridToWorld(cell), MoveStepDuration).SetEase(Ease.Linear));
                seq.OnComplete(() => done());
                seq.SetTarget(this);
            });
        }

        /// <summary>공격 대상 쪽으로 살짝 달려들었다가 돌아오는 연출을 큐에 쌓는다.</summary>
        public void PlayAttackLunge(Vector3 towardWorldPos)
        {
            Enqueue(done =>
            {
                var home = transform.position;
                var dir = (towardWorldPos - home);
                dir.z = 0;
                if (dir.sqrMagnitude > 0.0001f) dir.Normalize(); else dir = Vector3.right;
                var lungeTo = home + dir * (0.32f * _cellSize);

                var seq = DOTween.Sequence();
                seq.Append(transform.DOMove(lungeTo, LungeOutDuration).SetEase(Ease.OutQuad));
                seq.Append(transform.DOMove(home, LungeBackDuration).SetEase(Ease.InQuad));
                seq.OnComplete(() => done());
                seq.SetTarget(this);
            });
        }

        /// <summary>사망 연출(축소+회전) 후 콜백. 콜백에서 GameObject 를 파괴하면 된다.</summary>
        public void PlayDeath(Action onComplete)
        {
            Enqueue(done =>
            {
                _intentBg?.gameObject.SetActive(false);
                _intentText?.gameObject.SetActive(false);

                var seq = DOTween.Sequence();
                seq.Join(transform.DOScale(Vector3.zero, 0.35f).SetEase(Ease.InBack));
                seq.Join(transform.DORotate(new Vector3(0, 0, 30f), 0.35f, RotateMode.FastBeyond360));
                seq.OnComplete(() =>
                {
                    done();
                    onComplete?.Invoke();
                });
                seq.SetTarget(this);
            });
        }

        void OnDestroy()
        {
            DOTween.Kill(this);              // 재생 중이던 트윈은 OnComplete 없이 죽으므로
            AnimationQueue.Abort(this);      // 전역 큐가 멈추지 않게 단계를 끝낸 것으로 처리
        }
    }
}
