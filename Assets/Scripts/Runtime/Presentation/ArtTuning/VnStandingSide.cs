using System;
using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 视觉小说里单侧的立绘。这里只放**美术需要调的那几项**，没有 Anchor / Pivot / SizeDelta 这类程序字段：
    /// - 贴在屏幕哪个位置（六选一的枚举）；
    /// - 整体缩放（就是 Transform 的 localScale）；
    /// - 离屏幕左边 / 右边 / 底边多远（框边缘到屏幕边缘的像素）。
    ///
    /// 出厂默认值 = DialogueOverlay 原来写死的那套：左侧贴左下角、离左边 70、离底边 40、框 700×900；右侧左右镜像。
    ///
    /// 程序语义（Anchor / Pivot / AnchoredPosition）由 <see cref="PortraitAnchorMath"/> 现场算出来，
    /// 界面上不出现，美术也不用手填。
    /// </summary>
    [Serializable]
    public sealed class VnStandingSide
    {
        public const float DefaultInsetX = 70f;
        public const float DefaultInsetY = 40f;
        public const float DefaultWidth = 700f;
        public const float DefaultHeight = 900f;
        public const float DefaultDim = 0.42f;

        [Tooltip("这一侧是否接管。关着 = 完全用 DialogueOverlay 的原值")]
        public bool apply;

        [Header("位置")]
        [Tooltip("立绘贴在屏幕的哪个位置：中央 / 左边中点 / 右边中点 / 左下角 / 右下角 / 底边中点")]
        public PortraitAnchor anchor = PortraitAnchor.BottomLeft;

        [Tooltip("离屏幕左边的距离（像素）。只有靠左的位置（左边中点、左下角）用得到")]
        public float gapLeft = DefaultInsetX;

        [Tooltip("离屏幕右边的距离（像素）。只有靠右的位置（右边中点、右下角）用得到")]
        public float gapRight = DefaultInsetX;

        [Tooltip("离屏幕底边的距离（像素）。只有贴底的位置（左下角、右下角、底边中点）用得到")]
        public float gapBottom = DefaultInsetY;

        [Header("大小")]
        [Tooltip("等比例缩放，就是 Transform 的 Local Scale。1 = 原始大小")]
        public float scale = 1f;

        [Tooltip("立绘框的原始尺寸（缩放前）。一般不用改")]
        public Vector2 baseSize = new Vector2(DefaultWidth, DefaultHeight);

        [Header("变暗")]
        [Tooltip("别人说话时这一侧的变暗强度：0 = 不变暗，1 = 全黑")]
        [Range(0f, 1f)]
        public float dim = DefaultDim;

        [Header("位置（界面自动换算，不用手填）")]
        [Tooltip("图在框里的摆放：按图原图像素 1:1 摆、超出的由框裁掉时，边距量的是图露出来的边；否则量框")]
        public bool positionOnVisibleEdge = true;

        [Tooltip("框在画布上的锚点（AnchoredPosition）。由「贴哪个位置 + 边距 + 图的可见边缘」算出来，界面不显示")]
        public Vector2 pos;

        [Header("立绘素材")]
        [Tooltip("图比例和框对不上时：原始尺寸 = 图按原图像素 1:1 摆进框、超出的由框裁掉（推荐）；塞进框 = 老行为（等比缩到框内，全部看见）")]
        public ArtRawMode rawMode = ArtRawMode.RawSize;

        [Tooltip("原始尺寸模式下图的整体缩放（相对原图像素）")]
        public float artScale = 1f;

        [Tooltip("原始尺寸模式下图的整体平移，单位逻辑像素")]
        public Vector2 artOffset;

        [Tooltip("水平镜像（左右翻转）")]
        public bool flipHorizontal;

        public static VnStandingSide CreateDefault(bool right)
        {
            return new VnStandingSide
            {
                apply = false,
                anchor = right ? PortraitAnchor.BottomRight : PortraitAnchor.BottomLeft,
                gapLeft = DefaultInsetX,
                gapRight = DefaultInsetX,
                gapBottom = DefaultInsetY,
                scale = 1f,
                baseSize = new Vector2(DefaultWidth, DefaultHeight),
                dim = DefaultDim,
                positionOnVisibleEdge = true,
                pos = Vector2.zero,
                rawMode = ArtRawMode.RawSize,
                artScale = 1f,
                artOffset = Vector2.zero,
                flipHorizontal = false
            };
        }

        /// <summary>锚点（AnchorMin = AnchorMax）。</summary>
        public Vector2 AnchorVector
        {
            get { return PortraitAnchorMath.ToAnchorPosition(anchor); }
        }

        /// <summary>枢轴。</summary>
        public Vector2 PivotVector
        {
            get { return PortraitAnchorMath.ToPivot(anchor); }
        }

        /// <summary>写进 RectTransform 前的框尺寸（缩放前）。</summary>
        public Vector2 FrameSize()
        {
            return baseSize.x > 1f && baseSize.y > 1f ? baseSize : new Vector2(DefaultWidth, DefaultHeight);
        }

        /// <summary>缩放后的实际尺寸（= 框尺寸 × 缩放）。</summary>
        public Vector2 ScaledSize()
        {
            Vector2 size = FrameSize();
            float s = SafeScale(scale);
            return new Vector2(size.x * s, size.y * s);
        }

        public bool IsFlipped
        {
            get { return scale < 0f || flipHorizontal; }
        }

        /// <summary>缩放的绝对值（图是框的子物体，摆图时要把它折回去）。</summary>
        public float ScaleAbs
        {
            get { return Mathf.Abs(SafeScale(scale)); }
        }

        /// <summary>是否仍是出厂值（窗口用来提示「改过」）。</summary>
        public static bool IsDefault(VnStandingSide side, bool right)
        {
            if (side == null)
            {
                return true;
            }

            VnStandingSide def = CreateDefault(right);
            return !side.apply
                   && side.anchor == def.anchor
                   && Mathf.Approximately(side.gapLeft, def.gapLeft)
                   && Mathf.Approximately(side.gapRight, def.gapRight)
                   && Mathf.Approximately(side.gapBottom, def.gapBottom)
                   && Mathf.Approximately(side.scale, def.scale)
                   && Approximately(side.baseSize, def.baseSize)
                   && Mathf.Approximately(side.dim, def.dim)
                   && side.positionOnVisibleEdge == def.positionOnVisibleEdge
                   && side.rawMode == def.rawMode
                   && Mathf.Approximately(side.artScale, def.artScale)
                   && Approximately(side.artOffset, def.artOffset)
                   && side.flipHorizontal == def.flipHorizontal;
        }

        public void CopyFrom(VnStandingSide other)
        {
            if (other == null)
            {
                return;
            }

            apply = other.apply;
            anchor = other.anchor;
            gapLeft = other.gapLeft;
            gapRight = other.gapRight;
            gapBottom = other.gapBottom;
            scale = other.scale;
            baseSize = other.baseSize;
            dim = other.dim;
            positionOnVisibleEdge = other.positionOnVisibleEdge;
            pos = other.pos;
            rawMode = other.rawMode;
            artScale = other.artScale;
            artOffset = other.artOffset;
            flipHorizontal = other.flipHorizontal;
            Sanitize();
        }

        /// <summary>把越界的值收回正常范围（资产被手改、旧版本升级、运行时每帧都会过一遍）。</summary>
        public void Sanitize()
        {
            if (!Enum.IsDefined(typeof(PortraitAnchor), anchor))
            {
                anchor = PortraitAnchor.BottomLeft;
            }

            gapLeft = Clamp(gapLeft, -8192f, 8192f);
            gapRight = Clamp(gapRight, -8192f, 8192f);
            gapBottom = Clamp(gapBottom, -8192f, 8192f);

            scale = SafeScale(scale);
            scale = Clamp(scale, 0.01f, 10f);

            if (baseSize.x < 1f || baseSize.y < 1f)
            {
                baseSize = new Vector2(DefaultWidth, DefaultHeight);
            }

            baseSize = new Vector2(Clamp(baseSize.x, 1f, 8192f), Clamp(baseSize.y, 1f, 8192f));

            pos = new Vector2(Clamp(pos.x, -8192f, 8192f), Clamp(pos.y, -8192f, 8192f));
            dim = Clamp01(dim);
            artScale = artScale <= 0f ? 1f : Clamp(artScale, 0.01f, 100f);
            artOffset = new Vector2(Clamp(artOffset.x, -8192f, 8192f), Clamp(artOffset.y, -8192f, 8192f));

            if (!Enum.IsDefined(typeof(ArtRawMode), rawMode))
            {
                rawMode = ArtRawMode.RawSize;
            }
        }

        /// <summary>
        /// 写进真正的 RectTransform。Anchor / Pivot / AnchoredPosition 都由锚点枚举和边距算出来，
        /// 所以界面上不出现这些程序字段，但写下去的结果和手动在 Inspector 里调完全等价。
        /// AnchoredPosition 来自 <see cref="ApplyPosition"/>（已按需求换算好「量图边还是量框边」）。
        /// </summary>
        public void WriteTo(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Sanitize();

            Vector2 anchorPos = AnchorVector;
            Vector2 pivot = PivotVector;
            Vector2 size = FrameSize();

            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.pivot = pivot;

            // 水平镜像靠 localScale.x = -1 实现，而它是绕「枢轴」翻的；不补偿的话框会翻到屏幕另一侧。
            // 未镜像时左边 = 锚点 + pos - pivot * 宽；镜像后左边 = 锚点 + pos - (1 - pivot) * 宽。
            // 要让两者相同：pos 加上 (1 - 2 * pivot) * 宽。
            Vector2 mirrorShift = flipHorizontal
                ? new Vector2((1f - 2f * pivot.x) * size.x, 0f)
                : Vector2.zero;

            rect.anchoredPosition = pos + mirrorShift;
            rect.sizeDelta = size;

            rect.localEulerAngles = Vector3.zero;
            float s = SafeScale(scale);
            rect.localScale = new Vector3(
                (flipHorizontal ? -1f : 1f) * Mathf.Abs(s),
                Mathf.Abs(s),
                Mathf.Abs(s));
        }

        /// <summary>这一侧的图在框里实际占的矩形（框局部坐标，原点 = 框中心）。</summary>
        public Rect ResolveArtRect(Vector2 native)
        {
            return ArtImagePlacement.ResolveRaw(native, artScale, artOffset).VisibleRect;
        }

        /// <summary>
        /// 按「图的可见边」重算框的 AnchoredPosition（<see cref="pos"/>）。
        /// <paramref name="art"/> = 图在框局部坐标里的矩形；<paramref name="visibleEdge"/> = 边距是否量图的可见边
        /// （没接管时沿用老行为：量框的边缘，保证和代码原值一致）。
        /// </summary>
        public void ApplyPosition(Vector2 frameSize, Vector2 canvasSize, Rect art, bool visibleEdge)
        {
            pos = visibleEdge
                ? PortraitArtMath.FrameAnchoredPosition(anchor, frameSize, canvasSize, art,
                    gapLeft, gapRight, gapBottom)
                : PortraitAnchorMath.ToAnchoredPosition(anchor, frameSize, gapLeft, gapRight, gapBottom);
            positionOnVisibleEdge = visibleEdge;
        }
        /// <summary>
        /// 从真正的 RectTransform 抄回来（「从游戏抄回」用）。
        /// 反推锚点枚举、边距、缩放；<c>pos</c> 直接照抄，所以抄回来和原位置一模一样。
        /// </summary>
        public void ReadFrom(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            apply = true;

            Vector2 anchorPos = rect.anchorMin;
            Vector2 pivot = rect.pivot;
            anchor = PortraitAnchorMath.FromAnchorPivot(anchorPos, pivot);

            Vector2 size = new Vector2(Mathf.Abs(rect.sizeDelta.x), Mathf.Abs(rect.sizeDelta.y));
            if (size.x < 1f || size.y < 1f)
            {
                size = new Vector2(DefaultWidth, DefaultHeight);
            }

            baseSize = size;

            Vector3 localScale = rect.localScale;
            scale = Mathf.Abs(localScale.x) < 0.0001f ? 1f : Mathf.Abs(localScale.x);
            flipHorizontal = localScale.x < 0f;

            // 镜像时 WriteTo 会给 anchoredPosition 加过补偿量，读回时要扣掉。
            Vector2 mirrorShift = flipHorizontal
                ? new Vector2((1f - 2f * pivot.x) * size.x, 0f)
                : Vector2.zero;
            pos = rect.anchoredPosition - mirrorShift;

            Sanitize();
        }

        /// <summary>按真实摆放反推三个边距（「从游戏抄回」时用，量的是图的可见边缘）。</summary>
        public void ReadGaps(Vector2 canvasSize, Rect art)
        {
            Vector3 gaps = PortraitArtMath.EdgeGaps(anchor, FrameSize(), canvasSize, pos, art);
            if (PortraitAnchorMath.IsLeftSide(anchor))
            {
                gapLeft = gaps.x;
            }
            else if (PortraitAnchorMath.IsRightSide(anchor))
            {
                gapRight = gaps.y;
            }

            if (PortraitAnchorMath.IsBottomSide(anchor))
            {
                gapBottom = gaps.z;
            }

            Sanitize();
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }

        private static float Clamp(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, min, max);
        }

        private static float SafeScale(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || Mathf.Approximately(value, 0f) ? 1f : value;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }
    }
}
