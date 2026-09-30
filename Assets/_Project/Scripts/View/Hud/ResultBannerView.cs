using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    public class ResultBannerView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text label;
        [SerializeField] Color victoryColor = new(0.5f, 1f, 0.6f);
        [SerializeField] Color defeatColor = new(1f, 0.4f, 0.4f);

        public void Show(bool playerWon)
        {
            panel.SetActive(true);
            label.text = playerWon ? "승리!" : "패배...";
            label.color = playerWon ? victoryColor : defeatColor;
        }

        public void Hide() => panel.SetActive(false);
    }
}
