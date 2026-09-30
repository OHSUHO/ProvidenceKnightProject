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

    /// <summary>필드 크기와 몬스터 배치도. 좌표는 (0,0) = 왼쪽 아래.</summary>
    [CreateAssetMenu(fileName = "Stage_", menuName = "ProvidenceKnight/Stage Data")]
    public class StageData : ScriptableObject
    {
        [Min(1)] public int width = 7;
        [Min(1)] public int height = 5;

        public UnitData player;
        public Vector2Int playerStart;

        public List<Vector2Int> blockedTiles = new();
        public List<MonsterSpawn> monsters = new();

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;

        /// <summary>배치 오류 목록. 비어 있으면 유효한 스테이지.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var used = new HashSet<Vector2Int>();

            if (player == null) errors.Add("player UnitData가 비어 있음");
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
                if (!InBounds(m.position)) errors.Add($"monsters[{i}] {m.position} 가 필드 밖");
                else if (blocked.Contains(m.position)) errors.Add($"monsters[{i}] {m.position} 가 장애물 위");
                else if (!used.Add(m.position)) errors.Add($"monsters[{i}] {m.position} 위치 중복");
            }
            return errors;
        }
    }
}
