using System;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>图在立绘框里最终怎么摆（像素级结果）。</summary>
    public struct ArtImagePlacement
    {
        /// <summary>图相对原图像素的缩放：1 = 原图像素 1:1。</summary>
        public float Scale;

        /// <summary>图最终显示的尺寸（逻辑像素）。</summary>
        public Vector2 Size;

        /// <summary>图中心相对框中心的偏移。</summary>
        public Vector2 Offset;

        /// <summary>
        /// 「匹配」这条老路子：把图等比塞进框 / 盖住框 / 拉伸铺满，
        /// 可选统一基准高（不同比例素材的人头/身高对齐）。只用于历史行为兼容。
        /// </summary>
        public static ArtImagePlacement Resolve(ArtImageMatch mode, Vector2 box, Vector2 native,
                                               float targetHeight, float alignY)
        {
            if (box.x < 1f || box.y < 1f || native.x < 1f || native.y < 1f)
            {
                return new ArtImagePlacement { Scale = 1f, Size = box, Offset = Vector2.zero };
            }

            if (mode == ArtImageMatch.Stretch)
            {
                return new ArtImagePlacement { Scale = 1f, Size = box, Offset = Vector2.zero };
            }

            float fit = Mathf.Min(box.x / native.x, box.y / native.y);
            float cover = Mathf.Max(box.x / native.x, box.y / native.y);

            float scale;
            Vector2 offset = Vector2.zero;

            if (mode == ArtImageMatch.Cover)
            {
                scale = cover;
            }
            else if (targetHeight > 0f)
            {
                // 统一基准高：图高 = targetHeight（不强制塞进框内——框只负责裁切）
                scale = targetHeight / native.y;
                float height = native.y * scale;
                offset = new Vector2(0f, (Mathf.Clamp01(alignY) - 0.5f) * (box.y - height));
            }
            else
            {
                scale = fit;
            }

            return new ArtImagePlacement
            {
                Scale = scale,
                Size = new Vector2(native.x * scale, native.y * scale),
                Offset = offset
            };
        }

        /// <summary>
        /// 「原始尺寸」这条新路子（RectTransform 语义）：图按原图像素 1:1 摆在框中心，
        /// 再整体缩放 <paramref name="scale"/>、整体平移 <paramref name="offset"/>。
        /// 超出框的部分由框上的 RectMask2D 裁掉。
        /// </summary>
        public static ArtImagePlacement ResolveRaw(Vector2 native, float scale, Vector2 offset)
        {
            if (native.x < 1f || native.y < 1f)
            {
                native = new Vector2(1f, 1f);
            }

            float safeScale = float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : Mathf.Max(0.0001f, scale);
            return new ArtImagePlacement
            {
                Scale = safeScale,
                Size = new Vector2(native.x * safeScale, native.y * safeScale),
                Offset = offset
            };
        }

        /// <summary>按原始尺寸摆放时，框至少要多大才装得下整张图（给「铺满框」用）。</summary>
        public static Vector2 SizeForFrame(Vector2 native, float scale)
        {
            float safeScale = float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f ? 1f : scale;
            return new Vector2(Mathf.Max(1f, native.x * safeScale), Mathf.Max(1f, native.y * safeScale));
        }

        /// <summary>
        /// 把图真正摆进框里（以框中心为原点）。
        /// Simple 类型 Image 的显示尺寸 = max(native × pixelsPerUnitMultiplier, native)，multiplier ∈ (0, 1]——
        /// 缩小只能靠 multiplier，放大只能靠 sizeDelta。
        /// </summary>
        public static void Layer(Image image, ArtImagePlacement placement, Vector2 native)
        {
            if (image == null)
            {
                return;
            }

            image.enabled = true;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            RectTransform rect = image.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = placement.Offset;

            if (placement.Scale < 1f)
            {
                image.pixelsPerUnitMultiplier = Mathf.Max(0.0001f, placement.Scale);
                rect.sizeDelta = new Vector2(Mathf.Max(1f, native.x), Mathf.Max(1f, native.y));
            }
            else
            {
                image.pixelsPerUnitMultiplier = 1f;
                rect.sizeDelta = new Vector2(
                    Mathf.Max(native.x, placement.Size.x),
                    Mathf.Max(native.y, placement.Size.y));
            }
        }
    }
}
