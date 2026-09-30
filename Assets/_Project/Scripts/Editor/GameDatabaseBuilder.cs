using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using UnityEditor;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// 프로젝트의 데이터 에셋을 GameDatabase 에 모은다. 데이터 에셋이 추가·삭제·이동되면 자동으로 다시 모은다.
    /// </summary>
    class GameDatabaseBuilder : AssetPostprocessor
    {
        const string DefaultPath = "Assets/_Project/Data/GameDatabase.asset";

        static bool _pending;

        [MenuItem("ProvidenceKnight/Rebuild Database")]
        static void RebuildMenu()
        {
            var db = Rebuild();
            Debug.Log($"[GameDatabase] 다시 모음: 카드 {db.cards.Count}, 유닛 {db.units.Count}, 스테이지 {db.stages.Count}, 덱 {db.decks.Count}, 보상 풀 {db.rewardPools.Count}, 장비 {db.equipment.Count}", db);
            EditorGUIUtility.PingObject(db);
        }

        /// <summary>데이터베이스 에셋을 찾아(없으면 만들어) 목록을 새로 채운다.</summary>
        public static GameDatabase Rebuild()
        {
            var db = Find();
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, DefaultPath);
            }

            var all = GameDataIdAssigner.AllAssets()
                .OrderBy(AssetDatabase.GetAssetPath, System.StringComparer.Ordinal)
                .ToList();
            bool changed = Fill(db.cards, all) | Fill(db.units, all) | Fill(db.stages, all) | Fill(db.decks, all) | Fill(db.rewardPools, all) | Fill(db.equipment, all);
            if (changed)
            {
                db.ClearCache();
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
            }
            return db;
        }

        public static GameDatabase Find() =>
            AssetDatabase.FindAssets("t:GameDatabase")
                .Select(g => AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(d => d != null);

        static bool Fill<T>(List<T> list, List<GameDataAsset> all) where T : GameDataAsset
        {
            var next = all.OfType<T>().ToList();
            if (list.SequenceEqual(next)) return false;
            list.Clear();
            list.AddRange(next);
            return true;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // 삭제된 경로는 타입을 알 수 없으니 .asset 이면 일단 다시 모은다 (변화가 없으면 저장하지 않음)
            bool relevant = deleted.Concat(movedFrom).Any(p => p.EndsWith(".asset"))
                || imported.Concat(moved).Any(p => p.EndsWith(".asset") && AssetDatabase.GetMainAssetTypeAtPath(p) is { } t && typeof(GameDataAsset).IsAssignableFrom(t));
            if (!relevant || _pending) return;

            _pending = true;
            EditorApplication.delayCall += () =>
            {
                _pending = false;
                if (Find() != null) Rebuild();   // 데이터베이스가 아직 없으면 메뉴로 처음 만들 때까지 기다림
            };
        }
    }
}
