using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>FloatingText 프리팹 풀. 팝업은 연출 큐를 막지 않는다 (떠오르는 동안 다음 단계가 재생됨).</summary>
    public class FloatingTextPool : MonoBehaviour
    {
        [SerializeField] FloatingText prefab;

        readonly Stack<FloatingText> _free = new();

        public void Spawn(Vector3 worldPos, string message, Color color)
        {
            var item = _free.Count > 0 ? _free.Pop() : Instantiate(prefab, transform);
            item.gameObject.SetActive(true);
            item.Play(worldPos, message, color, () =>
            {
                if (item == null) return;
                item.gameObject.SetActive(false);
                _free.Push(item);
            });
        }
    }
}
