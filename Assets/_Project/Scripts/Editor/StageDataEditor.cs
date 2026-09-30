using System.Collections.Generic;
using System.Linq;
using ProvidenceKnight.Data;
using ProvidenceKnight.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// StageData 인스펙터: 칸 격자를 클릭·드래그로 칠해 장애물·플레이어 시작·몬스터를 배치한다.
    /// 칸에는 몬스터 이름 첫 글자·색·적 턴 행동 순서를 표시하고, 검증 오류는 맨 위에 빨간 박스로 띄운다.
    /// 저장 형식(blockedTiles / monsters 목록)은 그대로라서 아래 "목록으로 보기"에서 직접 고쳐도 된다.
    /// </summary>
    [CustomEditor(typeof(StageData))]
    public class StageDataEditor : UnityEditor.Editor
    {
        enum Brush { Obstacle, PlayerStart, Monster, Eraser }

        const string BattleScenePath = "Assets/_Project/Scenes/Battle.unity";
        const int MaxSize = 20;
        const float AxisMargin = 18f;

        static readonly GUIContent[] BrushLabels =
        {
            new("장애물", "지나갈 수 없는 칸"),
            new("플레이어 시작", "플레이어가 전투를 시작하는 칸 (하나)"),
            new("몬스터", "아래에서 고른 몬스터를 놓는다"),
            new("지우개", "장애물·몬스터를 지워 바닥으로 (오른쪽 클릭도 지우개)"),
        };

        static readonly Color FloorA = new(0.30f, 0.32f, 0.35f);
        static readonly Color FloorB = new(0.26f, 0.28f, 0.31f);
        static readonly Color ObstacleColor = new(0.08f, 0.08f, 0.09f);
        static readonly Color PlayerColor = new(0.25f, 0.55f, 1f);
        static readonly Color ErrorOutline = new(1f, 0.25f, 0.2f);

        // 브러시·몬스터 선택은 스테이지를 바꿔 가며 칠할 때도 유지
        static Brush _brush = Brush.Obstacle;
        static UnitData _monster;
        static bool _showLists;

        UnitData[] _enemies;
        int _strokeGroup = -1;
        Brush _strokeBrush;
        Vector2Int? _lastPainted;

        GUIStyle _letterStyle, _orderStyle, _axisStyle;

        StageData Stage => (StageData)target;

        void OnEnable()
        {
            _enemies = AssetDatabase.FindAssets("t:UnitData")
                .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(u => u != null && u.team == Team.Enemy)
                .OrderBy(u => u.name)
                .ToArray();
            if (_monster == null) _monster = _enemies.FirstOrDefault();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            serializedObject.ApplyModifiedProperties();

            var errors = Stage.Validate();
            if (errors.Count > 0)
                EditorGUILayout.HelpBox("배치 오류 — 이대로는 전투를 시작할 수 없습니다.\n• " + string.Join("\n• ", errors), MessageType.Error);

            EditorGUILayout.Space();
            DrawSize();
            EditorGUILayout.Space();
            DrawBrushes();
            DrawGrid(errors.Count > 0 ? ProblemCells() : null);
            EditorGUILayout.LabelField("왼쪽 클릭/드래그: 칠하기 · 오른쪽 클릭/드래그: 지우기", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("칸의 숫자 = 적 턴 행동 순서. '2~3' 은 우선순위가 같아 전투 시작 때 무작위로 정해짐", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            DrawActions(errors.Count == 0);

            EditorGUILayout.Space();
            _showLists = EditorGUILayout.Foldout(_showLists, "목록으로 보기 (좌표 직접 편집)", true);
            if (_showLists)
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(StageData.playerStart)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(StageData.blockedTiles)), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(StageData.monsters)), true);
                serializedObject.ApplyModifiedProperties();
            }
        }

        // ---------------- 크기 ----------------

        void DrawSize()
        {
            var stage = Stage;
            int w, h;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("크기 (가로 × 세로)");
                w = Mathf.Clamp(EditorGUILayout.DelayedIntField(stage.width), 1, MaxSize);
                h = Mathf.Clamp(EditorGUILayout.DelayedIntField(stage.height), 1, MaxSize);
            }
            if (w == stage.width && h == stage.height) return;

            bool Inside(Vector2Int p) => p.x < w && p.y < h;
            var lostBlocks = stage.blockedTiles.Count(b => !Inside(b));
            var lostMonsters = stage.monsters.Where(m => !Inside(m.position)).ToList();
            bool playerOut = !Inside(stage.playerStart);

            if (lostBlocks + lostMonsters.Count > 0 || playerOut)
            {
                var lines = new List<string>();
                if (lostBlocks > 0) lines.Add($"장애물 {lostBlocks}개");
                foreach (var m in lostMonsters) lines.Add($"몬스터 {Name(m.unit)} {m.position}");
                if (playerOut) lines.Add($"플레이어 시작 {stage.playerStart} → 안쪽으로 옮김");
                if (!EditorUtility.DisplayDialog("스테이지 크기 변경",
                        $"{w}×{h} 로 줄이면 격자 밖으로 나가는 배치가 있습니다.\n\n- " + string.Join("\n- ", lines) + "\n\n잘라낼까요?",
                        "잘라내기", "취소"))
                    return;
            }

            Undo.RecordObject(stage, "스테이지 크기 변경");
            stage.width = w;
            stage.height = h;
            stage.blockedTiles.RemoveAll(b => !Inside(b));
            stage.monsters.RemoveAll(m => !Inside(m.position));
            if (playerOut)
            {
                stage.playerStart = new Vector2Int(Mathf.Min(stage.playerStart.x, w - 1), Mathf.Min(stage.playerStart.y, h - 1));
                Clear(stage, stage.playerStart);
            }
            EditorUtility.SetDirty(stage);
        }

        // ---------------- 브러시 ----------------

        void DrawBrushes()
        {
            _brush = (Brush)GUILayout.Toolbar((int)_brush, BrushLabels);
            if (_brush != Brush.Monster) return;

            if (_enemies.Length > 0)
            {
                int selected = System.Array.IndexOf(_enemies, _monster);
                var labels = _enemies.Select(u => new GUIContent(
                    $"{Name(u)}  (우선순위 {(u.actionPriority > 0 ? u.actionPriority.ToString() : "-")})",
                    $"HP {u.maxHp} · 이동 {u.moveRange} · 공격 {u.attackDamage}")).ToArray();
                int next = GUILayout.SelectionGrid(selected, labels, Mathf.Min(3, _enemies.Length));
                if (next != selected && next >= 0) _monster = _enemies[next];
            }
            _monster = (UnitData)EditorGUILayout.ObjectField("칠할 몬스터", _monster, typeof(UnitData), false);
            if (_monster == null)
                EditorGUILayout.HelpBox("칠할 몬스터(UnitData, 팀 Enemy)를 고르세요.", MessageType.Warning);
            else if (_monster.team != Team.Enemy)
                EditorGUILayout.HelpBox($"'{_monster.name}' 은 팀이 Enemy 가 아닙니다.", MessageType.Warning);
        }

        // ---------------- 격자 ----------------

        void DrawGrid(HashSet<Vector2Int> problems)
        {
            var stage = Stage;
            EnsureStyles();

            float avail = EditorGUIUtility.currentViewWidth - 40f - AxisMargin;
            float cell = Mathf.Clamp(Mathf.Floor(avail / stage.width), 18f, 44f);
            var area = GUILayoutUtility.GetRect(AxisMargin + cell * stage.width, AxisMargin + cell * stage.height, GUILayout.ExpandWidth(false));
            var origin = new Vector2(area.x + AxisMargin, area.y);

            Rect CellRect(Vector2Int p) => new(origin.x + p.x * cell, origin.y + (stage.height - 1 - p.y) * cell, cell - 1f, cell - 1f);

            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            HandleMouse(e, id, p => CellRect(p).Contains(e.mousePosition));

            if (e.type != EventType.Repaint) return;

            var blocked = new HashSet<Vector2Int>(stage.blockedTiles);
            var orders = ActionOrderLabels(stage);
            _letterStyle.fontSize = Mathf.RoundToInt(cell * 0.42f);

            for (int y = 0; y < stage.height; y++)
            for (int x = 0; x < stage.width; x++)
            {
                var p = new Vector2Int(x, y);
                var r = CellRect(p);
                EditorGUI.DrawRect(r, blocked.Contains(p) ? ObstacleColor : (x + y) % 2 == 0 ? FloorA : FloorB);

                string tip = $"({x}, {y})" + (blocked.Contains(p) ? " 장애물" : "");
                if (p == stage.playerStart)
                {
                    EditorGUI.DrawRect(Inset(r, cell * 0.12f), PlayerColor);
                    Letter(r, "P", Color.white);
                    tip += " 플레이어 시작";
                }

                int mi = stage.monsters.FindIndex(m => m.position == p);
                if (mi >= 0)
                {
                    var unit = stage.monsters[mi].unit;
                    var color = unit != null ? unit.color : Color.magenta;
                    EditorGUI.DrawRect(Inset(r, cell * 0.12f), color);
                    Letter(r, unit != null && unit.displayName.Length > 0 ? unit.displayName.Substring(0, 1) : "?", ContrastText(color));
                    _orderStyle.normal.textColor = ContrastText(color);
                    if (orders.TryGetValue(mi, out var order))
                        GUI.Label(new Rect(r.x, r.y, r.width - 2f, r.height), order, _orderStyle);
                    tip += $" {Name(unit)}" + (order != null ? $" · 행동 순서 {order}" : "");
                }

                if (problems != null && problems.Contains(p)) Outline(r, ErrorOutline, 2f);
                GUI.Label(r, new GUIContent("", tip));
            }

            // 좌표 눈금: 왼쪽 y, 아래 x 는 칸이 작아 생략하고 위쪽에 x
            for (int y = 0; y < stage.height; y++)
                GUI.Label(new Rect(area.x, origin.y + (stage.height - 1 - y) * cell, AxisMargin - 2f, cell), y.ToString(), _axisStyle);
            for (int x = 0; x < stage.width; x++)
                GUI.Label(new Rect(origin.x + x * cell, origin.y + stage.height * cell, cell, AxisMargin), x.ToString(), _axisStyle);
        }

        void HandleMouse(Event e, int id, System.Func<Vector2Int, bool> hit)
        {
            var stage = Stage;
            Vector2Int? CellUnderMouse()
            {
                for (int y = 0; y < stage.height; y++)
                for (int x = 0; x < stage.width; x++)
                    if (hit(new Vector2Int(x, y))) return new Vector2Int(x, y);
                return null;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button is 0 or 1:
                    var start = CellUnderMouse();
                    if (!start.HasValue) return;
                    GUIUtility.hotControl = id;
                    Undo.IncrementCurrentGroup();
                    _strokeGroup = Undo.GetCurrentGroup();
                    _strokeBrush = e.button == 1 ? Brush.Eraser : _brush;
                    _lastPainted = null;
                    Paint(start.Value);
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    var c = CellUnderMouse();
                    if (c.HasValue && c != _lastPainted) Paint(c.Value);
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    if (_strokeGroup >= 0) Undo.CollapseUndoOperations(_strokeGroup);
                    _strokeGroup = -1;
                    e.Use();
                    break;
            }
        }

        void Paint(Vector2Int p)
        {
            _lastPainted = p;
            var stage = Stage;
            if ((_strokeBrush is Brush.Obstacle or Brush.Monster) && p == stage.playerStart) return;   // 플레이어 칸은 덮지 않음
            if (_strokeBrush == Brush.Monster && _monster == null) return;

            Undo.RecordObject(stage, "스테이지 칠하기");
            switch (_strokeBrush)
            {
                case Brush.Obstacle:
                    stage.monsters.RemoveAll(m => m.position == p);
                    if (!stage.blockedTiles.Contains(p)) stage.blockedTiles.Add(p);
                    break;
                case Brush.PlayerStart:
                    Clear(stage, p);
                    stage.playerStart = p;   // 드래그하면 따라 움직인다
                    break;
                case Brush.Monster:
                    stage.blockedTiles.Remove(p);
                    int i = stage.monsters.FindIndex(m => m.position == p);
                    if (i >= 0) stage.monsters[i] = new MonsterSpawn { unit = _monster, position = p };
                    else stage.monsters.Add(new MonsterSpawn { unit = _monster, position = p });
                    break;
                case Brush.Eraser:
                    Clear(stage, p);
                    break;
            }
            EditorUtility.SetDirty(stage);
            Repaint();
        }

        static void Clear(StageData stage, Vector2Int p)
        {
            stage.blockedTiles.RemoveAll(b => b == p);
            stage.monsters.RemoveAll(m => m.position == p);
        }

        /// <summary>
        /// 몬스터 인덱스 → 행동 순서 표시. 실제 규칙(BattleRules): 우선순위 1 이상이 작은 순서로 먼저, 0(미지정)은 맨 뒤,
        /// 같은 값끼리는 전투 시작 때 무작위 → 범위로 표시.
        /// </summary>
        static Dictionary<int, string> ActionOrderLabels(StageData stage)
        {
            var labels = new Dictionary<int, string>();
            var groups = stage.monsters
                .Select((m, i) => (m, i))
                .Where(t => t.m.unit != null)
                .GroupBy(t => t.m.unit.actionPriority > 0 ? t.m.unit.actionPriority : int.MaxValue)
                .OrderBy(g => g.Key);

            int next = 1;
            foreach (var g in groups)
            {
                int n = g.Count();
                var label = n == 1 ? next.ToString() : $"{next}~{next + n - 1}";
                foreach (var (_, i) in g) labels[i] = label;
                next += n;
            }
            return labels;
        }

        /// <summary>검증 오류와 관련된 칸 (빨간 테두리).</summary>
        HashSet<Vector2Int> ProblemCells()
        {
            var stage = Stage;
            var set = new HashSet<Vector2Int>();
            var blocked = new HashSet<Vector2Int>(stage.blockedTiles);
            if (blocked.Contains(stage.playerStart)) set.Add(stage.playerStart);
            foreach (var g in stage.monsters.GroupBy(m => m.position))
            {
                if (g.Count() > 1 || blocked.Contains(g.Key) || g.Key == stage.playerStart) set.Add(g.Key);
                if (g.Any(m => m.unit == null || m.unit.team != Team.Enemy)) set.Add(g.Key);
            }
            return set;
        }

        // ---------------- 실행 버튼 ----------------

        void DrawActions(bool valid)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("▶ 이 스테이지만 플레이", "Battle 씬을 열고 RunConfig 의 플레이어·시작 덱으로 이 스테이지 한 판만 진행")))
                        PlayThisStage();
                }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("씬 보드에 미리보기", "열린 씬의 BoardView 에 이 스테이지를 깔아 본다 (저장되지 않음)")))
                        PreviewOnBoard();
                }
            }
        }

        void PlayThisStage()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssetIfDirty(Stage);

            if (Object.FindFirstObjectByType<RunController>() == null)
                EditorSceneManager.OpenScene(BattleScenePath);

            SessionState.SetString(RunController.TestStageKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Stage)));
            EditorApplication.EnterPlaymode();
        }

        void PreviewOnBoard()
        {
            var board = Object.FindFirstObjectByType<BoardView>();
            if (board == null)
            {
                EditorUtility.DisplayDialog("미리보기", "열린 씬에 BoardView 가 없습니다. Battle 씬을 여세요.", "확인");
                return;
            }
            var so = new SerializedObject(board);
            so.FindProperty("previewStage").objectReferenceValue = Stage;
            so.ApplyModifiedProperties();
            board.BuildPreview();
            Selection.activeObject = board.gameObject;
            SceneView.FrameLastActiveSceneView();
        }

        // ---------------- 그리기 도우미 ----------------

        void EnsureStyles()
        {
            _letterStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            _orderStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.UpperRight, normal = { textColor = Color.white } };
            _axisStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        }

        void Letter(Rect r, string text, Color color)
        {
            _letterStyle.normal.textColor = color;
            GUI.Label(r, text, _letterStyle);
        }

        static Rect Inset(Rect r, float d) => new(r.x + d, r.y + d, r.width - d * 2f, r.height - d * 2f);

        static void Outline(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static Color ContrastText(Color bg) => bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f > 0.6f ? Color.black : Color.white;

        static string Name(UnitData u) => u == null ? "(비어 있음)" : string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName;
    }
}
