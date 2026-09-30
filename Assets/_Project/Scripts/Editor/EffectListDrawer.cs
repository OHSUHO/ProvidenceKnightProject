using System;
using System.Linq;
using ProvidenceKnight.Battle.Effects;
using UnityEditor;
using UnityEngine;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>
    /// [SerializeReference] List&lt;CardEffect&gt; 를 그리는 공용 도우미. 기본 인스펙터에서는 종류를 고를 수 없어서
    /// "＋ 효과 추가" 메뉴(CardEffect 하위 클래스 자동 수집)와 순서 변경/삭제 버튼을 직접 그린다.
    /// </summary>
    static class EffectListDrawer
    {
        static readonly Type[] EffectTypes = TypeCache.GetTypesDerivedFrom<CardEffect>()
            .Where(t => !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name)
            .ToArray();

        /// <summary>serializedObject 의 기본 필드를 모두 그리되, listField 만 효과 리스트 UI 로 그린다.</summary>
        public static void DrawInspector(SerializedObject so, string listField, string label)
        {
            so.Update();
            var it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                    continue;
                }
                if (it.name == listField)
                {
                    DrawList(so, it.Copy(), label);
                    continue;
                }
                EditorGUILayout.PropertyField(it, true);
            }
            so.ApplyModifiedProperties();
        }

        static void DrawList(SerializedObject so, SerializedProperty list, string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

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
                var path = list.propertyPath;
                foreach (var type in EffectTypes)
                {
                    var t = type;
                    menu.AddItem(new GUIContent(t.Name), false, () => Add(so, path, t));
                }
                menu.ShowAsContext();
            }
        }

        static void Add(SerializedObject so, string listPath, Type type)
        {
            so.Update();
            var list = so.FindProperty(listPath);
            list.arraySize++;
            // arraySize++ 는 마지막 원소의 참조를 복제하므로 반드시 새 인스턴스로 덮어쓴다 (공유 방지)
            list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(type);
            so.ApplyModifiedProperties();
        }
    }
}
