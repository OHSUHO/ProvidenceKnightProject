using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>아트 없이 프로토타이핑하기 위한 런타임 생성 스프라이트 / 폰트.</summary>
    public static class SpriteFactory
    {
        static Sprite _square;
        static Sprite _triangle;
        static Font _font;

        /// <summary>+X 방향을 가리키는 1x1 유닛 크기의 흰색 삼각형 (화살촉용).</summary>
        public static Sprite Triangle
        {
            get
            {
                if (_triangle != null) return _triangle;
                const int n = 32;
                var tex = new Texture2D(n, n) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "ProtoTriangle" };
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 왼쪽 변 전체 → 오른쪽 가운데 꼭짓점
                    float halfHeight = (1f - (x + 0.5f) / n) * 0.5f;
                    bool inside = Mathf.Abs((y + 0.5f) / n - 0.5f) <= halfHeight;
                    pixels[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _triangle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                _triangle.name = "ProtoTriangle";
                return _triangle;
            }
        }

        /// <summary>1x1 유닛 크기의 흰색 네모.</summary>
        public static Sprite Square
        {
            get
            {
                if (_square != null) return _square;
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, name = "ProtoSquare" };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels32(pixels);
                tex.Apply();
                _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                _square.name = "ProtoSquare";
                return _square;
            }
        }

        /// <summary>한글이 나오는 OS 동적 폰트 (없으면 기본 폰트).</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" }, 32);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }
    }
}
