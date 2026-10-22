using System.Text;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>
    /// 모든 게임 데이터 SO 의 부모. id 는 세이브·GameDatabase 조회에 쓰는 고유 키.
    /// 에셋을 만들거나 복제하면 파일 이름으로 자동 부여된다 (Editor/GameDataIdAssigner). 파일 이름을 바꿔도 id 는 그대로.
    /// </summary>
    public abstract class GameDataAsset : ScriptableObject
    {
        [Tooltip("세이브·데이터베이스 조회용 고유 키. 비워 두면 파일 이름으로 자동 부여. 이미 세이브에 쓰인 뒤에는 바꾸지 말 것")]
        [SerializeField] string id;

        public string Id => id;

        /// <summary>"Card_EnergyAwakening" → "card_energy_awakening"</summary>
        public static string MakeId(string assetName)
        {
            var sb = new StringBuilder(assetName.Length + 8);
            for (int i = 0; i < assetName.Length; i++)
            {
                char c = assetName[i];
                if (char.IsLetterOrDigit(c))
                {
                    bool wordStart = char.IsUpper(c) && i > 0 && (char.IsLower(assetName[i - 1]) || char.IsDigit(assetName[i - 1]));
                    if (wordStart && sb.Length > 0 && sb[^1] != '_') sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (sb.Length > 0 && sb[^1] != '_')
                {
                    sb.Append('_');   // 공백·하이픈·밑줄 등은 하나의 '_' 로
                }
            }
            return sb.ToString().Trim('_');
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용: id 자동 부여 / 테스트용.</summary>
        public void EditorSetId(string value) => id = value;
#endif
    }
}
