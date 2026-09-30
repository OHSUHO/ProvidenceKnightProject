using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>플레이어 덱. 리스트 순서 그대로 사용한다 (셔플 없음). 앞쪽 handSize 장이 시작 손패.</summary>
    [CreateAssetMenu(fileName = "Deck_", menuName = "ProvidenceKnight/Deck Data")]
    public class DeckData : GameDataAsset
    {
        public List<CardData> cards = new();
    }
}
