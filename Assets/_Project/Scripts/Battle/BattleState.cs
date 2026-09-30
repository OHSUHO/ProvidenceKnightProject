using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Data;

namespace ProvidenceKnight.Battle
{
    public enum BattlePhase
    {
        NotStarted,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }

    /// <summary>
    /// 전투 한 판의 상태만 담는다 (규칙은 BattleRules, 진행은 BattleController).
    /// CloneForSimulation 으로 이벤트를 버리는 복사본을 만들어 적 계획·행동 미리보기에 쓴다.
    /// </summary>
    public class BattleState
    {
        public GridMap Grid { get; }
        public Unit Player { get; internal set; }
        public IReadOnlyList<Unit> Units => _units;
        public IEnumerable<Unit> Enemies => _units.Where(u => u.Team == Team.Enemy && !u.IsDead);

        /// <summary>적 턴 행동 순서 (스폰 시 정해진 ActionOrderKey 기준, 전투 끝까지 고정).</summary>
        public IEnumerable<Unit> EnemiesInActionOrder => Enemies.OrderBy(u => u.ActionOrder);

        public CardCycle Cards { get; }
        public int MaxEnergy { get; internal set; }   // GainMaxEnergy 효과로 전투 중 영구 증가 가능 (런 전체에 지속)
        public int Energy { get; internal set; }
        public int Turn { get; internal set; }
        public BattlePhase Phase { get; internal set; } = BattlePhase.NotStarted;
        public bool IsBattleOver => Phase is BattlePhase.Victory or BattlePhase.Defeat;

        /// <summary>화면에 보여주는 적 턴 계획. 적 턴에는 이 계획이 그대로 실행된다.</summary>
        public EnemyTurnPlan EnemyPlan { get; internal set; } = EnemyTurnPlan.Empty;

        /// <summary>격자/유닛 상태가 바뀔 때마다 증가. 계획이 최신인지 확인하는 데 쓴다.</summary>
        public int Revision { get; private set; }

        internal IBattleEventSink Events { get; }
        internal System.Random Rng { get; }

        readonly List<Unit> _units = new();
        int _nextUnitId;

        internal BattleState(GridMap grid, CardCycle cards, int maxEnergy, System.Random rng, IBattleEventSink events)
        {
            Grid = grid;
            Cards = cards;
            MaxEnergy = maxEnergy;
            Rng = rng;
            Events = events ?? NullEventSink.Instance;
        }

        public Unit GetUnit(int id) => _units.FirstOrDefault(u => u.Id == id);

        internal void Emit(BattleEvent e) => Events.Emit(e);
        internal void Touch() => Revision++;
        internal int NextUnitId() => _nextUnitId++;
        internal void AddUnit(Unit unit) => _units.Add(unit);

        /// <summary>
        /// 시뮬레이션용 깊은 복사본. 이벤트는 버려지므로 무엇을 해도 화면에 영향이 없다.
        /// </summary>
        public BattleState CloneForSimulation()
        {
            var grid = new GridMap(Grid.Width, Grid.Height) { MoveDirections = Grid.MoveDirections };
            foreach (var p in Grid.AllPositions())
                if (Grid.IsBlocked(p)) grid.SetBlocked(p, true);

            var copy = new BattleState(grid, Cards.Clone(), MaxEnergy, new System.Random(0), NullEventSink.Instance)
            {
                Energy = Energy,
                Turn = Turn,
                Phase = Phase,
                Revision = Revision,
                _nextUnitId = _nextUnitId,
            };

            foreach (var u in _units)
            {
                var c = u.Clone();
                copy._units.Add(c);
                if (!c.IsDead) grid.PlaceUnit(c, c.Position);
                if (u == Player) copy.Player = c;
            }
            return copy;
        }
    }
}
