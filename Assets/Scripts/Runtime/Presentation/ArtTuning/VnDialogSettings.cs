using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 视觉小说：对话框（位置 + 尺寸 + 整体比例）。
    ///
    /// 和立绘那一组一样，数值全按「1920×1080 的参考画面」写，运行时按画布实际尺寸换算，
    /// 所以 16:9 之外的窗口也不会跑偏。
    /// </summary>
    [System.Serializable]
    public sealed class VnDialogSettings
    {
        /// <summary>代码里原来的对话框宽度（1560）。</summary>
        public const float DefaultWidth = 1560f;

        /// <summary>代码里原来的对话框高度（148）。</summary>
        public const float DefaultHeight = 148f;

        /// <summary>代码里原来的对话框离屏幕底边的距离（36）。</summary>
        public const float DefaultGap = 36f;

        [Tooltip("接管：关着 = 完全按代码里原来的对话框（1560×148，底部居中，离底边 36）")]
        public bool apply;

        [Tooltip("对话框贴在屏幕的哪个位置")]
        public PortraitAnchor anchor = PortraitAnchor.BottomCenter;

        [Tooltip("只在靠左的位置用得到：对话框左边缘到屏幕左边缘的距离（像素）")]
        public float gapLeft;

        [Tooltip("只在靠右的位置用得到：对话框右边缘到屏幕右边缘的距离（像素）")]
        public float gapRight;

        [Tooltip("只在贴底的位置用得到：对话框下边缘到屏幕下边缘的距离（像素）")]
        public float gapBottom = DefaultGap;

        [Tooltip("在自动对齐的基础上再往右挪多少（像素，负数往左）")]
        public float offsetX;

        [Tooltip("在自动对齐的基础上再往上挪多少（像素，负数往下）")]
        public float offsetY;

        [Tooltip("对话框宽度（像素）")]
        public float width = DefaultWidth;

        [Tooltip("对话框高度（像素）")]
        public float height = DefaultHeight;

        [Tooltip("宽度不写死，按屏幕宽度的百分比算（开的时候上面的「宽度」不生效）")]
        public bool widthByScreen;

        [Tooltip("宽度占屏幕宽度的百分比")]
        public float widthPercent = 81.25f;

        [Tooltip("整体比例：对话框和里面的字一起缩放，就是 Transform 的 Local Scale")]
        public float scale = 1f;

        /// <summary>对话框在画布上的实际尺寸（像素）。</summary>
        public Vector2 ResolveSize(Vector2 canvas)
        {
            float w = widthByScreen
                ? canvas.x * Mathf.Clamp(widthPercent, 1f, 100f) * 0.01f
                : width;

            return new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, height));
        }

        /// <summary>对话框左上角（其实返回的是 AnchoredPosition）该是多少。</summary>
        public Vector2 ResolvePosition(Vector2 size, Vector2 canvas)
        {
            Vector2 pos = PortraitAnchorMath.ToAnchoredPosition(anchor, size, gapLeft, gapRight, gapBottom);
            return new Vector2(pos.x + offsetX, pos.y + offsetY);
        }

        /// <summary>把这一组参数写到对话框的 RectTransform 上。</summary>
        public void WriteTo(RectTransform rect, Vector2 canvas)
        {
            if (rect == null)
            {
                return;
            }

            Sanitize();

            Vector2 size = ResolveSize(canvas);
            Vector2 anchorPos = PortraitAnchorMath.ToAnchorPosition(anchor);

            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.pivot = PortraitAnchorMath.ToPivot(anchor);
            rect.anchoredPosition = ResolvePosition(size, canvas);
            rect.sizeDelta = size;
            rect.localEulerAngles = Vector3.zero;

            float s = Mathf.Max(0.01f, scale);
            rect.localScale = new Vector3(s, s, s);
        }

        /// <summary>从画面上的 RectTransform 反推这一组参数（「从游戏抄回」）。</summary>
        public void ReadFrom(RectTransform rect, Vector2 canvas)
        {
            if (rect == null)
            {
                return;
            }

            apply = true;
            anchor = PortraitAnchorMath.FromAnchorPivot(rect.anchorMin, rect.pivot);

            Vector2 size = new Vector2(Mathf.Abs(rect.sizeDelta.x), Mathf.Abs(rect.sizeDelta.y));
            if (size.x < 1f || size.y < 1f)
            {
                size = new Vector2(DefaultWidth, DefaultHeight);
            }

            width = size.x;
            height = size.y;
            widthByScreen = false;

            float s = Mathf.Abs(rect.localScale.x);
            scale = s < 0.0001f ? 1f : s;

            Vector2 anchorPos = PortraitAnchorMath.ToAnchorPosition(anchor);
            Vector2 pivot = PortraitAnchorMath.ToPivot(anchor);
            Vector2 pos = rect.anchoredPosition;

            // 这一组反推的是「实际贴在哪」，微调偏移已经吃进位置里了，所以清 0
            offsetX = 0f;
            offsetY = 0f;

            float left = anchorPos.x * canvas.x + pos.x - pivot.x * size.x;
            float bottom = anchorPos.y * canvas.y + pos.y - pivot.y * size.y;

            gapLeft = left;
            gapRight = canvas.x - (left + size.x);
            gapBottom = bottom;

            Sanitize();
        }

        public void Sanitize()
        {
            if (!System.Enum.IsDefined(typeof(PortraitAnchor), anchor))
            {
                anchor = PortraitAnchor.BottomCenter;
            }

            width = Mathf.Clamp(width, 1f, 8192f);
            height = Mathf.Clamp(height, 1f, 8192f);
            widthPercent = Mathf.Clamp(widthPercent, 1f, 100f);
            scale = Mathf.Clamp(scale, 0.05f, 5f);
            gapLeft = Mathf.Clamp(gapLeft, -8192f, 8192f);
            gapRight = Mathf.Clamp(gapRight, -8192f, 8192f);
            gapBottom = Mathf.Clamp(gapBottom, -8192f, 8192f);
            offsetX = Mathf.Clamp(offsetX, -8192f, 8192f);
            offsetY = Mathf.Clamp(offsetY, -8192f, 8192f);
        }

        public void CopyFrom(VnDialogSettings other)
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
            offsetX = other.offsetX;
            offsetY = other.offsetY;
            width = other.width;
            height = other.height;
            widthByScreen = other.widthByScreen;
            widthPercent = other.widthPercent;
            scale = other.scale;
        }

        public void ResetToDefault()
        {
            apply = false;
            anchor = PortraitAnchor.BottomCenter;
            gapLeft = 0f;
            gapRight = 0f;
            gapBottom = DefaultGap;
            offsetX = 0f;
            offsetY = 0f;
            width = DefaultWidth;
            height = DefaultHeight;
            widthByScreen = false;
            widthPercent = 81.25f;
            scale = 1f;
        }

        public bool IsDefault()
        {
            return !apply
                   && anchor == PortraitAnchor.BottomCenter
                   && Mathf.Approximately(gapLeft, 0f)
                   && Mathf.Approximately(gapRight, 0f)
                   && Mathf.Approximately(gapBottom, DefaultGap)
                   && Mathf.Approximately(offsetX, 0f)
                   && Mathf.Approximately(offsetY, 0f)
                   && Mathf.Approximately(width, DefaultWidth)
                   && Mathf.Approximately(height, DefaultHeight)
                   && !widthByScreen
                   && Mathf.Approximately(widthPercent, 81.25f)
                   && Mathf.Approximately(scale, 1f);
        }
    }
}
