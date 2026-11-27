using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 「电影感」的薄薄一层：黑场转场、胶片颗粒、氛围暗角、瞬间闪光。
    ///
    /// 为什么是 UI 层而不是 URP 后处理：当前整个画面（桌面、卡牌、笔记本）都是
    /// Screen Space - Overlay 的 UGUI，Volume 后处理对 Overlay 不产生任何效果。
    /// 要让后处理生效得先把 Canvas 改成 Screen Space - Camera，那是一次独立的渲染管线改造，
    /// 不该和玩法迭代绑在一起。这里先给出零风险的「胶片」层：
    /// - 转场：黑场淡入 → 换内容 → 淡出，切换阶段/场景时遮住重建
    /// - 颗粒：低透明度噪声，每 60ms 抖动一次，给静态画面一点颗粒感
    /// - 暗角：常驻压角，换阶段时加深一档
    /// - 闪光：线索入档、破案时的一次性高光
    /// </summary>
    public sealed class CinematicDirector : MonoBehaviour
    {
        private CanvasGroup _fadeGroup;
        private CanvasGroup _flashGroup;
        private CanvasGroup _vignetteGroup;
        private RawImage _grain;
        private float _grainTimer;
        private Coroutine _transition;
        private Coroutine _flashRoutine;
        private float _vignetteBase = 0.55f;
        private float _vignettePulse;

        public bool IsTransitioning
        {
            get { return _transition != null; }
        }

        public static CinematicDirector Create(Transform parent)
        {
            RectTransform root = UIFactory.MakePanel(parent, "Cinematic", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(root);
            root.GetComponent<Image>().raycastTarget = false;

            var director = root.gameObject.AddComponent<CinematicDirector>();
            director.Build(root);
            return director;
        }

        /// <summary>
        /// 黑场转场：淡入黑 → 执行 <paramref name="midpoint"/>（重建画面/播剧情）→ 淡出。
        /// 用在场景切换与阶段切换。
        /// </summary>
        public void Transition(Action midpoint, float fadeIn = 0.28f, float hold = 0.14f, float fadeOut = 0.46f)
        {
            if (_transition != null)
            {
                StopCoroutine(_transition);
            }

            _transition = StartCoroutine(TransitionRoutine(midpoint, fadeIn, hold, fadeOut));
        }

        /// <summary>一次性闪光：新线索、破案、命中关键配方。</summary>
        public void Flash(Color color, float peak = 0.55f, float duration = 0.42f)
        {
            if (_flashGroup == null)
            {
                return;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashGroup.GetComponent<Image>().color = color;
            _flashRoutine = StartCoroutine(FlashRoutine(peak, duration));
        }

        /// <summary>让暗角短暂加深一档（换阶段、失败反馈）。</summary>
        public void PulseVignette(float extra, float duration = 0.8f)
        {
            StartCoroutine(PulseRoutine(extra, duration));
        }

        // ── 搭建 ────────────────────────────────────────────────────────────

        private void Build(RectTransform root)
        {
            // 0) 台灯光池：压出「桌上有一盏灯」的氛围（位置与 CardView.LightPosition 对应）
            RectTransform pool = UIFactory.MakePanel(root, "LightPool", new Color(1f, 0.94f, 0.82f, 0.30f), false);
            pool.anchorMin = new Vector2(0.71f, 0.73f);
            pool.anchorMax = new Vector2(0.71f, 0.73f);
            pool.pivot = new Vector2(0.5f, 0.5f);
            pool.sizeDelta = new Vector2(1500f, 1500f);
            var poolImage = pool.GetComponent<Image>();
            poolImage.sprite = UIFactory.LightPool;
            poolImage.type = Image.Type.Simple;
            poolImage.raycastTarget = false;

            // 1) 胶片颗粒（RawImage 才有 uvRect，用来抖动噪声）
            var grainGo = new GameObject("FilmGrain", typeof(RectTransform), typeof(RawImage));
            grainGo.transform.SetParent(root, false);
            var grainRect = (RectTransform)grainGo.transform;
            UIFactory.Stretch(grainRect);
            _grain = grainGo.GetComponent<RawImage>();
            _grain.texture = CreateNoiseTexture(128);
            _grain.color = new Color(1f, 1f, 1f, 0.05f);
            _grain.raycastTarget = false;
            _grain.uvRect = new Rect(0f, 0f, 3f, 3f);

            // 2) 氛围暗角
            RectTransform vignette = UIFactory.MakePanel(root, "Atmosphere", new Color(1f, 1f, 1f, 1f), false);
            UIFactory.Stretch(vignette);
            var vignetteImage = vignette.GetComponent<Image>();
            vignetteImage.sprite = UIFactory.Vignette;
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.color = new Color(1f, 1f, 1f, _vignetteBase);
            vignetteImage.raycastTarget = false;
            _vignetteGroup = vignette.gameObject.AddComponent<CanvasGroup>();
            _vignetteGroup.alpha = 1f;

            // 3) 闪光
            RectTransform flash = UIFactory.MakePanel(root, "Flash", new Color(1f, 0.96f, 0.86f, 0f), false);
            UIFactory.Stretch(flash);
            var flashImage = flash.GetComponent<Image>();
            flashImage.raycastTarget = false;
            _flashGroup = flash.gameObject.AddComponent<CanvasGroup>();
            _flashGroup.alpha = 0f;

            // 4) 黑场（最上层）
            RectTransform fade = UIFactory.MakePanel(root, "Fade", new Color(0f, 0f, 0f, 1f), false);
            UIFactory.Stretch(fade);
            var fadeImage = fade.GetComponent<Image>();
            fadeImage.raycastTarget = false;
            _fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
        }

        private static Texture2D CreateNoiseTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var random = new System.Random(7);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = (float)random.NextDouble();
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, value));
                }
            }

            texture.Apply();
            return texture;
        }

        // ── 动画 ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_grain == null)
            {
                return;
            }

            _grainTimer -= Time.unscaledDeltaTime;
            if (_grainTimer > 0f)
            {
                return;
            }

            _grainTimer = 0.06f;
            _grain.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 3f, 3f);
            _grain.color = new Color(1f, 1f, 1f, 0.010f + UnityEngine.Random.value * 0.008f);
        }

        private IEnumerator TransitionRoutine(Action midpoint, float fadeIn, float hold, float fadeOut)
        {
            yield return FadeFade(0f, 1f, fadeIn);

            if (midpoint != null)
            {
                midpoint();
            }

            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            yield return FadeFade(1f, 0f, fadeOut);
            _transition = null;
        }

        private IEnumerator FadeFade(float from, float to, float duration)
        {
            if (_fadeGroup == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _fadeGroup.alpha = to;
        }

        private IEnumerator FlashRoutine(float peak, float duration)
        {
            float half = duration * 0.35f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                _flashGroup.alpha = Mathf.Lerp(0f, peak, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration - half)
            {
                elapsed += Time.unscaledDeltaTime;
                _flashGroup.alpha = Mathf.Lerp(peak, 0f, Mathf.Clamp01(elapsed / (duration - half)));
                yield return null;
            }

            _flashGroup.alpha = 0f;
            _flashRoutine = null;
        }

        private IEnumerator PulseRoutine(float extra, float duration)
        {
            if (_vignetteGroup == null)
            {
                yield break;
            }

            float half = duration * 0.4f;
            float elapsed = 0f;
            float baseAlpha = _vignetteBase + _vignettePulse;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                _vignettePulse = Mathf.Lerp(0f, extra, Mathf.Clamp01(elapsed / half));
                _vignetteGroup.alpha = Mathf.Clamp01((baseAlpha + _vignettePulse) / _vignetteBase);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration - half)
            {
                elapsed += Time.unscaledDeltaTime;
                _vignettePulse = Mathf.Lerp(extra, 0f, Mathf.Clamp01(elapsed / (duration - half)));
                _vignetteGroup.alpha = Mathf.Clamp01((_vignetteBase + _vignettePulse) / _vignetteBase);
                yield return null;
            }

            _vignettePulse = 0f;
            _vignetteGroup.alpha = 1f;
        }
    }
}
