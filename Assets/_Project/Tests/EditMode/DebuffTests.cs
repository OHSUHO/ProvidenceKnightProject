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
    /// <summary>상태이상(출혈·화상·독·기절·빙결·암흑)과 턴 제한이 있는 몬스터 턴 시작 효과.</summary>
    public class StatusEffectTests
    {
        static CardData Card(string name, TargetPattern targeting, params CardEffect[] effects)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.cardName = name;
            c.cost = 0;
            c.targeting = targeting;
            c.effects = effects.ToList();
            return c;
        }

        static readonly TargetPattern Adjacent = new(TargetShape.Adjacent, 1, true);

        static CardData Slash(int dmg = 6) => Card("Slash", Adjacent, new DamageEffect { amount = dmg });

        static UnitData Def(Team team, int hp, List<CardData> cards = null, params CardEffect[] turnStart)
        {
            var d = ScriptableObject.CreateInstance<UnitData>();
            d.team = team;
            d.maxHp = hp;
            d.turnStartEffects = turnStart.Select(e => new TurnStartEntry(e)).ToList();
            if (cards != null) d.cards = cards;
            return d;
        }

        /// 5x3, 플레이어 (0,1) hp 20, 적 enemyX,1 (기본 인접)
        static BattleController Make(UnitData enemy, List<CardData> deck = null, int enemyX = 1, int playerHp = 20)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.width = 5;
            stage.height = 3;
            stage.playerStart = new Vector2Int(0, 1);
            stage.monsters = new List<MonsterSpawn> { new() { unit = enemy, position = new Vector2Int(enemyX, 1) } };
            deck ??= Enumerable.Range(0, 5).Select(_ => Slash()).ToList();
            var battle = BattleController.Create(stage, deck, 5, 3, seed: 0);
            battle.SpawnFromStage(stage, Def(Team.Player, playerHp));
            battle.StartPlayerTurn();
            return battle;
        }

        static UnitData Melee(int hp = 10, int dmg = 3, params CardEffect[] turnStart) =>
            Def(Team.Enemy, hp, TestUnits.Cards(0, dmg), turnStart);

        static Unit Enemy(BattleController b) => b.State.Enemies.Single();

        // ---------------- 피해형 ----------------

        [Test]
        public void Poison_CardAppliesIt_AndTicksThenDecaysAtEnemyTurnStart()
        {
            var venom = Card("Venom", Adjacent, new ApplyStatusEffect { status = StatusType.Poison, amount = 3 });
            var battle = Make(Melee(), Enumerable.Repeat(venom, 5).ToList());
            var enemy = Enemy(battle);

            Assert.IsTrue(battle.TryPlayCard(0, enemy.Position, out _));
            Assert.AreEqual(3, enemy.GetStatus(StatusType.Poison).Amount);

            battle.EndTurn();
            Assert.AreEqual(7, enemy.Hp);                                  // 3 피해
            Assert.AreEqual(2, enemy.GetStatus(StatusType.Poison).Amount);  // 그 뒤 1 감소
        }

        [Test]
        public void Poison_StacksAndEventuallyWearsOff()
        {
            var battle = Make(Melee(hp: 50));
            var enemy = Enemy(battle);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Poison, 2, 0);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Poison, 1, 0);
            Assert.AreEqual(3, enemy.GetStatus(StatusType.Poison).Amount);

            battle.EndTurn();   // 3
            battle.EndTurn();   // 2
            battle.EndTurn();   // 1
            Assert.AreEqual(50 - 6, enemy.Hp);
            Assert.IsFalse(enemy.Has(StatusType.Poison));
        }

        [Test]
        public void Bleed_LastsItsTurns_ThenEnds()
        {
            var battle = Make(Melee(hp: 50));
            var enemy = Enemy(battle);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Bleed, 2, 2);

            battle.EndTurn();
            Assert.AreEqual(48, enemy.Hp);
            battle.EndTurn();
            Assert.AreEqual(46, enemy.Hp);
            Assert.IsFalse(enemy.Has(StatusType.Bleed));
            battle.EndTurn();
            Assert.AreEqual(46, enemy.Hp);
        }

        [Test]
        public void Bleed_IgnoresBlock_ButBurnIsAbsorbedByIt()
        {
            var bleedBattle = Make(Melee(10, 3, new BlockEffect { amount = 4 }));
            BattleRules.ApplyStatus(bleedBattle.State, Enemy(bleedBattle), StatusType.Bleed, 3, 1);
            bleedBattle.EndTurn();
            Assert.AreEqual(7, Enemy(bleedBattle).Hp);

            var burnBattle = Make(Melee(10, 3, new BlockEffect { amount = 4 }));
            BattleRules.ApplyStatus(burnBattle.State, Enemy(burnBattle), StatusType.Burn, 3, 1);
            burnBattle.EndTurn();
            Assert.AreEqual(10, Enemy(burnBattle).Hp);
        }

        [Test]
        public void StatusDamage_IgnoresNegate()
        {
            var battle = Make(Melee(10, 3, new NegateAttackEffect()));
            var enemy = Enemy(battle);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Poison, 2, 0);
            battle.EndTurn();
            Assert.AreEqual(8, enemy.Hp);
            Assert.AreEqual(1, enemy.Negate);
        }

        [Test]
        public void StatusDamage_CanKillTheLastEnemy_BeforeItActs()
        {
            var battle = Make(Melee(hp: 2, dmg: 5));
            var enemy = Enemy(battle);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Poison, 2, 0);
            battle.RefreshEnemyPlan();
            Assert.AreEqual(0, battle.State.EnemyPlan.Actions.Count);   // 계획도 이미 쓰러진 것으로 본다

            battle.EndTurn();

            Assert.AreEqual(BattlePhase.Victory, battle.State.Phase);
            Assert.AreEqual(20, battle.State.Player.Hp);
        }

        [Test]
        public void PlayerPoison_TicksAtPlayerTurnStart_AndCanDefeat()
        {
            var battle = Make(Melee(dmg: 1), playerHp: 3);
            BattleRules.ApplyStatus(battle.State, battle.State.Player, StatusType.Poison, 5, 0);
            battle.EndTurn();   // 적이 1 → HP 2, 다음 턴 시작에 독 5

            Assert.AreEqual(BattlePhase.Defeat, battle.State.Phase);
        }

        [Test]
        public void PlayerBurn_IsAbsorbedByLeftoverBlock()
        {
            var guard = Card("Guard", new TargetPattern(TargetShape.Self, 1, false), new BlockEffect { amount = 9 });
            var battle = Make(Melee(dmg: 3), Enumerable.Repeat(guard, 5).ToList());
            BattleRules.ApplyStatus(battle.State, battle.State.Player, StatusType.Burn, 4, 1);
            battle.TryPlayCard(0, battle.State.Player.Position, out _);   // 방어 9, 적 공격 3 → 6 남음
            battle.EndTurn();

            Assert.AreEqual(20, battle.State.Player.Hp);   // 화상 4 는 남은 방어 6 이 막음
        }

        // ---------------- 기절 ----------------

        [Test]
        public void StunnedEnemy_SkipsItsTurn_AndIntentSaysSo()
        {
            var battle = Make(Melee());
            var enemy = Enemy(battle);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Stun, 0, 1);
            battle.RefreshEnemyPlan();
            Assert.AreEqual(IntentType.Stunned, battle.State.EnemyPlan.For(enemy.Id).Type);

            battle.EndTurn();
            Assert.AreEqual(20, battle.State.Player.Hp);
            Assert.IsFalse(enemy.Has(StatusType.Stun));

            battle.EndTurn();
            Assert.AreEqual(17, battle.State.Player.Hp);   // 풀리고 나서는 다시 공격
        }

        [Test]
        public void Stun_LastsAsManyEnemyTurnsAsItsDuration()
        {
            var battle = Make(Melee());
            BattleRules.ApplyStatus(battle.State, Enemy(battle), StatusType.Stun, 0, 2);
            battle.EndTurn();
            battle.EndTurn();
            Assert.AreEqual(20, battle.State.Player.Hp);
            battle.EndTurn();
            Assert.AreEqual(17, battle.State.Player.Hp);
        }

        [Test]
        public void StunnedPlayer_CannotPlayCards_ForExactlyOneTurn()
        {
            var stunner = Card("Shock", Adjacent, new DamageEffect { amount = 1 },
                new ApplyStatusEffect { status = StatusType.Stun, turns = 1 });
            var battle = Make(Def(Team.Enemy, 10, new List<CardData> { stunner }));

            battle.EndTurn();   // 적이 플레이어를 기절시킴
            var player = battle.State.Player;
            Assert.IsTrue(player.Has(StatusType.Stun));
            Assert.IsFalse(battle.CanSelectCard(0, out var reason));
            StringAssert.Contains("기절", reason);

            battle.EndTurn();   // 기절한 턴을 넘김 (그 사이 또 기절당함)
            Assert.IsTrue(player.Has(StatusType.Stun));
        }

        [Test]
        public void PlayerStun_ExpiresAtEndOfThatTurn()
        {
            var battle = Make(Melee(dmg: 1));
            BattleRules.ApplyStatus(battle.State, battle.State.Player, StatusType.Stun, 0, 1);
            Assert.IsFalse(battle.CanSelectCard(0, out _));
            battle.EndTurn();
            Assert.IsTrue(battle.CanSelectCard(0, out _));
        }

        // ---------------- 빙결 ----------------

        [Test]
        public void FrozenEnemy_CannotMove_ButStillAttacks()
        {
            var battle = Make(Def(Team.Enemy, 10, TestUnits.Cards(2, 3)), enemyX: 4);
            var enemy = Enemy(battle);
            BattleRules.ApplyStatus(battle.State, enemy, StatusType.Freeze, 0, 1);
            battle.RefreshEnemyPlan();

            battle.EndTurn();
            Assert.AreEqual(new Vector2Int(4, 1), enemy.Position);   // 빙결 동안 제자리

            battle.EndTurn();
            Assert.AreEqual(new Vector2Int(2, 1), enemy.Position);   // 풀리자 이동
        }

        [Test]
        public void FrozenEnemy_AttackCardsStillWork()
        {
            var battle = Make(Melee());
            BattleRules.ApplyStatus(battle.State, Enemy(battle), StatusType.Freeze, 0, 2);
            battle.EndTurn();
            Assert.AreEqual(17, battle.State.Player.Hp);
        }

        [Test]
        public void FrozenPlayer_CannotUseMoveCards_ButCanAttack()
        {
            var deck = new List<CardData>
            {
                Card("Move", new TargetPattern(TargetShape.Walk, 2, false), new MoveEffect()),
                Slash(), Slash(), Slash(), Slash()
            };
            var battle = Make(Melee(), deck);
            BattleRules.ApplyStatus(battle.State, battle.State.Player, StatusType.Freeze, 0, 1);

            Assert.IsFalse(battle.CanSelectCard(0, out var reason));
            StringAssert.Contains("빙결", reason);
            Assert.IsTrue(battle.CanSelectCard(1, out _));
        }

        // ---------------- 암흑 ----------------

        [Test]
        public void DarknessEnemy_CannotUseRangedCards()
        {
            var ranged = Def(Team.Enemy, 10, TestUnits.Cards(0, 3, TargetShape.Line, 3));   // (3,1) 에서 플레이어 (0,1) 까지 직선 3칸
            var battle = Make(ranged, enemyX: 3);

            var clear = Make(ranged, enemyX: 3);
            clear.EndTurn();
            Assert.AreEqual(17, clear.State.Player.Hp);

            BattleRules.ApplyStatus(battle.State, Enemy(battle), StatusType.Darkness, 0, 1);
            battle.EndTurn();
            Assert.AreEqual(20, battle.State.Player.Hp);
        }

        [Test]
        public void Darkness_DoesNotBlockAdjacentOrMoveCards()
        {
            var adjacent = Card("Slash", Adjacent, new DamageEffect());
            var walk = Card("Move", new TargetPattern(TargetShape.Walk, 3, false), new MoveEffect());
            var line1 = Card("Jab", new TargetPattern(TargetShape.Line, 1, true), new DamageEffect());
            var line2 = Card("Arrow", new TargetPattern(TargetShape.Line, 2, true), new DamageEffect());
            var diamond2 = Card("Burst", new TargetPattern(TargetShape.Diamond, 2, true), new DamageEffect());

            var battle = Make(Melee());
            BattleRules.ApplyStatus(battle.State, battle.State.Player, StatusType.Darkness, 0, 1);
            var p = battle.State.Player;

            Assert.IsTrue(StatusRules.CanUse(p, adjacent));
            Assert.IsTrue(StatusRules.CanUse(p, walk));
            Assert.IsTrue(StatusRules.CanUse(p, line1));
            Assert.IsFalse(StatusRules.CanUse(p, line2));
            Assert.IsFalse(StatusRules.CanUse(p, diamond2));
        }

        // ---------------- 기타 ----------------

        [Test]
        public void Status_DoesNotApplyToAllies_AndSelfFlagTargetsCaster()
        {
            var battle = Make(Melee());
            var enemy = Enemy(battle);
            var ctx = new EffectContext(battle.State, battle.State.Player, battle.State.Player.Position, null);
            new ApplyStatusEffect { status = StatusType.Stun, turns = 1 }.Resolve(ctx);   // 자기 칸 = 적대 유닛 아님
            Assert.IsFalse(battle.State.Player.Has(StatusType.Stun));

            new ApplyStatusEffect { status = StatusType.Stun, turns = 1, onSelf = true }.Resolve(ctx);
            Assert.IsTrue(battle.State.Player.Has(StatusType.Stun));
            Assert.IsFalse(enemy.Has(StatusType.Stun));
        }

        [Test]
        public void SimulationCopies_CarryStatuses_SoPreviewMatchesReality()
        {
            var stunner = Card("Shock", Adjacent, new ApplyStatusEffect { status = StatusType.Stun, turns = 1 });
            var battle = Make(Melee(), Enumerable.Repeat(stunner, 5).ToList());
            var enemy = Enemy(battle);

            var preview = battle.PreviewCard(0, enemy.Position);

            Assert.AreEqual(IntentType.Stunned, preview.Plan.For(enemy.Id).Type);
            Assert.IsFalse(enemy.Has(StatusType.Stun));   // 실제 상태는 그대로

            battle.TryPlayCard(0, enemy.Position, out _);
            battle.EndTurn();
            Assert.AreEqual(20, battle.State.Player.Hp);   // 미리보기대로 공격 안 함
        }

        [Test]
        public void StatusChanged_EventsAreEmitted()
        {
            var battle = Make(Melee(hp: 50));
            BattleRules.ApplyStatus(battle.State, Enemy(battle), StatusType.Bleed, 2, 2);
            battle.EndTurn();

            var events = battle.Events.History.OfType<StatusChanged>().ToList();
            Assert.IsTrue(events.First().Applied);
            Assert.AreEqual(1, events.Last().Turns);
            Assert.IsFalse(events.Last().Applied);
        }

        // ---------------- 턴 시작 효과의 턴 제한 ----------------

        static UnitData LimitedBlock(int turns)
        {
            var d = Def(Team.Enemy, 10, TestUnits.Cards(0, 3));
            d.turnStartEffects = new List<TurnStartEntry> { new(new BlockEffect { amount = 4 }, turns) };
            return d;
        }

        [Test]
        public void TurnStartEffect_DisappearsAfterItsTurnLimit()
        {
            var battle = Make(LimitedBlock(2));
            var enemy = Enemy(battle);
            Assert.AreEqual(4, enemy.Block);   // 1턴째

            battle.EndTurn();
            Assert.AreEqual(4, enemy.Block);   // 2턴째

            battle.EndTurn();
            Assert.AreEqual(0, enemy.Block);   // 3턴째부터는 사라짐
            battle.EndTurn();
            Assert.AreEqual(0, enemy.Block);
        }

        [Test]
        public void TurnStartEffect_ZeroTurnsMeansForever()
        {
            var battle = Make(LimitedBlock(0));
            for (int i = 0; i < 5; i++) battle.EndTurn();
            Assert.AreEqual(4, Enemy(battle).Block);
        }

        [Test]
        public void TurnStartEffect_LimitsApplyPerEffect()
        {
            var d = Def(Team.Enemy, 10, TestUnits.Cards(0, 3));
            d.turnStartEffects = new List<TurnStartEntry>
            {
                new(new BlockEffect { amount = 4 }, 1),
                new(new NegateAttackEffect { maxStacks = 0 }, 0),
            };
            var battle = Make(d);
            var enemy = Enemy(battle);
            battle.EndTurn();
            battle.EndTurn();

            Assert.AreEqual(0, enemy.Block);    // 방어도는 1턴만
            Assert.AreEqual(3, enemy.Negate);   // 무효화는 계속 쌓임
        }

        [Test]
        public void TurnStartEffect_CanApplyStatusToSelf_ForLimitedTurns()
        {
            var d = Def(Team.Enemy, 10, TestUnits.Cards(0, 3));
            d.turnStartEffects = new List<TurnStartEntry>
            {
                new(new ApplyStatusEffect { status = StatusType.Stun, turns = 1, onSelf = true }, 1)
            };
            var battle = Make(d);
            Assert.IsTrue(Enemy(battle).Has(StatusType.Stun));   // 첫 턴은 스스로 기절 (느리게 깨어나는 몬스터)

            battle.EndTurn();
            Assert.AreEqual(20, battle.State.Player.Hp);
            battle.EndTurn();
            Assert.AreEqual(17, battle.State.Player.Hp);
        }

        [Test]
        public void Validator_AcceptsSelfStatus_RejectsHostileStatusAsTurnStartEffect()
        {
            var ok = Def(Team.Enemy, 10, TestUnits.Cards(0, 3), new ApplyStatusEffect { onSelf = true });
            var bad = Def(Team.Enemy, 10, TestUnits.Cards(0, 3), new ApplyStatusEffect { onSelf = false });
            var db = ScriptableObject.CreateInstance<GameDatabase>();

            db.units = new List<UnitData> { ok };
            Assert.IsFalse(DataValidator.Validate(db, null).Any(i => i.Message.Contains("턴 시작 효과에는")));

            db.units = new List<UnitData> { bad };
            Assert.IsTrue(DataValidator.Validate(db, null).Any(i => i.Message.Contains("턴 시작 효과에는")));
        }
    }
}
