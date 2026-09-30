using System.Linq;
using ProvidenceKnight.Battle;
using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>다음 손패 미리보기. 턴을 끝내면 (지금까지 낸 카드가 없다면) 정확히 이 카드들로 손패가 교체된다.</summary>
    public class DeckPreviewView : MonoBehaviour
    {
        [SerializeField] TMP_Text header;
        [SerializeField] TMP_Text list;

        public int Count { get; set; } = 5;

        public void Set(CardCycle cards)
        {
            header.text = $"다음 손패 (덱 {cards.DeckCount})";
            list.text = string.Join("\n", cards.PeekDeck(Count).Select((c, i) => $"{i + 1}. {c.cardName}"));
        }
    }
}
