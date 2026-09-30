using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>레벨 곡선. 레벨 L 에서 L+1 이 되는 데 필요한 경험치 = round(baseExp × growth^(L-1)).</summary>
    [CreateAssetMenu(fileName = "Progression", menuName = "ProvidenceKnight/Progression Data")]
    public class ProgressionData : ScriptableObject
    {
        [Min(1)] public int maxLevel = 30;
        [Min(1)] public int baseExp = 20;
        [Min(1f)] public float growth = 1.35f;

        public int ExpToNext(int level) =>
            level >= maxLevel ? 0 : Mathf.Max(1, Mathf.RoundToInt(baseExp * Mathf.Pow(growth, Mathf.Max(0, level - 1))));
    }
}
