using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 메뉴·대화가 열려 있는 동안 플레이어 조작을 막는다. 여러 개가 겹쳐도 마지막 것이 닫힐 때만 풀린다.
    /// freezeTime 이 true 면 시간도 멈춘다 (장비창·상점). 대화는 글자 타이핑 연출이 시간을 쓰므로 시간은 흐르게 두고 조작만 막는다.
    /// </summary>
    public static class GamePause
    {
        static int _blockers;
        static int _freezers;

        /// <summary>조작이 막혀 있는가 (시간이 멈췄는지와는 별개).</summary>
        public static bool IsPaused => _blockers > 0;

        public static void Push(bool freezeTime = true)
        {
            _blockers++;
            if (freezeTime && _freezers++ == 0) Time.timeScale = 0f;
        }

        /// <summary>Push 할 때와 같은 freezeTime 값으로 부른다.</summary>
        public static void Pop(bool freezeTime = true)
        {
            if (_blockers > 0) _blockers--;
            if (freezeTime && _freezers > 0 && --_freezers == 0) Time.timeScale = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _blockers = 0;
            _freezers = 0;
            Time.timeScale = 1f;
        }
    }
}
