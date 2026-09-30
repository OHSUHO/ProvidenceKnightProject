using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Data;
using UnityEngine;
using UnityEngine.TestTools;
using Range = NUnit.Framework.RangeAttribute;

namespace ProvidenceKnight.Tests
{
    /// <summary>v1 적 계획 규칙: "보이는 대로 일어난다" 불변식, 순차 계획, 경로 거리, 행동 순서.</summary>
    public class EnemyPlanTests
    {
        // ---------------- helpers ----------------

        static UnitData UnitDef(Team team, int hp, int moveRange = 0, int attackDamage = 0, int priority = 0,
            TargetShape shape = TargetShape.Adjacent, int attackRange = 1)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.displayName = team == Team.Player ? "P" : "E";
            d.team = team;
            d.maxHp = hp;
            d.cards = TestUnits.Cards(moveRange, attackDamage, shape, attackRange);
            d.actionPriority = priority;
            return d;
        }

        static BattleController MakeBattle(int width, int height, Vector2Int playerPos,
            IEnumerable<(UnitData def, Vector2Int pos)> monsters, IEnumerable<Vector2Int> blocked = null,
            int seed = 0, int playerHp = 100)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = width;
            stage.height = height;
            stage.playerStart = playerPos;
            stage.blockedTiles = blocked?.ToList() ?? new List<Vector2Int>();
            stage.monsters = monsters.Select(m => new MonsterSpawn { unit = m.def, position = m.pos }).ToList();
            var battle = BattleController.Create(stage, new List<CardData>(), 5, 3, seed);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, playerHp));
            battle.StartPlayerTurn();
            return battle;
        }

        /// <summary>실제 적 턴에서 일어난 이동/공격을 이벤트 스트림에서 뽑아낸다.</summary>
        static List<string> RecordEnemyTurn(BattleController battle)
        {
            int from = battle.Events.History.Count;
            battle.RunEnemyTurn();
            var log = new List<string>();
            foreach (var e in battle.Events.History.Skip(from))
            {
                switch (e)
                {
                    case UnitMoved m: log.Add($"move {m.Unit.Id} {m.Path[m.Path.Count - 1]}"); break;
                    case UnitAttacked a: log.Add($"attack {a.Attacker.Id} {a.Cell} {a.Damage}"); break;
                }
            }
            return log;
        }

        /// <summary>계획을 같은 형식의 기록으로 풀어 쓴다.</summary>
        static List<string> Expected(EnemyTurnPlan plan)
        {
            var log = new List<string>();
            foreach (var a in plan.Actions)
            {
                if (a.Path.Count > 0) log.Add($"move {a.ActorId} {a.Destination}");
                if (a.Type == IntentType.Attack)
                    foreach (var c in a.AttackCells) log.Add($"attack {a.ActorId} {c} {a.Damage}");
            }
            return log;
        }

        /// <summary>전투가 끝나거나 maxTurns 가 될 때까지: 계획 확인 → 적 턴 실행 → 계획과 비교.</summary>
        static void AssertPlanMatchesExecution(BattleController battle, int maxTurns)
        {
            var state = battle.State;
            for (int turn = 0; turn < maxTurns && !state.IsBattleOver; turn++)
            {
                var plan = state.EnemyPlan;
                AssertPlanInsideThreatAreas(state, $"turn {turn + 1}");
                var expected = Expected(plan);
                var actual = RecordEnemyTurn(battle);

                CollectionAssert.AreEqual(expected, actual, $"turn {turn + 1}: 표시된 계획과 실제 실행이 다름");
                Assert.AreEqual(plan.PredictedPlayerHp, state.Player.Hp, $"turn {turn + 1}: 예고한 HP와 실제 HP가 다름");
                Assert.AreEqual(plan.PredictedPlayerBlock, state.Player.Block, $"turn {turn + 1}: 예고한 방어도와 실제가 다름");

                battle.StartPlayerTurn();
            }
        }

        /// <summary>호버로 보여주는 위험 지역(차례 시점 기준) 안에 계획된 도착 칸·공격 칸이 모두 들어가야 한다.</summary>
        static void AssertPlanInsideThreatAreas(BattleState state, string context)
        {
            foreach (var a in state.EnemyPlan.Actions)
            {
                var (move, attack) = EnemyPlanner.GetThreatAreaAtTurn(state, state.GetUnit(a.ActorId));
                if (a.Steps > 0)
                    Assert.Contains(a.Destination, move, $"{context}: {a} 의 도착 칸이 위험 지역(이동) 밖");
                foreach (var c in a.AttackCells)
                    Assert.IsTrue(attack.Contains(c), $"{context}: {a} 의 공격 칸 {c} 이 위험 지역(공격) 밖");
            }
        }

        static List<int> PlanOrder(BattleController battle) => battle.State.EnemyPlan.Actions.Select(a => a.ActorId).ToList();

        // ---------------- 불변식: 보이는 대로 일어난다 ----------------

        /// <summary>
        /// v0 버그 재현: 플레이어에게 붙을 수 있는 칸이 하나뿐인데 두 적이 모두 "공격" 의도를 띄우고,
        /// 실행해 보면 뒤의 적은 막혀서 공격하지 못했다.
        ///   # . . . .
        ///   P . . E .      P 옆에서 설 수 있는 칸은 (1,1) 하나
        ///   # . . E .
        /// </summary>
        [Test]
        public void TwoEnemies_OneOpenSlot_OnlyOneShowsAttack_AndThatIsWhatHappens([Range(0, 9)] int seed)
        {
            // 플레이어 옆 빈 칸은 (1,1) 하나뿐. 거기 선 몬스터만 공격하고, 뒤의 몬스터는 이동 카드를 쓴다 (한 턴에 카드 한 장)
            var goblin = UnitDef(Team.Enemy, 10, moveRange: 3, attackDamage: 5);
            var battle = MakeBattle(5, 3, new Vector2Int(0, 1),
                new[] { (goblin, new Vector2Int(1, 1)), (goblin, new Vector2Int(3, 1)) },
                blocked: new[] { new Vector2Int(0, 0), new Vector2Int(0, 2) }, seed: seed);

            var plan = battle.State.EnemyPlan;
            Assert.AreEqual(2, plan.Actions.Count);
            Assert.AreEqual(1, plan.Actions.Count(a => a.Type == IntentType.Attack), "공격 의도는 한 마리만");
            Assert.AreEqual(new Vector2Int(1, 1), plan.Actions.Single(a => a.Type == IntentType.Attack).From);
            Assert.AreEqual(1, plan.Actions.Count(a => a.Type == IntentType.Move), "나머지 한 마리는 이동");
            Assert.AreEqual(95, plan.PredictedPlayerHp);

            AssertPlanMatchesExecution(battle, 1);
        }

        [Test]
        public void Plan_ThenExecute_Matches_OnCrowdedStageWithWalls([Range(0, 9)] int seed)
        {
            // Stage_02 과 비슷한 배치: 9x6, 기둥 5개, 몬스터 5마리
            var goblin = UnitDef(Team.Enemy, 18, moveRange: 3, attackDamage: 7);
            var slime = UnitDef(Team.Enemy, 12, moveRange: 2, attackDamage: 4);
            var battle = MakeBattle(9, 6, new Vector2Int(4, 0),
                new[]
                {
                    (goblin, new Vector2Int(0, 5)), (goblin, new Vector2Int(8, 5)),
                    (slime, new Vector2Int(4, 5)), (slime, new Vector2Int(3, 4)), (slime, new Vector2Int(5, 4)),
                },
                blocked: new[] { new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(6, 2), new Vector2Int(6, 3), new Vector2Int(4, 3) },
                seed: seed, playerHp: 200);

            AssertPlanMatchesExecution(battle, 8);
        }

        [Test]
        public void Plan_ThenExecute_Matches_InNarrowCorridor([Range(0, 4)] int seed)
        {
            // 1줄 복도에 몬스터 4마리가 줄 서 있음 → 앞 칸이 비어야 뒤가 움직인다
            var e = UnitDef(Team.Enemy, 10, moveRange: 2, attackDamage: 3);
            var battle = MakeBattle(10, 1, new Vector2Int(0, 0),
                new[] { (e, new Vector2Int(4, 0)), (e, new Vector2Int(5, 0)), (e, new Vector2Int(7, 0)), (e, new Vector2Int(9, 0)) },
                seed: seed);

            AssertPlanMatchesExecution(battle, 6);
        }

        [Test]
        public void Plan_PredictsPlayerHpAndBlock()
        {
            var a = UnitDef(Team.Enemy, 10, attackDamage: 3);
            var b = UnitDef(Team.Enemy, 10, attackDamage: 4);
            var battle = MakeBattle(3, 1, new Vector2Int(1, 0), new[] { (a, new Vector2Int(0, 0)), (b, new Vector2Int(2, 0)) }, playerHp: 20);
            BattleRules.GainBlock(battle.State, battle.State.Player, 5);
            battle.RefreshEnemyPlan();

            Assert.AreEqual(20 - (3 + 4 - 5), battle.State.EnemyPlan.PredictedPlayerHp);
            Assert.AreEqual(0, battle.State.EnemyPlan.PredictedPlayerBlock);
        }

        [Test]
        public void Plan_StopsAtPlayerDeath_LaterEnemiesHaveNoAction()
        {
            var killer = UnitDef(Team.Enemy, 10, attackDamage: 999, priority: 1);
            var other = UnitDef(Team.Enemy, 10, attackDamage: 1, priority: 2);
            var battle = MakeBattle(3, 1, new Vector2Int(1, 0), new[] { (other, new Vector2Int(0, 0)), (killer, new Vector2Int(2, 0)) }, playerHp: 10);

            Assert.AreEqual(1, battle.State.EnemyPlan.Actions.Count);
            Assert.AreEqual(0, battle.State.EnemyPlan.PredictedPlayerHp);
            AssertPlanMatchesExecution(battle, 1);
            Assert.IsTrue(battle.State.Player.IsDead);
        }

        [Test]
        public void Plan_IsDeterministic()
        {
            var goblin = UnitDef(Team.Enemy, 18, moveRange: 3, attackDamage: 7);
            var battle = MakeBattle(9, 6, new Vector2Int(4, 0),
                new[] { (goblin, new Vector2Int(0, 5)), (goblin, new Vector2Int(8, 5)), (goblin, new Vector2Int(4, 5)) },
                blocked: new[] { new Vector2Int(4, 3) }, seed: 3);

            var a = EnemyPlanner.Plan(battle.State).Actions.Select(x => x.ToString()).ToList();
            var b = EnemyPlanner.Plan(battle.State).Actions.Select(x => x.ToString()).ToList();
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Plan_DoesNotMutateState_AndEmitsNoEvents()
        {
            var goblin = UnitDef(Team.Enemy, 18, moveRange: 3, attackDamage: 7);
            var battle = MakeBattle(6, 3, new Vector2Int(0, 1),
                new[] { (goblin, new Vector2Int(3, 1)), (goblin, new Vector2Int(5, 2)) }, playerHp: 10);
            var state = battle.State;
            var before = state.Units.Select(u => (u.Id, u.Position, u.Hp, u.Block)).ToList();
            int revision = state.Revision;
            int eventCount = battle.Events.History.Count;

            var plan = EnemyPlanner.Plan(state);   // 이 계획대로면 플레이어가 죽는다 (시뮬레이션 안에서만)

            Assert.Greater(plan.Actions.Count, 0);
            Assert.AreEqual(eventCount, battle.Events.History.Count, "계획(시뮬레이션)이 실제 전투에 이벤트를 흘렸음");
            CollectionAssert.AreEqual(before, state.Units.Select(u => (u.Id, u.Position, u.Hp, u.Block)).ToList());
            Assert.AreEqual(revision, state.Revision);
            Assert.IsFalse(state.IsBattleOver);
            foreach (var u in state.Units)
                Assert.AreSame(u, state.Grid.GetUnit(u.Position), "격자 점유가 바뀜");
        }

        [Test]
        public void RunEnemyTurn_WithStalePlan_Replans()
        {
            var e = UnitDef(Team.Enemy, 10, moveRange: 1, attackDamage: 5);
            var battle = MakeBattle(5, 1, new Vector2Int(0, 0), new[] { (e, new Vector2Int(4, 0)) });
            BattleRules.SpawnUnit(battle.State, e, new Vector2Int(1, 0));   // 계획 갱신 없이 상태만 바뀜

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("적 계획이 최신이 아님"));
            battle.RunEnemyTurn();

            Assert.AreEqual(95, battle.State.Player.Hp, "새로 생긴 적의 공격이 반영되어야 함");
        }

        // ---------------- 행동 선택 규칙 ----------------

        /// <summary>
        ///   . . . . .
        ///   E . . . .      E 는 벽 바로 위. 맨해튼 거리로는 어느 칸도 가까워지지 않지만,
        ///   # # # # .      실제 경로(오른쪽 틈으로 돌아가기)로는 오른쪽이 가까워진다.
        ///   P . . . .
        /// </summary>
        [Test]
        public void Chase_UsesPathDistance_NotManhattan()
        {
            var grid = new GridMap(5, 4);
            for (int x = 0; x < 4; x++) grid.SetBlocked(new Vector2Int(x, 1), true);
            var player = new Unit(0, UnitDef(Team.Player, 20), new Vector2Int(0, 0));
            var enemy = new Unit(1, UnitDef(Team.Enemy, 10, moveRange: 1), new Vector2Int(0, 2));
            grid.PlaceUnit(player, player.Position);
            grid.PlaceUnit(enemy, enemy.Position);

            var action = EnemyPlanner.ChooseAction(grid, enemy, player);

            Assert.AreEqual(IntentType.Move, action.Type);
            Assert.AreEqual(new Vector2Int(1, 2), action.Destination);
        }

        [Test]
        public void RangedLineAttack_IsBlockedByWall()
        {
            var archer = UnitDef(Team.Enemy, 10, moveRange: 0, attackDamage: 5, shape: TargetShape.Line, attackRange: 3);

            var open = MakeBattle(4, 1, new Vector2Int(0, 0), new[] { (archer, new Vector2Int(3, 0)) });
            Assert.AreEqual(IntentType.Attack, open.State.EnemyPlan.Actions.Single().Type);

            var walled = MakeBattle(4, 1, new Vector2Int(0, 0), new[] { (archer, new Vector2Int(3, 0)) }, blocked: new[] { new Vector2Int(1, 0) });
            Assert.AreEqual(IntentType.Wait, walled.State.EnemyPlan.Actions.Single().Type, "벽 너머로 공격하면 안 됨");
        }

        [Test]
        public void Move_PicksCellClosestToPlayer()
        {
            // 사거리 밖이면 이동 카드 한 장만 쓴다 (이동 후 공격은 다음 턴)
            var e = UnitDef(Team.Enemy, 10, moveRange: 4, attackDamage: 5);
            var battle = MakeBattle(5, 5, new Vector2Int(2, 2), new[] { (e, new Vector2Int(4, 2)) });
            var action = battle.State.EnemyPlan.Actions.Single();

            Assert.AreEqual(IntentType.Move, action.Type);
            Assert.AreEqual(new Vector2Int(3, 2), action.Destination);
            Assert.AreEqual(1, action.Steps);
        }

        [Test]
        public void ThreatArea_MoveCardCellsAndAttackCardFromCurrentPosition()
        {
            var grid = new GridMap(7, 1);
            var enemy = new Unit(1, UnitDef(Team.Enemy, 10, moveRange: 2, attackDamage: 1), new Vector2Int(3, 0));
            grid.PlaceUnit(enemy, enemy.Position);

            var (move, attack) = EnemyPlanner.GetThreatArea(grid, enemy);

            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 0), new Vector2Int(4, 0), new Vector2Int(1, 0), new Vector2Int(5, 0) }, move);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 0), new Vector2Int(4, 0) }, attack);   // 이동 후 공격은 못 하므로 제자리 인접 칸만
        }

        [Test]
        public void ThreatAreaAtTurn_AccountsForEarlierEnemiesMovingAway()
        {
            // P . . E1 E2  — E1(먼저)이 비켜 줘야 E2 가 움직일 수 있다
            var first = UnitDef(Team.Enemy, 10, moveRange: 2, attackDamage: 1, priority: 1);
            var second = UnitDef(Team.Enemy, 10, moveRange: 2, attackDamage: 1, priority: 2);
            var battle = MakeBattle(5, 1, new Vector2Int(0, 0), new[] { (first, new Vector2Int(3, 0)), (second, new Vector2Int(4, 0)) });
            var state = battle.State;
            var e2 = state.GetUnit(2);

            var (nowMove, _) = EnemyPlanner.GetThreatArea(state.Grid, e2);
            var (atTurnMove, _) = EnemyPlanner.GetThreatAreaAtTurn(state, e2);

            Assert.IsEmpty(nowMove, "지금 격자에선 E1 에 막혀 못 움직임");
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 0), new Vector2Int(2, 0) }, atTurnMove);
            Assert.Contains(state.EnemyPlan.For(2).Destination, atTurnMove);
        }

        // ---------------- 행동 순서 ----------------

        static (UnitData, Vector2Int) Far(UnitData def, int x) => (def, new Vector2Int(x, 4));

        [Test]
        public void Order_PrioritizedBeforeUnprioritized([Range(0, 9)] int seed)
        {
            var none = UnitDef(Team.Enemy, 10);
            var p2 = UnitDef(Team.Enemy, 10, priority: 2);
            var battle = MakeBattle(8, 5, new Vector2Int(0, 0), new[] { Far(none, 1), Far(p2, 3), Far(none, 5) }, seed: seed);

            var order = PlanOrder(battle);
            Assert.AreEqual(2, order[0], "우선순위가 지정된 몬스터(Id 2)가 먼저");
        }

        [Test]
        public void Order_LowerPriorityValueActsFirst([Range(0, 4)] int seed)
        {
            var battle = MakeBattle(8, 5, new Vector2Int(0, 0), new[]
            {
                Far(UnitDef(Team.Enemy, 10, priority: 3), 1),   // Id 1
                Far(UnitDef(Team.Enemy, 10, priority: 1), 3),   // Id 2
                Far(UnitDef(Team.Enemy, 10, priority: 2), 5),   // Id 3
            }, seed: seed);

            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, PlanOrder(battle));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, battle.State.EnemyPlan.Actions.Select(a => a.Order));
        }

        [Test]
        public void Order_TiesAreShuffledBySeed()
        {
            var none = UnitDef(Team.Enemy, 10);
            var monsters = new[] { Far(none, 1), Far(none, 3), Far(none, 5), Far(none, 7) };

            var orders = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
                orders.Add(string.Join(",", PlanOrder(MakeBattle(8, 5, new Vector2Int(0, 0), monsters, seed: seed))));

            Assert.Greater(orders.Count, 1, "같은 우선순위끼리의 순서가 무작위로 정해져야 함");
        }

        [Test]
        public void Order_IsFixedForWholeStage_EvenAfterDeaths([Range(0, 4)] int seed)
        {
            var none = UnitDef(Team.Enemy, 10, moveRange: 1);
            var battle = MakeBattle(10, 5, new Vector2Int(0, 0),
                new[] { Far(none, 2), Far(none, 4), Far(none, 6), Far(none, 8) }, seed: seed);
            var initial = PlanOrder(battle);

            for (int turn = 0; turn < 3; turn++)
            {
                battle.EndTurn();
                CollectionAssert.AreEqual(initial, PlanOrder(battle), $"turn {turn + 2}: 순서가 바뀜");
            }

            var victimId = initial[1];
            BattleRules.DealDamage(battle.State, battle.State.GetUnit(victimId), 999);
            battle.RefreshEnemyPlan();
            CollectionAssert.AreEqual(initial.Where(id => id != victimId).ToList(), PlanOrder(battle), "사망 후 남은 몬스터 순서가 유지되어야 함");
        }
    }
}
