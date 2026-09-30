using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    public class TurnTests
    {
        static UnitData UnitDef(Team team, int hp, int moveRange = 0, int attackRange = 1, int attackDamage = 0)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            d.cards = TestUnits.Cards(moveRange, attackDamage, TargetShape.Adjacent, attackRange);
            return d;
        }

        // ---------------- EnemyPlanner.ChooseAction (순수 로직) ----------------

        [Test]
        public void Plan_AttacksWhenAlreadyInRange()
        {
            var grid = new GridMap(5, 5);
            var player = new Unit(0, UnitDef(Team.Player, 20), new Vector2Int(2, 2));
            var enemy = new Unit(1, UnitDef(Team.Enemy, 10, moveRange: 3, attackDamage: 6), new Vector2Int(2, 3));
            grid.PlaceUnit(player, player.Position);
            grid.PlaceUnit(enemy, enemy.Position);

            var intent = EnemyPlanner.ChooseAction(grid, enemy, player);

            Assert.AreEqual(IntentType.Attack, intent.Type);
            CollectionAssert.AreEqual(new[] { player.Position }, intent.AttackCells);
            Assert.AreEqual(6, intent.Damage);
            Assert.AreEqual(0, intent.Steps);
        }

        [Test]
        public void Plan_MovesCloser_WhenOutOfRange()
        {
            var grid = new GridMap(10, 3);
            var player = new Unit(0, UnitDef(Team.Player, 20), new Vector2Int(0, 1));
            var enemy = new Unit(1, UnitDef(Team.Enemy, 10, moveRange: 3), new Vector2Int(9, 1));
            grid.PlaceUnit(player, player.Position);
            grid.PlaceUnit(enemy, enemy.Position);

            var intent = EnemyPlanner.ChooseAction(grid, enemy, player);

            Assert.AreEqual(IntentType.Move, intent.Type);
            Assert.AreEqual(new Vector2Int(6, 1), intent.Destination);   // 3칸 이동 한도 그대로 소모
        }

        [Test]
        public void Plan_OnlyMoves_EvenIfAttackWouldBePossibleAfterMoving()
        {
            var grid = new GridMap(5, 1);
            var player = new Unit(0, UnitDef(Team.Player, 20), new Vector2Int(0, 0));
            var enemy = new Unit(1, UnitDef(Team.Enemy, 10, moveRange: 3, attackDamage: 5), new Vector2Int(4, 0));
            grid.PlaceUnit(player, player.Position);
            grid.PlaceUnit(enemy, enemy.Position);

            var intent = EnemyPlanner.ChooseAction(grid, enemy, player);

            Assert.AreEqual(IntentType.Move, intent.Type);
            Assert.AreEqual(new Vector2Int(1, 0), intent.Destination);
        }

        [Test]
        public void Plan_Waits_WhenBoxedIn()
        {
            // . # .
            // E # P   <- 벽 사이에 완전히 막힘
            // . # .
            var grid = new GridMap(3, 3);
            grid.SetBlocked(new Vector2Int(1, 0), true);
            grid.SetBlocked(new Vector2Int(1, 1), true);
            grid.SetBlocked(new Vector2Int(1, 2), true);
            var player = new Unit(0, UnitDef(Team.Player, 20), new Vector2Int(2, 1));
            var enemy = new Unit(1, UnitDef(Team.Enemy, 10, moveRange: 3), new Vector2Int(0, 1));
            grid.PlaceUnit(player, player.Position);
            grid.PlaceUnit(enemy, enemy.Position);

            var intent = EnemyPlanner.ChooseAction(grid, enemy, player);
            Assert.AreEqual(IntentType.Wait, intent.Type);
        }

        // ---------------- BattleController turn flow ----------------

        static StageData Stage(int width, int height, Vector2Int playerPos, params (UnitData def, Vector2Int pos)[] monsters)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = width;
            stage.height = height;
            stage.playerStart = playerPos;
            stage.monsters = monsters.Select(m => new MonsterSpawn { unit = m.def, position = m.pos }).ToList();
            return stage;
        }

        static BattleController MakeBattle(int width, int height, Vector2Int playerPos, UnitData enemyDef, Vector2Int enemyPos, int playerHp = 20)
        {
            var stage = Stage(width, height, playerPos, (enemyDef, enemyPos));
            var battle = BattleController.Create(stage, new List<CardData>(), 5, 3);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, playerHp));
            battle.StartPlayerTurn();
            return battle;
        }

        [Test]
        public void RefreshEnemyPlan_PopulatesOneEntryPerLivingEnemy()
        {
            var enemyDef = UnitDef(Team.Enemy, 10, moveRange: 2, attackDamage: 5);
            var battle = MakeBattle(6, 3, new Vector2Int(0, 1), enemyDef, new Vector2Int(5, 1));

            Assert.AreEqual(1, battle.State.EnemyPlan.Actions.Count);
            Assert.AreEqual(IntentType.Move, battle.State.EnemyPlan.Actions.Single().Type);
        }

        [Test]
        public void RunEnemyTurn_AdjacentEnemy_DamagesPlayer_ThroughBlock()
        {
            var enemyDef = UnitDef(Team.Enemy, 10, moveRange: 0, attackDamage: 6);
            var battle = MakeBattle(5, 1, new Vector2Int(0, 0), enemyDef, new Vector2Int(1, 0));
            BattleRules.GainBlock(battle.State, battle.State.Player, 4);
            battle.RefreshEnemyPlan();

            battle.RunEnemyTurn();

            Assert.AreEqual(20 - (6 - 4), battle.State.Player.Hp);
            Assert.AreEqual(0, battle.State.Player.Block);
        }

        [Test]
        public void RunEnemyTurn_ChasesOverMultipleTurns_ThenAttacks()
        {
            var enemyDef = UnitDef(Team.Enemy, 10, moveRange: 2, attackDamage: 5);
            var battle = MakeBattle(8, 1, new Vector2Int(0, 0), enemyDef, new Vector2Int(7, 0));
            var state = battle.State;
            var enemy = state.Enemies.Single();

            battle.RunEnemyTurn();
            Assert.AreEqual(new Vector2Int(5, 0), enemy.Position);
            Assert.AreEqual(20, state.Player.Hp);   // 아직 안 닿음

            battle.StartPlayerTurn();
            battle.RunEnemyTurn();
            Assert.AreEqual(new Vector2Int(3, 0), enemy.Position);
            Assert.AreEqual(20, state.Player.Hp);

            battle.StartPlayerTurn();
            battle.RunEnemyTurn();
            Assert.AreEqual(new Vector2Int(1, 0), enemy.Position);   // 사거리 안까지 이동만
            Assert.AreEqual(20, state.Player.Hp);

            battle.StartPlayerTurn();
            battle.RunEnemyTurn();
            Assert.AreEqual(15, state.Player.Hp);   // 다음 턴에 공격 카드
        }

        [Test]
        public void BattleEnded_Fires_OnEnemyWipe()
        {
            var enemyDef = UnitDef(Team.Enemy, 5, attackDamage: 1);
            var battle = MakeBattle(3, 1, new Vector2Int(0, 0), enemyDef, new Vector2Int(1, 0));

            BattleRules.DealDamage(battle.State, battle.State.Enemies.Single(), 999);

            Assert.IsTrue(battle.State.IsBattleOver);
            Assert.AreEqual(BattlePhase.Victory, battle.State.Phase);
            Assert.AreEqual(true, battle.Events.History.OfType<BattleEnded>().Single().PlayerWon);
            Assert.IsEmpty(battle.State.EnemyPlan.Actions);
        }

        [Test]
        public void BattleEnded_Fires_OnPlayerDeath_AndStopsFurtherEnemies()
        {
            var strongDef = UnitDef(Team.Enemy, 10, moveRange: 0, attackRange: 1, attackDamage: 999);
            var stage = Stage(3, 1, new Vector2Int(1, 0), (strongDef, new Vector2Int(0, 0)), (strongDef, new Vector2Int(2, 0)));
            var battle = BattleController.Create(stage, new List<CardData>(), 5, 3);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 10));
            battle.StartPlayerTurn();

            battle.RunEnemyTurn();

            Assert.IsTrue(battle.State.IsBattleOver);
            Assert.AreEqual(BattlePhase.Defeat, battle.State.Phase);
            Assert.AreEqual(false, battle.Events.History.OfType<BattleEnded>().Single().PlayerWon);
            Assert.IsTrue(battle.State.Player.IsDead);
            Assert.AreEqual(1, battle.Events.History.OfType<UnitAttacked>().Count(), "플레이어가 죽은 뒤엔 다음 적이 행동하지 않음");
        }

        [Test]
        public void CanSelectCard_False_AfterBattleOver()
        {
            var enemyDef = UnitDef(Team.Enemy, 1);
            var card = ScriptableObject.CreateInstance<CardData>();
            card.cardName = "Guard"; card.cost = 1; card.targeting = new TargetPattern(TargetShape.Self, 1, false);
            card.effects = new List<CardEffect> { new BlockEffect { amount = 5 } };
            var stage = Stage(3, 1, new Vector2Int(0, 0), (enemyDef, new Vector2Int(1, 0)));
            var battle = BattleController.Create(stage, new List<CardData> { card, card, card, card, card }, 5, 3);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 20));
            battle.StartPlayerTurn();

            BattleRules.DealDamage(battle.State, battle.State.Enemies.Single(), 999);
            Assert.IsTrue(battle.State.IsBattleOver);

            Assert.IsFalse(battle.CanSelectCard(0, out var reason));
            Assert.AreEqual("전투 종료", reason);
        }

        // ---------------- 턴 상태머신 ----------------

        [Test]
        public void Phase_FollowsTurnFlow()
        {
            var enemyDef = UnitDef(Team.Enemy, 10, moveRange: 1, attackDamage: 1);
            var stage = Stage(6, 1, new Vector2Int(0, 0), (enemyDef, new Vector2Int(5, 0)));
            var battle = BattleController.Create(stage, new List<CardData>(), 5, 3);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 20));
            Assert.AreEqual(BattlePhase.NotStarted, battle.State.Phase);

            battle.StartPlayerTurn();
            Assert.AreEqual(BattlePhase.PlayerTurn, battle.State.Phase);

            int from = battle.Events.History.Count;
            battle.EndTurn();
            var phases = battle.Events.History.Skip(from).OfType<PhaseChanged>().Select(p => p.Phase).ToList();
            CollectionAssert.AreEqual(new[] { BattlePhase.EnemyTurn, BattlePhase.PlayerTurn }, phases);
            Assert.AreEqual(2, battle.State.Turn);
        }

        [Test]
        public void CanSelectCard_False_OutsidePlayerTurn()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.targeting = new TargetPattern(TargetShape.Self, 1, false);
            card.effects = new List<CardEffect> { new BlockEffect { amount = 1 } };
            var stage = Stage(3, 1, new Vector2Int(0, 0), (UnitDef(Team.Enemy, 5), new Vector2Int(2, 0)));
            var battle = BattleController.Create(stage, new List<CardData> { card }, 5, 3);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 20));   // 아직 StartPlayerTurn 전

            Assert.IsFalse(battle.CanSelectCard(0, out var reason));
            Assert.AreEqual("플레이어 턴이 아님", reason);
        }
    }
}
