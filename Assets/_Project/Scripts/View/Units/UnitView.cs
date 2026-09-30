using System.Collections.Generic;
using DG.Tweening;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Data;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// Unit 프리팹의 루트. 몸체·HP 바·방어도 배지·의도 말풍선은 프리팹에 미리 배치되어 있고, 여기서는 값만 채운다.
    /// 연출 메서드는 Tween 을 돌려주기만 하고, 재생 순서는 BattleEventPlayer 가 정한다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] SpriteRenderer body;
        [SerializeField] TMP_Text initial;
        [SerializeField] HpBarView hpBar;
        [SerializeField] BlockBadgeView blockBadge;
        [SerializeField] IntentBadgeView intentBadge;

        [Header("아트가 없을 때 (UnitData.sprite 비어 있음)")]
        [SerializeField] Sprite placeholderSprite;
        [SerializeField] float placeholderScale = 0.62f;

        [Header("이동 / 공격")]
        [SerializeField] float moveStepDuration = 0.13f;
        [SerializeField] float lungeDistance = 0.32f;
        [SerializeField] float lungeOutDuration = 0.12f;
        [SerializeField] float lungeBackDuration = 0.13f;

        [Header("피격 / 사망")]
        [SerializeField] float hpBarDuration = 0.25f;
        [SerializeField] float shakeDuration = 0.22f;
        [SerializeField] float shakeStrength = 0.08f;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float deathDuration = 0.35f;
        [SerializeField] float deathSpin = 30f;

        [Header("팝업 / 말풍선 색")]
        [SerializeField] Vector3 damagePopupOffset = new(0f, 0.1f, 0f);
        [SerializeField] Vector3 blockPopupOffset = new(0.25f, 0f, 0f);
        [SerializeField] Color damageColor = new(1f, 0.35f, 0.3f);
        [SerializeField] Color blockColor = new(0.4f, 0.75f, 1f);
        [SerializeField] Color waitColor = new(0.7f, 0.7f, 0.75f);
        [SerializeField] Color moveColor = new(0.55f, 0.75f, 1f);
        [SerializeField] Color attackColor = new(1f, 0.5f, 0.45f);
        [SerializeField] Color incomingColor = new(1f, 0.45f, 0.4f);
        [SerializeField] Color killedColor = new(1f, 0.85f, 0.35f);

        [Header("공격 무효화 (배지는 코드로 만든다 — 방어도 배지의 왼쪽 대칭 자리)")]
        [SerializeField] Vector3 negateBadgeOffset = new(-0.38f, -0.06f, 0f);
        [SerializeField] float negateBadgeFontSize = 1.2f;
        [SerializeField] int negateBadgeSortingOrder = 17;
        [SerializeField] Color negateColor = new(1f, 0.85f, 0.35f);

        [Header("상태이상 (텍스트 줄은 코드로 만든다 — 유닛 칸 아래쪽)")]
        [SerializeField] Vector3 statusBadgeOffset = new(0f, -0.5f, 0f);
        [SerializeField] float statusBadgeFontSize = 1f;
        [SerializeField] int statusBadgeSortingOrder = 17;
        [SerializeField] Vector3 statusPopupOffset = new(0f, 0.45f, 0f);

        public Unit Unit { get; private set; }

        TextMeshPro _statusBadge;
        readonly StatusState[] _statuses = new StatusState[StatusRules.All.Length];   // 이벤트 시점 값 (연출이 늦어도 화면이 정확하도록)

        TextMeshPro _negateBadge;
        BoardView _board;
        FloatingTextPool _popups;
        Color _bodyColor;
        float Cell => _board != null ? _board.CellSize : 1f;

        public void Init(Unit unit, BoardView board, FloatingTextPool popups)
        {
            Unit = unit;
            _board = board;
            _popups = popups;
            name = $"Unit {unit.Data.displayName}#{unit.Id}";
            transform.position = board.GridToWorld(unit.Position);
            ApplyData(unit.Data);
            hpBar.SetInstant(unit.Hp, unit.MaxHp);
            blockBadge.Set(unit.Block);
            SetNegateBadge(unit.Negate);
            foreach (var type in StatusRules.All) _statuses[(int)type] = unit.GetStatus(type);
            RefreshStatusBadge();
            intentBadge.Hide();
        }

        /// <summary>에디터 스테이지 미리보기용 (로직 유닛 없이 데이터만).</summary>
        public void InitPreview(UnitData data, Vector3 worldPos)
        {
            name = $"Preview {data.displayName}";
            transform.position = worldPos;
            ApplyData(data);
            hpBar.SetInstant(data.maxHp, data.maxHp);
            blockBadge.Set(0);
            intentBadge.Hide();
        }

        void ApplyData(UnitData data)
        {
            bool hasArt = data.sprite != null;
            body.sprite = hasArt ? data.sprite : placeholderSprite;
            body.color = hasArt ? Color.white : data.color;
            body.transform.localScale = Vector3.one * (hasArt ? 1f : placeholderScale);
            initial.gameObject.SetActive(!hasArt);
            initial.text = data.displayName.Length > 0 ? data.displayName.Substring(0, 1) : "?";
            _bodyColor = body.color;
        }

        // ---------------- 말풍선 ----------------

        /// <summary>적 의도. null 이면 숨김 (이번 적 턴에 행동하지 않음 — 예: 앞선 적이 플레이어를 쓰러뜨리는 계획).</summary>
        public void ShowIntent(EnemyAction action)
        {
            if (action == null) { intentBadge.Hide(); return; }
            switch (action.Type)
            {
                case IntentType.Wait:
                    intentBadge.Show("대기", waitColor, action.Order);
                    break;
                case IntentType.Stunned:
                    intentBadge.Show("기절", waitColor, action.Order);
                    break;
                case IntentType.Move:
                    intentBadge.Show($"{action.Card.cardName} {action.Steps}", moveColor, action.Order);
                    break;
                default:
                    intentBadge.Show($"{action.Card.cardName} {action.Damage}", attackColor, action.Order);
                    break;
            }
        }

        /// <summary>행동 미리보기에서 이 카드로 쓰러지는 적.</summary>
        public void ShowKilledPreview() => intentBadge.Show("처치", killedColor);

        /// <summary>플레이어 머리 위 피해 예고. hp/block = 적 턴 직전 값, plan = 그 상태의 적 계획.</summary>
        public void ShowIncoming(int hp, int block, EnemyTurnPlan plan)
        {
            int hpLoss = hp - plan.PredictedPlayerHp;
            int blockLoss = block - plan.PredictedPlayerBlock;
            if (plan.Actions.Count == 0 || (hpLoss <= 0 && blockLoss <= 0)) { intentBadge.Hide(); return; }

            // 한 줄로 (자기 칸 안에 들어가게). 자세한 내역은 툴팁
            var loss = blockLoss > 0 ? $"-{hpLoss + blockLoss} (방어 {blockLoss})" : $"-{hpLoss}";
            intentBadge.Show($"{loss} → HP {plan.PredictedPlayerHp}", incomingColor);
        }

        public void HideBadge() => intentBadge.Hide();

        // ---------------- 연출 (Tween 반환, 재생은 BattleEventPlayer) ----------------

        public Tween MoveAlong(IReadOnlyList<Vector2Int> path)
        {
            if (path == null || path.Count == 0) return null;
            var seq = NewSequence();
            foreach (var cell in path)
                seq.Append(transform.DOMove(_board.GridToWorld(cell), moveStepDuration).SetEase(Ease.Linear));
            return seq;
        }

        /// <summary>공격 대상 쪽으로 살짝 달려들었다가 돌아온다.</summary>
        public Tween Lunge(Vector3 towardWorld)
        {
            var home = transform.position;
            var dir = towardWorld - home;
            dir.z = 0;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;

            var seq = NewSequence();
            seq.Append(transform.DOMove(home + dir * (lungeDistance * Cell), lungeOutDuration).SetEase(Ease.OutQuad));
            seq.Append(transform.DOMove(home, lungeBackDuration).SetEase(Ease.InQuad));
            return seq;
        }

        /// <summary>피해 (UnitDamaged). hp/block 은 이벤트 시점 값이라 연출이 늦게 재생되어도 화면 값이 정확하다.</summary>
        public Tween Damaged(int hpLoss, int hp, int block)
        {
            var seq = NewSequence();
            blockBadge.Set(block);
            if (hpLoss > 0)
            {
                seq.Join(hpBar.AnimateTo(hp, Unit.MaxHp, hpBarDuration));
                seq.Join(body.transform.DOShakePosition(shakeDuration, shakeStrength * Cell, 12, 90));
                var flash = DOTween.Sequence();
                flash.Append(DOTween.To(() => body.color, c => body.color = c, flashColor, 0.06f));
                flash.Append(DOTween.To(() => body.color, c => body.color = c, _bodyColor, 0.14f));
                seq.Join(flash);
                Popup(damagePopupOffset, $"-{hpLoss}", damageColor);
            }
            else
            {
                hpBar.SetInstant(hp, Unit.MaxHp);
                if (block > 0) seq.Join(blockBadge.Shake());   // 방어도로 전부 막았을 때도 맞은 느낌은 준다
            }
            return seq;
        }

        /// <summary>방어도 변화 (BlockChanged). 획득이면 배지 펀치 + "+N", 초기화면 조용히 숨김.</summary>
        public Tween BlockChanged(int delta, int block)
        {
            blockBadge.Set(block);
            if (delta <= 0) return null;
            Popup(blockPopupOffset, $"+{delta}", blockColor);
            return NewSequence().Join(blockBadge.Punch());
        }

        /// <summary>공격 무효화 횟수 변화 (NegateChanged). 획득이면 "+N", 공격을 막아 소모되면 "막음!".</summary>
        public Tween NegateChanged(int delta, int negate)
        {
            SetNegateBadge(negate);
            if (delta == 0) return null;
            Popup(blockPopupOffset, delta > 0 ? "무효화" : "막음!", negateColor);
            if (_negateBadge == null) return null;
            return NewSequence().Join(_negateBadge.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f, 6));
        }

        /// <summary>상태이상 변화 (StatusChanged). 새로 걸렸으면 이름 팝업, 틱·만료는 조용히 줄만 갱신.</summary>
        public Tween StatusChanged(StatusType type, int amount, int turns, bool applied)
        {
            _statuses[(int)type] = new StatusState(amount, turns);
            RefreshStatusBadge();
            if (!applied) return null;
            Popup(statusPopupOffset, StatusRules.Name(type), StatusColor(type));
            return _statusBadge == null ? null : NewSequence().Join(_statusBadge.transform.DOPunchScale(Vector3.one * 0.3f, 0.25f, 6));
        }

        static Color StatusColor(StatusType type) => type switch
        {
            StatusType.Bleed => new Color(0.85f, 0.2f, 0.25f),
            StatusType.Burn => new Color(1f, 0.6f, 0.2f),
            StatusType.Poison => new Color(0.55f, 0.85f, 0.3f),
            StatusType.Stun => new Color(1f, 0.92f, 0.35f),
            StatusType.Freeze => new Color(0.55f, 0.9f, 1f),
            StatusType.Darkness => new Color(0.7f, 0.5f, 0.95f),
            _ => Color.white
        };

        void RefreshStatusBadge()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var type in StatusRules.All)
            {
                var s = _statuses[(int)type];
                if (!StatusRules.IsActive(type, s)) continue;
                var c = StatusColor(type);
                if (sb.Length > 0) sb.Append(' ');
                // 이름 + 강도(피해형) / 남은 턴 (독은 강도만)
                var value = StatusRules.IsDamageOverTime(type) ? s.Amount : s.Turns;
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{StatusRules.Name(type)}{value}</color>");
            }

            if (_statusBadge == null)
            {
                if (sb.Length == 0) return;
                var go = new GameObject("StatusBadge");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = statusBadgeOffset;
                _statusBadge = go.AddComponent<TextMeshPro>();
                _statusBadge.font = initial.font;
                _statusBadge.fontSize = statusBadgeFontSize;
                _statusBadge.alignment = TextAlignmentOptions.Center;
                _statusBadge.enableWordWrapping = false;
                _statusBadge.sortingOrder = statusBadgeSortingOrder;
                _statusBadge.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f);
            }
            _statusBadge.gameObject.SetActive(sb.Length > 0);
            _statusBadge.text = sb.ToString();
        }

        void SetNegateBadge(int negate)
        {
            if (_negateBadge == null)
            {
                if (negate <= 0) return;
                var go = new GameObject("NegateBadge");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = negateBadgeOffset;
                _negateBadge = go.AddComponent<TextMeshPro>();
                _negateBadge.font = initial.font;
                _negateBadge.fontSize = negateBadgeFontSize;
                _negateBadge.alignment = TextAlignmentOptions.Center;
                _negateBadge.color = negateColor;
                _negateBadge.sortingOrder = negateBadgeSortingOrder;
                _negateBadge.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
            }
            _negateBadge.gameObject.SetActive(negate > 0);
            if (negate > 0) _negateBadge.text = negate > 1 ? $"무효x{negate}" : "무효";
        }

        /// <summary>사망: 축소 + 회전 후 GameObject 파괴.</summary>
        public Tween Die()
        {
            intentBadge.Hide();
            var seq = NewSequence();
            seq.Join(transform.DOScale(Vector3.zero, deathDuration).SetEase(Ease.InBack));
            seq.Join(transform.DORotate(new Vector3(0, 0, deathSpin), deathDuration, RotateMode.FastBeyond360));
            seq.OnComplete(() => Destroy(gameObject));
            return seq;
        }

        Sequence NewSequence() => DOTween.Sequence().SetTarget(this);

        void Popup(Vector3 offset, string text, Color color)
        {
            if (_popups != null) _popups.Spawn(transform.position + offset * Cell, text, color);
        }

        /// <summary>파괴되면 재생 중이던 트윈을 죽인다 → OnKill 로 BattleEventPlayer 큐가 다음 단계로 넘어감.</summary>
        void OnDestroy() => DOTween.Kill(this);
    }
}
