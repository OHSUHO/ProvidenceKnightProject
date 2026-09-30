using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.AI;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEngine;
using Range = NUnit.Framework.RangeAttribute;

namespace ProvidenceKnight.Tests
{
    /// <summary>행동 미리보기(BattleController.PreviewCard): 미리 보여준 적 계획 = 실제로 카드를 쓴 뒤의 적 계획.</summary>
    public class ActionPreviewTests
    {
        static CardData Card(string name, TargetShape shape, int range, bool requiresEnemy, params CardEffect[] effects)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.name = c.cardName = name;
            c.cost = 1;
            c.targeting = new TargetPattern(shape, range, requiresEnemy);
            c.effects = effects.ToList();
            return c;
        }

        static CardData Move(int range) => Card("Move", TargetShape.Walk, range, false, new MoveEffect());
        static CardData Slash(int dmg) => Card("Slash", TargetShape.Adjacent, 1, true, new DamageEffect { amount = dmg });
        static CardData Guard(int block) => Card("Guard", TargetShape.Self, 1, false, new BlockEffect { amount = block });

        static UnitData UnitDef(Team team, int hp, int moveRange = 0, int attackDamage = 0)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            d.cards = TestUnits.Cards(moveRange, attackDamage);
            return d;
        }

        /// 7x5, 플레이어 (1,2), 적 3마리 (이동 2, 공격 4). 장애물 (3,2)
        static BattleController MakeBattle(IEnumerable<CardData> hand, int seed)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = 7;
            stage.height = 5;
            stage.playerStart = new Vector2Int(1, 2);
            stage.blockedTiles = new List<Vector2Int> { new(3, 2) };
            var e = UnitDef(Team.Enemy, 6, moveRange: 2, attackDamage: 4);
            stage.monsters = new List<MonsterSpawn>
            {
                new() { unit = e, position = new Vector2Int(2, 2) },
                new() { unit = e, position = new Vector2Int(5, 1) },
                new() { unit = e, position = new Vector2Int(5, 3) },
            };
            var battle = BattleController.Create(stage, hand, 5, 3, seed);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 30));
            battle.StartPlayerTurn();
            return battle;
        }

        static List<string> Describe(EnemyTurnPlan plan) =>
            plan.Actions.Select(a => a.ToString()).Append($"hp {plan.PredictedPlayerHp} block {plan.PredictedPlayerBlock}").ToList();

        static string Snapshot(BattleState s) =>
            $"r{s.Revision} e{s.Energy} hand[{string.Join(",", s.Cards.Hand.Select(c => c.cardName))}] " +
            string.Join(" ", s.Units.Select(u => $"{u.Id}@{u.Position}:{u.Hp}/{u.Block}")) + " " + string.Join(";", Describe(s.EnemyPlan));

        [Test]
        public void Preview_MatchesPlanAfterPlaying_ForEveryTarget([Values(0, 1, 2)] int handIndex, [Range(0, 2)] int seed)
        {
            var hand = new[] { Move(2), Slash(6), Guard(5) };
            var probe = MakeBattle(hand, seed);
            var targets = probe.GetValidTargets(handIndex);
            Assert.IsNotEmpty(targets);

            foreach (var target in targets)
            {
                var battle = MakeBattle(hand, seed);   // 대상마다 새 전투 (카드를 실제로 써 봐야 하므로)
                var preview = battle.PreviewCard(handIndex, target);
                Assert.IsNotNull(preview, $"{target}");

                Assert.IsTrue(battle.TryPlayCard(handIndex, target, out var reason), reason);
                CollectionAssert.AreEqual(Describe(battle.State.EnemyPlan), Describe(preview.Plan), $"card {handIndex} → {target}");
                Assert.AreEqual(battle.State.Player.Position, preview.State.Player.Position);
                Assert.AreEqual(battle.State.Player.Block, preview.State.Player.Block);
            }
        }

        [Test]
        public void Preview_DoesNotChangeRealState()
        {
            var battle = MakeBattle(new[] { Move(2), Slash(6), Guard(5) }, 0);
            var before = Snapshot(battle.State);
            int events = battle.Events.History.Count;

            foreach (var i in new[] { 0, 1, 2 })
                foreach (var t in battle.GetValidTargets(i))
                    battle.PreviewCard(i, t);

            Assert.AreEqual(before, Snapshot(battle.State));
            Assert.AreEqual(events, battle.Events.History.Count, "미리보기는 이벤트를 내보내지 않음");
        }

        [Test]
        public void Preview_KillingEnemy_RemovesItsAction()
        {
            var battle = MakeBattle(new[] { Slash(6) }, 0);
            var victim = battle.State.Grid.GetUnit(new Vector2Int(2, 2));
            Assert.IsNotNull(battle.State.EnemyPlan.For(victim.Id));

            var preview = battle.PreviewCard(0, victim.Position);

            Assert.IsTrue(preview.State.GetUnit(victim.Id).IsDead);
            Assert.IsNull(preview.Plan.For(victim.Id));
        }

        [Test]
        public void Preview_InvalidTarget_ReturnsNull()
        {
            var battle = MakeBattle(new[] { Slash(6) }, 0);
            Assert.IsNull(battle.PreviewCard(0, new Vector2Int(6, 4)));
            Assert.IsNull(battle.PreviewCard(3, new Vector2Int(2, 2)));
        }
    }
}
