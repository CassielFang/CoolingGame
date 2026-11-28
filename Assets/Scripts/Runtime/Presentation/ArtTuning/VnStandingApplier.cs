using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 运行时把「正在调」的 <see cref="VnStandingSettings"/> 打到视觉小说控件上（由 DialogueOverlay 在 Build 时挂载）。
    /// 每帧读一次工作副本——美术调试窗口改的就是它，所以编辑器里拖滑条，Play 中的画面当场跟着动，不用重进 Play。
    ///
    /// 没启用任何一项时不碰任何东西：一切沿用 DialogueOverlay 里写死的行为。
    /// </summary>
    public sealed class VnStandingApplier : MonoBehaviour
    {
        private Image _left;
        private Image _right;
        private Image _backdrop;
        private RectTransform _canvas;

        // 上一次的开关状态，用来只在状态切换时才做「退回出厂」的清理
        private bool _wasBackgroundOn;

        internal void Bind(Image left, Image right, Image backdrop, RectTransform canvas)
        {
            _left = left;
            _right = right;
            _backdrop = backdrop;
            _canvas = canvas;
        }

        /// <summary>参数刚被改过：立即重算一次（不依赖下一帧）。</summary>
        public void MarkDirty()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>读一次工作副本并打上去。</summary>
        private void Apply()
        {
            VnStandingSettings tuning = ArtTuningStore.WorkingVnStanding;
            if (tuning == null)
            {
                return;
            }

            tuning.Sanitize();

            Vector2 canvasSize = _canvas != null && _canvas.rect.size.x > 1f
                ? _canvas.rect.size
                : new Vector2(1920f, 1080f);

            bool live = tuning.apply;

            ApplyFrame(_left, true, tuning, canvasSize, live);
            ApplyFrame(_right, false, tuning, canvasSize, live);
            ApplyImage(_left, true, tuning, canvasSize, live);
            ApplyImage(_right, false, tuning, canvasSize, live);
            ApplyBackdrop(tuning);
            ApplyDim();
        }

        /// <summary>
        /// 压暗由这里统一决定：每帧按「这一侧是不是正在说话」重算亮度。
        /// 没接管（总开关或「接管压暗」关着）= 原值 0.42；接管了则读统一的变暗强度（两侧共用）。
        /// </summary>
        private void ApplyDim()
        {
            var overlay = _left != null ? _left.GetComponentInParent<DialogueOverlay>() : null;
            if (overlay == null)
            {
                return;
            }

            VnStandingSettings tuning = ArtTuningStore.WorkingVnStanding;
            bool live = tuning != null && tuning.apply;

            ApplySideDim(overlay.LeftPortrait, overlay.SpeakingLeft, tuning, live, true);
            ApplySideDim(overlay.RightPortrait, overlay.SpeakingRight, tuning, live, false);
        }

        private static void ApplySideDim(Image image, bool speaking, VnStandingSettings tuning, bool live, bool isLeft)
        {
            if (image == null || !image.enabled)
            {
                return;
            }

            // 画面上的实例是 VnStandingApplier 每帧刷新的工作副本，这里按「工作副本」的规则取值：
            // ResolveDim 的第二个参数是 useLiveSettings；变暗强度两侧共用，isLeft 只用于将来分侧扩展
            float value = speaking ? 1f : tuning != null ? tuning.ResolveDim(isLeft, live) : VnStandingSide.DefaultDim;

            Color color = image.color;
            if (Mathf.Abs(color.r - value) < 0.001f
                && Mathf.Abs(color.g - value) < 0.001f
                && Mathf.Abs(color.b - value) < 0.001f)
            {
                return;
            }

            image.color = new Color(value, value, value, color.a);
        }

        private void ApplyFrame(Image image, bool isLeft, VnStandingSettings tuning, Vector2 canvasSize, bool live)
        {
            if (image == null)
            {
                return;
            }

            VnStandingSide side = tuning.ResolveSide(isLeft, live);
            side.WriteTo(image.rectTransform);
        }

        private void ApplyImage(Image image, bool isLeft, VnStandingSettings tuning, Vector2 canvasSize, bool live)
        {
            if (image == null)
            {
                return;
            }

            var matcher = image.GetComponent<VnPortraitMatcher>();
            bool artOn = live && tuning.applyArt;

            if (!artOn)
            {
                if (matcher != null && matcher.enabled)
                {
                    matcher.enabled = false;
                }

                return;
            }

            if (matcher == null)
            {
                matcher = image.gameObject.AddComponent<VnPortraitMatcher>();
            }

            if (!matcher.enabled)
            {
                matcher.enabled = true;
            }

            Sprite sprite = image.sprite;
            if (sprite == null)
            {
                return;
            }

            Vector2 native = sprite.rect.size;
            ArtImagePlacement placement = tuning.ResolveArt(isLeft, live, canvasSize, native,
                tuning.ResolveFlip(isLeft, live));

            // 「统一基准高 / 塞进框」这两种老模式没有裁切；原始尺寸模式一律按框裁
            bool clip = !(tuning.unifiedHeight > 0f || tuning.ResolveSide(isLeft, live).rawMode == ArtRawMode.FitInside);
            matcher.SetPlacement(placement, native, clip);
        }

        private void ApplyBackdrop(VnStandingSettings tuning)
        {
            if (_backdrop == null)
            {
                return;
            }

            if (!tuning.applyBackground)
            {
                if (_wasBackgroundOn)
                {
                    // 退回原行为：纯黑铺满
                    var old = _backdrop.GetComponent<VnBackdropGradient>();
                    if (old != null)
                    {
                        old.enabled = false;
                    }

                    _backdrop.sprite = null;
                    _backdrop.type = Image.Type.Simple;
                    _backdrop.color = new Color(0.02f, 0.02f, 0.02f, 1f);
                    _wasBackgroundOn = false;
                }

                return;
            }

            var gradient = _backdrop.GetComponent<VnBackdropGradient>();
            if (gradient == null)
            {
                gradient = _backdrop.gameObject.AddComponent<VnBackdropGradient>();
            }

            if (!gradient.enabled)
            {
                gradient.enabled = true;
            }

            gradient.SetColors(tuning.backgroundTop, tuning.backgroundBottom);

            // 只有颜色真的变了才重画贴图（1×256，很便宜，但没必要每帧）
            if (gradient.RefreshIfNeeded())
            {
                _backdrop.color = Color.white;
            }

            _wasBackgroundOn = true;
        }
    }
}
