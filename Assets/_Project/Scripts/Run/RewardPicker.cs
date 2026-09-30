using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;

namespace ProvidenceKnight.Run
{
    /// <summary>보상 후보 풀에서 중복 없이 count 장을 무작위로 뽑는다 (난수 주입 → 테스트에서 결과 고정 가능).</summary>
    public static class RewardPicker
    {
        public static List<CardData> Pick(IEnumerable<CardData> pool, int count, System.Random rng)
        {
            var candidates = pool?.Where(c => c != null).ToList() ?? new List<CardData>();
            count = System.Math.Min(count, candidates.Count);

            // Fisher-Yates 로 섞어 앞에서 count 장
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            return candidates.Take(count).ToList();
        }
    }
}
