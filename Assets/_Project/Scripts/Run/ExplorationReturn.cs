using System.Collections.Generic;
using ProvidenceKnight.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProvidenceKnight.Run
{
    /// <summary>
    /// 탐험 씬 → 전투 씬 → 탐험 씬 왕복 사이에 넘기는 정보. 어디서 들어갔는지, 어떤 스테이지를 치를지, 누구를 이겼는지.
    /// 씬이 바뀌어도 유지되는 정적 상태이며, 플레이 모드를 새로 시작할 때 초기화된다.
    /// </summary>
    public static class ExplorationReturn
    {
        /// <summary>탐험 씬에서 전투로 들어와 아직 돌아가지 않은 상태.</summary>
        public static bool Active { get; private set; }
        public static string ReturnScene { get; private set; }
        public static Vector3 ReturnPosition { get; private set; }
        public static string TriggerId { get; private set; }
        /// <summary>이 전투로 치를 스테이지. null 이면 RunConfig 의 기본 런을 쓴다.</summary>
        public static StageData Stage { get; private set; }

        static bool _hasPosition;
        static readonly HashSet<string> Defeated = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Active = _hasPosition = false;
            ReturnScene = TriggerId = null;
            Stage = null;
            Defeated.Clear();
        }

        public static void Begin(string triggerId, Vector3 playerPosition, StageData stage)
        {
            Active = true;
            ReturnScene = SceneManager.GetActiveScene().name;
            ReturnPosition = playerPosition;
            TriggerId = triggerId;
            Stage = stage;
            _hasPosition = true;
        }

        /// <summary>전투가 끝났을 때. 이겼으면 그 트리거는 다시 발동하지 않는다.</summary>
        public static string Finish(bool playerWon)
        {
            Active = false;
            Stage = null;
            if (playerWon && TriggerId != null) Defeated.Add(TriggerId);
            return ReturnScene;
        }

        public static bool IsDefeated(string triggerId) => Defeated.Contains(triggerId);

        /// <summary>복귀한 씬에서 플레이어를 놓을 위치를 한 번만 꺼낸다.</summary>
        public static bool TryConsumePosition(out Vector3 position)
        {
            position = ReturnPosition;
            bool ok = _hasPosition && !Active && SceneManager.GetActiveScene().name == ReturnScene;
            if (ok) _hasPosition = false;
            return ok;
        }
    }
}
