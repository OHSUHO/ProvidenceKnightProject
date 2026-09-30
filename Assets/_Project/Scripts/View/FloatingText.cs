using DG.Tweening;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>위로 떠오르며 사라지는 숫자 팝업 (피해/방어도 획득 표시용).</summary>
    public static class FloatingText
    {
        public static void Spawn(Vector3 worldPos, string text, Color color, Transform parent, float cellSize)
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            tm.font = SpriteFactory.Font;
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.GetComponent<MeshRenderer>().sortingOrder = 20;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = 0.03f * cellSize;
            tm.text = text;
            tm.color = color;

            var seq = DOTween.Sequence();
            seq.Append(go.transform.DOMoveY(go.transform.position.y + 0.55f * cellSize, 0.7f).SetEase(Ease.OutCubic));
            seq.Join(DOTween.To(() => tm.color, c => tm.color = c, new Color(color.r, color.g, color.b, 0f), 0.7f).SetEase(Ease.InQuad).SetDelay(0.15f));
            seq.OnComplete(() => Object.Destroy(go));
        }
    }
}
