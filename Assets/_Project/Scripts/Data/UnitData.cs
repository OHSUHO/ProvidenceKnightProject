using System.Collections.Generic;
using ProvidenceKnight.Battle.Effects;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>플레이어/몬스터 공통 유닛 정의.</summary>
    [CreateAssetMenu(fileName = "Unit_", menuName = "ProvidenceKnight/Unit Data")]
    public class UnitData : GameDataAsset
    {
        public string displayName = "Unit";
        public Team team = Team.Enemy;
        [Min(1)] public int maxHp = 10;

        [Header("Combat (Enemy AI 전용, 플레이어는 미사용)")]
        [Tooltip("이 몬스터가 쓰는 카드. 적 턴마다 AI 가 이 중 한 장만 골라 쓴다 (공격 카드 우선, 없으면 이동 카드). 플레이어 카드와 같은 CardData 를 쓴다")]
        public List<CardData> cards = new();

        [Tooltip("적 턴 행동 순서. 0 = 미지정(지정된 몬스터들 뒤에 행동), 1 이상은 작을수록 먼저. 같은 값끼리는 전투 시작 시 무작위로 정해 스테이지 끝까지 고정")]
        [Min(0)] public int actionPriority;

        [Tooltip("매 턴이 시작될 때(플레이어 턴 시작 시) 이 몬스터에게 자동으로 걸리는 효과. 예: 방어도 N, 공격 1회 무효화. 몬스터의 방어도는 이때 먼저 초기화된다. 공격·이동 효과는 쓸 수 없다. 효과마다 '턴 제한'을 걸 수 있다")]
        public List<TurnStartEntry> turnStartEffects = new();

        [Header("View")]
        [Tooltip("이 유닛 전용 프리팹 (UnitView 필요). 비어 있으면 BoardView 의 팀별 기본 프리팹에 sprite/color 만 적용")]
        public GameObject viewPrefab;
        public Sprite sprite;               // 비어 있으면 색 네모 + 이름 첫 글자로 표시
        public Color color = Color.white;
    }
}
