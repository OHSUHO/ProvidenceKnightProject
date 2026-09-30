using System.Collections.Generic;
using ProvidenceKnight.Battle;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 필드. 칸 좌표 ↔ 월드 좌표 변환, Tile 프리팹 배치, Unit 프리팹 생성을 맡는다.
    /// 필드 전체는 이 오브젝트 위치를 중심으로 놓인다.
    /// 에디터에서는 인스펙터의 "스테이지 미리보기"로 저장되지 않는 타일·유닛을 깔아 레이아웃을 확인할 수 있다.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [SerializeField] float cellSize = 1f;
        [SerializeField] TileView tilePrefab;
        [SerializeField] Transform tilesRoot;
        [SerializeField] Transform unitsRoot;

        [Header("Unit Prefabs (UnitData.viewPrefab 이 비어 있을 때)")]
        [SerializeField] UnitView playerUnitPrefab;
        [SerializeField] UnitView enemyUnitPrefab;

        [Header("Editor")]
        [Tooltip("아무것도 깔려 있지 않을 때 카메라가 맞출 필드 크기")]
        [SerializeField] Vector2Int designSize = new(7, 5);
        [SerializeField] StageData previewStage;
        [Tooltip("미리보기에서 플레이어 시작 칸에 놓을 유닛 (게임에서는 RunConfig.player)")]
        [SerializeField] UnitData previewPlayer;

        public float CellSize => cellSize;
        public Transform UnitsRoot => unitsRoot;
        public Vector2Int Size { get; private set; }
        public Vector2 WorldSize => (Vector2)(Size != Vector2Int.zero ? Size
            : previewStage != null ? new Vector2Int(previewStage.width, previewStage.height) : designSize) * cellSize;

        /// <summary>필드가 차지하는 월드 사각형 (CameraFramer 가 맞춘다).</summary>
        public Rect WorldRect => new((Vector2)transform.position - WorldSize * 0.5f, WorldSize);

        TileView[,] _tiles;

        // ---------------- 생성 ----------------

        public void Build(GridMap grid)
        {
            ClearChildren(tilesRoot);
            ClearChildren(unitsRoot);
            BuildTiles(grid.Width, grid.Height, grid.IsBlocked);
        }

        void BuildTiles(int width, int height, System.Func<Vector2Int, bool> isBlocked)
        {
            Size = new Vector2Int(width, height);
            _tiles = new TileView[width, height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = Instantiate(tilePrefab, tilesRoot);
                tile.transform.position = GridToWorld(cell);
                tile.transform.localScale = Vector3.one * cellSize;
                tile.Setup(cell, isBlocked(cell));
                _tiles[x, y] = tile;
            }
        }

        /// <summary>유닛 표시를 만든다: UnitData.viewPrefab → 없으면 팀별 기본 프리팹.</summary>
        public UnitView CreateUnitView(UnitData data)
        {
            UnitView prefab = null;
            if (data.viewPrefab != null && !data.viewPrefab.TryGetComponent(out prefab))
                Debug.LogWarning($"[BoardView] {data.name}.viewPrefab 에 UnitView 가 없어 기본 프리팹을 씁니다.", data);
            if (prefab == null) prefab = data.team == Team.Player ? playerUnitPrefab : enemyUnitPrefab;

            var view = Instantiate(prefab, unitsRoot);
            view.transform.localScale = Vector3.one * cellSize;
            return view;
        }

        // ---------------- 좌표 ----------------

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Size.x && c.y < Size.y;

        public TileView TileAt(Vector2Int c) => _tiles != null && InBounds(c) ? _tiles[c.x, c.y] : null;

        public IEnumerable<TileView> Tiles
        {
            get
            {
                if (_tiles == null) yield break;
                foreach (var t in _tiles) if (t != null) yield return t;
            }
        }

        /// <summary>칸 중심의 월드 좌표.</summary>
        public Vector3 GridToWorld(Vector2Int cell)
        {
            var pos = WorldRect.min + ((Vector2)cell + Vector2.one * 0.5f) * cellSize;
            return new Vector3(pos.x, pos.y, 0f);
        }

        public bool TryWorldToGrid(Vector3 world, out Vector2Int cell)
        {
            var local = ((Vector2)world - WorldRect.min) / cellSize;
            cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            return _tiles != null && InBounds(cell);
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var go = root.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

#if UNITY_EDITOR
        // ---------------- 에디터 미리보기 (씬에 저장되지 않음) ----------------

        public StageData PreviewStage => previewStage;
        public bool HasPreview => _tiles != null && !Application.isPlaying;

        public void BuildPreview()
        {
            ClearPreview();
            if (previewStage == null) return;

            var blocked = new HashSet<Vector2Int>(previewStage.blockedTiles);
            BuildTiles(previewStage.width, previewStage.height, blocked.Contains);
            foreach (var t in Tiles) MarkPreview(t.gameObject);

            if (previewPlayer != null) SpawnPreviewUnit(previewPlayer, previewStage.playerStart);
            foreach (var m in previewStage.monsters)
                if (m.unit != null) SpawnPreviewUnit(m.unit, m.position);
        }

        void SpawnPreviewUnit(UnitData data, Vector2Int cell)
        {
            var view = CreateUnitView(data);
            view.InitPreview(data, GridToWorld(cell));
            MarkPreview(view.gameObject);
        }

        static void MarkPreview(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
        }

        public void ClearPreview()
        {
            ClearChildren(tilesRoot);
            ClearChildren(unitsRoot);
            _tiles = null;
            Size = Vector2Int.zero;
        }
#endif
    }
}
