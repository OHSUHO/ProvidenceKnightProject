using System;
using System.Linq;
using ProvidenceKnight.Battle.Effects;
using ProvidenceKnight.Data;
using UnityEditor;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// CardData 인스펙터. [SerializeReference] 효과 리스트는 기본 인스펙터에서 종류를 고를 수 없어서,
    /// "＋ 효과 추가" 메뉴(CardEffect 하위 클래스 자동 수집)와 순서 변경/삭제 버튼을 직접 그린다.
    /// 맨 아래에 자동 생성되는 카드 설명을 미리 보여준다.
    /// </summary>
    [CustomEditor(typeof(CardData))]
    public class CardDataEditor : UnityEditor.Editor
    {
        const string EffectsField = nameof(CardData.effects);

        static readonly Type[] EffectTypes = TypeCache.GetTypesDerivedFrom<CardEffect>()
            .Where(t => !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name)
            .ToArray();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                    continue;
                }
                if (it.name == EffectsField)
                {
                    DrawEffects(it.Copy());
                    continue;
                }
                EditorGUILayout.PropertyField(it, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("카드 설명 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(((CardData)target).GetDescription(), MessageType.None);
        }

        void DrawEffects(SerializedProperty list)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effects (위에서부터 순서대로 실행)", EditorStyles.boldLabel);

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var typeName = element.managedReferenceValue?.GetType().Name ?? "(비어 있음)";
                        EditorGUILayout.LabelField($"{i + 1}. {typeName}", EditorStyles.boldLabel);

                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button("▲", GUILayout.Width(24))) { list.MoveArrayElement(i, i - 1); return; }
                        using (new EditorGUI.DisabledScope(i == list.arraySize - 1))
                            if (GUILayout.Button("▼", GUILayout.Width(24))) { list.MoveArrayElement(i, i + 1); return; }
                        if (GUILayout.Button("✕", GUILayout.Width(24))) { list.DeleteArrayElementAtIndex(i); return; }
                    }

                    // 효과의 필드들 (필드가 없는 효과는 아무것도 안 그림)
                    var end = element.GetEndProperty();
                    var child = element.Copy();
                    EditorGUI.indentLevel++;
                    for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                        EditorGUILayout.PropertyField(child, true);
                    EditorGUI.indentLevel--;
                }
            }

            if (GUILayout.Button("＋ 효과 추가"))
            {
                var menu = new GenericMenu();
                foreach (var type in EffectTypes)
                {
                    var t = type;
                    menu.AddItem(new GUIContent(t.Name), false, () => AddEffect(t));
                }
                menu.ShowAsContext();
            }
        }

        void AddEffect(Type type)
        {
            serializedObject.Update();
            var list = serializedObject.FindProperty(EffectsField);
            list.arraySize++;
            // arraySize++ 는 마지막 원소의 참조를 복제하므로 반드시 새 인스턴스로 덮어쓴다 (공유 방지)
            list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
