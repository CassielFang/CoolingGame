using System;
using System.Collections;
using System.Collections.Generic;
using CoolingGame.Core;
using CoolingGame.Core.Samples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 核心机制 demo 的主控制器：桌面 + 卡牌 + 行动面板（点卡→行动按钮→卡槽→确认）
    /// + 新卡翻面 + 笔记本（线索页/问题页）。
    /// 所有规则都委托给 <see cref="CaseSession"/>，这里只做表现与输入。
    /// </summary>
    public sealed class DeskDemoController : MonoBehaviour
    {
        [SerializeField] private CaseDefinitionAsset caseAsset;

        private CaseDefinition _definition;
        private CaseSession _session;

        private RectTransform _cardGrid;
        private RectTransform _actionPanel;
        private Button _notebookButton;
        private Text _timeLabel;
        private Text _toastLabel;
        private CanvasGroup _toastGroup;
        private NotebookPanel _notebook;
        private StoryOverlay _story;
        private Coroutine _toastRoutine;

        private string _selectedCardId;
        private ActionKind _selectedAction;
        private bool _actionSelected;
        private bool _panelOpen;
        private readonly List<string> _context = new List<string>();

        public CaseSession Session
        {
            get { return _session; }
        }

        private void Start()
        {
            _definition = ResolveDefinition();
            StartNewSession();
            BuildUi();
            _notebook.Bind(_session);
            RefreshAll();
            ShowToast("点击任意一张卡牌开始调查。", true);
        }

        private CaseDefinition ResolveDefinition()
        {
            if (caseAsset != null)
            {
                return caseAsset.ToDefinition();
            }

            CaseDefinitionAsset resource = Resources.Load<CaseDefinitionAsset>("Cases/SampleManorCase");
            if (resource != null)
            {
                return resource.ToDefinition();
            }

            return SampleManorCase.Build();
        }

        private void StartNewSession()
        {
            _session = new CaseSession(_definition);
            _selectedCardId = null;
            _context.Clear();
            _actionSelected = false;
            _panelOpen = false;
        }

        // ── 搭 UI ───────────────────────────────────────────────────────────

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;

            RectTransform background = UIFactory.MakePanel(root, "Desk", Color.white, false);
            UIFactory.Stretch(background);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = UIFactory.Wood;
            backgroundImage.type = Image.Type.Tiled;

            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;
            backgroundButton.onClick.AddListener(() =>
            {
                if (_panelOpen)
                {
                    CloseActionPanel();
                }
            });

            RectTransform vignette = UIFactory.MakePanel(root, "Vignette", Color.white, false);
            UIFactory.Stretch(vignette);
            var vignetteImage = vignette.GetComponent<Image>();
            vignetteImage.sprite = UIFactory.Vignette;
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.raycastTarget = false;

            BuildHeader(root);
            BuildCardGrid(root);
            BuildActionPanel(root);
            BuildToast(root);

            _notebook = NotebookPanel.Create(root, ShowToast);
            _story = StoryOverlay.Create(root);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private void BuildHeader(Transform root)
        {
            RectTransform header = UIFactory.MakePanel(root, "Header", new Color(0.07f, 0.05f, 0.04f, 0.86f), false);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 96f);

            Text title = UIFactory.MakeText(header, "Title", _definition.Title, 30, Palette.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(title.rectTransform, 36, 18, 900, 60);

            _timeLabel = UIFactory.MakeText(header, "Time", string.Empty, 22, Palette.Gold, TextAnchor.MiddleRight);
            UIFactory.PlaceRightTop(_timeLabel.rectTransform, 430, 20, 380, 56);

            _notebookButton = UIFactory.MakeButton(header, "NotebookButton", "笔记本", () => OpenNotebook(0),
                Palette.Paper, Palette.Ink, 20);
            UIFactory.PlaceRightTop(_notebookButton.GetComponent<RectTransform>(), 240, 20, 170, 56);

            Button restart = UIFactory.MakeButton(header, "Restart", "重新开始", Restart, Palette.Red, Palette.Paper, 20);
            UIFactory.PlaceRightTop(restart.GetComponent<RectTransform>(), 36, 20, 170, 56);
        }

        private void BuildCardGrid(Transform root)
        {
            RectTransform gridRoot = UIFactory.MakePanel(root, "Cards", new Color(0f, 0f, 0f, 0f), false);
            gridRoot.anchorMin = Vector2.zero;
            gridRoot.anchorMax = Vector2.one;
            gridRoot.offsetMin = new Vector2(60f, 392f);
            gridRoot.offsetMax = new Vector2(-60f, -190f);
            gridRoot.GetComponent<Image>().raycastTarget = false;

            _cardGrid = gridRoot;
        }

        private void BuildActionPanel(Transform root)
        {
            _actionPanel = UIFactory.MakePanel(root, "ActionPanel", new Color(0.11f, 0.085f, 0.065f, 1f));
            _actionPanel.anchorMin = new Vector2(0.5f, 0f);
            _actionPanel.anchorMax = new Vector2(0.5f, 0f);
            _actionPanel.pivot = new Vector2(0.5f, 0f);
            _actionPanel.anchoredPosition = new Vector2(0f, 26f);
            _actionPanel.sizeDelta = new Vector2(1200f, 330f);
            _actionPanel.gameObject.SetActive(false);
        }

        private void BuildToast(Transform root)
        {
            RectTransform toast = UIFactory.MakePanel(root, "Toast", new Color(0.08f, 0.06f, 0.05f, 0.95f));
            toast.anchorMin = new Vector2(0.5f, 1f);
            toast.anchorMax = new Vector2(0.5f, 1f);
            toast.pivot = new Vector2(0.5f, 1f);
            toast.anchoredPosition = new Vector2(0f, -104f);
            toast.sizeDelta = new Vector2(1000f, 64f);

            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            _toastLabel = UIFactory.MakeText(toast, "Label", string.Empty, 22, Palette.Paper,
                TextAnchor.MiddleCenter);
            UIFactory.Stretch(_toastLabel.rectTransform, 16, 16, 0, 0);
        }

        // ── 刷新 ────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshCards();
            RefreshHud();
        }

        private void RefreshCards()
        {
            if (_cardGrid == null)
            {
                return;
            }

            UIFactory.ClearChildren(_cardGrid);

            IReadOnlyList<CardState> cards = _session.Cards;
            var visible = new List<CardState>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].IsConsumed)
                {
                    visible.Add(cards[i]);
                }
            }

            // 手写居中排布：任意张数都能整体居中，比 GridLayoutGroup 好控
            const float cellWidth = 172f;
            const float cellHeight = 240f;
            const float gapX = 20f;
            const float gapY = 20f;
            const int maxColumns = 6;

            int count = visible.Count;
            if (count == 0)
            {
                return;
            }

            int columns = Mathf.Min(maxColumns, count);
            int rows = Mathf.CeilToInt(count / (float)columns);
            float totalHeight = rows * cellHeight + (rows - 1) * gapY;

            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                int rowCount = Mathf.Min(columns, count - row * columns);
                float rowWidth = rowCount * cellWidth + (rowCount - 1) * gapX;

                CardView view = CardView.Create(_cardGrid, visible[i], visible[i].Id == _selectedCardId, OnCardClicked);
                var rect = (RectTransform)view.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cellWidth, cellHeight);
                rect.anchoredPosition = new Vector2(
                    -rowWidth * 0.5f + cellWidth * 0.5f + column * (cellWidth + gapX),
                    totalHeight * 0.5f - cellHeight * 0.5f - row * (cellHeight + gapY));
            }
        }

        private void RefreshHud()
        {
            if (_timeLabel != null)
            {
                _timeLabel.text = _session.TimeBudget >= 0
                    ? string.Format("时间 {0} / {1}", _session.TimeSpent, _session.TimeBudget)
                    : string.Format("行动 {0} 次", _session.TimeSpent);
            }

            if (_notebookButton != null)
            {
                Text label = _notebookButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = _session.Notebook.UnreadCount > 0
                        ? string.Format("笔记本 ({0})", _session.Notebook.UnreadCount)
                        : "笔记本";
                }
            }
        }

        // ── 卡牌交互（草案流程图） ──────────────────────────────────────────

        private void OnCardClicked(string cardId)
        {
            if (_notebook != null && _notebook.Visible)
            {
                return;
            }

            if (_story != null && _story.Visible)
            {
                return;
            }

            CardState card = _session.GetCard(cardId);
            if (card == null || card.IsConsumed)
            {
                return;
            }

            if (!card.IsFaceUp)
            {
                FlipCard(card);
                return;
            }

            if (_panelOpen && cardId != _selectedCardId && HasFreeSlot())
            {
                AddContext(cardId);
                return;
            }

            SelectCard(cardId);
        }

        private void FlipCard(CardState card)
        {
            int before = _session.Notebook.Count;
            if (!_session.Reveal(card.Id))
            {
                return;
            }

            RefreshCards();
            RefreshHud();

            int gained = _session.Notebook.Count - before;
            ShowToast(gained > 0
                    ? string.Format("翻开「{0}」，线索 +{1}。", card.Definition.DisplayName, gained)
                    : string.Format("翻开「{0}」。", card.Definition.DisplayName),
                true);
        }

        private void SelectCard(string cardId)
        {
            _selectedCardId = cardId;
            _context.Clear();

            IReadOnlyList<ActionOption> options = _session.GetActions(cardId);
            bool found = false;
            ActionKind defaultAction = ActionKind.Investigate;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].IsAvailable)
                {
                    defaultAction = options[i].Action;
                    found = true;
                    break;
                }
            }

            if (!found && options.Count > 0)
            {
                defaultAction = options[0].Action;
                found = true;
            }

            if (!found)
            {
                _panelOpen = false;
                _actionPanel.gameObject.SetActive(false);
                RefreshCards();
                ShowToast("这张牌现在没有可用的行动。", false);
                return;
            }

            _selectedAction = defaultAction;
            _actionSelected = true;
            _panelOpen = true;

            BuildActionPanelContents();
            _actionPanel.gameObject.SetActive(true);
            RefreshCards();
        }

        private int RequiredSlotCount()
        {
            if (!_actionSelected || string.IsNullOrEmpty(_selectedCardId))
            {
                return 0;
            }

            IReadOnlyList<ActionOption> options = _session.GetActions(_selectedCardId);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Action == _selectedAction)
                {
                    return options[i].ContextSlotCount;
                }
            }

            return 0;
        }

        private bool HasFreeSlot()
        {
            return _context.Count < RequiredSlotCount();
        }

        private void AddContext(string cardId)
        {
            if (_context.Contains(cardId))
            {
                ShowToast("这张卡已经在卡槽里了。", false);
                return;
            }

            _context.Add(cardId);
            BuildActionPanelContents();
            RefreshCards();
        }

        private void RemoveContext(int index)
        {
            if (index < 0 || index >= _context.Count)
            {
                return;
            }

            _context.RemoveAt(index);
            BuildActionPanelContents();
            RefreshCards();
        }

        private void ChooseAction(ActionKind action)
        {
            if (!_actionSelected || _selectedAction != action)
            {
                _selectedAction = action;
                _actionSelected = true;
                _context.Clear();
            }

            BuildActionPanelContents();
            RefreshCards();
        }

        // ── 行动面板 ────────────────────────────────────────────────────────

        private void BuildActionPanelContents()
        {
            if (_actionPanel == null)
            {
                return;
            }

            UIFactory.ClearChildren(_actionPanel);

            CardState primary = _session.GetCard(_selectedCardId);
            if (primary == null)
            {
                return;
            }

            int slots = RequiredSlotCount();
            _actionPanel.sizeDelta = new Vector2(1200f, slots > 0 ? 330f : 238f);

            Text title = UIFactory.MakeText(_actionPanel, "Title",
                string.Format("对「{0}」采取行动", primary.Definition.DisplayName), 24, Palette.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(title.rectTransform, 26, 14, 700, 38);

            string hint = slots == 0
                ? "该行动不需要其他卡牌，确认后结算。"
                : (_context.Count < slots ? "点击桌面上的其他卡牌放入卡槽。" : "卡槽已填满，可以确认。");

            Text hintText = UIFactory.MakeText(_actionPanel, "Hint", hint, 17,
                new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.66f), TextAnchor.MiddleRight);
            UIFactory.PlaceRightTop(hintText.rectTransform, 26, 14, 520, 38);

            IReadOnlyList<ActionOption> options = _session.GetActions(_selectedCardId);
            for (int i = 0; i < options.Count; i++)
            {
                ActionOption option = options[i];
                bool active = _actionSelected && option.Action == _selectedAction;
                string label = option.ContextSlotCount > 0
                    ? string.Format("{0} +{1}", option.Action.ToChinese(), option.ContextSlotCount)
                    : option.Action.ToChinese();

                ActionKind action = option.Action;
                Button button = UIFactory.MakeButton(_actionPanel, "Action_" + option.Action, label,
                    () => ChooseAction(action), active ? Palette.Gold : Palette.PaperDark, Palette.Ink, 21);
                button.interactable = option.IsAvailable;
                UIFactory.PlaceLeftTop(button.GetComponent<RectTransform>(), 26 + i * 214, 58, 200, 56);
            }

            for (int i = 0; i < slots; i++)
            {
                bool filled = i < _context.Count;
                string slotLabel = filled
                    ? _session.GetCard(_context[i]).Definition.DisplayName
                    : "＋\n空卡槽";

                int index = i;
                Button slotButton = UIFactory.MakeButton(_actionPanel, "Slot" + i, slotLabel,
                    () => RemoveContext(index),
                    filled ? Palette.Paper : new Color(0.28f, 0.23f, 0.18f),
                    filled ? Palette.Ink : new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.9f), 17);
                UIFactory.PlaceLeftTop(slotButton.GetComponent<RectTransform>(), 26 + i * 136, 124, 122, 112);
                slotButton.interactable = filled;
            }

            Button confirm = UIFactory.MakeButton(_actionPanel, "Confirm", "确认", ConfirmAction, Palette.Green,
                Palette.Paper, 22);
            UIFactory.PlaceRightBottom(confirm.GetComponent<RectTransform>(), 26, 22, 190, 54);

            Button cancel = UIFactory.MakeButton(_actionPanel, "Cancel", "取消", CloseActionPanel, Palette.PaperDark,
                Palette.Ink, 20);
            UIFactory.PlaceRightBottom(cancel.GetComponent<RectTransform>(), 234, 22, 140, 54);
        }

        private void CloseActionPanel()
        {
            _panelOpen = false;
            _selectedCardId = null;
            _context.Clear();
            _actionSelected = false;

            if (_actionPanel != null)
            {
                _actionPanel.gameObject.SetActive(false);
            }

            RefreshCards();
        }

        private void ConfirmAction()
        {
            if (!_panelOpen || string.IsNullOrEmpty(_selectedCardId))
            {
                return;
            }

            ActionResult result = _session.Perform(_selectedCardId, _selectedAction, _context.ToArray());
            RefreshHud();

            if (!result.Success)
            {
                ShowToast(result.Message, false);
                return;
            }

            CloseActionPanel();
            ShowToast(result.Message, true);

            StoryNodeDefinition node = string.IsNullOrEmpty(result.StoryNodeId)
                ? null
                : _session.GetStoryNode(result.StoryNodeId);

            if (node != null)
            {
                _story.Show(node, RefreshAll);
            }
        }

        // ── 笔记本 / 情节 / 重开 ────────────────────────────────────────────

        private void OpenNotebook(int tabIndex)
        {
            if (_notebook == null)
            {
                return;
            }

            CloseActionPanel();
            _notebook.Show(tabIndex);
        }

        private void Restart()
        {
            if (_story != null)
            {
                _story.gameObject.SetActive(false);
            }

            if (_notebook != null)
            {
                _notebook.Hide();
            }

            StartNewSession();
            _notebook.Bind(_session);
            RefreshAll();
            ShowToast("已重新开始。", true);
        }

        // ── 提示条 ──────────────────────────────────────────────────────────

        private void ShowToast(string message, bool good)
        {
            if (_toastLabel == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            _toastLabel.text = message;
            _toastLabel.color = good ? Palette.Paper : new Color(1f, 0.72f, 0.62f);
            _toastGroup.alpha = 1f;

            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }

            _toastRoutine = StartCoroutine(HideToastAfter(2.8f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);

            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                _toastGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.4f);
                yield return null;
            }

            _toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        // ── 供自动化/调试驱动，不参与玩家操作 ──────────────────────────────

        public void DebugClick(string cardId)
        {
            OnCardClicked(cardId);
        }

        public void DebugChoose(string actionName)
        {
            ActionKind kind;
            if (Enum.TryParse(actionName, true, out kind))
            {
                ChooseAction(kind);
            }
        }

        public void DebugConfirm()
        {
            ConfirmAction();
        }

        public void DebugOpenNotebook()
        {
            OpenNotebook(0);
        }

        public void DebugOpenQuestions()
        {
            OpenNotebook(1);
        }

        public void DebugRestart()
        {
            Restart();
        }

        public string DescribeState()
        {
            int faceDown = 0;
            for (int i = 0; i < _session.Cards.Count; i++)
            {
                if (!_session.Cards[i].IsFaceUp)
                {
                    faceDown++;
                }
            }

            return string.Format(
                "cards={0} facedown={1} clues={2} unread={3} filled={4}/{5} solved={6} time={7} panel={8} selected={9}",
                _session.Cards.Count, faceDown, _session.Notebook.Count, _session.Notebook.UnreadCount,
                _session.QuestionPage.FilledCount, _session.QuestionPage.SlotCount, _session.IsCaseSolved,
                _session.TimeSpent, _panelOpen, _selectedCardId ?? "-");
        }
    }
}
