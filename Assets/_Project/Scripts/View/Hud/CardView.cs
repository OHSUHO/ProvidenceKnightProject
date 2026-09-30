using ProvidenceKnight.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>Card 프리팹. 손패와 보상 화면이 같이 쓴다.</summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image outline;
        [SerializeField] Image background;
        [SerializeField] TMP_Text cost;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;

        [Header("Look")]
        [SerializeField, Range(0f, 1f)] float backgroundTint = 0.35f;
        [SerializeField, Range(0f, 1f)] float outlineTint = 0.8f;
        [SerializeField] Color selectedOutline = new(1f, 0.9f, 0.3f);
        [SerializeField] Color disabledBackground = new(0.15f, 0.15f, 0.15f);
        [SerializeField] Color disabledOutline = new(0.3f, 0.3f, 0.3f);
        [SerializeField] Color disabledText = new(0.55f, 0.55f, 0.55f);
        [SerializeField] float selectedScale = 1.06f;

        public Button Button => button;
        public CardData Card { get; private set; }

        public void Set(CardData card, bool affordable, bool selected)
        {
            Card = card;
            cost.text = card.cost.ToString();
            title.text = card.cardName;
            description.text = card.GetDescription();

            var bg = card.color * backgroundTint;
            bg.a = 1f;
            var line = card.color * outlineTint;
            line.a = 1f;
            background.color = affordable ? bg : disabledBackground;
            outline.color = selected ? selectedOutline : (affordable ? line : disabledOutline);
            title.color = description.color = affordable ? Color.white : disabledText;
            transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
        }
    }
}
