using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    /// <summary>몬스터 턴 시작 효과 (방어도 획득, 공격 무효화).</summary>
    public class TurnStartEffectTests
    {
        static CardData Slash(int dmg = 6)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.cardName = "Slash";
            c.cost = 1;
            c.targeting = new TargetPattern(TargetShape.Adjacent, 1, true);
            c.effects = new List<CardEffect> { new DamageEffect { amount = dmg } };
            return c;
        }

        static UnitData UnitDef(Team team, int hp, params CardEffect[] turnStart)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            d.turnStartEffects = turnStart.ToList();
            if (team == Team.Enemy) d.cards = TestUnits.Cards(0, 3);   // 인접하면 3 피해
            return d;
        }

        /// 5x3, 플레이어 (0,1) hp 20, 적 (1,1) hp 10 (이미 인접)
        static BattleController MakeBattle(UnitData enemy, int slashes = 3)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = 5;
            stage.height = 3;
            stage.playerStart = new Vector2Int(0, 1);
            stage.monsters = new List<MonsterSpawn> { new() { unit = enemy, position = new Vector2Int(1, 1) } };
            var deck = Enumerable.Range(0, Mathf.Max(slashes, 5)).Select(_ => Slash(6)).ToList();
            var battle = BattleController.Create(stage, deck, 5, 3, seed: 0);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 20));
            battle.StartPlayerTurn();
            return battle;
        }

        [Test]
        public void Block_IsGrantedAtTurnStart_AndAbsorbsPlayerAttack()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new BlockEffect { amount = 4 }));
            var enemy = battle.State.Enemies.Single();
            Assert.AreEqual(4, enemy.Block);

            Assert.IsTrue(battle.TryPlayCard(0, enemy.Position, out _));   // 6 피해 - 방어 4 = HP 2 감소
            Assert.AreEqual(8, enemy.Hp);
            Assert.AreEqual(0, enemy.Block);
        }

        [Test]
        public void Block_DoesNotAccumulateAcrossTurns()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new BlockEffect { amount = 4 }));
            var enemy = battle.State.Enemies.Single();

            battle.EndTurn();
            battle.EndTurn();

            Assert.AreEqual(4, enemy.Block);   // 매 턴 초기화 뒤 4 다시 획득
        }

        [Test]
        public void Negate_BlocksExactlyOneAttack()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new NegateAttackEffect()));
            var enemy = battle.State.Enemies.Single();
            Assert.AreEqual(1, enemy.Negate);

            battle.TryPlayCard(0, enemy.Position, out _);
            Assert.AreEqual(10, enemy.Hp);
            Assert.AreEqual(0, enemy.Negate);
            Assert.AreEqual(NegateChangedDelta(battle), -1);

            battle.TryPlayCard(0, enemy.Position, out _);
            Assert.AreEqual(4, enemy.Hp);
        }

        [Test]
        public void Negate_LeavesBlockUntouched()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new BlockEffect { amount = 4 }, new NegateAttackEffect()));
            var enemy = battle.State.Enemies.Single();

            battle.TryPlayCard(0, enemy.Position, out _);

            Assert.AreEqual(10, enemy.Hp);
            Assert.AreEqual(4, enemy.Block);
        }

        [Test]
        public void Negate_StaysAcrossTurns_ButRespectsMaxStacks()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new NegateAttackEffect { amount = 1, maxStacks = 2 }));
            var enemy = battle.State.Enemies.Single();
            Assert.AreEqual(1, enemy.Negate);

            battle.EndTurn();
            Assert.AreEqual(2, enemy.Negate);
            battle.EndTurn();
            Assert.AreEqual(2, enemy.Negate);   // 상한
        }

        [Test]
        public void Negate_UnlimitedWhenMaxStacksIsZero()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new NegateAttackEffect { amount = 1, maxStacks = 0 }));
            var enemy = battle.State.Enemies.Single();
            battle.EndTurn();
            battle.EndTurn();
            Assert.AreEqual(3, enemy.Negate);
        }

        [Test]
        public void Preview_ShowsNegatedAttack_AsNoDamage()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 5, new NegateAttackEffect()));
            var enemy = battle.State.Enemies.Single();

            var preview = battle.PreviewCard(0, enemy.Position);

            Assert.IsNotNull(preview);
            Assert.AreEqual(5, preview.State.GetUnit(enemy.Id).Hp);   // 6 피해면 죽었을 공격이 무효화됨
            Assert.AreEqual(1, enemy.Negate);                          // 실제 상태는 그대로
        }

        [Test]
        public void EffectsRunBeforeEnemyPlan_AndTurnStartEventsAreEmitted()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10, new BlockEffect { amount = 2 }, new NegateAttackEffect()));
            var kinds = battle.Events.History.Select(e => e.GetType()).ToList();

            int block = kinds.LastIndexOf(typeof(BlockChanged));
            int negate = kinds.LastIndexOf(typeof(NegateChanged));
            int plan = kinds.LastIndexOf(typeof(EnemyPlanChanged));
            Assert.Greater(block, -1);
            Assert.Greater(negate, block);
            Assert.Greater(plan, negate);
        }

        [Test]
        public void PlayerUnit_WithoutEffects_IsUnaffected()
        {
            var battle = MakeBattle(UnitDef(Team.Enemy, 10));
            Assert.AreEqual(0, battle.State.Player.Negate);
            Assert.AreEqual(0, battle.State.Enemies.Single().Block);
        }

        [Test]
        public void Validator_RejectsAttackOrMoveTurnStartEffects()
        {
            var bad = UnitDef(Team.Enemy, 10, new DamageEffect(), null);
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.units = new List<UnitData> { bad };

            var issues = DataValidator.Validate(db, null);

            Assert.IsTrue(issues.Any(i => i.Message.Contains("턴 시작 효과에는")));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("비어 있는 턴 시작 효과")));
        }

        static int NegateChangedDelta(BattleController battle) =>
            battle.Events.History.OfType<NegateChanged>().Last().Delta;
    }
}
