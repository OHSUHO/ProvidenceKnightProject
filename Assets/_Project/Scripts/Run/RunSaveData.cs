using System;
using System.IO;
using UnityEngine;

namespace ProvidenceKnight.Run
{
    /// <summary>
    /// 런 이어하기용 저장 데이터 (JsonUtility). 에셋은 id 로만 가리킨다 → 불러올 때 GameDatabase 로 찾는다.
    /// 스테이지 목록·손패 수 등 설정은 RunConfig 에서 다시 읽는다. 전투 도중 상태는 저장하지 않는다 (스테이지 사이에서만 저장).
    /// P4 에서는 구조만: 실제 저장/불러오기 버튼은 아직 연결하지 않았다.
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int stageIndex;
        public bool hasPlayerHp;   // false = 풀피로 시작 (JsonUtility 가 int? 를 못 다룸)
        public int playerHp;
        public int maxEnergy;
        public string[] deckCardIds = Array.Empty<string>();
        public int rngSeed;
    }

    /// <summary>persistentDataPath 의 JSON 파일 하나에 저장한다.</summary>
    public static class RunSaveStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        public static bool HasSave => File.Exists(FilePath);

        public static void Save(RunSaveData data) => File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));

        public static bool TryLoad(out RunSaveData data)
        {
            data = null;
            if (!HasSave) return false;
            try
            {
                data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSaveStore] 세이브를 읽지 못했습니다: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            if (HasSave) File.Delete(FilePath);
        }
    }
}
