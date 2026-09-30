using System.Collections.Generic;
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

        [Header("View")]
        [Tooltip("이 유닛 전용 프리팹 (UnitView 필요). 비어 있으면 BoardView 의 팀별 기본 프리팹에 sprite/color 만 적용")]
        public GameObject viewPrefab;
        public Sprite sprite;               // 비어 있으면 색 네모 + 이름 첫 글자로 표시
        public Color color = Color.white;
    }
}
