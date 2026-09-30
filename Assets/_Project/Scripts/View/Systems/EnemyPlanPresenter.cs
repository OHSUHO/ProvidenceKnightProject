using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 적 계획을 화면에 그린다: 필드 오버레이(IntentOverlayView) + 적 의도 말풍선 + 플레이어 피해 예고.
    /// - 연출 재생 중에는 화면의 유닛 위치가 아직 로직을 따라오지 못했으므로 숨겼다가, 끝나면 표시한다.
    /// - 행동 미리보기(CardPreview)가 걸려 있으면 실제 계획 대신 "그 카드를 쓴 뒤의 계획"을 그린다.
    /// </summary>
    public class EnemyPlanPresenter : MonoBehaviour
    {
        [SerializeField] IntentOverlayView overlay;
        [SerializeField] BattleEventPlayer eventPlayer;

        /// <summary>연출이 끝나기를 기다리는 중 (이 동안 위험 지역도 보여주지 않는다).</summary>
        public bool IsPending { get; private set; }
        public bool IsPreviewing => _preview != null;

        BattleController _battle;
        CardPreview _preview;
        int? _focus;

        void Awake()
        {
            eventPlayer.PlanChanged += Refresh;
            eventPlayer.PlaybackFinished += OnPlaybackFinished;
        }

        void OnDestroy()
        {
            if (eventPlayer == null) return;
            eventPlayer.PlanChanged -= Refresh;
            eventPlayer.PlaybackFinished -= OnPlaybackFinished;
        }

        public void Bind(BattleController battle)
        {
            _battle = battle;
            _preview = null;
            _focus = null;
            IsPending = false;
            overlay.Clear();
        }

        public void ShowPreview(CardPreview preview)
        {
            if (preview == _preview) return;
            _preview = preview;
            Refresh();
        }

        public void ClearPreview()
        {
            if (_preview == null) return;
            _preview = null;
            Refresh();
        }

        /// <summary>한 적의 경로·잔상·공격만 선명하게 (null = 모두 보통).</summary>
        public void SetFocus(int? actorId)
        {
            _focus = actorId;
            overlay.SetFocus(actorId);
        }

        public void Refresh()
        {
            if (_battle == null) return;
            IsPending = eventPlayer.IsPlaying;
            if (IsPending) { Hide(); return; }

            var real = _battle.State;
            var state = _preview?.State ?? real;
            var plan = _preview?.Plan ?? real.EnemyPlan;

            foreach (var (unit, view) in eventPlayer.Views)
            {
                if (view == null) continue;
                var sim = state.GetUnit(unit.Id);
                if (unit.Team == Team.Enemy)
                {
                    if (_preview != null && sim.IsDead) view.ShowKilledPreview();
                    else view.ShowIntent(plan.For(unit.Id));
                }
                else if (unit == real.Player)
                {
                    view.ShowIncoming(sim.Hp, sim.Block, plan);
                }
            }

            // 미리보기에서 플레이어가 옮겨 가면 그 칸에 플레이어 잔상
            (Unit, Vector2Int)? playerGhost = null;
            if (_preview != null && state.Player != null && state.Player.Position != real.Player.Position)
                playerGhost = (real.Player, state.Player.Position);

            overlay.Show(plan, state.GetUnit, playerGhost);
            overlay.SetFocus(_focus);
        }

        void Hide()
        {
            overlay.Clear();
            foreach (var view in eventPlayer.Views.Values)
                if (view != null) view.HideBadge();
        }

        void OnPlaybackFinished()
        {
            if (IsPending) Refresh();
        }
    }
}
