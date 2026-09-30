using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using UnityEditor;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// GameDataAsset 이 임포트될 때(새로 만들기·복제 포함) id 가 비었거나 다른 에셋과 겹치면 파일 이름으로 새 id 를 붙인다.
    /// 복제(Ctrl+D)한 에셋은 원본 id 를 그대로 들고 오므로, 새로 임포트된 쪽의 id 를 바꾼다.
    /// </summary>
    class GameDataIdAssigner : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var assets = imported.Where(p => p.EndsWith(".asset"))
                .Select(AssetDatabase.LoadAssetAtPath<GameDataAsset>)
                .Where(a => a != null)
                .ToList();
            if (assets.Count == 0) return;

            Dictionary<string, List<GameDataAsset>> byId = null;
            foreach (var asset in assets)
            {
                byId ??= AllAssets().Where(a => !string.IsNullOrEmpty(a.Id))
                    .GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.ToList());

                bool empty = string.IsNullOrEmpty(asset.Id);
                bool clash = !empty && byId.TryGetValue(asset.Id, out var owners) && owners.Any(o => o != asset);
                if (!empty && !clash) continue;

                var old = asset.Id;
                var id = UniqueId(GameDataAsset.MakeId(asset.name), byId);
                asset.EditorSetId(id);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);

                if (!empty) byId[old].Remove(asset);
                byId[id] = new List<GameDataAsset> { asset };
                if (clash) Debug.Log($"[GameData] '{asset.name}' id 가 '{old}' 와 겹쳐 '{id}' 로 바꿨습니다 (복제된 에셋).", asset);
            }
        }

        static string UniqueId(string baseId, Dictionary<string, List<GameDataAsset>> byId)
        {
            if (string.IsNullOrEmpty(baseId)) baseId = "data";
            var id = baseId;
            for (int n = 2; byId.TryGetValue(id, out var list) && list.Count > 0; n++) id = $"{baseId}_{n}";
            return id;
        }

        /// <summary>프로젝트의 모든 GameDataAsset.</summary>
        public static IEnumerable<GameDataAsset> AllAssets() =>
            AssetDatabase.FindAssets("t:GameDataAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameDataAsset>)
                .Where(a => a != null);
    }
}
