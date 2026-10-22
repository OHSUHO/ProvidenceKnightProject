using ProvidenceKnight.Data;

namespace ProvidenceKnight.Battle
{
    public enum StatusType
    {
        Bleed,      // 출혈: 턴 시작마다 Amount 피해 (방어도 무시), 지속 턴 감소
        Burn,       // 화상: 턴 시작마다 Amount 피해 (방어도가 막아줌), 지속 턴 감소
        Poison,     // 독: 턴 시작마다 Amount 피해 (방어도 무시), 그 뒤 Amount 1 감소. 지속 턴 없음
        Stun,       // 기절: 그 유닛의 턴 동안 카드를 전혀 못 쓴다
        Freeze,     // 빙결: 이동 카드를 못 쓴다
        Darkness    // 암흑: 사거리 2 이상 카드(직선/범위 원거리)를 못 쓴다
    }

    /// <summary>상태이상 하나의 현재 값. Amount = 강도(피해량), Turns = 남은 턴.</summary>
    public readonly struct StatusState
    {
        public int Amount { get; }
        public int Turns { get; }

        public StatusState(int amount, int turns)
        {
            Amount = amount;
            Turns = turns;
        }
    }

    /// <summary>
    /// 상태이상 규칙. 지속 시간은 "그 유닛의 턴" 단위로 센다.
    /// 플레이어는 자기 턴 시작(StartPlayerTurn)에 피해를 받고 턴 종료(EndPlayerTurn)에 제어 효과가 한 턴 줄어든다.
    /// 몬스터는 적 턴이 시작될 때 피해를 받고, 자기 행동이 끝나면 제어 효과가 한 턴 줄어든다.
    /// </summary>
    public static class StatusRules
    {
        public static readonly StatusType[] All = (StatusType[])System.Enum.GetValues(typeof(StatusType));

        public static bool IsDamageOverTime(StatusType type) => type is StatusType.Bleed or StatusType.Burn or StatusType.Poison;
        public static bool IgnoresBlock(StatusType type) => type is StatusType.Bleed or StatusType.Poison;

        public static bool IsActive(StatusType type, StatusState s) =>
            type == StatusType.Poison ? s.Amount > 0
            : IsDamageOverTime(type) ? s.Amount > 0 && s.Turns > 0
            : s.Turns > 0;

        public static string Name(StatusType type) => type switch
        {
            StatusType.Bleed => "출혈",
            StatusType.Burn => "화상",
            StatusType.Poison => "독",
            StatusType.Stun => "기절",
            StatusType.Freeze => "빙결",
            StatusType.Darkness => "암흑",
            _ => type.ToString()
        };

        /// <summary>예: "출혈 3 (2턴)", "독 4", "기절 (1턴)".</summary>
        public static string Describe(StatusType type, int amount, int turns) => type switch
        {
            StatusType.Poison => $"{Name(type)} {amount}",
            StatusType.Bleed or StatusType.Burn => $"{Name(type)} {amount} ({turns}턴)",
            _ => $"{Name(type)} ({turns}턴)"
        };

        public static string Describe(StatusType type, StatusState s) => Describe(type, s.Amount, s.Turns);

        /// <summary>이 유닛이 이 카드를 지금 쓸 수 있는가. 플레이어 손패와 몬스터 AI 가 같은 규칙을 쓴다.</summary>
        public static bool CanUse(Unit unit, CardData card, out string reason)
        {
            reason = null;
            if (unit == null || card == null) return true;

            if (unit.Has(StatusType.Stun)) { reason = "기절 상태 (카드를 쓸 수 없음)"; return false; }
            if (unit.Has(StatusType.Freeze) && card.Has(Effects.EffectKind.Move)) { reason = "빙결 상태 (이동 카드 사용 불가)"; return false; }
            if (unit.Has(StatusType.Darkness) && card.targeting.IsRanged) { reason = "암흑 상태 (사거리 2 이상 카드 사용 불가)"; return false; }
            return true;
        }

        public static bool CanUse(Unit unit, CardData card) => CanUse(unit, card, out _);
    }
}
