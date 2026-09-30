using System;
using System.Collections.Generic;
using DG.Tweening;
using ProvidenceKnight.Battle;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// BattleController 의 이벤트 스트림을 연출로 옮긴다. 로직은 한 번에 끝나지만 연출은 이벤트 순서대로 한 단계씩 재생된다
    /// (앞 적의 이동·공격이 끝나야 다음 적 시작 → 행동 순서대로 보이고 서로 겹치지 않음).
    /// 각 단계는 Tween 을 돌려주고, 그 Tween 이 끝나거나 죽으면(OnKill) 다음 단계로 넘어간다.
    /// 유닛이 연출 도중 파괴돼도 트윈이 죽으면서 큐가 계속 흐른다.
    /// </summary>
    public class BattleEventPlayer : MonoBehaviour
    {
        [SerializeField] BoardView board;
        [SerializeField] FloatingTextPool popups;
        [Tooltip("연출 재생 속도 배율 (플레이 테스트용)")]
        [SerializeField, Range(0.25f, 4f)] float playbackSpeed = 1f;

        /// <summary>재생 중이거나 재생할 단계가 남아 있음. 이 동안 입력을 잠그고 계획 표시를 숨긴다.</summary>
        public bool IsPlaying => _current != null || _steps.Count > 0;

        public IReadOnlyDictionary<Unit, UnitView> Views => _views;

        /// <summary>손패·에너지·턴 표시가 바뀜 (연출과 무관하게 즉시).</summary>
        public event Action ResourcesChanged;
        /// <summary>적 계획이 바뀜. 연출 중이면 EnemyPlanPresenter 가 끝날 때까지 기다렸다 표시한다.</summary>
        public event Action PlanChanged;
        /// <summary>대기 중인 연출을 모두 재생함.</summary>
        public event Action PlaybackFinished;
        /// <summary>전투 종료. 마지막 공격·사망 연출이 끝난 뒤에 알린다.</summary>
        public event Action<bool> BattleEnded;

        readonly Dictionary<Unit, UnitView> _views = new();
        readonly Queue<Func<Tween>> _steps = new();
        Tween _current;
        bool _running;
        BattleController _battle;

        public UnitView ViewOf(Unit unit) => unit != null && _views.TryGetValue(unit, out var v) ? v : null;

        public void Bind(BattleController battle)
        {
            Unbind();
            _battle = battle;
            _battle.Events.Emitted += OnEvent;
        }

        /// <summary>이전 전투 정리: 구독 해제, 남은 연출 취소, 유닛 표시 파괴.</summary>
        public void Unbind()
        {
            if (_battle != null) _battle.Events.Emitted -= OnEvent;
            _battle = null;

            _steps.Clear();
            var current = _current;
            _current = null;          // OnKill 에서 다음 단계로 넘어가지 않게 먼저 비움
            current?.Kill();

            foreach (var view in _views.Values)
                if (view != null) Destroy(view.gameObject);
            _views.Clear();
        }

        void OnDestroy() => Unbind();

        void OnEvent(BattleEvent e)
        {
            switch (e)
            {
                case UnitSpawned s:
                    var created = board.CreateUnitView(s.Unit.Data);
                    created.Init(s.Unit, board, popups);
                    _views[s.Unit] = created;
                    break;
                case UnitMoved m:
                    Enqueue(ViewOf(m.Unit), v => v.MoveAlong(m.Path));
                    break;
                case UnitAttacked a:
                    Enqueue(ViewOf(a.Attacker), v => v.Lunge(board.GridToWorld(a.Cell)));
                    break;
                case UnitDamaged d:
                    Enqueue(ViewOf(d.Unit), v => v.Damaged(d.HpLoss, d.Hp, d.Block));
                    break;
                case BlockChanged b:
                    Enqueue(ViewOf(b.Unit), v => v.BlockChanged(b.Delta, b.Block));
                    break;
                case NegateChanged n:
                    Enqueue(ViewOf(n.Unit), v => v.NegateChanged(n.Delta, n.Negate));
                    break;
                case UnitDied x:
                    if (_views.Remove(x.Unit, out var dead)) Enqueue(dead, v => v.Die());
                    break;
                case Battle.ResourcesChanged:
                    ResourcesChanged?.Invoke();
                    break;
                case EnemyPlanChanged:
                    PlanChanged?.Invoke();
                    break;
                case Battle.BattleEnded end:
                    var battle = _battle;
                    Enqueue(() =>
                    {
                        if (_battle == battle) BattleEnded?.Invoke(end.PlayerWon);   // 그사이 다음 전투로 넘어갔으면 무시
                        return null;
                    });
                    break;
            }
        }

        void Enqueue(UnitView view, Func<UnitView, Tween> step)
        {
            if (view == null) return;
            Enqueue(() => view != null ? step(view) : null);   // 차례가 오기 전에 파괴됐으면 건너뜀
        }

        /// <summary>연출 단계를 큐 끝에 넣는다. null 을 돌려주는 단계는 즉시 끝난 것으로 본다.</summary>
        public void Enqueue(Func<Tween> step)
        {
            _steps.Enqueue(step);
            RunNext();
        }

        void RunNext()
        {
            if (_running || _current != null) return;
            _running = true;
            try
            {
                while (_steps.Count > 0)
                {
                    var tween = _steps.Dequeue()();
                    if (tween == null || !tween.IsActive()) continue;

                    tween.timeScale = playbackSpeed;
                    _current = tween;
                    tween.OnKill(() =>
                    {
                        if (_current != tween) return;   // Unbind 로 취소됨
                        _current = null;
                        RunNext();
                    });
                    return;
                }
            }
            finally
            {
                _running = false;
            }
            PlaybackFinished?.Invoke();
        }
    }
}
