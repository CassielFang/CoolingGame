using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 笔记本面板：屏幕中间一本摊开的书，左页记线索、右页是案件板（钉在一块软木板上）。
    ///
    /// 线索页按「来源卡牌」分组：一组 = 来源卡的图 + 卡名，图下面把这条来源的线索条目按获得顺序（旧→新）排开。
    /// 条目可以按住拖到右侧的案件板空槽里：拖的时候提出来跟手，槽会亮，松手吸附进去。
    /// 放进去以后线索页里那一条变成虚线空位（位置不重排，刷新一下就回来了），再拖出来还能放回原处。
    ///
    /// 没配线索的来源卡，开局就先在笔记里占一组「待补线索」——这样线索页一打开就有按来源分好的组可看。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusNotebookPanel : MonoBehaviour, IPointerClickHandler
    {
        public const string TabClueName = "线索";
        public const string TabCaseName = "案件";
        public const string NoCluePlaceholder = "暂无线索";

        [Header("引用")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private GameObject _bookLayer;
        [SerializeField] private GameObject _boardView;
        [SerializeField] private GameObject _coverView;
        [SerializeField] private Button _bookButton;
        [SerializeField] private Button _boardButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Image _tabUnderline;
        [SerializeField] private Text _tabClueLabel;
        [SerializeField] private Text _tabCaseLabel;
        [SerializeField] private Text _footerLabel;
        [SerializeField] private Text _detailText;
        [SerializeField] private Text _emptyHint;
        [SerializeField] private ScrollRect _clueScroll;
        [SerializeField] private RectTransform _clueContent;
        [SerializeField] private FocusNotebookCaseBoard _board;
        [SerializeField] private CanvasGroup _dragGroup;
        [SerializeField] private RectTransform _dragGhost;
        [SerializeField] private Image _dragGhostIcon;
        [SerializeField] private Text _dragGhostLabel;
        [SerializeField] private CanvasGroup _detailGroup;
        [SerializeField] private FocusSubtitlePanel _subtitle;

        [Header("线索表")]
        [Tooltip("场景里能拿到的全部线索；按这里登记的顺序决定「同一条来源」下条目的先后。")]
        [SerializeField] private FocusClueData[] _clues;

        [Header("来源卡")]
        [Tooltip("线索页里要先摆出来的来源卡（还没记到线索也先占一组）。留空就只摆有线索的来源。")]
        [SerializeField] private FocusCardData[] _sourceCards;

        [Header("卡片外观")]
        [SerializeField] private Sprite _chipSprite;
        [SerializeField, Min(16f)] private float _chipHeight = 58f;
        [SerializeField, Min(0f)] private float _chipGap = 10f;
        [SerializeField, Min(8f)] private float _chipFontSize = 21f;
        [SerializeField, Min(0f)] private float _sourceCardWidth = 120f;

        [Header("动画")]
        [SerializeField, Min(0.1f)] private float _openSpeed = 12f;
        [SerializeField, Min(0.1f)] private float _pageSpeed = 9f;

        private readonly List<FocusClueData> _seen = new List<FocusClueData>();
        private readonly List<FocusClueEntry> _entries = new List<FocusClueEntry>();
        private readonly List<EntryRecord> _records = new List<EntryRecord>();

        private FocusClueEntry _draggedEntry;
        private FocusClueData _draggedClue;
        private FocusNotebookSlot _hoveredSlot;
        private FocusClueEntry _expanded;
        private bool _isOpen;
        private bool _suppressClose;
        private float _openAlpha;
        private float _pageSlide = 1f;
        private PendingHint _pending = PendingHint.None;

        private struct EntryRecord
        {
            public FocusClueEntry Entry;
            public string GroupKey;
        }

        private struct PendingHint
        {
            public static readonly PendingHint None = new PendingHint { Text = null, Duration = 0f };

            public string Text;
            public float Duration;
        }

        /// <summary>打开/关闭笔记本。</summary>
        public event Action<FocusNotebookPanel, bool> OpenChanged;

        /// <summary>一条新线索被记进来。</summary>
        public event Action<FocusClueData> ClueRecorded;

        public bool IsOpen => _isOpen;

        public float Alpha => _openAlpha;

        public FocusNotebookCaseBoard Board => _board;

        public IReadOnlyList<FocusClueData> SeenClues => _seen;

        /// <summary>线索页上现存的条目（含已经被放进案件板的）。</summary>
        public IReadOnlyList<FocusClueEntry> Entries => _entries;

        public int ClueCount => _seen.Count;

        public string FooterText => _footerLabel != null ? _footerLabel.text : string.Empty;

        public string DetailText => _detailText != null && _detailText.gameObject.activeInHierarchy ? _detailText.text : string.Empty;

        public FocusClueEntry DraggedEntry => _draggedEntry;

        /// <summary>拖拽时条目临时挂的父级：拍在纸上，但不受滚动视图裁剪。</summary>
        public RectTransform DragLayer => _dragGroup != null ? (RectTransform)_dragGroup.transform : _clueContent;

        public RectTransform ListParent => _clueContent;

        public RectTransform ClueContent => _clueContent;

        public Camera UiCamera
        {
            get
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
        }

        /// <summary>场景里还有几条线索没记进笔记。</summary>
        public int PendingClueCount
        {
            get
            {
                if (_clues == null)
                {
                    return 0;
                }

                int pending = 0;
                for (int i = 0; i < _clues.Length; i++)
                {
                    if (_clues[i] != null && !_seen.Contains(_clues[i]))
                    {
                        pending++;
                    }
                }

                return pending;
            }
        }

        // ---- 生命周期 ---------------------------------------------------

        private void Awake()
        {
            CacheRefs();
            HideImmediate();

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_boardButton != null)
            {
                _boardButton.onClick.AddListener(ShowCasePage);
            }

            if (_bookButton != null)
            {
                _bookButton.onClick.AddListener(ShowCluePage);
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(OnBackClicked);
            }
        }

        private void OnDisable()
        {
            _isOpen = false;
            _openAlpha = 0f;
            CancelDragInternal();
        }

        // ---- 打开 / 关闭 ------------------------------------------------

        public void Open()
        {
            CacheRefs();
            if (_root == null)
            {
                return;
            }

            _suppressClose = true;
            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }

            _suppressClose = false;

            _isOpen = true;
            if (_boardButton != null)
            {
                _boardButton.interactable = true;
            }

            RebuildCluePage();
            ShowCluePage(instant: true);

            if (_group != null)
            {
                _group.blocksRaycasts = true;
                _group.interactable = true;
            }

            SetFooter("共 " + _seen.Count + " 条线索 · 左键拖条目到右边空槽，右键看详情");

            OpenChanged?.Invoke(this, true);
        }

        public void Close()
        {
            if (!_isOpen || _suppressClose)
            {
                return;
            }

            CancelDragInternal();
            CloseDetail();
            _isOpen = false;

            if (_boardButton != null)
            {
                _boardButton.interactable = true;
            }

            if (_group != null)
            {
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }

            FlushPendingHint();
            OpenChanged?.Invoke(this, false);
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void HideImmediate()
        {
            _isOpen = false;
            _openAlpha = 0f;

            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }

            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }

            if (_dragGroup != null)
            {
                _dragGroup.alpha = 0f;
                _dragGroup.blocksRaycasts = false;
            }

            CloseDetail();
        }

        private void OnBackClicked()
        {
            // 「翻到封面」：把案子先收起来，再合上本子。
            ShowCluePage();
            Close();
        }

        // ---- 页面切换 ---------------------------------------------------

        private enum NotebookPage
        {
            Clue = 0,
            Case = 1,
        }

        public void ShowCluePage()
        {
            ShowCluePage(instant: false);
        }

        public void ShowCasePage()
        {
            if (!_isOpen)
            {
                return;
            }

            CloseDetail();
            ShowPage(NotebookPage.Case, instant: false);
            RefreshDetail();
        }

        private void ShowCluePage(bool instant)
        {
            ShowPage(NotebookPage.Clue, instant);
            RefreshDetail();
        }

        private void ShowPage(NotebookPage page, bool instant)
        {
            bool cluePage = page == NotebookPage.Clue;

            if (_clueScroll != null)
            {
                _clueScroll.gameObject.SetActive(cluePage);
            }

            if (_emptyHint != null)
            {
                _emptyHint.gameObject.SetActive(cluePage && _seen.Count == 0);
            }

            if (_boardView != null)
            {
                _boardView.SetActive(!cluePage);
            }

            TabUnderlineTo(cluePage);

            if (!cluePage)
            {
                _board?.ReplayPinAnimation();
                SetFooter(_board != null
                    ? "案件板 · " + _board.SummaryText + " · 右键槽可取下线索"
                    : string.Empty);
            }

            _pageSlide = instant ? 1f : 0f;
            ApplyPageSlide();
        }

        private void TabUnderlineTo(bool onCluePage)
        {
            if (_tabUnderline == null)
            {
                return;
            }

            // 页签下划线跟着当前页走；两个页签的宽度一样，直接换算成位置。
            RectTransform rect = _tabUnderline.rectTransform;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Text anchorLabel = onCluePage ? _tabClueLabel : _tabCaseLabel;
            if (anchorLabel == null)
            {
                return;
            }

            var labelRect = (RectTransform)anchorLabel.transform;
            Vector3 world = labelRect.TransformPoint(new Vector3(0f, labelRect.rect.yMin, 0f));
            Vector2 local = parent.InverseTransformPoint(world);
            Vector2 position = rect.anchoredPosition;
            position.x = Mathf.Round(local.x + labelRect.rect.width * 0.5f);
            rect.anchoredPosition = position;
        }

        private void ApplyPageSlide()
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_pageSlide));

            if (_clueScroll != null && _clueScroll.gameObject.activeSelf)
            {
                CanvasGroup group = _clueScroll.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = t;
                }
            }

            if (_boardView != null && _boardView.activeSelf)
            {
                CanvasGroup group = _boardView.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = t;
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                CloseDetail();
            }
        }

        // ---- 线索 -------------------------------------------------------

        /// <summary>记一条线索（剧情流程里线索出现时调）。已经在笔记里的不重复记。</summary>
        public bool RecordClue(FocusClueData clue)
        {
            if (clue == null || _seen.Contains(clue))
            {
                return false;
            }

            _seen.Add(clue);

            if (_isOpen)
            {
                RebuildCluePage();
            }

            ClueRecorded?.Invoke(clue);
            QueueHint("记下新线索：" + clue.Text, 4f);
            return true;
        }

        /// <summary>把线索表里第一条还没记的线索记进来（调试按钮用）。</summary>
        public bool CollectNextClue()
        {
            if (_clues == null)
            {
                return false;
            }

            for (int i = 0; i < _clues.Length; i++)
            {
                if (_clues[i] != null && !_seen.Contains(_clues[i]))
                {
                    return RecordClue(_clues[i]);
                }
            }

            return false;
        }

        public bool HasClue(FocusClueData clue)
        {
            return clue != null && _seen.Contains(clue);
        }

        public FocusClueEntry FindEntry(FocusClueData clue)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null && _entries[i].Clue == clue)
                {
                    return _entries[i];
                }
            }

            return null;
        }

        public int SiblingIndexFor(FocusClueEntry entry)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Entry == entry)
                {
                    return i;
                }
            }

            return Mathf.Max(0, _clueContent != null ? _clueContent.childCount - 1 : 0);
        }

        /// <summary>按当前线索表重建左侧线索页（分组 → 来源卡 → 条目）。</summary>
        public void RebuildCluePage()
        {
            if (_clueContent == null)
            {
                return;
            }

            CancelDragInternal();
            CloseDetail();

            for (int i = _clueContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _clueContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            _entries.Clear();
            _records.Clear();

            var keys = new List<string>();
            var sources = new List<FocusCardData>();

            for (int i = 0; i < _seen.Count; i++)
            {
                AddSource(keys, sources, _seen[i] != null ? _seen[i].SourceCard : null);
            }

            if (_sourceCards != null)
            {
                for (int i = 0; i < _sourceCards.Length; i++)
                {
                    AddSource(keys, sources, _sourceCards[i]);
                }
            }

            for (int g = 0; g < keys.Count; g++)
            {
                BuildGroup(keys[g], sources[g], g == 0);
            }

            if (_emptyHint != null)
            {
                _emptyHint.gameObject.SetActive(_seen.Count == 0);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_clueContent);

            if (_clueScroll != null)
            {
                _clueScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void AddSource(List<string> keys, List<FocusCardData> sources, FocusCardData card)
        {
            string key = SourceKey(card);
            if (keys.Contains(key))
            {
                return;
            }

            keys.Add(key);
            sources.Add(card);
        }

        private void BuildGroup(string key, FocusCardData source, bool first)
        {
            RectTransform group = NewRect("Source_" + Sanitize(key), _clueContent, false);

            var layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, first ? 4 : 18, 6);

            // 来源卡 + 卡名 + 条数
            RectTransform row = NewRect("SourceCard", group, false);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.UpperLeft;

            float cardHeight = _sourceCardWidth * (360f / 270f);

            RectTransform thumb = NewRect("Thumb", row, false);
            LayoutElement thumbElement = thumb.gameObject.AddComponent<LayoutElement>();
            thumbElement.preferredWidth = _sourceCardWidth;
            thumbElement.preferredHeight = cardHeight;
            thumbElement.minWidth = _sourceCardWidth;
            thumbElement.minHeight = cardHeight;
            Image thumbImage = thumb.gameObject.AddComponent<Image>();
            thumbImage.sprite = source != null ? source.FrontSprite : null;
            thumbImage.preserveAspect = true;
            thumbImage.raycastTarget = false;

            RectTransform names = NewRect("Names", row, false);
            LayoutElement namesElement = names.gameObject.AddComponent<LayoutElement>();
            namesElement.flexibleWidth = 1f;
            namesElement.preferredHeight = cardHeight;
            namesElement.minHeight = cardHeight;
            var namesLayout = names.gameObject.AddComponent<VerticalLayoutGroup>();
            namesLayout.childControlWidth = true;
            namesLayout.childControlHeight = true;
            namesLayout.childForceExpandWidth = true;
            namesLayout.childForceExpandHeight = false;
            namesLayout.spacing = 3f;
            namesLayout.childAlignment = TextAnchor.UpperLeft;

            MakeLabel("CardName", names, source != null ? source.DisplayName : "来源未记", 26, new Color(0.30f, 0.20f, 0.11f, 1f), 34f);
            MakeLabel("CardKind", names, source != null ? source.CategoryName : "线索", 18, new Color(0.47f, 0.37f, 0.25f, 1f), 24f);

            int count = CountClues(key);
            RectTransform pill = NewRect("Count", names, false);
            LayoutElement pillElement = pill.gameObject.AddComponent<LayoutElement>();
            pillElement.preferredHeight = 26f;
            pillElement.preferredWidth = 96f;
            pillElement.minWidth = 96f;
            Image pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.sprite = _chipSprite;
            pillImage.type = Image.Type.Sliced;
            pillImage.raycastTarget = false;
            Color accent = FocusClueKinds.Accent(source != null ? CategoryToKind(source.Category) : FocusClueKind.Thing);
            pillImage.color = new Color(accent.r, accent.g, accent.b, 0.85f);

            RectTransform pillLabel = NewRect("CountLabel", pill, true);
            Text pillText = pillLabel.gameObject.AddComponent<Text>();
            pillText.font = UiFont();
            pillText.fontSize = 17;
            pillText.color = Color.white;
            pillText.alignment = TextAnchor.MiddleCenter;
            pillText.raycastTarget = false;
            pillText.text = count + " 条";
            FocusLabFont.Apply(pillText);

            RectTransform divider = NewRect("Divider", group, false);
            LayoutElement dividerElement = divider.gameObject.AddComponent<LayoutElement>();
            dividerElement.preferredHeight = 2f;
            dividerElement.minHeight = 2f;
            Image dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = new Color(0.62f, 0.50f, 0.34f, 0.45f);
            dividerImage.raycastTarget = false;

            // 条目：同一条来源下按线索表顺序（旧→新）排开
            int made = 0;
            for (int i = 0; i < _seen.Count; i++)
            {
                FocusClueData clue = _seen[i];
                if (clue == null || SourceKey(clue.SourceCard) != key)
                {
                    continue;
                }

                CreateEntry(key, clue);
                made++;
            }

            if (made == 0)
            {
                CreatePlaceholder(group, NoCluePlaceholder);
            }
        }

        private void CreateEntry(string key, FocusClueData clue)
        {
            RectTransform item = NewRect("Clue_" + Sanitize(clue.ClueId), _clueContent, false);

            LayoutElement element = item.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = _chipHeight;
            element.minHeight = _chipHeight;

            RectTransform card = NewRect("Chip", item, true);
            Image chip = card.gameObject.AddComponent<Image>();
            chip.sprite = _chipSprite;
            chip.type = Image.Type.Sliced;
            chip.color = new Color(0.95f, 0.93f, 0.87f, 1f);

            Color accent = FocusClueKinds.Accent(clue.Kind);

            RectTransform stripe = NewRect("Stripe", card, false);
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.sizeDelta = new Vector2(7f, 0f);
            stripe.anchoredPosition = Vector2.zero;
            Image stripeImage = stripe.gameObject.AddComponent<Image>();
            stripeImage.sprite = _chipSprite;
            stripeImage.type = Image.Type.Sliced;
            stripeImage.color = accent;
            stripeImage.raycastTarget = false;

            RectTransform labelRect = NewRect("Text", card, true);
            labelRect.offsetMin = new Vector2(20f, 4f);
            labelRect.offsetMax = new Vector2(-46f, -4f);
            Text label = labelRect.gameObject.AddComponent<Text>();
            label.font = UiFont();
            label.fontSize = Mathf.RoundToInt(_chipFontSize);
            label.color = new Color(0.20f, 0.15f, 0.09f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
            label.text = clue.Text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            FocusLabFont.Apply(label);

            RectTransform dot = NewRect("DragDot", card, false);
            dot.anchorMin = new Vector2(1f, 0.5f);
            dot.anchorMax = new Vector2(1f, 0.5f);
            dot.pivot = new Vector2(1f, 0.5f);
            dot.sizeDelta = new Vector2(16f, 24f);
            dot.anchoredPosition = new Vector2(-16f, 0f);
            Image dotImage = dot.gameObject.AddComponent<Image>();
            dotImage.sprite = _chipSprite;
            dotImage.type = Image.Type.Sliced;
            dotImage.color = new Color(0.55f, 0.47f, 0.36f, 0.9f);
            dotImage.raycastTarget = false;
            dotImage.enabled = false;

            FocusClueEntry entry = item.gameObject.AddComponent<FocusClueEntry>();
            entry.EditorBind(null, chip, label, dotImage);
            entry.Bind(this, clue);
            entry.RightClicked += NotifyEntryRightClicked;

            _entries.Add(entry);
            _records.Add(new EntryRecord { Entry = entry, GroupKey = key });
        }

        private void CreatePlaceholder(RectTransform parent, string text)
        {
            RectTransform holder = NewRect("Placeholder", parent, false);

            LayoutElement element = holder.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 42f;
            element.minHeight = 42f;

            Image dashed = holder.gameObject.AddComponent<Image>();
            dashed.sprite = _chipSprite;
            dashed.type = Image.Type.Sliced;
            dashed.color = new Color(0.55f, 0.44f, 0.30f, 0.22f);
            dashed.raycastTarget = false;

            RectTransform labelRect = NewRect("Text", holder, true);
            Text label = labelRect.gameObject.AddComponent<Text>();
            label.font = UiFont();
            label.fontSize = 18;
            label.color = new Color(0.50f, 0.40f, 0.27f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            FocusLabFont.Apply(label);

            FocusCluePlaceholder placeholder = holder.gameObject.AddComponent<FocusCluePlaceholder>();
            placeholder.EditorBind(label);
            placeholder.SetText(text);
        }

        private void NotifyEntryRightClicked(FocusClueEntry entry)
        {
            if (entry == null || entry.Clue == null)
            {
                return;
            }

            _expanded = entry;
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            if (_detailGroup == null || _detailText == null)
            {
                return;
            }

            FocusClueData clue = _isOpen ? (_expanded != null ? _expanded.Clue : null) : null;
            bool show = clue != null;

            _detailGroup.alpha = show ? 1f : 0f;
            _detailGroup.blocksRaycasts = show;
            _detailGroup.interactable = show;
            _detailText.gameObject.SetActive(show);

            if (!show)
            {
                return;
            }

            FocusLabFont.Apply(_detailText);
            _detailText.text = clue.Text
                + "\n\n来源：" + clue.SourceName + "（" + FocusClueKinds.ToChinese(clue.Kind) + "）\n"
                + (string.IsNullOrEmpty(clue.Detail) ? "（这条线索暂时没有更多记录）" : clue.Detail);
        }

        public void CloseDetail()
        {
            _expanded = null;

            if (_detailGroup != null)
            {
                _detailGroup.alpha = 0f;
                _detailGroup.blocksRaycasts = false;
                _detailGroup.interactable = false;
            }

            if (_detailText != null)
            {
                _detailText.gameObject.SetActive(false);
            }
        }

        // ---- 拖拽 -------------------------------------------------------

        /// <summary>开始拖一条线索：从滚动视图里提出来挂到翻页层。返回 false 表示这次拖不起来。</summary>
        public bool BeginClueDrag(FocusClueEntry entry, Vector2 screenPosition)
        {
            if (entry == null || _clueContent == null || _dragGroup == null)
            {
                return false;
            }

            CancelDragInternal();
            _draggedEntry = entry;
            _draggedClue = entry.Clue;

            entry.BeginFollow(DragLayer);
            entry.FollowPointer(screenPosition, UiCamera);

            ShowDragGhost(true, screenPosition);
            return true;
        }

        public void MoveDraggedClue(FocusClueEntry entry, Vector2 screenPosition)
        {
            if (entry == null || entry != _draggedEntry)
            {
                return;
            }

            entry.FollowPointer(screenPosition, UiCamera);
            ShowDragGhost(true, screenPosition);
            UpdateHover(screenPosition);
        }

        /// <summary>松手：落在空槽上就吸附进去，否则飞回原位。</summary>
        public bool EndClueDrag(FocusClueEntry entry, Vector2 screenPosition, bool released)
        {
            if (entry == null || entry != _draggedEntry)
            {
                return false;
            }

            FocusNotebookSlot slot = released && _board != null && _board.gameObject.activeInHierarchy
                ? _board.FindHoveredSlot(screenPosition, UiCamera)
                : null;

            _draggedEntry = null;
            _draggedClue = null;
            _hoveredSlot = null;
            _board?.Highlight(null);
            ShowDragGhost(false, screenPosition);

            entry.CancelDrag();

            if (slot == null)
            {
                SetFooter("松手的位置没有空槽，线索回到原位");
                return false;
            }

            ApplyDrop(entry, slot);
            return true;
        }

        /// <summary>把某条线索放进槽里（拖拽、测试和剧情直接调用都走这里）。</summary>
        public void ApplyDrop(FocusClueEntry entry, FocusNotebookSlot slot)
        {
            if (entry == null || slot == null || entry.Clue == null)
            {
                return;
            }

            FocusClueData clue = entry.Clue;
            FocusNotebookCaseBoard board = _board;

            // 同一个槽里原来那条线索要还给线索页。
            if (slot.Clue != null && slot.Clue != clue)
            {
                ReturnToNotebook(slot.Clue);
            }

            // 这条线索要是已经在别的槽里，就先从那儿撤掉。
            if (board != null)
            {
                FocusNotebookSlot[] slots = board.Slots;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i] != slot && slots[i].Clue == clue)
                    {
                        slots[i].SetClue(null);
                    }
                }
            }

            board?.Place(slot, clue);
            entry.SetDetached(true);

            SetFooter("「" + clue.Text + "」放进了「" + (slot.Question != null ? slot.Question.Title : slot.name) + "」"
                + (board != null ? " · 已填 " + board.FilledCount + " / " + board.Slots.Length : string.Empty));

            if (board != null && board.IsComplete)
            {
                QueueHint("案件板填满了。（填满即完成关卡的判定这期还没接）", 6f);
            }
        }

        /// <summary>槽里的线索被换下来：线索页对应的条目恢复成正常的词组。</summary>
        public void ReturnToNotebook(FocusClueData clue)
        {
            FindEntry(clue)?.SetDetached(false);
        }

        /// <summary>把某个槽清空（右键槽用）。</summary>
        public void ClearSlot(FocusNotebookSlot slot)
        {
            if (slot == null || _board == null)
            {
                return;
            }

            FocusClueData clue = _board.ClearSlot(slot);
            if (clue != null)
            {
                ReturnToNotebook(clue);
                SetFooter("把「" + clue.Text + "」从案件板上取了下来");
            }
        }

        public void ClearBoard()
        {
            if (_board == null)
            {
                return;
            }

            FocusNotebookSlot[] slots = _board.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Clue != null)
                {
                    ReturnToNotebook(slots[i].Clue);
                }
            }

            _board.ClearAll();
            SetFooter("案件板已经清空");
        }

        private void UpdateHover(Vector2 screenPosition)
        {
            FocusNotebookSlot slot = _board != null && _board.gameObject.activeInHierarchy
                ? _board.FindHoveredSlot(screenPosition, UiCamera)
                : null;

            if (slot == _hoveredSlot)
            {
                return;
            }

            _hoveredSlot = slot;
            _board?.Highlight(slot);
        }

        private void ShowDragGhost(bool visible, Vector2 screenPosition)
        {
            if (_dragGroup == null)
            {
                return;
            }

            _dragGroup.alpha = visible ? 1f : 0f;
            _dragGroup.blocksRaycasts = false;
            _dragGroup.interactable = false;

            if (!visible || _dragGhost == null)
            {
                return;
            }

            if (_dragGhostLabel != null)
            {
                FocusLabFont.Apply(_dragGhostLabel);
                _dragGhostLabel.text = _draggedClue != null ? _draggedClue.Text : string.Empty;
            }

            if (_dragGhostIcon != null)
            {
                _dragGhostIcon.color = _draggedClue != null
                    ? FocusClueKinds.Accent(_draggedClue.Kind)
                    : new Color(0.5f, 0.4f, 0.3f, 1f);
            }

            RectTransform parent = (RectTransform)_dragGroup.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, UiCamera, out Vector2 local))
            {
                _dragGhost.anchoredPosition = local;
            }
        }

        private void CancelDragInternal()
        {
            if (_draggedEntry != null)
            {
                _draggedEntry.CancelDrag();
                _draggedEntry = null;
            }

            _draggedClue = null;
            _hoveredSlot = null;
            _board?.Highlight(null);

            if (_dragGroup != null)
            {
                _dragGroup.alpha = 0f;
                _dragGroup.blocksRaycasts = false;
            }
        }

        // ---- 每帧 -------------------------------------------------------

        private void Update()
        {
            if (_root == null)
            {
                return;
            }

            float k = 1f - Mathf.Exp(-_openSpeed * Time.unscaledDeltaTime);
            _openAlpha = Mathf.Lerp(_openAlpha, _isOpen ? 1f : 0f, k);

            if (_group != null && Mathf.Abs(_group.alpha - _openAlpha) > 0.001f)
            {
                _group.alpha = _openAlpha;
            }

            if (!_isOpen && _openAlpha < 0.01f && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }

            if (_pageSlide < 1f)
            {
                _pageSlide = Mathf.Clamp01(_pageSlide + Time.unscaledDeltaTime * _pageSpeed);
                ApplyPageSlide();
            }

            if (!_isOpen)
            {
                return;
            }

            if (!TryReadPointer(out Vector2 pointer, out bool held))
            {
                return;
            }

            if (_draggedEntry != null)
            {
                MoveDraggedClue(_draggedEntry, pointer);

                if (!held)
                {
                    EndClueDrag(_draggedEntry, pointer, released: true);
                }

                return;
            }

            // 指针被系统吞掉（切窗口）时，别让拖拽碎片留在屏幕上。
            if (_dragGroup != null && _dragGroup.alpha > 0.01f)
            {
                _dragGroup.alpha = 0f;
            }
        }

        private void SetFooter(string text)
        {
            if (_footerLabel == null)
            {
                return;
            }

            FocusLabFont.Apply(_footerLabel);
            _footerLabel.text = text ?? string.Empty;
        }

        /// <summary>笔记本是模态的，提示先攒着，等合上再交给字幕条。</summary>
        private void QueueHint(string text, float duration)
        {
            _pending = new PendingHint { Text = text, Duration = duration };
        }

        private void FlushPendingHint()
        {
            if (string.IsNullOrEmpty(_pending.Text))
            {
                return;
            }

            FocusSubtitlePanel subtitle = _subtitle != null ? _subtitle : FindAnyObjectByType<FocusSubtitlePanel>();
            if (subtitle != null && !subtitle.IsScript)
            {
                subtitle.ShowHint(_pending.Text, _pending.Duration);
            }

            _pending = PendingHint.None;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelDragInternal();
            }
        }

        // ---- 输入与布局工具 ---------------------------------------------

        /// <summary>读当前指针位置与是否按着左键（新输入系统优先）。</summary>
        public static bool TryReadPointer(out Vector2 position, out bool held)
        {
            position = Vector2.zero;
            held = false;
            bool found = false;

#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                held = mouse.leftButton.isPressed;
                found = true;
            }

            if (!found)
            {
                UnityEngine.InputSystem.Touchscreen touch = UnityEngine.InputSystem.Touchscreen.current;
                if (touch != null)
                {
                    position = touch.primaryTouch.position.ReadValue();
                    held = touch.primaryTouch.press.isPressed;
                    found = true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!found)
            {
                position = UnityEngine.Input.mousePosition;
                held = UnityEngine.Input.GetMouseButton(0);
                found = true;
            }
#endif

            return found;
        }

        private static Font UiFont()
        {
            return FocusLabFont.UiFont != null ? FocusLabFont.UiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static RectTransform NewRect(string rectName, Transform parent, bool stretch)
        {
            var go = new GameObject(rectName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.localPosition = Vector3.zero;

            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return rect;
        }

        private static Text MakeLabel(string labelName, Transform parent, string text, int size, Color color, float height)
        {
            RectTransform rect = NewRect(labelName, parent, false);

            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleWidth = 1f;

            Text label = rect.gameObject.AddComponent<Text>();
            label.font = UiFont();
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            FocusLabFont.Apply(label);
            return label;
        }

        private static string Sanitize(string text)
        {
            return string.IsNullOrEmpty(text) ? "None" : text.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        }

        private static string SourceKey(FocusCardData card)
        {
            return card != null ? card.CardId : "(none)";
        }

        private int CountClues(string key)
        {
            int count = 0;
            for (int i = 0; i < _seen.Count; i++)
            {
                if (_seen[i] != null && SourceKey(_seen[i].SourceCard) == key)
                {
                    count++;
                }
            }

            return count;
        }

        private static FocusClueKind CategoryToKind(FocusCardCategory category)
        {
            switch (category)
            {
                case FocusCardCategory.Character:
                    return FocusClueKind.Person;
                case FocusCardCategory.Location:
                    return FocusClueKind.Place;
                default:
                    return FocusClueKind.Thing;
            }
        }

        private void CacheRefs()
        {
            if (_root == null)
            {
                _root = transform as RectTransform;
            }

            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
        }

#if UNITY_EDITOR
        public void EditorBind(
            RectTransform root,
            CanvasGroup group,
            GameObject bookLayer,
            GameObject boardView,
            GameObject coverView,
            Button bookButton,
            Button boardButton,
            Button backButton,
            Button closeButton,
            Image tabUnderline,
            Text tabClueLabel,
            Text tabCaseLabel,
            Text footerLabel,
            Text detailText,
            Text emptyHint,
            ScrollRect clueScroll,
            RectTransform clueContent,
            FocusNotebookCaseBoard board,
            CanvasGroup dragGroup,
            RectTransform dragGhost,
            Image dragGhostIcon,
            Text dragGhostLabel,
            CanvasGroup detailGroup,
            FocusSubtitlePanel subtitle,
            FocusClueData[] clues,
            FocusCardData[] sourceCards,
            Sprite chipSprite)
        {
            _root = root;
            _group = group;
            _bookLayer = bookLayer;
            _boardView = boardView;
            _coverView = coverView;
            _bookButton = bookButton;
            _boardButton = boardButton;
            _backButton = backButton;
            _closeButton = closeButton;
            _tabUnderline = tabUnderline;
            _tabClueLabel = tabClueLabel;
            _tabCaseLabel = tabCaseLabel;
            _footerLabel = footerLabel;
            _detailText = detailText;
            _emptyHint = emptyHint;
            _clueScroll = clueScroll;
            _clueContent = clueContent;
            _board = board;
            _dragGroup = dragGroup;
            _dragGhost = dragGhost;
            _dragGhostIcon = dragGhostIcon;
            _dragGhostLabel = dragGhostLabel;
            _detailGroup = detailGroup;
            _subtitle = subtitle;
            _clues = clues;
            _sourceCards = sourceCards;
            _chipSprite = chipSprite;
        }
#endif
    }
}
