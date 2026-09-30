using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProvidenceKnight.Tests
{
    public class ProfileTests
    {
        static ProgressionData Curve(int maxLevel = 5)
        {
            var p = ScriptableObject.CreateInstance<ProgressionData>();
            p.maxLevel = maxLevel;
            p.baseExp = 10;
            p.growth = 1f;   // 매 레벨 10
            return p;
        }

        static EquipmentData Equip(string id, EquipSlot slot, int reqLevel, StatBlock stats)
        {
            var e = ScriptableObject.CreateInstance<EquipmentData>();
            e.EditorSetId(id);
            e.displayName = id;
            e.slot = slot;
            e.requiredLevel = reqLevel;
            e.stats = stats;
            return e;
        }

        // ---------------- 경험치 · 골드 ----------------

        [Test]
        public void Exp_LevelsUp_AndCarriesOverflow()
        {
            var p = new PlayerProfile(Curve());
            int levels = p.AddExp(25);

            Assert.AreEqual(2, levels);
            Assert.AreEqual(3, p.Level);
            Assert.AreEqual(5, p.Exp);
        }

        [Test]
        public void Exp_AtMaxLevel_IsIgnored()
        {
            var p = new PlayerProfile(Curve(maxLevel: 2));
            p.AddExp(500);

            Assert.AreEqual(2, p.Level);
            Assert.IsTrue(p.IsMaxLevel);
            Assert.AreEqual(0, p.AddExp(50));
            Assert.AreEqual(0, p.Exp);
        }

        [Test]
        public void Gold_SpendFailsWhenShort_AndDoesNotChangeBalance()
        {
            var p = new PlayerProfile(Curve());
            p.AddGold(30);

            Assert.IsFalse(p.TrySpendGold(31));
            Assert.AreEqual(30, p.Gold);
            Assert.IsTrue(p.TrySpendGold(30));
            Assert.AreEqual(0, p.Gold);
        }

        // ---------------- 장비 ----------------

        [Test]
        public void Equip_RequiresOwnershipAndLevel()
        {
            var p = new PlayerProfile(Curve());
            var sword = Equip("sword", EquipSlot.Weapon, 2, new StatBlock { attack = 2 });

            Assert.AreEqual(EquipCheck.NotOwned, p.TryEquip(sword));
            p.AddOwned(sword);
            Assert.AreEqual(EquipCheck.LevelTooLow, p.TryEquip(sword));
            p.AddExp(10);
            Assert.AreEqual(EquipCheck.Ok, p.TryEquip(sword));
            Assert.AreSame(sword, p.GetEquipped(EquipSlot.Weapon));
        }

        [Test]
        public void Equip_SameSlotReplaces_AndStatsSumOnlyEquipped()
        {
            var p = new PlayerProfile(Curve());
            var a = Equip("a", EquipSlot.Weapon, 1, new StatBlock { attack = 1, maxHp = 5 });
            var b = Equip("b", EquipSlot.Weapon, 1, new StatBlock { attack = 3 });
            var c = Equip("c", EquipSlot.Body, 1, new StatBlock { defense = 2, handSize = 1, maxEnergy = 1 });
            foreach (var e in new[] { a, b, c }) p.AddOwned(e);

            p.TryEquip(a);
            p.TryEquip(c);
            p.TryEquip(b);   // a 가 벗겨짐

            var s = p.Stats;
            Assert.AreEqual(3, s.attack);
            Assert.AreEqual(0, s.maxHp);
            Assert.AreEqual(2, s.defense);
            Assert.AreEqual(1, s.handSize);
            Assert.AreEqual(1, s.maxEnergy);

            p.Unequip(EquipSlot.Body);
            Assert.AreEqual(0, p.Stats.defense);
        }

        // ---------------- 저장 ----------------

        [Test]
        public void Save_RoundTrip_KeepsProgressEquipmentAndCollected()
        {
            var prog = Curve();
            var sword = Equip("equip_sword", EquipSlot.Weapon, 1, new StatBlock { attack = 2 });
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.equipment = new List<EquipmentData> { sword };

            var p = new PlayerProfile(prog);
            p.AddExp(12);
            p.AddGold(77);
            p.AddOwned(sword);
            p.TryEquip(sword);
            p.MarkCollected("Field:coin1");

            var json = JsonUtility.ToJson(p.ToSaveData());
            var back = PlayerProfile.FromSaveData(JsonUtility.FromJson<ProfileSaveData>(json), prog, db);

            Assert.AreEqual(2, back.Level);
            Assert.AreEqual(2, back.Exp);
            Assert.AreEqual(77, back.Gold);
            Assert.AreSame(sword, back.GetEquipped(EquipSlot.Weapon));
            Assert.IsTrue(back.IsCollected("Field:coin1"));
        }

        [Test]
        public void Save_UnknownEquipmentId_IsSkipped()
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            var data = new ProfileSaveData { ownedIds = new[] { "gone" }, equippedIds = new[] { "gone" } };
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("gone"));

            var p = PlayerProfile.FromSaveData(data, Curve(), db);

            Assert.AreEqual(0, p.Owned.Count);
        }

        // ---------------- 보상 ----------------

        [Test]
        public void StageReward_SumsMonsterRewards()
        {
            var goblin = ScriptableObject.CreateInstance<UnitData>();
            goblin.rewardExp = 7;
            goblin.rewardGold = 3;
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.monsters = new List<MonsterSpawn>
            {
                new() { unit = goblin, position = new Vector2Int(1, 0) },
                new() { unit = goblin, position = new Vector2Int(2, 0) },
            };

            var r = ProfileRewards.ForStage(stage);

            Assert.AreEqual(14, r.Exp);
            Assert.AreEqual(6, r.Gold);
        }

        // ---------------- 전투 반영 ----------------

        static (BattleController battle, Unit enemy) Battle(StatBlock bonus, int cardDamage = 6, int cardBlock = 0)
        {
            var playerDef = ScriptableObject.CreateInstance<UnitData>();
            playerDef.team = Team.Player;
            playerDef.maxHp = 20;
            var enemyDef = ScriptableObject.CreateInstance<UnitData>();
            enemyDef.team = Team.Enemy;
            enemyDef.maxHp = 30;
            enemyDef.cards = TestUnits.Cards(0, 1);

            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = 5;
            stage.height = 1;
            stage.playerStart = new Vector2Int(0, 0);
            stage.monsters = new List<MonsterSpawn> { new() { unit = enemyDef, position = new Vector2Int(1, 0) } };

            var card = ScriptableObject.CreateInstance<CardData>();
            card.cardName = "Slash";
            card.cost = 0;
            card.targeting = new TargetPattern(TargetShape.Adjacent, 1, true);
            card.effects = new List<CardEffect> { new DamageEffect { amount = cardDamage } };
            var guard = ScriptableObject.CreateInstance<CardData>();
            guard.cardName = "Guard";
            guard.cost = 0;
            guard.targeting = new TargetPattern(TargetShape.Self, 0, false);
            guard.effects = new List<CardEffect> { new BlockEffect { amount = cardBlock } };

            var b = BattleController.Create(stage, new List<CardData> { card, guard }, 2, 3);
            b.SpawnFromStage(stage, playerDef, null, bonus);
            b.StartPlayerTurn();
            return (b, b.State.Enemies.First());
        }

        [Test]
        public void Bonus_MaxHp_RaisesPlayerMaxAndStartingHp()
        {
            var (b, _) = Battle(new StatBlock { maxHp = 8 });
            Assert.AreEqual(28, b.State.Player.MaxHp);
            Assert.AreEqual(28, b.State.Player.Hp);
        }

        [Test]
        public void Bonus_Attack_AddsToCardDamage()
        {
            var (b, enemy) = Battle(new StatBlock { attack = 2 });
            Assert.IsTrue(b.TryPlayCard(0, enemy.Position, out _));
            Assert.AreEqual(30 - 8, enemy.Hp);
        }

        [Test]
        public void Bonus_Defense_AddsToCardBlock()
        {
            var (b, _) = Battle(new StatBlock { defense = 3 }, cardBlock: 5);
            Assert.IsTrue(b.TryPlayCard(1, b.State.Player.Position, out _));
            Assert.AreEqual(8, b.State.Player.Block);
        }

        [Test]
        public void Bonus_None_LeavesNumbersUnchanged()
        {
            var (b, enemy) = Battle(default, cardBlock: 5);
            Assert.IsTrue(b.TryPlayCard(0, enemy.Position, out _));
            Assert.AreEqual(24, enemy.Hp);
            Assert.AreEqual(20, b.State.Player.MaxHp);
        }

        [Test]
        public void RunState_EffectiveValues_IncludeBonus()
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            var playerDef = ScriptableObject.CreateInstance<UnitData>();
            playerDef.team = Team.Player;
            var run = new RunState(new List<StageData> { stage }, playerDef, new List<CardData>(), 5, 3)
            {
                Bonus = new StatBlock { handSize = 1, maxEnergy = 2 },
            };

            Assert.AreEqual(6, run.EffectiveHandSize);
            Assert.AreEqual(5, run.EffectiveMaxEnergy);
        }
    }
}
