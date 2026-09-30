using System.Collections.Generic;
using NUnit.Framework;
using ProvidenceKnight.Data;
using ProvidenceKnight.Battle;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    public class GridMapTests
    {
        static UnitData MakeUnit(Team team, int hp = 10)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            return d;
        }

        [Test]
        public void InBounds_And_Blocked()
        {
            var g = new GridMap(3, 2);
            Assert.IsTrue(g.InBounds(new Vector2Int(2, 1)));
            Assert.IsFalse(g.InBounds(new Vector2Int(3, 0)));
            Assert.IsFalse(g.InBounds(new Vector2Int(0, -1)));

            g.SetBlocked(new Vector2Int(1, 1), true);
            Assert.IsFalse(g.IsWalkable(new Vector2Int(1, 1)));
        }

        [Test]
        public void Reachable_RespectsRange_Obstacles_AndUnits()
        {
            // . . . . .
            // . # . . .
            // P E . . .
            var g = new GridMap(5, 3);
            g.SetBlocked(new Vector2Int(1, 1), true);
            var player = new Unit(0, MakeUnit(Team.Player), Vector2Int.zero);
            var enemy = new Unit(1, MakeUnit(Team.Enemy), Vector2Int.zero);
            g.PlaceUnit(player, new Vector2Int(0, 0));
            g.PlaceUnit(enemy, new Vector2Int(1, 0));

            var reach = g.GetReachable(player.Position, 2);

            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 1), new Vector2Int(0, 2) },
                reach.Keys);
        }

        [Test]
        public void Reachable_Diagonal_WhenDirections8()
        {
            var g = new GridMap(3, 3) { MoveDirections = GridMap.Directions8 };
            var reach = g.GetReachable(new Vector2Int(1, 1), 1);
            Assert.AreEqual(8, reach.Count);
        }

        [Test]
        public void FindPath_GoesAroundObstacle()
        {
            // . . .
            // S # G
            var g = new GridMap(3, 2);
            g.SetBlocked(new Vector2Int(1, 0), true);
            var path = g.FindPath(new Vector2Int(0, 0), new Vector2Int(2, 0));
            Assert.IsNotNull(path);
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(new Vector2Int(2, 0), path[^1]);
        }

        [Test]
        public void MoveUnit_UpdatesOccupancy()
        {
            var g = new GridMap(3, 3);
            var u = new Unit(0, MakeUnit(Team.Player), Vector2Int.zero);
            g.PlaceUnit(u, new Vector2Int(0, 0));
            g.MoveUnit(u, new Vector2Int(2, 2));

            Assert.IsNull(g.GetUnit(new Vector2Int(0, 0)));
            Assert.AreEqual(u, g.GetUnit(new Vector2Int(2, 2)));
            Assert.AreEqual(new Vector2Int(2, 2), u.Position);
        }

        [Test]
        public void Damage_IsAbsorbedByBlockFirst()
        {
            var u = new Unit(0, MakeUnit(Team.Player, 10), Vector2Int.zero);
            u.AddBlock(5);
            Assert.AreEqual(3, u.TakeDamage(8));
            Assert.AreEqual(7, u.Hp);
            Assert.AreEqual(0, u.Block);
        }

        [Test]
        public void Battle_SpawnsFromStage()
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = 6;
            stage.height = 4;
            stage.playerStart = new Vector2Int(0, 1);
            stage.blockedTiles = new List<Vector2Int> { new(3, 3) };
            var slime = MakeUnit(Team.Enemy);
            stage.monsters = new List<MonsterSpawn>
            {
                new() { unit = slime, position = new Vector2Int(5, 0) },
                new() { unit = slime, position = new Vector2Int(5, 2) },
            };

            var battle = BattleController.Create(stage, null);
            var state = battle.State;
            battle.SpawnFromStage(stage, MakeUnit(Team.Player));

            Assert.AreEqual(6, state.Grid.Width);
            Assert.AreEqual(3, state.Units.Count);
            Assert.AreEqual(state.Player, state.Grid.GetUnit(new Vector2Int(0, 1)));
            Assert.IsTrue(state.Grid.IsBlocked(new Vector2Int(3, 3)));
        }

        [Test]
        public void Stage_Validate_DetectsOverlap()
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.playerStart = new Vector2Int(0, 0);
            stage.monsters = new List<MonsterSpawn> { new() { unit = MakeUnit(Team.Enemy), position = new Vector2Int(0, 0) } };
            Assert.IsNotEmpty(stage.Validate());
        }
    }
}
