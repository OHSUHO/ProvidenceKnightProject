using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using ProvidenceKnight.View;
using UnityEngine;

namespace ProvidenceKnight.Player
{
    /// <summary>
    /// 플레이어가 닿으면 이동을 멈추고 암전 연출 후 전투 씬으로 넘어간다. (Trigger 콜라이더 필요)
    /// 이기면 사라지고, 지면 남아 있되 플레이어가 한 번 벗어나야 다시 발동한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BattleTrigger : MonoBehaviour
    {
        [SerializeField] string battleScene = "Battle";
        [Tooltip("이 전투로 치를 스테이지. 비우면 RunConfig 의 기본 런")]
        [SerializeField] StageData stage;
        [Tooltip("승리 기록용 ID. 씬 안에서 겹치지 않게. 비우면 오브젝트 이름")]
        [SerializeField] string id;

        string Id => string.IsNullOrEmpty(id) ? name : id;

        bool _armed = true;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void Start()
        {
            if (ExplorationReturn.IsDefeated(Id)) { Destroy(gameObject); return; }
            // 졌을 때 그 자리로 돌아오므로, 겹친 채로 시작하면 벗어날 때까지 잠근다
            if (ExplorationReturn.TriggerId == Id) _armed = false;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_armed || !other.TryGetComponent<PlayerController2D>(out var player)) return;
            _armed = false;
            player.enabled = false;
            if (player.TryGetComponent<Rigidbody2D>(out var rb)) rb.linearVelocity = Vector2.zero;
            ExplorationReturn.Begin(Id, player.transform.position, stage);
            BattleTransition.Play(battleScene);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController2D>() != null) _armed = true;
        }
    }
}
