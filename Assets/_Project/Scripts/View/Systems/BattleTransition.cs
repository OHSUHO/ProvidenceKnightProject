using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProvidenceKnight.View
{
    /// <summary>
    /// 전투 진입 암전 연출. 화면을 5x5 칸으로 나눠, 네 꼭짓점 칸에서 동시에 시계 방향 소용돌이로 안쪽을 향해 한 칸씩 암전시키고
    /// 전부 덮이면 씬을 옮긴다. 새 씬이 뜨면 화면을 걷어낸다. 어디서든 <c>BattleTransition.Play("Battle")</c> 로 호출.
    /// </summary>
    public class BattleTransition : MonoBehaviour
    {
        const int N = 5;

        public static bool Running { get; private set; }

        [Min(0.01f)] public float stepDelay = 0.09f;
        [Min(0.01f)] public float cellFade = 0.12f;
        public float holdBlack = 0.25f;
        public float revealTime = 0.4f;
        public Color color = Color.black;

        /// <param name="onCovered">전 칸이 암전된 직후, 씬 로드 직전에 호출 (테스트용 훅).</param>
        public static void Play(string sceneName, Action onCovered = null)
        {
            if (Running) return;
            var go = new GameObject("BattleTransition");
            DontDestroyOnLoad(go);
            go.AddComponent<BattleTransition>().Begin(sceneName, onCovered);
        }

        void Begin(string sceneName, Action onCovered) => StartCoroutine(Run(sceneName, onCovered));

        IEnumerator Run(string sceneName, Action onCovered)
        {
            Running = true;
            var cells = Build();
            int maxRank = 0;
            var ranks = new int[N * N];
            for (int i = 0; i < ranks.Length; i++)
            {
                ranks[i] = CellRank(i % N, i / N);
                maxRank = Mathf.Max(maxRank, ranks[i]);
            }

            float t = 0f, total = maxRank * stepDelay + cellFade;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < cells.Length; i++)
                    SetAlpha(cells[i], Mathf.Clamp01((t - ranks[i] * stepDelay) / cellFade));
                yield return null;
            }
            foreach (var c in cells) SetAlpha(c, 1f);

            onCovered?.Invoke();
            yield return new WaitForSecondsRealtime(holdBlack);

            var load = SceneManager.LoadSceneAsync(sceneName);
            while (!load.isDone) yield return null;
            yield return null;

            for (float r = 0f; r < revealTime; r += Time.unscaledDeltaTime)
            {
                float a = 1f - r / revealTime;
                foreach (var c in cells) SetAlpha(c, a);
                yield return null;
            }
            Running = false;
            Destroy(gameObject);
        }

        /// <summary>모서리 4곳에서 동시에 출발한 시계 방향 나선 중 가장 빨리 닿는 순번. 같은 순번의 칸은 동시에 암전된다.</summary>
        public static int CellRank(int x, int y)
        {
            int best = int.MaxValue;
            int cx = x, cy = y;
            for (int k = 0; k < 4; k++)
            {
                best = Mathf.Min(best, SpiralRank(cx, cy));
                (cx, cy) = (N - 1 - cy, cx); // 90° 회전 → 다음 모서리에서 출발한 나선
            }
            return best;
        }

        // (0,0) 좌상단에서 오른쪽 → 아래 → 왼쪽 → 위 순으로 감아 들어가는 나선의 순번
        static int SpiralRank(int x, int y)
        {
            int ring = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y));
            int before = 0;
            for (int r = 0; r < ring; r++) before += N - 1 - 2 * r; // 네 나선이 링을 나눠 맡으므로 한 변 길이만큼씩
            int lo = ring, hi = N - 1 - ring;
            if (lo == hi) return before;
            int side = hi - lo, pos;
            if (y == lo && x < hi) pos = x - lo;
            else if (x == hi && y < hi) pos = side + (y - lo);
            else if (y == hi && x > lo) pos = 2 * side + (hi - x);
            else pos = 3 * side + (hi - y);
            return before + pos;
        }

        Image[] Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var cells = new Image[N * N];
            for (int i = 0; i < cells.Length; i++)
            {
                int x = i % N, y = i / N;
                var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)go.transform;
                // y=0 이 화면 위쪽. 이음새가 비지 않게 약간 겹친다.
                rt.anchorMin = new Vector2(x / (float)N, 1f - (y + 1) / (float)N);
                rt.anchorMax = new Vector2((x + 1) / (float)N, 1f - y / (float)N);
                rt.offsetMin = new Vector2(-1f, -1f);
                rt.offsetMax = new Vector2(1f, 1f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                SetAlpha(img, 0f);
                cells[i] = img;
            }
            return cells;
        }

        void SetAlpha(Image img, float a)
        {
            var c = color;
            c.a = a;
            img.color = c;
        }
    }
}
