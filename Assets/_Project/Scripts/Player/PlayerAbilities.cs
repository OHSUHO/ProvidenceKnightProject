using System;
using UnityEngine;

namespace ProvidenceKnight.Player
{
    public enum Ability
    {
        Move, Jump, Run, Crouch, CrouchMove, DoubleJump, WallSlide, WallClimb, WallJump, Glide,
    }

    /// <summary>
    /// 플레이어 능력 잠금/해제 스위치. 인스펙터에서 체크로 켜고 끄거나, 코드에서 Unlock/Lock 으로 바꾼다.
    /// 예) 아이템 획득 시 <c>player.Abilities.Unlock(Ability.DoubleJump)</c>
    /// </summary>
    [Serializable]
    public class PlayerAbilities
    {
        [Tooltip("좌우 이동")] public bool move = true;
        [Tooltip("점프")] public bool jump = true;
        [Tooltip("달리기 (Shift)")] public bool run = true;
        [Tooltip("앉기 (↓)")] public bool crouch = true;
        [Tooltip("앉은 채 이동 (앉기 필요)")] public bool crouchMove = true;
        [Tooltip("이단 점프 (공중 점프 횟수는 PlayerController2D.airJumps)")] public bool doubleJump = true;
        [Tooltip("벽에 붙어 천천히 미끄러지기 (벽 방향 키 유지)")] public bool wallSlide = true;
        [Tooltip("벽에 붙은 채 ↑/↓ 로 오르내리기")] public bool wallClimb = true;
        [Tooltip("벽 점프")] public bool wallJump = true;
        [Tooltip("활강 (공중에서 낙하 중 점프 키를 다시 눌러 유지)")] public bool glide = true;

        public bool Has(Ability a) => a switch
        {
            Ability.Move => move,
            Ability.Jump => jump,
            Ability.Run => run,
            Ability.Crouch => crouch,
            Ability.CrouchMove => crouchMove,
            Ability.DoubleJump => doubleJump,
            Ability.WallSlide => wallSlide,
            Ability.WallClimb => wallClimb,
            Ability.WallJump => wallJump,
            Ability.Glide => glide,
            _ => false,
        };

        public void Set(Ability a, bool value)
        {
            switch (a)
            {
                case Ability.Move: move = value; break;
                case Ability.Jump: jump = value; break;
                case Ability.Run: run = value; break;
                case Ability.Crouch: crouch = value; break;
                case Ability.CrouchMove: crouchMove = value; break;
                case Ability.DoubleJump: doubleJump = value; break;
                case Ability.WallSlide: wallSlide = value; break;
                case Ability.WallClimb: wallClimb = value; break;
                case Ability.WallJump: wallJump = value; break;
                case Ability.Glide: glide = value; break;
            }
        }

        public void Unlock(Ability a) => Set(a, true);
        public void Lock(Ability a) => Set(a, false);

        public void SetAll(bool value)
        {
            foreach (Ability a in Enum.GetValues(typeof(Ability))) Set(a, value);
        }
    }
}
