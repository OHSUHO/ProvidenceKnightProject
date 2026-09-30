using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    public class SaveTests
    {
        static CardData Card(string id)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.name = c.cardName = id;
            c.EditorSetId(id);
            c.effects = new List<CardEffect> { new BlockEffect { amount = 1 } };
            return c;
        }

        static (RunConfig config, GameDatabase db, CardData a, CardData b, CardData reward) Setup()
        {
            var a = Card("card_a");
            var b = Card("card_b");
            var reward = Card("card_reward");

            var player = ScriptableObject.CreateInstance<UnitData>();
            player.team = Team.Player;
            var stages = Enumerable.Range(0, 3).Select(_ => ScriptableObject.CreateInstance<StageData>()).ToList();
            var deck = ScriptableObject.CreateInstance<DeckData>();
            deck.cards = new List<CardData> { a, b, a };

            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.player = player;
            config.stages = stages;
            config.startingDeck = deck;
            config.handSize = 2;
            config.startingEnergy = 3;

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.cards = new List<CardData> { a, b, reward };
            return (config, db, a, b, reward);
        }

        [Test]
        public void RunSave_RoundTripsThroughJson()
        {
            var (config, db, a, b, reward) = Setup();
            var run = RunState.FromConfig(config, seed: 1234);
            run.AddReward(reward);
            run.AdvanceStage();
            int stageSeed = run.StageSeed(RunState.BattleSalt);

            var json = JsonUtility.ToJson(run.ToSaveData());
            Assert.IsTrue(json.Contains("card_reward"), "카드는 id 로 저장");

            Assert.IsTrue(RunState.TryFromSaveData(JsonUtility.FromJson<RunSaveData>(json), config, db, out var loaded, out var error), error);
            Assert.AreEqual(1, loaded.StageIndex);
            Assert.AreSame(config.stages[1], loaded.CurrentStage);
            CollectionAssert.AreEqual(new[] { a, b, a, reward }, loaded.Deck);
            Assert.IsNull(loaded.PlayerHp, "아직 전투 결과가 없으면 풀피");
            Assert.AreEqual(3, loaded.MaxEnergy);
            Assert.AreEqual(1234, loaded.Seed);
            Assert.AreEqual(stageSeed, loaded.StageSeed(RunState.BattleSalt), "같은 스테이지 시드 → 같은 행동 순서·보상");
        }

        [Test]
        public void RunSave_KeepsCarriedHpAndMaxEnergy()
        {
            var (config, db, _, _, _) = Setup();
            var data = RunState.FromConfig(config, 1).ToSaveData();
            data.hasPlayerHp = true;
            data.playerHp = 7;
            data.maxEnergy = 4;

            Assert.IsTrue(RunState.TryFromSaveData(data, config, db, out var loaded, out _));
            Assert.AreEqual(7, loaded.PlayerHp);
            Assert.AreEqual(4, loaded.MaxEnergy);
        }

        [Test]
        public void RunSave_UnknownCardId_Fails()
        {
            var (config, db, _, _, _) = Setup();
            var data = RunState.FromConfig(config, 1).ToSaveData();
            data.deckCardIds = new[] { "card_a", "card_deleted" };

            Assert.IsFalse(RunState.TryFromSaveData(data, config, db, out var loaded, out var error));
            Assert.IsNull(loaded);
            StringAssert.Contains("card_deleted", error);
        }

        [Test]
        public void RunSave_StageIndexOutOfRange_Fails()
        {
            var (config, db, _, _, _) = Setup();
            var data = RunState.FromConfig(config, 1).ToSaveData();
            data.stageIndex = 5;

            Assert.IsFalse(RunState.TryFromSaveData(data, config, db, out _, out _));
        }

        [Test]
        public void StageSeed_DiffersPerStageAndPurpose()
        {
            var (config, _, _, _, _) = Setup();
            var run = RunState.FromConfig(config, 99);
            int battle0 = run.StageSeed(RunState.BattleSalt), reward0 = run.StageSeed(RunState.RewardSalt);
            run.AdvanceStage();

            Assert.AreNotEqual(battle0, reward0);
            Assert.AreNotEqual(battle0, run.StageSeed(RunState.BattleSalt));
        }
    }
}
