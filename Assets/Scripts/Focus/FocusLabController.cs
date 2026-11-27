using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 测试场的胶水层：卡牌的悬停/翻面反馈、选中规则、人物立绘与「确认操作」都收在这里。
    /// 卡牌本身不知道浮层、字幕、立绘的存在，以后换表现只要改这里。
    /// 规则：
    /// <list type="bullet">
    /// <item>悬停 → 名字浮层 + 一条提示字幕；</item>
    /// <item>翻开 → 一条台本字幕（卡名 + 卡面描述），台本字幕必须玩家点击才消失；</item>
    /// <item>选中 → 人物卡弹出左侧立绘，非人物卡只有描边；人物 + 非人物同时选中时出现确认键。</item>
    /// </list>
    /// 卡牌用「主动找控制器」的方式回调，不走 C# 事件订阅：
    /// 进入播放模式时脚本可能被重新加载，Awake 里订阅的事件会丢，主动调用不会。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusLabController : MonoBehaviour
    {
        /// <summary>约定：id 为「-1」的剧本用来放测试文本，右上角按钮优先播它。</summary>
        public const string DefaultNovelId = "-1";

        [Header("引用")]
        [SerializeField] private FocusCardView[] _cards;
        [SerializeField] private FocusCardTooltip _tooltip;
        [SerializeField] private FocusSubtitlePanel _subtitle;
        [SerializeField] private FocusPortraitPanel _portrait;

        [Header("确认操作")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private CanvasGroup _confirmGroup;
        [SerializeField] private Text _confirmLabel;
        [SerializeField, Min(0f)] private float _confirmGap = 6f;

        private FocusCardView _selectedCharacter;
        private FocusCardView _selectedTarget;

        public FocusCardView[] Cards
        {
            get
            {
                EnsureRefs();
                return _cards;
            }
        }

        public FocusCardTooltip Tooltip
        {
            get
            {
                EnsureRefs();
                return _tooltip;
            }
        }

        public FocusSubtitlePanel Subtitle
        {
            get
            {
                EnsureRefs();
                return _subtitle;
            }
        }

        public FocusPortraitPanel Portrait
        {
            get
            {
                EnsureRefs();
                return _portrait;
            }
        }

        public Button ConfirmButton => _confirmButton;

        /// <summary>当前选中的人物卡（行动发起方），没有就是 null。</summary>
        public FocusCardView SelectedCharacter => _selectedCharacter;

        /// <summary>当前选中的非人物卡（被操作的对象），没有就是 null。</summary>
        public FocusCardView SelectedTarget => _selectedTarget;

        /// <summary>人物 + 非人物都选中了，可以确认操作。</summary>
        public bool HasPendingOperation => _selectedCharacter != null && _selectedTarget != null;

        /// <summary>确认键是否亮着（也算对 UI 状态的断言口径）。</summary>
        public bool IsConfirmVisible => _confirmGroup != null && _confirmGroup.alpha > 0.5f;

        private void Awake()
        {
            EnsureRefs();
            ResetVisuals();
        }

        /// <summary>引用缺了就现找，播放模式重载脚本后也不会失效。</summary>
        private void EnsureRefs()
        {
            if (_cards == null || _cards.Length == 0)
            {
                _cards = FindObjectsByType<FocusCardView>(FindObjectsInactive.Exclude);
            }

            if (_tooltip == null)
            {
                _tooltip = FindAnyObjectByType<FocusCardTooltip>();
            }

            if (_subtitle == null)
            {
                _subtitle = FindAnyObjectByType<FocusSubtitlePanel>();
            }

            if (_portrait == null)
            {
                _portrait = FindAnyObjectByType<FocusPortraitPanel>();
            }
        }

        private void ResetVisuals()
        {
            if (_tooltip != null)
            {
                _tooltip.HideImmediate();
            }

            if (_subtitle != null)
            {
                _subtitle.HideImmediate();
            }

            if (_portrait != null)
            {
                _portrait.HideImmediate();
            }

            _selectedCharacter = null;
            _selectedTarget = null;
            ApplySelection();
        }

        /// <summary>卡牌被鼠标指到。</summary>
        public void NotifyHoverEnter(FocusCardView card)
        {
            if (card == null)
            {
                return;
            }

            EnsureRefs();

            if (_tooltip != null)
            {
                _tooltip.Show(card.TooltipTitle, card.TooltipHint);
            }
        }

        /// <summary>鼠标离开卡牌。</summary>
        public void NotifyHoverExit(FocusCardView card)
        {
            // 字幕各有自己的寿命，鼠标移开不急着收，只收名字浮层。
            EnsureRefs();

            if (_tooltip != null)
            {
                _tooltip.Hide();
            }
        }

        /// <summary>卡牌翻开了。</summary>
        public void NotifyFlipped(FocusCardView card)
        {
            if (card == null)
            {
                return;
            }

            EnsureRefs();

            // 指针还停在牌上，浮层标题要从「未翻开」换成卡名。
            if (_tooltip != null)
            {
                _tooltip.Show(card.TooltipTitle, card.TooltipHint);
            }

            // 卡牌的剧情台词改由视觉小说讲，这里不再往字幕条里塞东西。
        }

        /// <summary>
        /// 正面朝上的卡被点了一下：按分类决定选中规则。
        /// 人物卡同一时间只能选一张；非人物卡同一时间也最多一张；
        /// 两边各自再点一次就取消那一侧（取消后如果是人物，立绘收起）。
        /// </summary>
        public void NotifyClicked(FocusCardView card)
        {
            if (card == null || !card.IsFaceUp)
            {
                return;
            }

            if (card.IsCharacter)
            {
                _selectedCharacter = _selectedCharacter == card ? null : card;
            }
            else
            {
                _selectedTarget = _selectedTarget == card ? null : card;
            }

            ApplySelection();
        }

        /// <summary>清空两侧选中（点空白处、确认操作之后都走这里）。</summary>
        public void ClearSelection()
        {
            _selectedCharacter = null;
            _selectedTarget = null;
            ApplySelection();
        }

        /// <summary>确认键：正式效果还没做，先打一行日志，然后清空选中。</summary>
        public void ConfirmOperation()
        {
            if (!HasPendingOperation)
            {
                return;
            }

            Debug.Log("[FocusLab] 执行操作：" + _selectedCharacter.DisplayName + "（" + _selectedCharacter.CategoryName + "） → "
                + _selectedTarget.DisplayName + "（" + _selectedTarget.CategoryName + "）");

            ClearSelection();
        }

        private void ApplySelection()
        {
            EnsureRefs();

            if (_cards != null)
            {
                for (int i = 0; i < _cards.Length; i++)
                {
                    FocusCardView card = _cards[i];
                    if (card == null)
                    {
                        continue;
                    }

                    card.SetSelected(card == _selectedCharacter || card == _selectedTarget);
                }
            }

            if (_portrait != null)
            {
                if (_selectedCharacter != null)
                {
                    _portrait.Show(_selectedCharacter);
                }
                else
                {
                    _portrait.Hide();
                }
            }

            ApplyConfirm();
        }

        private void ApplyConfirm()
        {
            bool show = HasPendingOperation;

            if (_confirmGroup != null)
            {
                _confirmGroup.alpha = show ? 1f : 0f;
                _confirmGroup.blocksRaycasts = show;
                _confirmGroup.interactable = show;
            }

            if (_confirmButton != null)
            {
                _confirmButton.interactable = show;
            }

            if (!show)
            {
                return;
            }

            if (_confirmLabel != null)
            {
                _confirmLabel.text = "执行操作";
            }

            PositionConfirm();
        }

        /// <summary>
        /// 确认键贴在人物卡正下方（水平居中、留一点缝），意思是「让这个人物去作用于那张牌」。
        /// 下方放不下就翻到人物卡上方，横向始终被夹在屏幕内。
        /// </summary>
        private void PositionConfirm()
        {
            if (_confirmButton == null || _selectedTarget == null)
            {
                return;
            }

            RectTransform confirm = (RectTransform)_confirmButton.transform;
            RectTransform parent = confirm.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            // 有人物就贴人物卡（动作的主体），只有非人物卡时才贴目标卡。
            Rect anchorRect = CanvasRect(
                _selectedCharacter != null
                    ? (RectTransform)_selectedCharacter.transform
                    : (RectTransform)_selectedTarget.transform,
                parent);

            Rect bounds = parent.rect;
            Vector2 size = confirm.rect.size;
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            float x = Mathf.Clamp(anchorRect.center.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);

            float y = anchorRect.yMin - halfHeight - _confirmGap;
            if (y - halfHeight < bounds.yMin)
            {
                y = anchorRect.yMax + halfHeight + _confirmGap;
            }

            y = Mathf.Clamp(y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);

            confirm.anchoredPosition = new Vector2(x - bounds.center.x, y - bounds.center.y);
        }

        private static Rect CanvasRect(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            Vector2 min = space.InverseTransformPoint(corners[0]);
            Vector2 max = min;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// 调试按钮：优先播 id 为「-1」的那组（约定用来放测试文本），没有就退回第一组。
        /// </summary>
        public void PlaySampleVisualNovel()
        {
            FocusVisualNovelPlayer player = FocusVisualNovelPlayer.Instance;
            if (player == null)
            {
                player = FindAnyObjectByType<FocusVisualNovelPlayer>();
            }

            if (player == null || player.Novels == null || player.Novels.Count == 0)
            {
                Debug.LogWarning("[FocusLab] 还没有导入视觉小说：先 Tools/视觉小说/导入 xlsx…");
                return;
            }

            FocusVisualNovel target = player.FindNovel(DefaultNovelId);
            if (target == null)
            {
                target = player.Novels[0];
            }

            if (target == null)
            {
                Debug.LogWarning("[FocusLab] 视觉小说目录里没有可播的剧本。");
                return;
            }

            if (player.FindNovel(DefaultNovelId) == null)
            {
                Debug.LogWarning(
                    "[FocusLab] 没有 id 为「" + DefaultNovelId + "」的剧本，先播目录里的第一组：" + target.NovelId);
            }

            FocusVnDirector.Play(target.NovelId);
        }

        /// <summary>调试按钮：收起当前视觉小说。</summary>
        public void StopVisualNovel()
        {
            FocusVnDirector.Stop();
        }

#if UNITY_EDITOR
        /// <summary>编辑器构建场景时注入引用（运行时不使用）。</summary>
        public void EditorBind(
            FocusCardView[] cards,
            FocusCardTooltip tooltip,
            FocusSubtitlePanel subtitle,
            FocusPortraitPanel portrait,
            Button confirmButton,
            CanvasGroup confirmGroup,
            Text confirmLabel)
        {
            _cards = cards;
            _tooltip = tooltip;
            _subtitle = subtitle;
            _portrait = portrait;
            _confirmButton = confirmButton;
            _confirmGroup = confirmGroup;
            _confirmLabel = confirmLabel;
        }
#endif
    }
}
