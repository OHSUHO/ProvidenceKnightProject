using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>마우스를 올린 유닛의 스탯 패널.</summary>
    public class UnitTooltipView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;

        public void Show(string header, string text)
        {
            panel.SetActive(true);
            title.text = header;
            body.text = text;
        }

        public void Hide() => panel.SetActive(false);
    }
}
