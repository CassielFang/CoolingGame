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

            // 图在框里占哪块和框摆在哪必须用同一份结果：先算摆放，再摆图，最后按
            // 「图露出来的边」把框挪到位
            ApplySide(_left, true, tuning, canvasSize, live);
            ApplySide(_right, false, tuning, canvasSize, live);
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
            if (image == null)
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

            // 接管图的摆放时，真正显示的是子物体 Art（根 Image 被 matcher 关掉了），颜色要同步过去
            var matcher = image.GetComponent<VnPortraitMatcher>();
            if (matcher != null && matcher.enabled)
            {
                matcher.SyncTint(image.color);
            }
        }

        /// <summary>
        /// 一侧的完整处理：先算「图在框里占哪块」，把图摆上去，再按同一份结果把框挪到
        /// 「图露出来的边正好落在边距上」的位置。两步共用一次计算，所以不会再出现
        /// 「数值对、画面错」。
        /// </summary>
        private void ApplySide(Image image, bool isLeft, VnStandingSettings tuning, Vector2 canvasSize, bool live)
        {
            if (image == null)
            {
                return;
            }

            VnStandingSide side = tuning.ResolveSide(isLeft, live);
            Vector2 frameSize = side.ScaledSize();
            Vector2 native = NativeSizeOf(image);

            // 三种模式（老行为 / 统一基准高 / 原始尺寸）都由 ResolveArt 给出最终摆放（画布单位）
            ArtImagePlacement placement = tuning.ResolveArt(isLeft, live, canvasSize, native,
                tuning.ResolveFlip(isLeft, live));

            // 接管了才按「图的可见边」量；没接管沿用老行为（量框的边缘），保证和代码原值一致
            bool visibleEdge = live && tuning.IsSideActive(isLeft);
            side.ApplyPosition(frameSize, canvasSize, placement.VisibleRect, visibleEdge);
            side.WriteTo(image.rectTransform);

            // 框写完之后再摆图：图是框的子物体，要把缩放折回去才不会双重缩放
            ApplyArt(image, placement, native, live && tuning.applyArt, side.ScaleAbs);
        }

        private static Vector2 NativeSizeOf(Image image)
        {
            Sprite sprite = image != null ? image.sprite : null;
            return sprite != null ? sprite.rect.size : new Vector2(VnStandingSide.DefaultWidth,
                VnStandingSide.DefaultHeight);
        }

        /// <summary>
        /// 把图摆进框（只在「管图的摆放」开着时挂 matcher，否则回到原 Image 行为）。
        /// <paramref name="parentScale"/> 是框的 localScale：图是框的子物体会被它再缩一次，
        /// 所以这里先把画布单位的摆放折成框的局部单位。
        /// </summary>
        private static void ApplyArt(Image image, ArtImagePlacement placement, Vector2 native, bool artOn,
                                     float parentScale)
        {
            var matcher = image.GetComponent<VnPortraitMatcher>();

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

            if (image.sprite == null)
            {
                return;
            }

            float s = parentScale > 0.0001f ? parentScale : 1f;
            ArtImagePlacement local = new ArtImagePlacement
            {
                Scale = placement.Scale / s,
                Size = placement.Size / s,
                Offset = placement.Offset / s
            };

            // 超出框的部分一律裁掉：框就是画框，露出来的边才是边距量的那条边
            matcher.SetPlacement(local, native, true);
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
