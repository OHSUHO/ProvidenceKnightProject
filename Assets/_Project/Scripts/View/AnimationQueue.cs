using System;
using System.Collections.Generic;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 전투 연출 전역 큐. 로직 이벤트 순서 그대로 한 단계씩 재생한다 (앞 단계가 끝나야 다음 단계 시작).
    /// 적 A 가 칸을 비우는 연출이 끝난 뒤에야 적 B 가 그 칸으로 들어오므로, 행동 순서대로 보이고 서로 겹치지 않는다.
    /// Busy 동안 BattleRunner/HudView 는 입력을 잠그고 다음 턴 계획 표시를 숨긴다.
    /// (P3 의 BattleEventPlayer 로 대체 예정)
    /// </summary>
    public static class AnimationQueue
    {
        static readonly Queue<(UnityEngine.Object owner, Action<Action> step)> Steps = new();
        static UnityEngine.Object _currentOwner;
        static Action _finishCurrent;

        public static bool Busy => _finishCurrent != null || Steps.Count > 0;

        /// <summary>owner 가 파괴되면 아직 시작하지 않은 그 단계는 건너뛴다.</summary>
        public static void Enqueue(UnityEngine.Object owner, Action<Action> step)
        {
            Steps.Enqueue((owner, step));
            if (_finishCurrent == null) RunNext();
        }

        /// <summary>owner 가 재생 중인 단계를 끝난 것으로 처리한다 (OnDestroy 에서 트윈을 죽였을 때 큐가 멈추지 않게).</summary>
        public static void Abort(UnityEngine.Object owner)
        {
            if (_finishCurrent != null && ReferenceEquals(_currentOwner, owner))
                _finishCurrent();
        }

        static void RunNext()
        {
            while (Steps.Count > 0)
            {
                var (owner, step) = Steps.Dequeue();
                if (owner == null) continue;   // 파괴됨 (Unity null)

                bool finished = false;
                Action finish = null;
                finish = () =>
                {
                    if (finished) return;
                    finished = true;
                    if (_finishCurrent == finish)
                    {
                        _finishCurrent = null;
                        _currentOwner = null;
                    }
                    RunNext();
                };
                _currentOwner = owner;
                _finishCurrent = finish;
                step(finish);
                return;
            }
        }
    }
}
