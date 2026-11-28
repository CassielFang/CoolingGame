using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 视觉小说立绘的全部美术参数。
    ///
    /// 只暴露美术真正要调的东西：
    /// - 左侧 / 右侧立绘：贴在屏幕哪、缩放多大、离屏幕边多远；
    /// - 变暗强度：别人说话时这一侧暗到多少（两侧共用一套）。
    ///
    /// 出厂值 = DialogueOverlay 原来写死的那套。总开关关着时游戏完全按原值走。
    /// </summary>
    [System.Serializable]
    public sealed class VnStandingSettings
    {
        public const float DefaultDim = 0.42f;

        [Tooltip("总开关。关着 = 游戏完全按 DialogueOverlay 里写死的值走")]
        public bool apply;

        [Header("左侧立绘")]
        public VnStandingSide left = VnStandingSide.CreateDefault(false);

        [Header("右侧立绘")]
        public VnStandingSide right = VnStandingSide.CreateDefault(true);

        [Header("变暗")]
        [Tooltip("接管变暗：别人说话时，没说话那一侧暗到多少")]
        public bool applyDim;

        [Range(0f, 1f)]
        [Tooltip("变暗强度：0 = 不变暗，1 = 全黑。两侧共用。原来的值是 0.42")]
        public float dimStrength = DefaultDim;

        [Header("立绘素材")]
        [Tooltip("接管素材摆放：控制图怎么摆进框")]
        public bool applyArt;

        [Tooltip("统一显示高度（像素）。0 = 不统一，图各自塞进框")]
        public float unifiedHeight = 820f;

        [Range(0f, 1f)]
        [Tooltip("统一高度时的垂直对齐：0 底对齐，1 顶对齐")]
        public float unifiedAlignY;

        [Header("背景")]
        [Tooltip("接管背景：竖向渐变")]
        public bool applyBackground;

        public Color backgroundTop = new Color(0.06f, 0.06f, 0.075f, 1f);
        public Color backgroundBottom = new Color(0.015f, 0.015f, 0.02f, 1f);

        /// <summary>侧别：true = 左。</summary>
        public VnStandingSide GetSide(bool isLeft)
        {
            return isLeft ? left : right;
        }

        /// <summary>这一侧现在是否真的接管（总开关 + 该侧开关）。</summary>
        public bool IsSideActive(bool isLeft)
        {
            return apply && GetSide(isLeft) != null && GetSide(isLeft).apply;
        }

        /// <summary>这一侧说话时另一侧变暗到多少（没接管变暗 = 原值 0.42）。</summary>
        public float ResolveDim(bool isLeft, bool useLiveSettings)
        {
            if (useLiveSettings && applyDim)
            {
                return Mathf.Clamp01(dimStrength);
            }

            return DefaultDim;
        }

        /// <summary>补空 + 收范围。反序列化、Inspector 改值、运行时每帧都会过一遍。</summary>
        public void Sanitize()
        {
            left = left ?? VnStandingSide.CreateDefault(false);
            right = right ?? VnStandingSide.CreateDefault(true);
            left.Sanitize();
            right.Sanitize();

            dimStrength = Mathf.Clamp01(dimStrength);

            if (float.IsNaN(unifiedHeight) || float.IsInfinity(unifiedHeight))
            {
                unifiedHeight = 0f;
            }

            unifiedHeight = Mathf.Clamp(unifiedHeight, 0f, 4096f);

            if (float.IsNaN(unifiedAlignY) || float.IsInfinity(unifiedAlignY))
            {
                unifiedAlignY = 0f;
            }

            unifiedAlignY = Mathf.Clamp01(unifiedAlignY);
        }

        public void CopyFrom(VnStandingSettings other)
        {
            if (other == null)
            {
                return;
            }

            apply = other.apply;
            applyDim = other.applyDim;
            dimStrength = other.dimStrength;
            applyArt = other.applyArt;
            unifiedHeight = other.unifiedHeight;
            unifiedAlignY = other.unifiedAlignY;
            applyBackground = other.applyBackground;
            backgroundTop = other.backgroundTop;
            backgroundBottom = other.backgroundBottom;

            left = left ?? VnStandingSide.CreateDefault(false);
            right = right ?? VnStandingSide.CreateDefault(true);
            left.CopyFrom(other.left);
            right.CopyFrom(other.right);
            Sanitize();
        }

        public void ResetToDefault()
        {
            left = VnStandingSide.CreateDefault(false);
            right = VnStandingSide.CreateDefault(true);
            dimStrength = DefaultDim;
            unifiedHeight = 820f;
            unifiedAlignY = 0f;
            backgroundTop = new Color(0.06f, 0.06f, 0.075f, 1f);
            backgroundBottom = new Color(0.015f, 0.015f, 0.02f, 1f);
            Sanitize();
        }

        /// <summary>是否与出厂一致（不影响各开关）。</summary>
        public bool IsDefault()
        {
            return VnStandingSide.IsDefault(left, false)
                   && VnStandingSide.IsDefault(right, true)
                   && Mathf.Approximately(dimStrength, DefaultDim);
        }

        /// <summary>这一侧实际用的设置（没接管就返回出厂值）。</summary>
        public VnStandingSide ResolveSide(bool isLeft, bool useLiveSettings)
        {
            VnStandingSide side = GetSide(isLeft);
            if (useLiveSettings && apply && side != null && side.apply)
            {
                return side;
            }

            return VnStandingSide.CreateDefault(isLeft);
        }

        /// <summary>这一侧的框实际在画面上的尺寸（含缩放）。</summary>
        public Vector2 FrameSize(VnStandingSide side)
        {
            if (side == null)
            {
                return new Vector2(VnStandingSide.DefaultWidth, VnStandingSide.DefaultHeight);
            }

            return side.ScaledSize();
        }

        /// <summary>这一侧的图最终怎么摆在框里（匹配 / 原始尺寸 / 老行为）。</summary>
        public ArtImagePlacement ResolveArt(bool isLeft, bool useLiveSettings, Vector2 canvasSize,
                                            Vector2 native, bool flipHorizontal)
        {
            VnStandingSide side = ResolveSide(isLeft, useLiveSettings);
            Vector2 box = FrameSize(side);

            bool live = useLiveSettings && apply;
            if (!live || !applyArt)
            {
                // 原行为：preserveAspect 塞进框内
                return ArtImagePlacement.Resolve(ArtImageMatch.Fit, box, native, 0f, 0f);
            }

            if (unifiedHeight > 0f)
            {
                return ArtImagePlacement.Resolve(ArtImageMatch.Fit, box, native, unifiedHeight, unifiedAlignY);
            }

            if (side.rawMode == ArtRawMode.FitInside)
            {
                return ArtImagePlacement.Resolve(ArtImageMatch.Fit, box, native, 0f, 0f);
            }

            return ArtImagePlacement.ResolveRaw(native, side.artScale, side.artOffset);
        }

        /// <summary>是否镜像（只在启用时生效）。</summary>
        public bool ResolveFlip(bool isLeft, bool useLiveSettings)
        {
            if (!useLiveSettings)
            {
                return false;
            }

            return IsSideActive(isLeft) && GetSide(isLeft).flipHorizontal;
        }
    }
}
