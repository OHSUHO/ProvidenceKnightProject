using System.Linq;
using TMPro;
using UnityEditor;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// 프로젝트 폰트(_Project/Fonts)와 TMP 대체 폰트(TextMesh Pro/Resources)의 Dynamic 폰트 에셋은
    /// 저장 직전에 생성된 글리프·아틀라스를 비운다.
    /// 플레이할 때마다 아틀라스가 채워져 에셋이 커지고 git 에 매번 변경으로 잡히는 것을 막는다.
    /// 글리프는 실행 중 필요할 때 다시 만들어진다.
    /// </summary>
    class DynamicFontSaveCleaner : AssetModificationProcessor
    {
        static readonly string[] FontDirs =
        {
            "Assets/_Project/Fonts/",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/",
        };

        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths.Where(p => p.EndsWith(".asset") && FontDirs.Any(p.StartsWith)))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                    font.ClearFontAssetData(setAtlasSizeToZero: true);
            }
            return paths;
        }
    }
}
