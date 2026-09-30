using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.Data
{
    [Serializable]
    public struct MonsterSpawn
    {
        public UnitData unit;
        public Vector2Int position;
    }

    /// <summary>
    /// 필드 크기, 장애물, 플레이어 시작 위치, 몬스터 배치. 좌표는 (0,0) = 왼쪽 아래.
    /// 플레이어 유닛 자체는 RunConfig 가 정한다. 인스펙터에서 격자를 칠해 편집한다 (Editor/StageDataEditor).
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_", menuName = "ProvidenceKnight/Stage Data")]
    public class StageData : GameDataAsset
    {
        [Min(1)] public int width = 7;
        [Min(1)] public int height = 5;

        public Vector2Int playerStart;

        public List<Vector2Int> blockedTiles = new();
        public List<MonsterSpawn> monsters = new();

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;

        /// <summary>배치 오류 목록. 비어 있으면 유효한 스테이지.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var used = new HashSet<Vector2Int>();

            if (!InBounds(playerStart)) errors.Add($"playerStart {playerStart} 가 필드 밖");
            used.Add(playerStart);

            foreach (var b in blockedTiles)
            {
                if (!InBounds(b)) errors.Add($"장애물 {b} 가 필드 밖");
                else if (b == playerStart) errors.Add($"장애물 {b} 가 플레이어 시작 위치와 겹침");
            }

            var blocked = new HashSet<Vector2Int>(blockedTiles);
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m.unit == null) errors.Add($"monsters[{i}] UnitData가 비어 있음");
                else if (m.unit.team != Team.Enemy) errors.Add($"monsters[{i}] '{m.unit.name}' 의 팀이 Enemy 가 아님");
                if (!InBounds(m.position)) errors.Add($"monsters[{i}] {m.position} 가 필드 밖");
                else if (blocked.Contains(m.position)) errors.Add($"monsters[{i}] {m.position} 가 장애물 위");
                else if (!used.Add(m.position)) errors.Add($"monsters[{i}] {m.position} 위치 중복");
            }
            if (monsters.Count == 0) errors.Add("몬스터가 없음 (시작하자마자 승리)");
            return errors;
        }
    }
}
