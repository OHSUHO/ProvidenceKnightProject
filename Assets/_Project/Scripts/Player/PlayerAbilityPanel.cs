using System;
using UnityEngine;

namespace ProvidenceKnight.Player
{
    /// <summary>테스트 씬용 런타임 토글 패널. Tab 으로 열고 닫으며, 체크박스로 능력을 즉시 잠그거나 푼다.</summary>
    public class PlayerAbilityPanel : MonoBehaviour
    {
        [SerializeField] PlayerController2D player;
        [SerializeField] bool visible = true;

        static readonly string[] Labels =
        {
            "좌우 이동", "점프", "달리기", "앉기", "앉아서 이동", "이단 점프", "벽 붙기(미끄럼)", "벽 오르기", "벽 점프", "활강",
        };

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible || player == null) return;
            var abilities = Enum.GetValues(typeof(Ability));
            GUILayout.BeginArea(new Rect(10, 10, 220, 400), GUI.skin.box);
            GUILayout.Label($"상태: {player.State}  (Tab: 패널 토글)");
            int i = 0;
            foreach (Ability a in abilities)
            {
                bool on = player.Abilities.Has(a);
                bool next = GUILayout.Toggle(on, Labels[i++]);
                if (next != on) player.Abilities.Set(a, next);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전부 해제")) player.Abilities.SetAll(true);
            if (GUILayout.Button("전부 잠금")) player.Abilities.SetAll(false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
