using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Tests
{
    public class CardTests
    {
        static CardData Card(string name, int cost, TargetShape shape, int range, bool requiresEnemy, params CardEffect[] effects)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.name = c.cardName = name;
            c.cost = cost;
            c.targeting = new TargetPattern(shape, range, requiresEnemy);
            c.effects = effects.ToList();
            return c;
        }

        static CardData Move(int range = 2) => Card("Move", 1, TargetShape.Walk, range, false, new MoveEffect());
        static CardData Slash(int dmg = 6) => Card("Slash", 1, TargetShape.Adjacent, 1, true, new DamageEffect { amount = dmg });
        static CardData Guard(int block = 5) => Card("Guard", 1, TargetShape.Self, 1, false, new BlockEffect { amount = block });
        static CardData Named(string n) => Card(n, 1, TargetShape.Self, 1, false);

        static UnitData UnitDef(Team team, int hp)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            return d;
        }

        /// 5x3, 플레이어 (0,1), 적 (1,1) hp 10
        static BattleController MakeBattle(IEnumerable<CardData> deck, int maxEnergy = 3)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = 5;
            stage.height = 3;
            stage.playerStart = new Vector2Int(0, 1);
            stage.monsters = new List<MonsterSpawn> { new() { unit = UnitDef(Team.Enemy, 10), position = new Vector2Int(1, 1) } };
            var battle = BattleController.Create(stage, deck, 5, maxEnergy);
            battle.SpawnFromStage(stage, UnitDef(Team.Player, 20));
            battle.StartPlayerTurn();
            return battle;
        }

        // ---------------- CardCycle ----------------

        [Test]
        public void Cycle_PlayedCardGoesToBottom_TopCardEntersHand()
        {
            var cards = "ABCDEFG".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);

            Assert.AreEqual("ABCDE", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual("FG", string.Concat(cycle.PeekDeck(5).Select(c => c.cardName)));

            cycle.Cycle(1); // B 사용
            Assert.AreEqual("ACDEF", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual("GB", string.Concat(cycle.PeekDeck(5).Select(c => c.cardName)));

            cycle.Cycle(0); // A
            cycle.Cycle(0); // C
            Assert.AreEqual("DEFGB", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual("AC", string.Concat(cycle.PeekDeck(5).Select(c => c.cardName)));
        }

        [Test]
        public void Cycle_WithExactlyHandSizeCards_ReturnsSameCard()
        {
            var cards = "ABCDE".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);
            cycle.Cycle(0);
            Assert.AreEqual("BCDEA", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual(0, cycle.DeckCount);
        }

        [Test]
        public void DiscardHandAndRefill_ReplacesWholeHand_WithNextDeckCards()
        {
            var cards = "ABCDEFGHIJ".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);   // 손 ABCDE, 덱 FGHIJ

            cycle.DiscardHandAndRefill();

            Assert.AreEqual("FGHIJ", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual("ABCDE", string.Concat(cycle.PeekDeck(5).Select(c => c.cardName)));   // 버린 손패는 덱 맨 아래로
        }

        [Test]
        public void DiscardHandAndRefill_AfterSomePlays_UsesCurrentHandAndDeckState()
        {
            var cards = "ABCDEFGHIJ".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);
            cycle.Cycle(0);   // A 사용 → 손 BCDEF, 덱 GHIJA

            cycle.DiscardHandAndRefill();

            Assert.AreEqual("GHIJA", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual("BCDEF", string.Concat(cycle.PeekDeck(5).Select(c => c.cardName)));
        }

        [Test]
        public void DiscardHandAndRefill_WhenDeckSmallerThanHandSize_FillsAsMuchAsPossible()
        {
            var cards = "ABC".Select(ch => Named(ch.ToString())).ToList();
            var cycle = new CardCycle(cards, 5);   // 카드 3장뿐 → 손 ABC, 덱 없음

            cycle.DiscardHandAndRefill();

            Assert.AreEqual("ABC", string.Concat(cycle.Hand.Select(c => c.cardName)));
            Assert.AreEqual(0, cycle.DeckCount);
        }

        // ---------------- Play ----------------

        [Test]
        public void Energy_IsConsumed_AndBlocksWhenInsufficient()
        {
            var battle = MakeBattle(new[] { Guard(), Guard(), Guard(), Guard(), Guard(), Guard() }, maxEnergy: 2);
            var state = battle.State;
            Assert.IsTrue(battle.TryPlayCard(0, state.Player.Position, out _));
            Assert.IsTrue(battle.TryPlayCard(0, state.Player.Position, out _));
            Assert.AreEqual(0, state.Energy);
            Assert.IsFalse(battle.TryPlayCard(0, state.Player.Position, out var reason));
            Assert.AreEqual("에너지 부족", reason);
            Assert.AreEqual(10, state.Player.Block);
        }

        [Test]
        public void Block_ResetsAtStartOfPlayerTurn()
        {
            var battle = MakeBattle(new[] { Guard() });
            var state = battle.State;
            battle.TryPlayCard(0, state.Player.Position, out _);
            Assert.AreEqual(5, state.Player.Block);
            battle.StartPlayerTurn();
            Assert.AreEqual(0, state.Player.Block);
            Assert.AreEqual(state.MaxEnergy, state.Energy);
            Assert.AreEqual(-5, battle.Events.History.OfType<BlockChanged>().Last().Delta);
        }

        [Test]
        public void Slash_DamagesAdjacentEnemy_AndKills()
        {
            var battle = MakeBattle(new[] { Slash(6), Slash(6) });
            var state = battle.State;
            var enemy = state.Enemies.Single();

            Assert.IsTrue(battle.TryPlayCard(0, enemy.Position, out _));
            Assert.AreEqual(4, enemy.Hp);
            Assert.IsTrue(battle.TryPlayCard(0, enemy.Position, out _));
            Assert.IsTrue(enemy.IsDead);
            Assert.IsNull(state.Grid.GetUnit(new Vector2Int(1, 1)));
            Assert.IsEmpty(state.Enemies);
        }

        [Test]
        public void Slash_CannotTargetEmptyTile()
        {
            var battle = MakeBattle(new[] { Slash() });
            var targets = battle.GetValidTargets(0);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(1, 1) }, targets);
            Assert.IsFalse(battle.TryPlayCard(0, new Vector2Int(0, 2), out _));
        }

        [Test]
        public void Move_GoesAroundEnemy()
        {
            var battle = MakeBattle(new[] { Move(4) });
            var state = battle.State;
            // (0,1) → (2,1): 적(1,1) 을 돌아서 4걸음 (0,2)(1,2)(2,2)(2,1)
            Assert.IsFalse(Targeting.GetValidTargets(state.Grid, state.Player, Move(3)).Contains(new Vector2Int(2, 1)));
            Assert.IsTrue(battle.TryPlayCard(0, new Vector2Int(2, 1), out _));
            Assert.AreEqual(new Vector2Int(2, 1), state.Player.Position);
        }

        [Test]
        public void Move_CannotExceedRange()
        {
            var battle = MakeBattle(new[] { Move(2) });
            Assert.IsFalse(battle.GetValidTargets(0).Contains(new Vector2Int(2, 1)));
        }

        [Test]
        public void Line_StopsAtFirstUnit()
        {
            var thrust = Card("Thrust", 1, TargetShape.Line, 3, true, new DamageEffect { amount = 4 });
            var battle = MakeBattle(new[] { thrust });
            BattleRules.SpawnUnit(battle.State, UnitDef(Team.Enemy, 5), new Vector2Int(2, 1)); // 첫 적 뒤에 숨은 적
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(1, 1) }, battle.GetValidTargets(0));
        }

        [Test]
        public void ComboCard_RunsEffectsInOrder()
        {
            // 방패치기: 인접 적 피해 5 → 방어도 5
            var bash = Card("Bash", 2, TargetShape.Adjacent, 1, true, new DamageEffect { amount = 5 }, new BlockEffect { amount = 5 });
            var battle = MakeBattle(new[] { bash });
            var enemy = battle.State.Enemies.Single();
            int from = battle.Events.History.Count;

            Assert.IsTrue(battle.TryPlayCard(0, enemy.Position, out _));

            Assert.AreEqual(5, enemy.Hp);
            Assert.AreEqual(5, battle.State.Player.Block);
            var kinds = battle.Events.History.Skip(from).Where(e => e is UnitDamaged or BlockChanged).Select(e => e.GetType().Name).ToList();
            CollectionAssert.AreEqual(new[] { nameof(UnitDamaged), nameof(BlockChanged) }, kinds);
        }

        [Test]
        public void Description_IsGeneratedFromEffects()
        {
            var bash = Card("Bash", 2, TargetShape.Adjacent, 1, true, new DamageEffect { amount = 5 }, new BlockEffect { amount = 5 });
            Assert.AreEqual("인접 피해 5\n방어도 5", bash.GetDescription());
            Assert.AreEqual("2칸 이동", Move(2).GetDescription());
        }

        // ---------------- Turn end: 손패 전량 교체 ----------------

        [Test]
        public void EndPlayerTurn_DiscardsWholeHand_AndDrawsFreshFive()
        {
            var deck = "ABCDEFGHIJ".Select(ch => Named(ch.ToString())).ToList();
            var battle = MakeBattle(deck);   // 손 ABCDE, 덱 FGHIJ

            battle.EndPlayerTurn();

            Assert.AreEqual("FGHIJ", string.Concat(battle.State.Cards.Hand.Select(c => c.cardName)));
        }

        [Test]
        public void EndPlayerTurn_UnplayedCardsGoToBottomOfDeck_ForFutureTurns()
        {
            var deck = "ABCDEFGHIJ".Select(ch => Named(ch.ToString())).ToList();
            var battle = MakeBattle(deck);   // 손 ABCDE, 덱 FGHIJ

            battle.EndPlayerTurn();          // 손 FGHIJ, 덱 ABCDE
            battle.EndPlayerTurn();          // 손 ABCDE, 덱 FGHIJ (한 바퀴 순환)

            Assert.AreEqual("ABCDE", string.Concat(battle.State.Cards.Hand.Select(c => c.cardName)));
        }

        [Test]
        public void EndPlayerTurn_EmitsResourcesChanged()
        {
            var battle = MakeBattle(new[] { Guard(), Guard(), Guard(), Guard(), Guard(), Guard() });
            int from = battle.Events.History.Count;

            battle.EndPlayerTurn();

            Assert.IsTrue(battle.Events.History.Skip(from).OfType<ResourcesChanged>().Any());
        }
    }
}
