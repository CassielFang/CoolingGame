using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 笔记本线索页上的一条线索。
    ///
    /// 拖拽不用 ScrollRect / IDragHandler：
    /// 线索页是一张竖着滚的纸，拖拽时要把条目从滚动视图里「提」到翻页层上跟着鼠标走，
    /// 并且用屏幕坐标判断有没有落进右侧案件板的空槽，这些走事件系统反而绕。
    /// 所以自己去读指针位置：按下 → 记下指针；移动超过阈值 → 交给笔记本带着走；松开 → 让笔记本决定是吸附进槽，还是飞回原位。
    ///
    /// 放到案件板以后不销毁本条：线索页上这一条变成虚线空位（位置不动），
    /// 所以「拖到案件板后原位置刷新出条目」不用重新排版；再按住它还能拖去别的槽。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusClueEntry : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float DefaultHoldThreshold = 6f;

        [Header("引用")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _chip;
        [SerializeField] private Text _textLabel;
        [SerializeField] private Image _grabDot;

        [Header("拖拽")]
        [Tooltip("按住后移动超过这个像素才开始拖，避免「想点一下看内容」被判成拖拽。")]
        [SerializeField, Min(0f)] private float _holdThreshold = DefaultHoldThreshold;

        private FocusNotebookPanel _notebook;
        private FocusClueData _clue;
        private RectTransform _rect;

        // 拖拽前的位置账本：父级、兄弟序号、锚点、位置、缩放都记下来，松手时原样放回。
        private RectTransform _restParent;
        private int _restSibling;
        private Vector2 _restAnchor;
        private Vector2 _restPosition;
        private Vector3 _restScale = Vector3.one;

        private Vector2 _pressPosition;
        private bool _hovered;
        private bool _pressed;
        private bool _dragging;
        private bool _followed;
        private bool _detached;

        public FocusClueData Clue => _clue;

        public string ClueId => _clue != null ? _clue.ClueId : string.Empty;

        /// <summary>被放到案件板上去了（这里只剩一个空位）。</summary>
        public bool IsDetached => _detached;

        public bool IsDragging => _dragging;

        public RectTransform Rect => _rect != null ? _rect : (RectTransform)transform;

        public string DisplayText => _textLabel != null && _textLabel.enabled ? _textLabel.text : string.Empty;

        /// <summary>右键点了这条线索。</summary>
        public event Action<FocusClueEntry> RightClicked;

        public void Bind(FocusNotebookPanel notebook, FocusClueData clue)
        {
            _notebook = notebook != null ? notebook : GetComponentInParent<FocusNotebookPanel>();
            _clue = clue;

            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            if (_textLabel != null)
            {
                _textLabel.text = clue != null ? clue.Text : string.Empty;
                FocusLabFont.Apply(_textLabel);
            }

            ApplyDetached();
        }

        /// <summary>标记为「已经放到案件板上」：变虚线空位，仍可按住再拖（换槽）。</summary>
        public void SetDetached(bool detached)
        {
            if (_detached == detached)
            {
                return;
            }

            _detached = detached;
            ApplyDetached();
        }

        // ---- 指针 -------------------------------------------------------

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pressed = true;
            _dragging = false;
            _followed = false;
            _pressPosition = eventData.position;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                RightClicked?.Invoke(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyVisual();
        }

        /// <summary>由笔记本每帧推进：负责判拖、跟手、松手。</summary>
        public void TickPointer(Vector2 screenPosition, bool held)
        {
            if (!_pressed)
            {
                return;
            }

            if (!held)
            {
                bool wasDragging = _dragging;
                _pressed = false;
                _dragging = false;

                if (wasDragging)
                {
                    _notebook?.EndClueDrag(this, screenPosition, released: true);
                }
                else
                {
                    CancelDrag();
                }

                return;
            }

            if (!_dragging)
            {
                if ((screenPosition - _pressPosition).sqrMagnitude < _holdThreshold * _holdThreshold)
                {
                    return;
                }

                _dragging = true;
                _followed = _notebook != null && _notebook.BeginClueDrag(this, screenPosition);

                if (_followed)
                {
                    ApplyVisual();
                }
            }

            if (_followed)
            {
                _notebook.MoveDraggedClue(this, screenPosition);
            }
        }

        /// <summary>取消拖拽（误操作、笔记本被关掉时），原样放回线索页。</summary>
        public void CancelDrag()
        {
            _pressed = false;
            _dragging = false;

            if (_followed)
            {
                RestoreRest();
            }

            _followed = false;
            ApplyVisual();
        }

        /// <summary>笔记本开始拖这条：挂到翻页层跟着鼠标走（不参与滚动视图的裁剪），先把原位记下来。</summary>
        public void BeginFollow(RectTransform followParent)
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            if (followParent == null)
            {
                return;
            }

            RememberRest();

            // 滚动视图会被滚没，所以从 ScrollRect 里摘出来，挂到整层的翻页层上。
            _rect.SetParent(followParent, false);
            _rect.SetAsLastSibling();
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.localScale = _restScale * 1.03f;

            // 指针路径会把这两个标记先设好；直接走 API（测试/剧情）时在这里补上。
            _dragging = true;
            _followed = true;
            ApplyVisual();
        }

        /// <summary>跟着光标走（父级是翻页层，所以直接用屏幕坐标换算）。</summary>
        public void FollowPointer(Vector2 screenPosition, Camera camera)
        {
            if (_rect == null || _rect.parent is not RectTransform parent)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out Vector2 local))
            {
                _rect.anchoredPosition = local;
            }
        }

        /// <summary>回到线索页里原来的位置（父级、兄弟顺序、锚点、位置、缩放都还原）。</summary>
        public void RestoreRest()
        {
            if (_rect == null)
            {
                return;
            }

            RectTransform parent = _restParent;
            if (parent == null && _notebook != null)
            {
                parent = _notebook.ListParent;
            }

            if (parent == null)
            {
                return;
            }

            if (_rect.parent != parent)
            {
                _rect.SetParent(parent, false);
            }

            int index = _restSibling;
            if (index >= 0 && index < parent.childCount)
            {
                _rect.SetSiblingIndex(index);
            }

            _rect.anchorMin = _restAnchor;
            _rect.anchorMax = _restAnchor;
            _rect.anchoredPosition = _restPosition;
            _rect.localScale = _restScale;
        }

        /// <summary>兼容旧调用名。</summary>
        public void ReparentToRest()
        {
            RestoreRest();
        }

        // ---- 表现 -------------------------------------------------------

        private void RememberRest()
        {
            if (_rect == null)
            {
                return;
            }

            _restParent = _rect.parent as RectTransform;
            _restSibling = _rect.GetSiblingIndex();
            _restAnchor = _rect.anchorMin;
            _restPosition = _rect.anchoredPosition;
            _restScale = _rect.localScale;
        }

        private void ApplyDetached()
        {
            if (_textLabel != null)
            {
                FocusLabFont.Apply(_textLabel);
                _textLabel.enabled = !_detached;
                _textLabel.text = _clue != null ? _clue.Text : string.Empty;
            }

            if (_chip != null)
            {
                _chip.color = _detached
                    ? new Color(0.42f, 0.34f, 0.24f, 0.28f)
                    : (_clue != null ? ChipColor(_clue) : _chip.color);
            }

            if (_grabDot != null)
            {
                _grabDot.enabled = !_detached;
            }

            ApplyVisual();
        }

        private static Color ChipColor(FocusClueData clue)
        {
            Color accent = FocusClueKinds.Accent(clue.Kind);
            return new Color(
                Mathf.Lerp(0.95f, accent.r, 0.22f),
                Mathf.Lerp(0.93f, accent.g, 0.22f),
                Mathf.Lerp(0.87f, accent.b, 0.22f),
                1f);
        }

        private void ApplyVisual()
        {
            if (_group != null)
            {
                _group.alpha = _dragging ? 0.96f : (_hovered ? 1f : 0.92f);
                _group.blocksRaycasts = true;
            }
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;

            if (_notebook == null)
            {
                _notebook = GetComponentInParent<FocusNotebookPanel>();
            }

            ApplyVisual();
        }

        private void OnDisable()
        {
            _pressed = false;
            _dragging = false;
            _followed = false;
            _hovered = false;
        }

#if UNITY_EDITOR
        public void EditorBind(CanvasGroup group, Image chip, Text textLabel, Image grabDot)
        {
            _group = group;
            _chip = chip;
            _textLabel = textLabel;
            _grabDot = grabDot;
        }
#endif
    }

    /// <summary>
    /// 线索页上某个来源卡下的一行「空位提示」（比如「暂无线索」）。
    /// 只是给布局用，没有逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusCluePlaceholder : MonoBehaviour
    {
        [SerializeField] private Text _label;

        public string Text => _label != null ? _label.text : string.Empty;

        public void SetText(string text)
        {
            if (_label != null)
            {
                FocusLabFont.Apply(_label);
                _label.text = text;
            }
        }

#if UNITY_EDITOR
        public void EditorBind(Text label)
        {
            _label = label;
        }
#endif
    }
}
