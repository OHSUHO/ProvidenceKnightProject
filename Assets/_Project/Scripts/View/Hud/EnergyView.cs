using TMPro;
using UnityEngine;

namespace ProvidenceKnight.View
{
    public class EnergyView : MonoBehaviour
    {
        [SerializeField] TMP_Text value;

        public void Set(int energy, int max) => value.text = $"{energy}/{max}";
    }
}
