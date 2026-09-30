using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>스테이지 클리어 보상으로 나올 수 있는 카드 후보 목록. 순서는 중요하지 않다 (무작위로 몇 장 뽑아 제시).</summary>
    [CreateAssetMenu(fileName = "RewardPool_", menuName = "ProvidenceKnight/Reward Pool Data")]
    public class RewardPoolData : ScriptableObject
    {
        public List<CardData> cards = new();
    }
}
