using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using ProvidenceKnight.EditorTools;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    public class DataTests
    {
        static T Asset<T>(string name, string id) where T : GameDataAsset
        {
            var a = ScriptableObject.CreateInstance<T>();
            a.name = name;
            a.EditorSetId(id);
            return a;
        }

        static CardData Card(string id, params CardEffect[] effects)
        {
            var c = Asset<CardData>(id, id);
            c.cardName = id;
            c.effects = effects.ToList();
            return c;
        }

        static UnitData Enemy(string id)
        {
            var u = Asset<UnitData>(id, id);
            u.displayName = id;
            u.team = Team.Enemy;
            return u;
        }

        static StageData ValidStage(string id, UnitData enemy)
        {
            var s = Asset<StageData>(id, id);
            s.width = 3;
            s.height = 1;
            s.playerStart = new Vector2Int(0, 0);
            s.monsters = new List<MonsterSpawn> { new() { unit = enemy, position = new Vector2Int(2, 0) } };
            return s;
        }

        static GameDatabase Db(params GameDataAsset[] assets)
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.cards = assets.OfType<CardData>().ToList();
            db.units = assets.OfType<UnitData>().ToList();
            db.stages = assets.OfType<StageData>().ToList();
            db.decks = assets.OfType<DeckData>().ToList();
            db.rewardPools = assets.OfType<RewardPoolData>().ToList();
            return db;
        }

        // ---------------- id ----------------

        [TestCase("Card_EnergyAwakening", "card_energy_awakening")]
        [TestCase("RewardPool_Basic", "reward_pool_basic")]
        [TestCase("Stage_01", "stage_01")]
        [TestCase("Card_Slash 1", "card_slash_1")]
        [TestCase("Unit-Big Goblin", "unit_big_goblin")]
        public void MakeId_FromAssetName(string assetName, string expected)
        {
            Assert.AreEqual(expected, GameDataAsset.MakeId(assetName));
        }

        // ---------------- GameDatabase ----------------

        [Test]
        public void Database_Get_FindsByIdAndType()
        {
            var slash = Card("card_slash", new DamageEffect { amount = 6 });
            var goblin = Enemy("unit_goblin");
            var db = Db(slash, goblin);

            Assert.AreSame(slash, db.Get<CardData>("card_slash"));
            Assert.AreSame(goblin, db.Get<UnitData>("unit_goblin"));
            Assert.IsNull(db.Get<UnitData>("card_slash"), "타입이 다르면 못 찾음");
            Assert.IsNull(db.Get<CardData>("nope"));
            Assert.IsNull(db.Get<CardData>(null));
        }

        // ---------------- 검증 ----------------

        [Test]
        public void Validator_ValidData_HasNoIssues()
        {
            var goblin = Enemy("unit_goblin");
            var slash = Card("card_slash", new DamageEffect { amount = 6 });
            goblin.cards.Add(slash);
            var db = Db(slash, goblin, ValidStage("stage_01", goblin));
            CollectionAssert.IsEmpty(DataValidator.Validate(db, null).Select(i => i.ToString()));
        }

        [Test]
        public void Validator_FindsEnemyWithoutCards()
        {
            var goblin = Enemy("unit_goblin");
            var db = Db(Card("card_slash", new DamageEffect { amount = 6 }), goblin, ValidStage("stage_01", goblin));
            CollectionAssert.Contains(DataValidator.Validate(db, null).Select(i => i.ToString()), "unit_goblin: 몬스터인데 카드가 없음");
        }

        [Test]
        public void Validator_FindsDuplicateIds_AndEmptyIds()
        {
            var a = Card("card_a", new BlockEffect { amount = 1 });
            var b = Card("card_b", new BlockEffect { amount = 1 });
            b.EditorSetId("card_a");
            var c = Card("card_c", new BlockEffect { amount = 1 });
            c.EditorSetId("");

            var issues = DataValidator.Validate(Db(a, b, c), null);

            Assert.AreEqual(2, issues.Count(i => i.Message.Contains("중복")), "겹친 두 에셋 모두 보고");
            Assert.AreEqual(1, issues.Count(i => i.Asset == c && i.Message.Contains("id 가 비어")));
        }

        [Test]
        public void Validator_FindsCardWithoutEffects_AndNullEffect()
        {
            var none = Card("card_none");
            var broken = Card("card_broken", new DamageEffect { amount = 1 }, null);

            var issues = DataValidator.Validate(Db(none, broken), null);

            Assert.IsTrue(issues.Any(i => i.Asset == none && i.Message.Contains("효과가 없음")));
            Assert.IsTrue(issues.Any(i => i.Asset == broken && i.Message.Contains("비어 있는 효과")));
        }

        [Test]
        public void Validator_FindsStageErrors_AndEmptyReferences()
        {
            var goblin = Enemy("unit_goblin");
            var stage = ValidStage("stage_bad", goblin);
            stage.monsters.Add(new MonsterSpawn { unit = null, position = new Vector2Int(1, 0) });
            var deck = Asset<DeckData>("deck", "deck");
            deck.cards = new List<CardData> { null };

            var issues = DataValidator.Validate(Db(goblin, stage, deck), null);

            Assert.IsTrue(issues.Any(i => i.Asset == stage && i.Message.Contains("UnitData가 비어")));
            Assert.IsTrue(issues.Any(i => i.Asset == deck && i.Message.Contains("비어 있는 카드")));
        }

        [Test]
        public void Validator_FindsAssetMissingFromDatabase()
        {
            var inDb = Card("card_in", new BlockEffect { amount = 1 });
            var outside = Card("card_out", new BlockEffect { amount = 1 });

            var issues = DataValidator.Validate(Db(inDb), null, new GameDataAsset[] { inDb, outside });

            Assert.AreEqual(1, issues.Count);
            Assert.AreSame(outside, issues[0].Asset);
        }

        [Test]
        public void Validator_ChecksRunConfig()
        {
            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.player = Enemy("unit_goblin");   // 팀이 Enemy → 오류

            var issues = DataValidator.Validate(Db(), new[] { config });

            Assert.IsTrue(issues.Any(i => i.Message.Contains("Player 가 아님")));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("stages")));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("startingDeck")));
        }

        /// <summary>실제 프로젝트 데이터 전체 (메뉴 Validate All Data 와 같은 검사).</summary>
        [Test]
        public void ProjectData_HasNoIssues()
        {
            var issues = ProjectDataValidator.Run();
            CollectionAssert.IsEmpty(issues.Select(i => i.ToString()));
        }
    }
}
