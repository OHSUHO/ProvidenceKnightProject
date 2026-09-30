using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using UnityEditor;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>프로젝트의 데이터 에셋을 모아 DataValidator 를 돌린다. 메뉴와 EditMode 테스트(DataValidationTests)가 같이 쓴다.</summary>
    public static class ProjectDataValidator
    {
        public static List<DataIssue> Run()
        {
            var configs = AssetDatabase.FindAssets("t:RunConfig")
                .Select(g => AssetDatabase.LoadAssetAtPath<RunConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null);
            return DataValidator.Validate(GameDatabaseBuilder.Find(), configs, GameDataIdAssigner.AllAssets());
        }

        [MenuItem("ProvidenceKnight/Validate All Data")]
        static void Menu()
        {
            var issues = Run();
            foreach (var issue in issues)
                Debug.LogError($"[Validate] {issue}", issue.Asset);

            if (issues.Count == 0)
            {
                Debug.Log("[Validate] 데이터 문제 없음");
                EditorUtility.DisplayDialog("Validate All Data", "문제 없음", "확인");
            }
            else
            {
                var preview = string.Join("\n", issues.Take(12).Select(i => "• " + i));
                if (issues.Count > 12) preview += $"\n… 외 {issues.Count - 12}개";
                EditorUtility.DisplayDialog("Validate All Data", $"문제 {issues.Count}개 (콘솔에서 항목을 누르면 에셋으로 이동)\n\n{preview}", "확인");
            }
        }
    }
}
