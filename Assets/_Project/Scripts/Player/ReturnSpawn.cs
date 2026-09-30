using ProvidenceKnight.Run;
using UnityEngine;

namespace ProvidenceKnight.Player
{
    /// <summary>전투에서 돌아온 탐험 씬에서 플레이어를 전투 직전 위치에 되돌려 놓는다. 플레이어에 붙인다.</summary>
    public class ReturnSpawn : MonoBehaviour
    {
        void Awake()
        {
            if (!ExplorationReturn.TryConsumePosition(out var pos)) return;
            transform.position = pos;
            if (TryGetComponent<Rigidbody2D>(out var rb)) rb.position = pos;
        }
    }
}
