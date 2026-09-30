using ProvidenceKnight.View;
using UnityEditor;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// BoardView 인스펙터: "스테이지 미리보기"로 previewStage 의 타일·유닛을 씬에 깐다 (저장되지 않음).
    /// HUD 의 BoardArea 를 옮기며 필드가 어떻게 보이는지 바로 확인할 때 쓴다. Play 에 들어가기 전 자동으로 지운다.
    /// </summary>
    [CustomEditor(typeof(BoardView))]
    public class BoardViewEditor : UnityEditor.Editor
    {
        [InitializeOnLoadMethod]
        static void ClearPreviewsBeforePlay()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                foreach (var board in Object.FindObjectsByType<BoardView>(FindObjectsSortMode.None))
                    board.ClearPreview();
            };
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var board = (BoardView)target;
            if (Application.isPlaying) return;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(board.PreviewStage == null))
                {
                    if (GUILayout.Button("스테이지 미리보기"))
                        board.BuildPreview();
                }
                if (GUILayout.Button("미리보기 지우기"))
                    board.ClearPreview();
            }
            if (board.PreviewStage == null)
                EditorGUILayout.HelpBox("Preview Stage 에 StageData 를 넣으면 필드를 미리 깔아 볼 수 있습니다. 미리보기는 씬에 저장되지 않습니다.", MessageType.Info);
        }
    }
}
