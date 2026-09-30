using ProvidenceKnight.Data;
using UnityEditor;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// CardData 인스펙터. 효과 리스트 UI 는 EffectListDrawer 가 그린다.
    /// 맨 아래에 자동 생성되는 카드 설명을 미리 보여준다.
    /// </summary>
    [CustomEditor(typeof(CardData))]
    public class CardDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EffectListDrawer.DrawInspector(serializedObject, nameof(CardData.effects), "Effects (위에서부터 순서대로 실행)");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("카드 설명 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(((CardData)target).GetDescription(), MessageType.None);
        }
    }
}
