using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 立绘贴在屏幕的哪个位置。名字就是画面上的位置，没有 Anchor / Pivot 这类程序词汇。
    /// </summary>
    public enum PortraitAnchor
    {
        /// <summary>屏幕中央。</summary>
        Center = 0,

        /// <summary>左边中点：贴屏幕左边缘、垂直居中。</summary>
        LeftMiddle = 1,

        /// <summary>右边中点：贴屏幕右边缘、垂直居中。</summary>
        RightMiddle = 2,

        /// <summary>左下角。</summary>
        BottomLeft = 3,

        /// <summary>右下角。</summary>
        BottomRight = 4,

        /// <summary>底边中点：贴屏幕底边、水平居中。</summary>
        BottomCenter = 5
    }

    /// <summary>立绘框尺寸随画面缩放的方式。</summary>
    public enum ArtSizeMode
    {
        /// <summary>固定「尺寸」里的 W×H（原来的行为）。</summary>
        Fixed = 0,

        /// <summary>宽度 = 画布宽 × 占比，高度按 W:H 等比。</summary>
        ByWidth = 1,

        /// <summary>高度 = 画布高 × 占比，宽度按 W:H 等比。</summary>
        ByHeight = 2,

        /// <summary>短边 = 画布短边 × 占比，再按 W:H 等比。</summary>
        ByShortSide = 3
    }

    /// <summary>图怎么摆进立绘框。</summary>
    public enum ArtImageMatch
    {
        /// <summary>整张图缩到框内，不裁切（原来的行为）。</summary>
        Fit = 0,

        /// <summary>等比放大到盖满框，超出部分裁掉。</summary>
        Cover = 1,

        /// <summary>整张图按框铺满，不保比例（会拉伸/压扁）。</summary>
        Stretch = 2
    }

    /// <summary>图比例和框对不上时怎么办。</summary>
    public enum ArtRawMode
    {
        /// <summary>图按原图像素 1:1 摆进框，超出的部分由框裁掉（框就是画框，推荐）。</summary>
        RawSize = 0,

        /// <summary>老行为：等比缩到框内，整张图都看得见（比例不同身高就会不一样）。</summary>
        FitInside = 1
    }

    /// <summary>可序列化的 Vector2：Inspector 里的 xy 就是画面上的左右/上下，比 Unity 自带的 (x,y) 直白。</summary>
    [System.Serializable]
    public struct ArtVector2
    {
        public float x;
        public float y;

        public ArtVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }

        public static ArtVector2 From(Vector2 value)
        {
            return new ArtVector2(value.x, value.y);
        }

        public override string ToString()
        {
            return string.Format("({0:0.##}, {1:0.##})", x, y);
        }
    }

    /// <summary>
    /// 一组美术参数的「已保存 / 正在调」两态接口。<see cref="ArtTuningStore"/> 只认这个，
    /// 以后每加一个调试对象，实现一个 wrapper 就能自动获得「保存 / 读取 / 丢弃」。
    /// </summary>
    public interface IArtTuningBlock
    {
        /// <summary>把「正在调」的工作副本抄进「已保存」的那份。</summary>
        void CaptureSavedToWorking();

        /// <summary>把「已保存」的那份抄回「正在调」的工作副本。</summary>
        void ApplySavedToWorking();

        /// <summary>把「正在调」的覆盖「已保存」（保存按钮）。</summary>
        void CommitWorkingToSaved();
    }

    /// <summary>
    /// 把「贴在屏幕哪一侧 + 离屏幕边多少距离」翻译成 Unity 的 Anchor / Pivot / AnchoredPosition。
    ///
    /// 美术只关心「贴在左下角、离左边 70、离底边 40」，程序要的是 AnchorMin = AnchorMax = (0,0)、
    /// Pivot = (0,0)、AnchoredPosition = (70,40)。这套换算只写在这一处，界面和运行时都调它。
    ///
    /// 枢轴永远取 0 或 1，也就是「锚在哪条边，就拿框的那条边去对」，
    /// 这样距离屏幕边的值（gapX / gapY）就等于框边缘到屏幕边缘的真实像素，不会因为枢轴在中间而偏移。
    /// </summary>
    public static class PortraitAnchorMath
    {
        /// <summary>这个锚点是否靠左（左右对称的位置用得到）。</summary>
        public static bool IsLeftSide(PortraitAnchor anchor)
        {
            return anchor == PortraitAnchor.LeftMiddle || anchor == PortraitAnchor.BottomLeft;
        }

        /// <summary>这个锚点是否靠右。</summary>
        public static bool IsRightSide(PortraitAnchor anchor)
        {
            return anchor == PortraitAnchor.RightMiddle || anchor == PortraitAnchor.BottomRight;
        }

        /// <summary>这个锚点是否贴底（左下 / 右下 / 底边中点）。</summary>
        public static bool IsBottomSide(PortraitAnchor anchor)
        {
            return anchor == PortraitAnchor.BottomLeft
                   || anchor == PortraitAnchor.BottomRight
                   || anchor == PortraitAnchor.BottomCenter;
        }

        /// <summary>锚点在 0..1 归一化坐标里的位置（也就是 AnchorMin = AnchorMax）。</summary>
        public static Vector2 ToAnchorPosition(PortraitAnchor anchor)
        {
            switch (anchor)
            {
                case PortraitAnchor.Center:
                    return new Vector2(0.5f, 0.5f);
                case PortraitAnchor.LeftMiddle:
                    return new Vector2(0f, 0.5f);
                case PortraitAnchor.RightMiddle:
                    return new Vector2(1f, 0.5f);
                case PortraitAnchor.BottomLeft:
                    return new Vector2(0f, 0f);
                case PortraitAnchor.BottomRight:
                    return new Vector2(1f, 0f);
                case PortraitAnchor.BottomCenter:
                    return new Vector2(0.5f, 0f);
                default:
                    return new Vector2(0f, 0f);
            }
        }

        /// <summary>
        /// 枢轴：锚在左就拿框的左边缘去对，锚在右拿右边缘，居中拿中间。
        /// 拿边缘对齐，gapX 才等于「框边缘离屏幕边的距离」。
        /// </summary>
        public static Vector2 ToPivot(PortraitAnchor anchor)
        {
            float x = 0.5f;
            if (IsLeftSide(anchor))
            {
                x = 0f;
            }
            else if (IsRightSide(anchor))
            {
                x = 1f;
            }

            // 贴底的三种一律拿框底边去对底边；其余（中央 / 左右中点）垂直居中
            float y = IsBottomSide(anchor) ? 0f : 0.5f;
            return new Vector2(x, y);
        }

        /// <summary>
        /// 由「离屏幕左边 / 右边 / 底边多远」算出锚点相对位置（AnchoredPosition）。
        /// 没参与定位的那一维（比如靠左的立绘用不到 gapRight）直接忽略。
        /// </summary>
        public static Vector2 ToAnchoredPosition(PortraitAnchor anchor, Vector2 size, float gapLeft, float gapRight, float gapBottom)
        {
            Vector2 pivot = ToPivot(anchor);

            // X：靠左就 +左距，靠右就 -右距，居中就不动
            float x;
            if (IsLeftSide(anchor))
            {
                x = gapLeft;
            }
            else if (IsRightSide(anchor))
            {
                x = -gapRight;
            }
            else
            {
                x = 0f;
            }

            // Y：贴底就 +底距（枢轴在框底，往上推 gapBottom 像素），否则不动
            float y = IsBottomSide(anchor) ? gapBottom : 0f;

            // 居中的那一维枢轴是 0.5，锚点直接落在框中心，AnchoredPosition 取 0 即可，
            // 所以这里不需要再按框尺寸补偿——框多大都自然居中。
            return new Vector2(x, y);
        }

        /// <summary>
        /// 反推锚点枚举：给一组 Anchor / Pivot，猜出美术选的哪个位置（用于读取旧资产、「从游戏抄回」）。
        /// </summary>
        public static PortraitAnchor FromAnchorPivot(Vector2 anchor, Vector2 pivot)
        {
            bool left = anchor.x < 0.25f;
            bool right = anchor.x > 0.75f;
            bool centerX = !left && !right;

            bool bottom = anchor.y < 0.25f;
            bool centerY = !bottom && anchor.y < 0.75f;

            if (centerY)
            {
                if (left)
                {
                    return PortraitAnchor.LeftMiddle;
                }

                if (right)
                {
                    return PortraitAnchor.RightMiddle;
                }

                return PortraitAnchor.Center;
            }

            if (bottom)
            {
                if (left)
                {
                    return PortraitAnchor.BottomLeft;
                }

                if (right)
                {
                    return PortraitAnchor.BottomRight;
                }

                return PortraitAnchor.BottomCenter;
            }

            // 贴顶之类没有对应项，按最近的下方位置算
            return centerX ? PortraitAnchor.Center : left ? PortraitAnchor.LeftMiddle : PortraitAnchor.RightMiddle;
        }

        /// <summary>界面上的中文名。</summary>
        public static string DisplayName(PortraitAnchor anchor)
        {
            switch (anchor)
            {
                case PortraitAnchor.Center:
                    return "中央";
                case PortraitAnchor.LeftMiddle:
                    return "左边中点";
                case PortraitAnchor.RightMiddle:
                    return "右边中点";
                case PortraitAnchor.BottomLeft:
                    return "左下角";
                case PortraitAnchor.BottomRight:
                    return "右下角";
                case PortraitAnchor.BottomCenter:
                    return "底边中点";
                default:
                    return anchor.ToString();
            }
        }

        /// <summary>枚举全部选项，顺序固定（界面下拉框用）。</summary>
        public static PortraitAnchor[] All()
        {
            return new[]
            {
                PortraitAnchor.Center,
                PortraitAnchor.LeftMiddle,
                PortraitAnchor.RightMiddle,
                PortraitAnchor.BottomLeft,
                PortraitAnchor.BottomRight,
                PortraitAnchor.BottomCenter
            };
        }
    }
}
