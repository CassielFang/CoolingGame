using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 测试场卡牌：点击翻面、拖拽移动、悬停放大到 1.1 倍并在鼠标附近浮出名字。
    /// 悬停缩放作用在自身（根）上，翻面压扁作用在 Flip 子节点上，两者互不干扰；
    /// 拖拽移动也在根上做，位移按父级 Cards 容器裁剪。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusCardView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [Header("数据")]
        [SerializeField] private FocusCardData _data;

        [Header("引用")]
        [SerializeField] private RectTransform _flipRoot;
        [SerializeField] private Image _faceImage;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Image _selectionRing;

        [Header("悬停")]
        [SerializeField, Range(1f, 1.4f)] private float _hoverScale = 1.1f;
        [SerializeField, Min(0f)] private float _hoverLift = 14f;
        [SerializeField, Min(0.1f)] private float _hoverSmoothing = 16f;

        [Header("选中")]
        [SerializeField, Min(0f)] private float _selectedLift = 10f;
        [SerializeField, Range(1f, 1.2f)] private float _selectedScale = 1.03f;

        /// <summary>选中描边的颜色：暖白，跟卡面分类色区分开。</summary>
        private static readonly Color SelectionColor = new Color(1f, 0.97f, 0.88f, 1f);

        [Header("翻面")]
        [SerializeField, Min(0.05f)] private float _flipDuration = 0.24f;

        [Header("拖拽")]
        [SerializeField, Min(0f)] private float _dragLift = 24f;
        [SerializeField, Min(1f)] private float _clickThreshold = 10f;

        /// <summary>鼠标进入卡牌（悬停开始）。</summary>
        public event Action<FocusCardView> HoverEntered;

        /// <summary>鼠标离开卡牌（悬停结束）。</summary>
        public event Action<FocusCardView> HoverExited;

        /// <summary>卡牌真的翻开了（背面 → 正面，动画播到换面那一刻）。</summary>
        public event Action<FocusCardView> Flipped;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private FocusLabController _controller;

        // 必须序列化：开局是背面还是正面属于场景数据，否则重开场景又变回正面。
        [SerializeField] private bool _faceUp = true;
        private bool _hovered;
        private bool _dragging;
        private bool _flipping;
        private bool _pointerDown;
        private bool _selected;

        private Vector2 _dragStartPointer;
        private Vector2 _dragStartAnchored;
        private float _dragDistance;

        public FocusCardData Data => _data;

        public string DisplayName => _data != null ? _data.DisplayName : name;

        public bool IsFaceUp => _faceUp;

        public bool IsHovered => _hovered;

        public bool IsDragging => _dragging;

        public bool IsFlipping => _flipping;

        /// <summary>是否处于选中状态（选中才有描边和高亮）。</summary>
        public bool IsSelected => _selected;

        /// <summary>是不是人物卡（人物卡被选中时左侧弹立绘）。</summary>
        public bool IsCharacter => FocusCardCategories.IsCharacter(Category);


        /// <summary>卡牌分类：角色 / 地点 / 物品。</summary>
        public FocusCardCategory Category => _data != null ? _data.Category : FocusCardCategory.Item;

        public string CategoryName => _data != null ? _data.CategoryName : FocusCardCategories.ToChinese(FocusCardCategory.Item);

        /// <summary>卡面描述，翻面后作为台本字幕念出来。</summary>
        public string Description => _data != null ? _data.Description : string.Empty;

        /// <summary>提示浮层的标题：正面给卡名，背面给状态。</summary>
        public string TooltipTitle => _faceUp ? DisplayName : "未翻开";

        /// <summary>提示浮层的副标题：提示还能做什么。</summary>
        public string TooltipHint => _faceUp ? "拖拽移动" : "点击翻开 · 拖拽移动";

        private void Awake()
        {
            CacheRefs();
            ApplyData();
        }

        private void Update()
        {
            if (_rect == null)
            {
                return;
            }

            // 指数平滑，帧率无关：k 越大越跟手。
            float k = 1f - Mathf.Exp(-_hoverSmoothing * Time.unscaledDeltaTime);

            float targetScale = 1f;
            if (_hovered && !_flipping)
            {
                targetScale = _hoverScale;
            }
            else if (_selected && !_flipping)
            {
                targetScale = _selectedScale;
            }

            _rect.localScale = Vector3.Lerp(_rect.localScale, Vector3.one * targetScale, k);

            if (_flipRoot != null)
            {
                float lift = 0f;
                if (_dragging)
                {
                    lift = _dragLift;
                }
                else
                {
                    lift = Mathf.Max(_hovered ? _hoverLift : 0f, _selected ? _selectedLift : 0f);
                }

                Vector2 pos = _flipRoot.anchoredPosition;
                pos.y = Mathf.Lerp(pos.y, lift, k);
                _flipRoot.anchoredPosition = pos;
            }
        }

        /// <summary>选中/取消选中：只切描边与上浮，别的状态不动。</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;

            if (_selectionRing == null)
            {
                CacheRefs();
            }

            if (_selectionRing != null)
            {
                // 选中用统一的暖白色描边：卡面本身已经按分类上色，再用分类色会跟卡框撞色。
                _selectionRing.color = SelectionColor;
                _selectionRing.enabled = _selected;
            }
        }

        /// <summary>把 _data 灌进画面（卡片图与名字）。</summary>
        public void ApplyData()
        {
            CacheRefs();

            if (_nameLabel != null)
            {
                FocusLabFont.Apply(_nameLabel);
            }

            ApplyFace();
        }

        /// <summary>正面/背面切换（不含动画）。</summary>
        public void SetFaceUp(bool faceUp)
        {
            if (_flipping)
            {
                return;
            }

            _faceUp = faceUp;
            ApplyFace();
        }

        /// <summary>
        /// 把背面翻到正面。已经正面朝上时直接返回——本作规定卡牌翻开后不再翻回去。
        /// </summary>
        public void Flip()
        {
            if (_flipping || _faceUp || _data == null)
            {
                return;
            }

            StartCoroutine(FlipRoutine());
        }

        /// <summary>把卡牌移到父容器内的某个位置（自动裁到容器里）。</summary>
        public void MoveTo(Vector2 anchoredPosition)
        {
            if (_rect == null)
            {
                CacheRefs();
            }

            if (_rect == null)
            {
                return;
            }

            if (_parentRect == null)
            {
                _rect.anchoredPosition = anchoredPosition;
                return;
            }

            // 约定：卡牌锚点居中对齐（anchorMin = anchorMax = 0.5），
            // 所以 anchoredPosition 就是相对父容器中心的偏移。
            Rect parent = _parentRect.rect;
            Vector2 half = _rect.rect.size * 0.5f;

            float minX = parent.xMin + half.x;
            float maxX = parent.xMax - half.x;
            float minY = parent.yMin + half.y;
            float maxY = parent.yMax - half.y;

            Vector2 center = new Vector2(parent.center.x + anchoredPosition.x, parent.center.y + anchoredPosition.y);

            center.x = maxX < minX ? parent.center.x : Mathf.Clamp(center.x, minX, maxX);
            center.y = maxY < minY ? parent.center.y : Mathf.Clamp(center.y, minY, maxY);

            _rect.anchoredPosition = new Vector2(center.x - parent.center.x, center.y - parent.center.y);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            HoverEntered?.Invoke(this);

            FocusLabController controller = ResolveController();
            if (controller != null)
            {
                controller.NotifyHoverEnter(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            HoverExited?.Invoke(this);

            FocusLabController controller = ResolveController();
            if (controller != null)
            {
                controller.NotifyHoverExit(this);
            }
        }

        /// <summary>找场景里的胶水层，找不到就记住空结果，下次再找。</summary>
        private FocusLabController ResolveController()
        {
            if (_controller == null)
            {
                _controller = FindAnyObjectByType<FocusLabController>();
            }

            return _controller;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _dragDistance = 0f;
            _dragStartAnchored = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _dragStartPointer = ScreenToParentLocal(eventData.position, eventData.pressEventCamera);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 没拖动、位移又小于阈值，才算一次点击。
            bool clicked = _pointerDown && !_dragging && _dragDistance < _clickThreshold;
            _pointerDown = false;

            if (!clicked)
            {
                return;
            }

            // 背面朝上：这一下只负责翻开，不参与选中。
            if (!_faceUp)
            {
                Flip();
                return;
            }

            FocusLabController controller = ResolveController();
            if (controller != null)
            {
                controller.NotifyClicked(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragStartAnchored = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _dragStartPointer = ScreenToParentLocal(eventData.position, eventData.pressEventCamera);

            // 正在拖的牌放到最上层，别被别的牌挡住。
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            _dragging = true;

            Vector2 local = ScreenToParentLocal(eventData.position, eventData.pressEventCamera);
            Vector2 delta = local - _dragStartPointer;
            _dragDistance = Mathf.Max(_dragDistance, delta.magnitude);

            MoveTo(_dragStartAnchored + delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        private void CacheRefs()
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            if (_parentRect == null)
            {
                _parentRect = _rect.parent as RectTransform;
            }

            if (_flipRoot == null)
            {
                Transform flip = transform.Find("Flip");
                if (flip != null)
                {
                    _flipRoot = flip as RectTransform;
                }
            }

            if (_faceImage == null && _flipRoot != null)
            {
                _faceImage = _flipRoot.GetComponent<Image>();
            }

            if (_nameLabel == null && _flipRoot != null)
            {
                _nameLabel = _flipRoot.GetComponentInChildren<Text>(true);
            }

            if (_selectionRing == null)
            {
                Transform selection = transform.Find("Selection");
                if (selection != null)
                {
                    _selectionRing = selection.GetComponent<Image>();
                }
            }
        }

        private void ApplyFace()
        {
            if (_faceImage != null)
            {
                _faceImage.sprite = _data != null ? (_faceUp ? _data.FrontSprite : _data.BackSprite) : _faceImage.sprite;
                _faceImage.enabled = _faceImage.sprite != null;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = _faceUp ? DisplayName : string.Empty;
            }
        }

        private void SetSquash(float value)
        {
            if (_flipRoot == null)
            {
                return;
            }

            // 留一点点厚度，避免出现 scale 为 0 的退化矩阵。
            _flipRoot.localScale = new Vector3(Mathf.Max(value, 0.0001f), 1f, 1f);
        }

        private IEnumerator FlipRoutine()
        {
            _flipping = true;

            float half = Mathf.Max(0.02f, _flipDuration * 0.5f);

            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                SetSquash(Mathf.SmoothStep(1f, 0f, t / half));
                yield return null;
            }

            SetSquash(0f);

            _faceUp = !_faceUp;
            ApplyFace();
            Flipped?.Invoke(this);

            FocusLabController controller = ResolveController();
            if (controller != null)
            {
                controller.NotifyFlipped(this);
            }

            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                SetSquash(Mathf.SmoothStep(0f, 1f, t / half));
                yield return null;
            }

            SetSquash(1f);
            _flipping = false;
        }

        private Vector2 ScreenToParentLocal(Vector2 screenPosition, Camera camera)
        {
            RectTransform parent = _parentRect != null ? _parentRect : _rect;
            if (parent == null)
            {
                return Vector2.zero;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out Vector2 local)
                ? local
                : Vector2.zero;
        }

#if UNITY_EDITOR
        /// <summary>编辑器构建场景时注入引用（运行时不使用）。</summary>
        public void EditorBind(FocusCardData data, RectTransform flipRoot, Image faceImage, Text nameLabel, Image selectionRing)
        {
            _data = data;
            _flipRoot = flipRoot;
            _faceImage = faceImage;
            _nameLabel = nameLabel;
            _selectionRing = selectionRing;
            CacheRefs();
            ApplyData();
            SetSelected(false);
        }
#endif
    }
}
