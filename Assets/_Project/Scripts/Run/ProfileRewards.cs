using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Run
{
    public readonly struct RewardAmount
    {
        public readonly int Exp;
        public readonly int Gold;
        public RewardAmount(int exp, int gold) { Exp = exp; Gold = gold; }
        public bool IsEmpty => Exp <= 0 && Gold <= 0;
        public override string ToString() => $"경험치 +{Exp}, 골드 +{Gold}";
    }

    /// <summary>경험치·골드 지급. 지급하면 바로 저장한다.</summary>
    public static class ProfileRewards
    {
        /// <summary>스테이지에 배치된 몬스터들의 처치 보상 합.</summary>
        public static RewardAmount ForStage(StageData stage)
        {
            int exp = 0, gold = 0;
            if (stage != null)
                foreach (var m in stage.monsters)
                {
                    if (m.unit == null) continue;
                    exp += m.unit.rewardExp;
                    gold += m.unit.rewardGold;
                }
            return new RewardAmount(exp, gold);
        }

        /// <summary>프로필에 지급하고 저장한다. 오른 레벨 수를 돌려준다.</summary>
        public static int Grant(PlayerProfile profile, RewardAmount reward)
        {
            if (profile == null || reward.IsEmpty) return 0;
            int levels = profile.AddExp(reward.Exp);
            profile.AddGold(reward.Gold);
            ProfileSession.Save();
            Debug.Log($"[ProfileRewards] {reward} → Lv {profile.Level}, 경험치 {profile.Exp}/{profile.ExpToNext}, 골드 {profile.Gold}" + (levels > 0 ? $" (레벨 업 x{levels})" : ""));
            return levels;
        }
    }
}
