using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 跟随鼠标的名字浮层。挂在 Canvas 下的一个面板上，坐标用画布本地坐标，
    /// 所以屏幕缩放/不同分辨率下都会贴着鼠标且不会跑出画布。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusCardTooltip : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _hintLabel;

        [Header("跟随")]
        [SerializeField, Min(0f)] private float _offsetX = 26f;
        [SerializeField, Min(0f)] private float _offsetY = 24f;
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 18f;
        [SerializeField, Min(0f)] private float _edgePadding = 12f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _visible;
        private float _alpha;
        private Vector2? _fixedScreenPosition;

        public bool IsVisible => _visible;

        public string TitleText => _titleLabel != null ? _titleLabel.text : string.Empty;

        public string HintText => _hintLabel != null ? _hintLabel.text : string.Empty;

        public float Alpha => _alpha;

        /// <summary>标题/副标题是否都接好了（编辑器校验用）。</summary>
        public bool HasContent => _titleLabel != null && _hintLabel != null;

        private void Awake()
        {
            CacheRefs();
            _alpha = 0f;
            ApplyAlpha();
        }

        private void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-_fadeSpeed * Time.unscaledDeltaTime);
            float target = _visible ? 1f : 0f;
            _alpha = Mathf.Lerp(_alpha, target, k);

            if (!_visible && _alpha < 0.01f)
            {
                _alpha = 0f;
            }

            ApplyAlpha();

            if (_visible)
            {
                FollowPointer();
            }
        }

        /// <summary>浮出内容，并跟随鼠标。</summary>
        public void Show(string title, string hint)
        {
            _fixedScreenPosition = null;
            ShowInternal(title, hint);
        }

        /// <summary>在指定屏幕位置浮出（自动化截图用，不跟随鼠标）。</summary>
        public void ShowAt(string title, string hint, Vector2 screenPosition)
        {
            _fixedScreenPosition = screenPosition;
            ShowInternal(title, hint);
            FollowPointer();
        }

        /// <summary>收回浮层。</summary>
        public void Hide()
        {
            _visible = false;
            _fixedScreenPosition = null;
        }

        /// <summary>立刻消失，不播淡出（切场景/初始化用）。</summary>
        public void HideImmediate()
        {
            Hide();
            _alpha = 0f;
            ApplyAlpha();
        }

#if UNITY_EDITOR
        /// <summary>编辑器构建场景时注入引用（运行时不使用）。</summary>
        public void EditorBind(RectTransform panel, CanvasGroup panelGroup, Text title, Text hint)
        {
            _panel = panel;
            _panelGroup = panelGroup;
            _titleLabel = title;
            _hintLabel = hint;
        }
#endif

        private void ShowInternal(string title, string hint)
        {
            CacheRefs();

            if (_titleLabel != null)
            {
                FocusLabFont.Apply(_titleLabel);
                _titleLabel.text = title ?? string.Empty;
            }

            if (_hintLabel != null)
            {
                FocusLabFont.Apply(_hintLabel);
                _hintLabel.text = hint ?? string.Empty;
            }

            _visible = true;
        }

        private void CacheRefs()
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (_canvasRect == null && _canvas != null)
            {
                _canvasRect = _canvas.transform as RectTransform;
            }

            if (_panel == null)
            {
                _panel = transform as RectTransform;
            }

            if (_panelGroup == null)
            {
                _panelGroup = GetComponent<CanvasGroup>();
            }
        }

        private void ApplyAlpha()
        {
            if (_panelGroup != null)
            {
                _panelGroup.alpha = _alpha;
                _panelGroup.blocksRaycasts = false;
                _panelGroup.interactable = false;
            }
        }

        private void FollowPointer()
        {
            if (_panel == null || _canvasRect == null)
            {
                return;
            }

            Vector2 screen = _fixedScreenPosition ?? PointerScreenPosition();

            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, camera, out Vector2 local))
            {
                return;
            }

            Vector2 size = _panel.rect.size;

            // 面板 pivot 在左下角，默认出现在鼠标右上方。
            Vector2 position = local + new Vector2(_offsetX, _offsetY);

            float minX = _canvasRect.rect.xMin + _edgePadding;
            float maxX = _canvasRect.rect.xMax - _edgePadding - size.x;
            float minY = _canvasRect.rect.yMin + _edgePadding;
            float maxY = _canvasRect.rect.yMax - _edgePadding - size.y;

            if (maxX >= minX)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }

            if (maxY >= minY)
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }

            _panel.anchoredPosition = position;
        }

        private static Vector2 PointerScreenPosition()
        {
            bool found = false;
            Vector2 position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                position = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                found = true;
            }

            if (!found && UnityEngine.InputSystem.Touchscreen.current != null
                && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
            {
                position = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
                found = true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!found)
            {
                position = UnityEngine.Input.mousePosition;
                found = true;
            }
#endif

            return found ? position : Vector2.zero;
        }
    }
}
