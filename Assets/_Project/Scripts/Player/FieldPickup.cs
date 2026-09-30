using ProvidenceKnight.Data;
using ProvidenceKnight.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProvidenceKnight.Player
{
    /// <summary>탐험 필드의 획득물. 플레이어가 닿으면 골드·경험치(·장비)를 주고 사라진다. 한 번 얻으면 세이브에 남아 다시 나오지 않는다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class FieldPickup : MonoBehaviour
    {
        [Tooltip("씬 안에서 겹치지 않는 ID. 비우면 오브젝트 이름")]
        [SerializeField] string id;
        [Min(0)] [SerializeField] int gold;
        [Min(0)] [SerializeField] int exp;
        [Tooltip("장비를 주는 상자로 쓸 때 (선택)")]
        [SerializeField] EquipmentData equipment;

        string Key => $"{SceneManager.GetActiveScene().name}:{(string.IsNullOrEmpty(id) ? name : id)}";

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void Start()
        {
            if (ProfileSession.Current.IsCollected(Key)) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerController2D>(out _)) return;
            var profile = ProfileSession.Current;
            if (profile.IsCollected(Key)) return;

            profile.MarkCollected(Key);
            if (equipment != null) profile.AddOwned(equipment);
            ProfileRewards.Grant(profile, new RewardAmount(exp, gold));
            ProfileSession.Save();   // Grant 가 비어 있으면 저장하지 않으므로 장비·수집 기록은 여기서 확실히 저장
            Destroy(gameObject);
        }
    }
}
