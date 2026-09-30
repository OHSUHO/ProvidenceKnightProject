using ProvidenceKnight.Data;
using UnityEditor;

namespace ProvidenceKnight.EditorTools
{
    /// <summary>UnitData 인스펙터. 턴 시작 효과 리스트를 "＋ 효과 추가" 로 편집한다 (EffectListDrawer).</summary>
    [CustomEditor(typeof(UnitData))]
    public class UnitDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() =>
            EffectListDrawer.DrawInspector(serializedObject, nameof(UnitData.turnStartEffects), "Turn Start Effects (매 턴 시작 시, 위에서부터)");
    }
}
