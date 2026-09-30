using System.Linq;
using TMPro;
using UnityEditor;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// _Project/Fonts 의 Dynamic TMP 폰트 에셋은 저장 직전에 생성된 글리프·아틀라스를 비운다.
    /// 플레이할 때마다 아틀라스(2048²)가 채워져 에셋이 수 MB 로 커지고 git 에 매번 변경으로 잡히는 것을 막는다.
    /// 글리프는 실행 중 필요할 때 다시 만들어진다.
    /// </summary>
    class DynamicFontSaveCleaner : AssetModificationProcessor
    {
        const string FontDir = "Assets/_Project/Fonts/";

        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths.Where(p => p.StartsWith(FontDir) && p.EndsWith(".asset")))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                    font.ClearFontAssetData(setAtlasSizeToZero: true);
            }
            return paths;
        }
    }
}
