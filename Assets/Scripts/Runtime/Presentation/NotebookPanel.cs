using System;
using System.Collections.Generic;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 笔记本：两个选项卡（线索页 / 问题页）。
    /// 问题页支持多张页（第一章 Q01–Q05）：顶部页签切换，每页的槽位可以填**卡牌**或**线索**，
    /// 进度页（Q04）显示「线索 n/7」。结案按钮只在结案页生效。
    /// </summary>
    public sealed class NotebookPanel : MonoBehaviour
    {
        private sealed class SlotRow
        {
            public string SlotId;
            public Text Value;
            public Button Clear;
            public Button Choose;
        }

        private CaseSession _session;
        private Action<string, bool> _notify;
        private Action _onSolved;

        private RectTransform _body;
        private RectTransform _clueBody;
        private RectTransform _questionBody;
        private RectTransform _clueList;
        private RectTransform _questionList;
        private RectTransform _pageTabs;
        private RectTransform _picker;
        private RectTransform _pickerList;
        private Text _pickerTitle;
        private Text _status;
        private InputField _search;
        private Button _tabClues;
        private Button _tabQuestions;
        private Button _submit;
        private string _pickerPageId;
        private string _pickerSlotId;
        private string _activePageId;
        private string _query = string.Empty;
        private int _tabIndex;
        private bool _bodiesBuilt;
        private readonly List<SlotRow> _slotRows = new List<SlotRow>();

        public bool Visible
        {
            get { return gameObject.activeSelf; }
        }

        public static NotebookPanel Create(Transform parent, Action<string, bool> notify, Action onSolved = null)
        {
            RectTransform root = UIFactory.MakePanel(parent, "Notebook", new Color(0f, 0f, 0f, 0.72f), false);
            UIFactory.Stretch(root);

            var panel = root.gameObject.AddComponent<NotebookPanel>();
            panel._notify = notify;
            panel._onSolved = onSolved;
            panel.BuildChrome(root);
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Bind(CaseSession session)
        {
            _session = session;
            _activePageId = null;
            BuildBodies();
            Refresh();
        }

        public void Show(int tabIndex = 0)
        {
            _tabIndex = tabIndex;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ClosePicker();
            Refresh();
        }

        public void Hide()
        {
            ClosePicker();
            gameObject.SetActive(false);
        }

        /// <summary>打开问题页并翻到指定页（自动化/调试用）。</summary>
        public void ShowPage(string pageId)
        {
            _activePageId = pageId;
            Show(1);
        }

        // ── 搭建 ────────────────────────────────────────────────────────────

        private void BuildChrome(RectTransform root)
        {
            RectTransform panel = UIFactory.MakePanel(root, "Book", new Color(0.90f, 0.87f, 0.79f, 1f));
            UIFactory.Stretch(panel, 190, 190, 70, 70);

            Text title = UIFactory.MakeText(panel, "Title", "侦探笔记本", 34, Palette.Ink, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UIFactory.PlaceLeftTop(title.rectTransform, 36, 24, 480, 48);

            // 页面左侧的装订线，让它像一本摊开的笔记
            RectTransform spine = UIFactory.MakePanel(panel, "Spine",
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.16f));
            spine.anchorMin = new Vector2(0f, 0f);
            spine.anchorMax = new Vector2(0f, 1f);
            spine.pivot = new Vector2(0f, 0.5f);
            spine.anchoredPosition = new Vector2(64f, 0f);
            spine.sizeDelta = new Vector2(2f, -48f);

            _tabClues = UIFactory.MakeButton(panel, "TabClues", "线索页", () => SwitchTab(0), Palette.Gold,
                Palette.Ink, 21);
            UIFactory.PlaceLeftTop(_tabClues.GetComponent<RectTransform>(), 80, 88, 170, 50);

            _tabQuestions = UIFactory.MakeButton(panel, "TabQuestions", "问题页", () => SwitchTab(1),
                Palette.PaperDark, Palette.Ink, 21);
            UIFactory.PlaceLeftTop(_tabQuestions.GetComponent<RectTransform>(), 260, 88, 170, 50);

            Button close = UIFactory.MakeButton(panel, "Close", "关闭", Hide, Palette.PaperDark, Palette.Ink, 20);
            UIFactory.PlaceRightTop(close.GetComponent<RectTransform>(), 36, 24, 130, 50);

            _status = UIFactory.MakeText(panel, "Status", string.Empty, 18,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.65f), TextAnchor.MiddleLeft);
            UIFactory.PlaceLeftBottom(_status.rectTransform, 80, 34, 1060, 44);

            _submit = UIFactory.MakeButton(panel, "Submit", "结案", Submit, Palette.Red, Palette.Paper, 22);
            UIFactory.PlaceRightBottom(_submit.GetComponent<RectTransform>(), 36, 30, 220, 58);

            _body = UIFactory.MakePanel(panel, "Body", new Color(0f, 0f, 0f, 0f), false);
            RectTransform bodyRect = _body;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(80f, 96f);
            bodyRect.offsetMax = new Vector2(-36f, -150f);

            _clueBody = UIFactory.MakePanel(_body, "ClueBody", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(_clueBody);

            _questionBody = UIFactory.MakePanel(_body, "QuestionBody", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(_questionBody);

            BuildPicker(root);
        }

        private void BuildBodies()
        {
            if (_bodiesBuilt || _session == null)
            {
                return;
            }

            _bodiesBuilt = true;

            // ── 线索页：搜索框 + 滚动列表 ────────────────────────────────
            RectTransform searchBox = UIFactory.MakePanel(_clueBody, "Search", Palette.PaperDark);
            UIFactory.PlaceLeftTop(searchBox, 0, 0, 560, 50);

            _search = searchBox.gameObject.AddComponent<InputField>();
            Text searchText = UIFactory.MakeText(searchBox, "Text", string.Empty, 20, Palette.Ink,
                TextAnchor.MiddleLeft);
            UIFactory.Stretch(searchText.rectTransform, 16, 16, 2, 2);
            searchText.supportRichText = false;

            Text placeholder = UIFactory.MakeText(searchBox, "Placeholder", "搜索线索（内容 / 来源 / 关键词）", 19,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.4f), TextAnchor.MiddleLeft);
            UIFactory.Stretch(placeholder.rectTransform, 16, 16, 2, 2);

            _search.textComponent = searchText;
            _search.placeholder = placeholder;
            _search.targetGraphic = searchBox.GetComponent<Image>();
            _search.customCaretColor = true;
            _search.caretColor = Palette.Ink;
            _search.onValueChanged.AddListener(value =>
            {
                _query = value;
                RefreshClueList();
            });

            Text hint = UIFactory.MakeText(_clueBody, "SearchHint", "◆ 置顶的线索会排在最上面", 17,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.5f), TextAnchor.MiddleLeft);
            UIFactory.PlaceLeftTop(hint.rectTransform, 584, 0, 700, 50);

            RectTransform scrollArea = UIFactory.MakePanel(_clueBody, "ScrollArea", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(scrollArea, 0, 0, 62, 0);
            UIFactory.MakeScroll(scrollArea, out _clueList, new Color(Palette.PaperDark.r, Palette.PaperDark.g,
                Palette.PaperDark.b, 0.25f));

            // ── 问题页：页签 + 滚动列表 ──────────────────────────────────
            _pageTabs = UIFactory.MakePanel(_questionBody, "PageTabs", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.PlaceLeftTop(_pageTabs, 0, 0, 1360, 54);

            RectTransform questionScroll = UIFactory.MakePanel(_questionBody, "QuestionScroll",
                new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(questionScroll, 0, 0, 64, 0);
            UIFactory.MakeScroll(questionScroll, out _questionList, new Color(Palette.PaperDark.r,
                Palette.PaperDark.g, Palette.PaperDark.b, 0.25f));
        }

        private void BuildPicker(RectTransform root)
        {
            _picker = UIFactory.MakePanel(root, "Picker", new Color(0f, 0f, 0f, 0.62f), false);
            UIFactory.Stretch(_picker);

            RectTransform panel = UIFactory.MakePanel(_picker, "PickerPanel", Palette.Paper);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(860f, 820f);

            _pickerTitle = UIFactory.MakeText(panel, "Title", "选择卡牌或线索", 28, Palette.Ink,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(_pickerTitle.rectTransform, 28, 22, 740, 44);

            Button cancel = UIFactory.MakeButton(panel, "Cancel", "取消", ClosePicker, Palette.PaperDark, Palette.Ink,
                18);
            UIFactory.PlaceRightTop(cancel.GetComponent<RectTransform>(), 28, 22, 120, 44);

            RectTransform scrollArea = UIFactory.MakePanel(panel, "PickerScroll", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(scrollArea, 28, 28, 84, 28);
            UIFactory.MakeScroll(scrollArea, out _pickerList, new Color(Palette.PaperDark.r, Palette.PaperDark.g,
                Palette.PaperDark.b, 0.25f));

            _picker.gameObject.SetActive(false);
        }

        // ── 刷新 ────────────────────────────────────────────────────────────

        private void SwitchTab(int index)
        {
            _tabIndex = index;
            Refresh();
        }

        private void Refresh()
        {
            if (_session == null)
            {
                return;
            }

            ApplyTabStyle();
            _clueBody.gameObject.SetActive(_tabIndex == 0);
            _questionBody.gameObject.SetActive(_tabIndex == 1);

            if (_tabIndex == 0)
            {
                RefreshClueList();
            }
            else
            {
                RefreshQuestionPages();
            }

            RefreshStatus();
        }

        private void ApplyTabStyle()
        {
            StyleTab(_tabClues, _tabIndex == 0);
            StyleTab(_tabQuestions, _tabIndex == 1);
        }

        private static void StyleTab(Button button, bool active)
        {
            button.GetComponent<Image>().color = active ? Palette.Gold : Palette.PaperDark;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = Palette.Ink;
                label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void RefreshClueList()
        {
            UIFactory.ClearChildren(_clueList);

            IReadOnlyList<ClueEntry> entries = string.IsNullOrWhiteSpace(_query)
                ? _session.Notebook.OrderedClues()
                : _session.Notebook.Search(_query);

            if (entries.Count == 0)
            {
                Text empty = UIFactory.MakeText(_clueList, "Empty",
                    string.IsNullOrWhiteSpace(_query) ? "还没有任何线索。先在桌上采取行动。" : "没有匹配的线索。",
                    20, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.5f), TextAnchor.MiddleCenter);
                var emptyElement = empty.gameObject.AddComponent<LayoutElement>();
                emptyElement.preferredHeight = 80f;
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ClueEntry entry = entries[i];
                RectTransform row = UIFactory.MakePanel(_clueList, "Clue_" + entry.ClueId,
                    entry.Pinned ? new Color(0.80f, 0.71f, 0.52f) : new Color(0.86f, 0.80f, 0.67f));
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 82f;
                element.minHeight = 82f;

                string source = entry.Pinned
                    ? "★ 已置顶"
                    : entry.IsNew
                        ? "● 新线索"
                        : "来源：" + (string.IsNullOrEmpty(entry.SourceCardName) ? "—" : entry.SourceCardName);

                Text sourceText = UIFactory.MakeText(row, "Source", source, 15,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.6f), TextAnchor.MiddleLeft);
                UIFactory.PlaceLeftTop(sourceText.rectTransform, 18, 10, 420, 24);

                Text body = UIFactory.MakeText(row, "Text", entry.Text, 20, Palette.Ink, TextAnchor.UpperLeft);
                RectTransform bodyRect = body.rectTransform;
                bodyRect.anchorMin = Vector2.zero;
                bodyRect.anchorMax = Vector2.one;
                bodyRect.offsetMin = new Vector2(18f, 12f);
                bodyRect.offsetMax = new Vector2(-190f, -36f);

                Button pin = UIFactory.MakeButton(row, "Pin", entry.Pinned ? "取消置顶" : "置顶",
                    () => TogglePin(entry.ClueId), entry.Pinned ? Palette.Paper : Palette.Gold, Palette.Ink, 16);
                UIFactory.PlaceRightTop(pin.GetComponent<RectTransform>(), 16, 24, 150, 40);

                var rowButton = row.gameObject.AddComponent<Button>();
                rowButton.targetGraphic = row.GetComponent<Image>();
                rowButton.transition = Selectable.Transition.None;
                string clueId = entry.ClueId;
                rowButton.onClick.AddListener(() =>
                {
                    _session.Notebook.MarkRead(clueId);
                    RefreshClueList();
                    RefreshStatus();
                });
            }
        }

        // ── 问题页（多页） ──────────────────────────────────────────────────

        private void RefreshQuestionPages()
        {
            IReadOnlyList<QuestionPageModel> pages = _session.QuestionBook.Pages;

            if (pages.Count == 0)
            {
                UIFactory.ClearChildren(_pageTabs);
                UIFactory.ClearChildren(_questionList);
                Text empty = UIFactory.MakeText(_questionList, "Empty", "这个案件还没有问题页。", 20,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.5f), TextAnchor.MiddleCenter);
                var element = empty.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 80f;
                return;
            }

            QuestionPageModel active = _session.QuestionBook.Find(_activePageId);
            if (active == null)
            {
                active = pages[0];
            }

            _activePageId = active.Id;

            BuildPageTabs(pages, active);
            BuildPageContent(active);

            _submit.interactable = IsFinalPage(active) && active.IsFilled;
        }

        private void BuildPageTabs(IReadOnlyList<QuestionPageModel> pages, QuestionPageModel active)
        {
            UIFactory.ClearChildren(_pageTabs);

            for (int i = 0; i < pages.Count; i++)
            {
                QuestionPageModel page = pages[i];
                bool isActive = page.Id == active.Id;

                string label = string.IsNullOrEmpty(page.Title) ? page.Id : page.Title;
                if (label.Length > 9)
                {
                    label = label.Substring(0, 9);
                }

                string suffix = page.IsProgressPage
                    ? string.Format(" {0}/{1}", CountCollected(page), RequiredCount(page))
                    : page.IsFilled ? " ✓" : string.Empty;

                string pageId = page.Id;
                Button tab = UIFactory.MakeButton(_pageTabs, "Page_" + page.Id, label + suffix,
                    () => SwitchPage(pageId), isActive ? Palette.Gold : Palette.PaperDark, Palette.Ink, 18);
                UIFactory.PlaceLeftTop(tab.GetComponent<RectTransform>(), i * 214, 0, 206, 52);
            }
        }

        private void BuildPageContent(QuestionPageModel page)
        {
            UIFactory.ClearChildren(_questionList);
            _slotRows.Clear();

            if (page.IsProgressPage)
            {
                BuildProgressPageContent(page);
                return;
            }

            if (page.Slots.Count == 0)
            {
                Text empty = UIFactory.MakeText(_questionList, "Empty", "这一页没有要填的内容。", 20,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.5f), TextAnchor.MiddleCenter);
                var element = empty.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 80f;
                return;
            }

            for (int i = 0; i < page.Slots.Count; i++)
            {
                QuestionSlotState slot = page.Slots[i];
                RectTransform row = UIFactory.MakePanel(_questionList, "Slot_" + slot.SlotId, Palette.PaperDark);
                var rowElement = row.gameObject.AddComponent<LayoutElement>();
                rowElement.preferredHeight = 96f;
                rowElement.minHeight = 96f;

                Text label = UIFactory.MakeText(row, "Label", slot.Definition.Label, 26, Palette.Ink,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.PlaceLeftTop(label.rectTransform, 20, 16, 180, 36);

                Text hint = UIFactory.MakeText(row, "Hint", slot.Definition.Hint, 15,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.55f), TextAnchor.MiddleLeft);
                UIFactory.PlaceLeftTop(hint.rectTransform, 20, 54, 300, 26);

                Text value = UIFactory.MakeText(row, "Value", "— 未填 —", 20,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.45f), TextAnchor.MiddleLeft);
                UIFactory.PlaceLeftTop(value.rectTransform, 210, 28, 780, 40);

                string pageId = page.Id;
                string slotId = slot.SlotId;
                Button choose = UIFactory.MakeButton(row, "Choose", "填入", () => OpenPicker(pageId, slotId),
                    Palette.Gold, Palette.Ink, 18);
                UIFactory.PlaceRightTop(choose.GetComponent<RectTransform>(), 186, 26, 150, 44);

                Button clear = UIFactory.MakeButton(row, "Clear", "清空", () => ClearSlot(pageId, slotId),
                    Palette.Paper, Palette.Ink, 18);
                UIFactory.PlaceRightTop(clear.GetComponent<RectTransform>(), 16, 26, 160, 44);

                _slotRows.Add(new SlotRow { SlotId = slot.SlotId, Value = value, Clear = clear, Choose = choose });
                RefreshSlotRow(page, slot);
            }
        }

        private void BuildProgressPageContent(QuestionPageModel page)
        {
            int collected = CountCollected(page);
            int required = RequiredCount(page);

            RectTransform bar = UIFactory.MakePanel(_questionList, "Progress", Palette.PaperDark);
            var barElement = bar.gameObject.AddComponent<LayoutElement>();
            barElement.preferredHeight = 70f;
            barElement.minHeight = 70f;

            Text barText = UIFactory.MakeText(bar, "Text",
                string.Format("线索 {0} / {1}", collected, required), 26, Palette.Ink,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(barText.rectTransform, 22, 16, 400, 40);

            Text explain = UIFactory.MakeText(bar, "Explain",
                "收齐这些线索后会自动进入下一段。缺的线索在「线索页」里还没出现。", 16,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.6f), TextAnchor.MiddleRight);
            UIFactory.PlaceRightTop(explain.rectTransform, 22, 20, 900, 36);

            QuestionPageDefinition definition = page.Definition;
            if (definition == null || definition.RequiredClueIds == null)
            {
                return;
            }

            for (int i = 0; i < definition.RequiredClueIds.Count; i++)
            {
                string clueId = definition.RequiredClueIds[i];
                bool has = _session.Notebook.Contains(clueId);
                ClueDefinition clue = _session.Case.FindClue(clueId);

                RectTransform row = UIFactory.MakePanel(_questionList, "Req_" + clueId,
                    has ? new Color(0.80f, 0.86f, 0.74f) : new Color(0.86f, 0.80f, 0.67f));
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 66f;
                element.minHeight = 66f;

                Text mark = UIFactory.MakeText(row, "Mark", has ? "✓" : "○", 26,
                    has ? Palette.Green : new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.45f),
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.PlaceLeftTop(mark.rectTransform, 16, 12, 46, 42);

                string text = has
                    ? (clue != null ? clue.Text : clueId)
                    : "（还没找到这条线索）";
                Text body = UIFactory.MakeText(row, "Text", text, 20,
                    has ? Palette.Ink : new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.5f),
                    TextAnchor.MiddleLeft);
                UIFactory.PlaceLeftTop(body.rectTransform, 66, 14, 1120, 40);
            }
        }

        private void RefreshSlotRow(QuestionPageModel page, QuestionSlotState slot)
        {
            SlotRow row = _slotRows.Find(r => r.SlotId == slot.SlotId);
            if (row == null)
            {
                return;
            }

            if (!slot.IsFilled)
            {
                row.Value.text = "— 未填 —";
                row.Value.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.45f);
            }
            else if (slot.HoldsClue)
            {
                ClueDefinition clue = _session.Case.FindClue(slot.PlacedClueId);
                string text = clue != null ? clue.Text : slot.PlacedClueId;
                row.Value.text = "◆ " + Trim(text, 34);
                row.Value.color = Palette.Ink;
            }
            else
            {
                CardState card = _session.GetCard(slot.PlacedCardId);
                CardDefinition definition = card != null ? card.Definition : _session.Case.FindCard(slot.PlacedCardId);
                row.Value.text = definition != null
                    ? Palette.CategoryGlyph(definition.Category) + "  " + definition.DisplayName
                    : slot.PlacedCardId;
                row.Value.color = Palette.Ink;
            }

            row.Clear.interactable = slot.IsFilled && !slot.IsAutoFilled;
            row.Choose.interactable = !slot.IsAutoFilled;
        }

        private void RefreshStatus()
        {
            if (_session == null)
            {
                return;
            }

            string scene = _session.CurrentScene != null ? _session.CurrentScene.DisplayName : "—";
            string stage = _session.CurrentStage != null ? _session.CurrentStage.DisplayName : "单阶段";

            _status.text = string.Format("{0} · {1} · 线索 {2} 条{3}",
                scene,
                stage,
                _session.Notebook.Count,
                _session.Notebook.UnreadCount > 0 ? string.Format("（新 {0}）", _session.Notebook.UnreadCount) : string.Empty);
        }

        // ── 交互 ────────────────────────────────────────────────────────────

        private void SwitchPage(string pageId)
        {
            _activePageId = pageId;
            RefreshQuestionPages();
        }

        private void TogglePin(string clueId)
        {
            _session.Notebook.TogglePin(clueId);
            RefreshClueList();
        }

        private void ClearSlot(string pageId, string slotId)
        {
            _session.ClearAnswer(pageId, slotId);
            RefreshQuestionPages();
        }

        private void OpenPicker(string pageId, string slotId)
        {
            QuestionPageModel page = _session.QuestionBook.Find(pageId);
            QuestionSlotState slot = page != null ? page.FindSlot(slotId) : null;
            if (slot == null)
            {
                return;
            }

            _pickerPageId = pageId;
            _pickerSlotId = slotId;
            _pickerTitle.text = "为「" + slot.Definition.Label + "」选择一条线索或一张卡";

            UIFactory.ClearChildren(_pickerList);

            AddPickerSection("笔记本里的线索");

            IReadOnlyList<ClueEntry> clues = _session.Notebook.OrderedClues();
            for (int i = 0; i < clues.Count; i++)
            {
                string clueId = clues[i].ClueId;
                string clueText = Trim(clues[i].Text, 30);
                Button button = UIFactory.MakeButton(_pickerList, "Clue_" + clueId, "◆  " + clueText,
                    () => PickClue(clueId), Palette.PaperDark, Palette.Ink, 19);
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 56f;
                element.minHeight = 56f;
            }

            AddPickerSection("桌面上的卡牌");

            IReadOnlyList<CardState> cards = _session.GetUsableCards();
            for (int i = 0; i < cards.Count; i++)
            {
                string cardId = cards[i].Id;
                Button button = UIFactory.MakeButton(_pickerList, "Card_" + cardId,
                    Palette.CategoryGlyph(cards[i].Definition.Category) + "   " + cards[i].Definition.DisplayName,
                    () => PickCard(cardId), Palette.Paper, Palette.Ink, 19);
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 56f;
                element.minHeight = 56f;
            }

            _picker.gameObject.SetActive(true);
            _picker.SetAsLastSibling();
        }

        private void AddPickerSection(string title)
        {
            Text header = UIFactory.MakeText(_pickerList, "Section_" + title, title, 17,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.6f), TextAnchor.MiddleLeft,
                FontStyle.Bold);
            var element = header.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 34f;
            element.minHeight = 34f;
        }

        private void ClosePicker()
        {
            _pickerPageId = null;
            _pickerSlotId = null;
            if (_picker != null)
            {
                _picker.gameObject.SetActive(false);
            }
        }

        private void PickClue(string clueId)
        {
            if (string.IsNullOrEmpty(_pickerPageId) || string.IsNullOrEmpty(_pickerSlotId))
            {
                return;
            }

            _session.PlaceClueAnswer(_pickerPageId, _pickerSlotId, clueId);
            AfterPick();
        }

        private void PickCard(string cardId)
        {
            if (string.IsNullOrEmpty(_pickerPageId) || string.IsNullOrEmpty(_pickerSlotId))
            {
                return;
            }

            _session.PlaceAnswer(_pickerPageId, _pickerSlotId, cardId);
            AfterPick();
        }

        private void AfterPick()
        {
            string pageId = _pickerPageId;
            ClosePicker();
            _activePageId = pageId;
            RefreshQuestionPages();

            QuestionPageModel page = _session.QuestionBook.Find(pageId);
            if (page != null && page.IsFilled && _notify != null)
            {
                _notify(string.Format("「{0}」填好了。", page.Title), true);
            }
        }

        private void Submit()
        {
            if (_session == null)
            {
                return;
            }

            QuestionPageModel page = _session.QuestionBook.Find(_activePageId);
            if (page == null || !IsFinalPage(page))
            {
                if (_notify != null)
                {
                    _notify("这一页不是结案页，结案请到「杀人案综合页」。", false);
                }

                return;
            }

            if (!page.IsFilled)
            {
                return;
            }

            bool correct = page.IsCorrect;
            if (_notify != null)
            {
                _notify(correct
                        ? "五格填满，真相吻合——破案。"
                        : "五格填满，但推理有误。再核对一下线索。",
                    correct);
            }

            if (correct)
            {
                Hide();

                if (_onSolved != null)
                {
                    _onSolved();
                }
            }
        }

        /// <summary>结案页提交（自动化/调试用，等同于点「结案」）。</summary>
        public void SubmitFinal()
        {
            Submit();
        }

        // ── 小工具 ──────────────────────────────────────────────────────────

        private bool IsFinalPage(QuestionPageModel page)
        {
            if (page == null)
            {
                return false;
            }

            string finalId = _session.Case.FinalQuestionPageId;
            if (!string.IsNullOrEmpty(finalId))
            {
                return page.Id == finalId;
            }

            IReadOnlyList<QuestionPageModel> pages = _session.QuestionBook.Pages;
            return pages.Count > 0 && page.Id == pages[pages.Count - 1].Id;
        }

        private int CountCollected(QuestionPageModel page)
        {
            QuestionPageDefinition definition = page.Definition;
            if (definition == null || definition.RequiredClueIds == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < definition.RequiredClueIds.Count; i++)
            {
                if (_session.Notebook.Contains(definition.RequiredClueIds[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private int RequiredCount(QuestionPageModel page)
        {
            QuestionPageDefinition definition = page.Definition;
            if (definition == null)
            {
                return 0;
            }

            return definition.RequiredClueIds != null ? definition.RequiredClueIds.Count : 0;
        }

        private static string Trim(string text, int limit)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Length <= limit ? text : text.Substring(0, limit) + "…";
        }
    }
}
