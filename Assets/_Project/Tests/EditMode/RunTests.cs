using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    public class RunTests
    {
        static CardData Card(string name, int cost, TargetShape shape, bool requiresEnemy, bool exhaust, params CardEffect[] effects)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.name = c.cardName = name;
            c.cost = cost;
            c.targeting = new TargetPattern(shape, 1, requiresEnemy);
            c.exhaust = exhaust;
            c.effects = effects.ToList();
            return c;
        }

        static CardData Named(string n) => Card(n, 1, TargetShape.Self, false, false);
        static CardData EnergyPotion(int amount = 1) =>
            Card("기력 각성", 2, TargetShape.Self, false, true, new GainMaxEnergyEffect { amount = amount });
        static CardData Slash(int dmg = 6) => Card("베기", 1, TargetShape.Adjacent, true, false, new DamageEffect { amount = dmg });

        static UnitData UnitDef(Team team, int hp)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            return d;
        }

        static StageData MakeStage(int w, int h, Vector2Int playerStart, UnitData enemyDef, Vector2Int enemyPos, int playerHp = 20)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = w;
            stage.height = h;
            stage.player = UnitDef(Team.Player, playerHp);
            stage.playerStart = playerStart;
            stage.monsters = new List<MonsterSpawn> { new() { unit = enemyDef, position = enemyPos } };
            return stage;
        }

        static BattleController StartBattle(StageData stage, List<CardData> deck)
        {
            var battle = BattleController.Create(stage, deck, 5, 3);
            battle.SpawnFromStage(stage);
            battle.StartPlayerTurn();
            return battle;
        }

        // ---------------- CardCycle: exhaust ----------------

        [Test]
        public void Cycle_Exhaust_DoesNotReturnCardToDeck()
        {
            var cards = "ABCDEFG".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);   // 손 ABCDE, 덱 FG

            cycle.Cycle(0, exhaust: true);   // A 소멸

            Assert.AreEqual("BCDEF", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual("G", string.Concat(cycle.PeekDeck(5).Select(c => c.cardName)));   // A 는 안 보임
            Assert.AreEqual(1, cycle.DeckCount);
        }

        [Test]
        public void Cycle_Exhaust_WhenDeckEmpty_HandShrinksWithoutError()
        {
            var cards = "ABCDE".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);   // 손 ABCDE, 덱 없음

            Assert.DoesNotThrow(() => cycle.Cycle(0, exhaust: true));
            Assert.AreEqual(4, cycle.Hand.Count);
            Assert.AreEqual(0, cycle.DeckCount);
        }

        // ---------------- 전투: exhaust / 최대 에너지 증가 ----------------

        [Test]
        public void PlayingExhaustCard_RemovesItForRestOfBattle()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(1, 1));
            var potion = EnergyPotion();
            var battle = StartBattle(stage, new List<CardData> { potion, Named("B"), Named("C"), Named("D"), Named("E"), Named("F") });
            var state = battle.State;

            Assert.IsTrue(battle.TryPlayCard(0, state.Player.Position, out _));

            Assert.IsFalse(state.Cards.Hand.Contains(potion));
            Assert.IsFalse(state.Cards.PeekDeck(10).Contains(potion));

            // 다음 턴이 와도 (손패 전량 교체) 다시 등장하지 않아야 한다.
            battle.EndPlayerTurn();
            Assert.IsFalse(state.Cards.Hand.Contains(potion));
            Assert.IsFalse(state.Cards.PeekDeck(10).Contains(potion));
        }

        [Test]
        public void GainMaxEnergy_IncreasesMaxAndCurrentEnergyImmediately()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(1, 1));
            var battle = StartBattle(stage, new List<CardData> { EnergyPotion(1), Named("B"), Named("C"), Named("D"), Named("E") });
            var state = battle.State;

            Assert.AreEqual(3, state.Energy);
            Assert.IsTrue(battle.TryPlayCard(0, state.Player.Position, out _));   // 코스트 2 지불

            Assert.AreEqual(4, state.MaxEnergy);          // 3 + 1
            Assert.AreEqual(2, state.Energy);              // (3-2) + 1

            battle.EndTurn();
            Assert.AreEqual(4, state.MaxEnergy);           // 다음 턴에도 유지
            Assert.AreEqual(4, state.Energy);
        }

        // ---------------- 전투: 체력 이어받기 ----------------

        [Test]
        public void SpawnFromStage_WithHpOverride_StartsAtThatHp()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(4, 1), playerHp: 30);
            var battle = BattleController.Create(stage, new List<CardData>(), 5, 3);

            battle.SpawnFromStage(stage, playerCurrentHp: 12);

            Assert.AreEqual(12, battle.State.Player.Hp);
            Assert.AreEqual(30, battle.State.Player.MaxHp);
        }

        [Test]
        public void SpawnFromStage_HpOverride_IsClampedToMax()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(4, 1), playerHp: 20);
            var battle = BattleController.Create(stage, new List<CardData>(), 5, 3);

            battle.SpawnFromStage(stage, playerCurrentHp: 999);

            Assert.AreEqual(20, battle.State.Player.Hp);
        }

        // ---------------- RunState ----------------

        [Test]
        public void RunState_CaptureResult_CarriesHpAndMaxEnergyForward()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(4, 1), playerHp: 30);
            var deck = new List<CardData> { Slash() };
            var battle = StartBattle(stage, deck);
            BattleRules.DealDamage(battle.State, battle.State.Player, 8);   // 임의로 체력을 깎아 다음 스테이지로 넘길 값 만들기

            var run = new RunState(new List<StageData> { stage, stage }, deck, 5, 3);
            Assert.IsNull(run.PlayerHp);   // 첫 스테이지는 풀피

            run.CaptureResult(battle.State);
            Assert.AreEqual(22, run.PlayerHp);
            Assert.AreEqual(3, run.MaxEnergy);
        }

        [Test]
        public void RunState_AddReward_AppendsToDeckEnd()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(4, 1));
            var starter = new List<CardData> { Named("A"), Named("B") };
            var run = new RunState(new List<StageData> { stage }, starter, 5, 3);
            var reward = Named("Reward");

            run.AddReward(reward);

            Assert.AreEqual(3, run.Deck.Count);
            Assert.AreEqual(reward, run.Deck[^1]);
        }

        [Test]
        public void RunState_AdvanceStage_TracksLastStageAndCompletion()
        {
            var stage = MakeStage(5, 3, new Vector2Int(0, 1), UnitDef(Team.Enemy, 10), new Vector2Int(4, 1));
            var stages = new List<StageData> { stage, stage, stage };
            var run = new RunState(stages, new List<CardData>(), 5, 3);

            Assert.IsTrue(run.HasCurrentStage);
            Assert.IsFalse(run.IsLastStage);   // 인덱스 0, Count 3

            run.AdvanceStage();
            Assert.IsTrue(run.HasCurrentStage);
            Assert.IsFalse(run.IsLastStage);   // 인덱스 1, Count 3

            run.AdvanceStage();
            Assert.IsTrue(run.HasCurrentStage);
            Assert.IsTrue(run.IsLastStage);    // 인덱스 2, Count 3 → 마지막

            run.AdvanceStage();
            Assert.IsFalse(run.HasCurrentStage);   // 모든 스테이지 클리어
        }

        // ---------------- RewardPicker ----------------

        [Test]
        public void RewardPicker_PicksDistinctCards_AndIsReproducibleWithSeed()
        {
            var pool = "ABCDEF".Select(ch => Named(ch.ToString())).Append(null).ToList();

            var a = RewardPicker.Pick(pool, 3, new System.Random(42));
            var b = RewardPicker.Pick(pool, 3, new System.Random(42));

            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(3, a.Distinct().Count(), "중복 없음");
            Assert.IsFalse(a.Contains(null), "빈 칸은 제외");
            CollectionAssert.AreEqual(a, b, "같은 시드면 같은 결과");
            Assert.AreEqual(2, RewardPicker.Pick(pool.Take(2), 3, new System.Random(1)).Count, "후보가 적으면 있는 만큼만");
        }
    }
}
