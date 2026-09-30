using UnityEngine;

namespace ProvidenceKnight.Data
{
    /// <summary>
    /// 씬 밖에서 어디서든 필요한 전역 참조 (Resources/GameSettings). 탐험·전투 씬이 같은 플레이어 프로필을 다루려면
    /// 레벨 곡선과 데이터베이스를 씬 참조 없이 찾을 수 있어야 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "ProvidenceKnight/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        public ProgressionData progression;
        public GameDatabase database;

        static GameSettings _instance;
        public static GameSettings Instance => _instance != null ? _instance : _instance = Resources.Load<GameSettings>("GameSettings");
    }
}
