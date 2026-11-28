using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 立绘「距离屏幕左边 / 右边 / 下边」的换算。
    ///
    /// 关键约定：这三个距离量的是**图片露出来的那部分**的边缘，不是立绘框的边。
    /// 框只是裁切窗口：图比框大时露出来的边就是框边；图比框小时（例如图只有 824 高、
    /// 框有 900 高）露出来的才是图自己的边。两种都要算对，所以统一按
    /// 「可见边 = 图边与框边的交」来摆框的位置。
    ///
    /// 两个方向：
    /// - <see cref="FrameAnchoredPosition"/>：给定边距，算出框该摆在哪（图在框里怎么摆不动它）；
    /// - <see cref="EdgeGaps"/>：给定摆放，反推实际边距（「从游戏抄回」用）。
    ///
    /// 传入的 <c>art</c> 是图在框局部坐标里的矩形（原点 = 框中心），可以直接用
    /// <see cref="ArtImagePlacement.VisibleRect"/>。
    /// </summary>
    public static class PortraitArtMath
    {
        /// <summary>
        /// 要让图的可见边落在这几个边距上，框（枢轴在自己那条边上）的 AnchoredPosition 该是多少。
        /// </summary>
        public static Vector2 FrameAnchoredPosition(PortraitAnchor anchor, Vector2 frameSize, Vector2 canvas,
                                                    Rect art, float gapLeft, float gapRight, float gapBottom)
        {
            Vector2 anchorPos = PortraitAnchorMath.ToAnchorPosition(anchor);
            Vector2 pivot = PortraitAnchorMath.ToPivot(anchor);

            // pos = 0 时框左下角在哪
            Vector2 origin = new Vector2(anchorPos.x * canvas.x - pivot.x * frameSize.x,
                anchorPos.y * canvas.y - pivot.y * frameSize.y);

            float halfX = frameSize.x * 0.5f;
            float halfY = frameSize.y * 0.5f;

            // 框局部 → 画布：画布 x = frameMin + halfX + localX，再被框裁掉框外的部分
            Vector2 frameMin = origin;

            if (PortraitAnchorMath.IsLeftSide(anchor))
            {
                // 图露出来的左边（框内坐标）→ 落正到 gapLeft
                frameMin.x = gapLeft - Mathf.Max(0f, halfX + art.x);
            }
            else if (PortraitAnchorMath.IsRightSide(anchor))
            {
                float visibleRight = Mathf.Min(frameSize.x, halfX + art.x + art.width);
                frameMin.x = canvas.x - gapRight - visibleRight;
            }

            if (PortraitAnchorMath.IsBottomSide(anchor))
            {
                frameMin.y = gapBottom - Mathf.Max(0f, halfY + art.y);
            }

            return frameMin - origin;
        }

        /// <summary>
        /// 反推：图上真正露出来的部分，贴着屏幕左边 / 右边 / 底边多远（像素）。
        /// 不靠边的方向返回 0，界面据此决定哪几项有意义。
        /// </summary>
        public static Vector3 EdgeGaps(PortraitAnchor anchor, Vector2 frameSize, Vector2 canvas, Vector2 pos,
                                       Rect art)
        {
            Vector2 anchorPos = PortraitAnchorMath.ToAnchorPosition(anchor);
            Vector2 pivot = PortraitAnchorMath.ToPivot(anchor);

            // 框左下角在画布上的位置
            Vector2 frameMin = new Vector2(anchorPos.x * canvas.x + pos.x - pivot.x * frameSize.x,
                anchorPos.y * canvas.y + pos.y - pivot.y * frameSize.y);

            float halfX = frameSize.x * 0.5f;
            float halfY = frameSize.y * 0.5f;

            // 图与框的交 = 真正露出来的部分
            float artLeft = frameMin.x + Mathf.Max(0f, halfX + art.x);
            float artBottom = frameMin.y + Mathf.Max(0f, halfY + art.y);
            float artRight = frameMin.x + Mathf.Min(frameSize.x, halfX + art.x + art.width);

            float gapL = PortraitAnchorMath.IsLeftSide(anchor) ? artLeft : 0f;
            float gapR = PortraitAnchorMath.IsRightSide(anchor) ? canvas.x - artRight : 0f;
            float gapB = PortraitAnchorMath.IsBottomSide(anchor) ? artBottom : 0f;

            return new Vector3(gapL, gapR, gapB);
        }
    }
}
