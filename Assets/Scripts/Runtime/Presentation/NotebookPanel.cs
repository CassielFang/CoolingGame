using System;
using System.Collections.Generic;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 笔记本：两个选项卡（线索页 / 问题页），线索可搜索、置顶，
    /// 问题页五个卡槽填满即结案。
    /// </summary>
    public sealed class NotebookPanel : MonoBehaviour
    {
        private sealed class SlotRow
        {
            public QuestionSlotKind Kind;
            public Text Value;
            public Button Clear;
        }

        private CaseSession _session;
        private Action<string, bool> _notify;

        private RectTransform _body;
        private RectTransform _clueBody;
        private RectTransform _questionBody;
        private RectTransform _clueList;
        private RectTransform _questionList;
        private RectTransform _picker;
        private RectTransform _pickerList;
        private Text _pickerTitle;
        private Text _status;
        private InputField _search;
        private Button _tabClues;
        private Button _tabQuestions;
        private Button _submit;
        private QuestionSlotKind? _pickerTarget;
        private string _query = string.Empty;
        private int _tabIndex;
        private bool _bodiesBuilt;
        private readonly List<SlotRow> _slotRows = new List<SlotRow>();

        public bool Visible
        {
            get { return gameObject.activeSelf; }
        }

        public static NotebookPanel Create(Transform parent, Action<string, bool> notify)
        {
            RectTransform root = UIFactory.MakePanel(parent, "Notebook", new Color(0f, 0f, 0f, 0.72f), false);
            UIFactory.Stretch(root);

            var panel = root.gameObject.AddComponent<NotebookPanel>();
            panel._notify = notify;
            panel.BuildChrome(root);
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Bind(CaseSession session)
        {
            _session = session;
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

        // ── 搭建 ────────────────────────────────────────────────────────────

        private void BuildChrome(RectTransform root)
        {
            RectTransform panel = UIFactory.MakePanel(root, "Book", Palette.Paper);
            UIFactory.Stretch(panel, 190, 190, 70, 70);

            Text title = UIFactory.MakeText(panel, "Title", "侦探笔记本", 34, Palette.Ink, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UIFactory.PlaceLeftTop(title.rectTransform, 36, 24, 480, 48);

            _tabClues = UIFactory.MakeButton(panel, "TabClues", "线索页", () => SwitchTab(0), Palette.Gold,
                Palette.Ink, 21);
            UIFactory.PlaceLeftTop(_tabClues.GetComponent<RectTransform>(), 36, 88, 170, 50);

            _tabQuestions = UIFactory.MakeButton(panel, "TabQuestions", "问题页", () => SwitchTab(1),
                Palette.PaperDark, Palette.Ink, 21);
            UIFactory.PlaceLeftTop(_tabQuestions.GetComponent<RectTransform>(), 216, 88, 170, 50);

            Button close = UIFactory.MakeButton(panel, "Close", "关闭", Hide, Palette.PaperDark, Palette.Ink, 20);
            UIFactory.PlaceRightTop(close.GetComponent<RectTransform>(), 36, 24, 130, 50);

            _status = UIFactory.MakeText(panel, "Status", string.Empty, 18,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.65f), TextAnchor.MiddleLeft);
            UIFactory.PlaceLeftBottom(_status.rectTransform, 36, 34, 900, 44);

            _submit = UIFactory.MakeButton(panel, "Submit", "结案", Submit, Palette.Red, Palette.Paper, 22);
            UIFactory.PlaceRightBottom(_submit.GetComponent<RectTransform>(), 36, 30, 220, 58);

            _body = UIFactory.MakePanel(panel, "Body", new Color(0f, 0f, 0f, 0f), false);
            RectTransform bodyRect = _body;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(36f, 96f);
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

            // 线索页：搜索框 + 滚动列表
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

            // 问题页：五行卡槽
            _questionList = _questionBody;
            var layout = _questionBody.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.padding = new RectOffset(0, 0, 0, 0);

            foreach (QuestionSlotState slot in _session.QuestionPage.Slots)
            {
                RectTransform row = UIFactory.MakePanel(_questionList, "Slot_" + slot.Kind, Palette.PaperDark);
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 96f;
                element.minHeight = 96f;

                Text label = UIFactory.MakeText(row, "Label", slot.Definition.Label, 26, Palette.Ink,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                UIFactory.PlaceLeftTop(label.rectTransform, 20, 16, 140, 36);

                Text slotHint = UIFactory.MakeText(row, "Hint", slot.Definition.Hint, 15,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.55f), TextAnchor.MiddleLeft);
                UIFactory.PlaceLeftTop(slotHint.rectTransform, 20, 54, 300, 26);

                Text value = UIFactory.MakeText(row, "Value", "— 未填 —", 22,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.45f), TextAnchor.MiddleLeft);
                UIFactory.PlaceLeftTop(value.rectTransform, 300, 28, 700, 40);

                QuestionSlotKind kind = slot.Kind;
                Button choose = UIFactory.MakeButton(row, "Choose", "选择卡牌", () => OpenPicker(kind), Palette.Gold,
                    Palette.Ink, 18);
                UIFactory.PlaceRightTop(choose.GetComponent<RectTransform>(), 186, 26, 150, 44);

                Button clear = UIFactory.MakeButton(row, "Clear", "清空", () => ClearSlot(kind), Palette.Paper,
                    Palette.Ink, 18);
                UIFactory.PlaceRightTop(clear.GetComponent<RectTransform>(), 16, 26, 160, 44);

                _slotRows.Add(new SlotRow { Kind = kind, Value = value, Clear = clear });
            }
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
            panel.sizeDelta = new Vector2(720f, 760f);

            _pickerTitle = UIFactory.MakeText(panel, "Title", "选择卡牌", 28, Palette.Ink, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UIFactory.PlaceLeftTop(_pickerTitle.rectTransform, 28, 22, 600, 44);

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
            _status.text = string.Format("线索 {0} 条 · 问题页 {1}/{2} {3}",
                _session.Notebook.Count,
                _session.QuestionPage.FilledCount,
                _session.QuestionPage.SlotCount,
                _session.Notebook.UnreadCount > 0 ? string.Format("· 新线索 {0}", _session.Notebook.UnreadCount) : string.Empty);

            _submit.interactable = _session.QuestionPage.IsFilled;

            _clueBody.gameObject.SetActive(_tabIndex == 0);
            _questionBody.gameObject.SetActive(_tabIndex == 1);

            if (_tabIndex == 0)
            {
                RefreshClueList();
            }
            else
            {
                RefreshQuestionList();
            }
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

        private void RefreshQuestionList()
        {
            for (int i = 0; i < _slotRows.Count; i++)
            {
                SlotRow row = _slotRows[i];
                QuestionSlotState state = _session.QuestionPage.Find(row.Kind);
                if (state == null)
                {
                    continue;
                }

                CardState card = state.IsFilled ? _session.GetCard(state.PlacedCardId) : null;
                row.Value.text = card != null
                    ? Palette.CategoryGlyph(card.Definition.Category) + "  " + card.Definition.DisplayName
                    : "— 未填 —";
                row.Value.color = card != null
                    ? Palette.Ink
                    : new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.45f);
                row.Clear.interactable = state.IsFilled;
            }
        }

        private void RefreshStatus()
        {
            if (_session == null)
            {
                return;
            }

            _status.text = string.Format("线索 {0} 条 · 问题页 {1}/{2}", _session.Notebook.Count,
                _session.QuestionPage.FilledCount, _session.QuestionPage.SlotCount);
        }

        // ── 交互 ────────────────────────────────────────────────────────────

        private void TogglePin(string clueId)
        {
            _session.Notebook.TogglePin(clueId);
            RefreshClueList();
        }

        private void ClearSlot(QuestionSlotKind kind)
        {
            _session.ClearAnswer(kind);
            RefreshQuestionList();
            RefreshStatus();
            _submit.interactable = _session.QuestionPage.IsFilled;
        }

        private void OpenPicker(QuestionSlotKind kind)
        {
            _pickerTarget = kind;
            QuestionSlotDefinition definition = _session.Case.FindQuestionSlot(kind);
            _pickerTitle.text = "为「" + (definition != null ? definition.Label : kind.ToString()) + "」选择一张卡";

            UIFactory.ClearChildren(_pickerList);
            foreach (CardState card in _session.GetUsableCards())
            {
                string cardId = card.Id;
                Button button = UIFactory.MakeButton(_pickerList, "Pick_" + cardId,
                    Palette.CategoryGlyph(card.Definition.Category) + "   " + card.Definition.DisplayName,
                    () => Pick(cardId), Palette.PaperDark, Palette.Ink, 20);
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 60f;
                element.minHeight = 60f;
            }

            _picker.gameObject.SetActive(true);
            _picker.SetAsLastSibling();
        }

        private void ClosePicker()
        {
            _pickerTarget = null;
            if (_picker != null)
            {
                _picker.gameObject.SetActive(false);
            }
        }

        private void Pick(string cardId)
        {
            if (!_pickerTarget.HasValue)
            {
                return;
            }

            _session.PlaceAnswer(_pickerTarget.Value, cardId);
            ClosePicker();
            RefreshQuestionList();
            RefreshStatus();
            _submit.interactable = _session.QuestionPage.IsFilled;

            if (_session.QuestionPage.IsFilled && _notify != null)
            {
                _notify("问题页已填满，可以结案了。", true);
            }
        }

        private void Submit()
        {
            if (_session == null || !_session.QuestionPage.IsFilled)
            {
                return;
            }

            bool correct = _session.IsCaseSolved;
            if (_notify != null)
            {
                _notify(correct
                        ? "五格填满，真相吻合——破案。"
                        : "五格填满，但推理有误。再核对一下线索。",
                    correct);
            }

            Hide();
        }
    }
}
