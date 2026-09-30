using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>
    /// 모든 게임 데이터 에셋 목록 + id 조회. 세이브 불러오기처럼 id 로 에셋을 찾아야 할 때 쓴다.
    /// 목록은 에디터에서 자동으로 모은다 (데이터 에셋이 추가·삭제되면 갱신, 메뉴 ProvidenceKnight/Rebuild Database).
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "ProvidenceKnight/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<CardData> cards = new();
        public List<UnitData> units = new();
        public List<StageData> stages = new();
        public List<DeckData> decks = new();
        public List<RewardPoolData> rewardPools = new();

        Dictionary<string, GameDataAsset> _byId;

        public IEnumerable<GameDataAsset> All =>
            cards.Cast<GameDataAsset>().Concat(units).Concat(stages).Concat(decks).Concat(rewardPools).Where(a => a != null);

        public T Get<T>(string id) where T : GameDataAsset => TryGet<T>(id, out var asset) ? asset : null;

        public bool TryGet<T>(string id, out T asset) where T : GameDataAsset
        {
            asset = null;
            if (string.IsNullOrEmpty(id)) return false;
            _byId ??= BuildIndex();
            asset = _byId.TryGetValue(id, out var found) ? found as T : null;
            return asset != null;
        }

        /// <summary>목록을 바꾼 뒤 호출 (다음 조회 때 색인을 다시 만든다).</summary>
        public void ClearCache() => _byId = null;

        Dictionary<string, GameDataAsset> BuildIndex()
        {
            var index = new Dictionary<string, GameDataAsset>();
            foreach (var asset in All)
            {
                if (string.IsNullOrEmpty(asset.Id)) continue;
                if (!index.TryAdd(asset.Id, asset))
                    Debug.LogError($"[GameDatabase] id '{asset.Id}' 중복: '{index[asset.Id].name}' / '{asset.name}'. 앞의 것만 조회됩니다.", asset);
            }
            return index;
        }

        void OnEnable() => _byId = null;
        void OnValidate() => _byId = null;
    }
}
