using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 案件板：竖排若干个问题空槽。
    /// 线索从左侧笔记本拖过来以后，这里放一条「卡片副本」；同一个槽只放一条，放新的把旧的换回线索页。
    /// 判定规则（哪条线索才配得上这个槽）这期不做，数据层已经留了 FocusQuestionData.AcceptedClues。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusNotebookCaseBoard : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _slotRoot;
        [SerializeField] private FocusNotebookSlot[] _slots;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _summaryLabel;

        private FocusNotebookPanel _notebook;
        private int _filledSnapshot = -1;

        public FocusNotebookSlot[] Slots
        {
            get
            {
                if (_slots == null || _slots.Length == 0)
                {
                    _slots = GetComponentsInChildren<FocusNotebookSlot>(true);
                }

                return _slots;
            }
        }

        public RectTransform SlotRoot => _slotRoot != null ? _slotRoot : (RectTransform)transform;

        public int FilledCount
        {
            get
            {
                int count = 0;
                FocusNotebookSlot[] slots = Slots;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].IsFilled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsComplete => Slots.Length > 0 && FilledCount == Slots.Length;

        public string SummaryText => _summaryLabel != null ? _summaryLabel.text : string.Empty;

        public FocusNotebookSlot FindSlot(string questionId)
        {
            if (string.IsNullOrEmpty(questionId))
            {
                return null;
            }

            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Question != null && slots[i].Question.QuestionId == questionId)
                {
                    return slots[i];
                }
            }

            return null;
        }

        /// <summary>槽里现在放着的线索（按槽的顺序，空槽对应 null）。</summary>
        public FocusClueData[] SlotClues()
        {
            FocusNotebookSlot[] slots = Slots;
            var clues = new FocusClueData[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                clues[i] = slots[i] != null ? slots[i].Clue : null;
            }

            return clues;
        }

        /// <summary>清空全部槽（只是清空，不会把线索还回线索页）。</summary>
        public void ClearSlots()
        {
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.SetClue(null);
            }

            RefreshSummary();
        }

        /// <summary>屏幕坐标落在哪个槽上（拖拽吸附用）；落在空处返回 null。</summary>
        public FocusNotebookSlot FindHoveredSlot(Vector2 screenPosition, Camera camera)
        {
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                FocusNotebookSlot slot = slots[i];
                if (slot != null && slot.ContainsScreenPoint(screenPosition, camera))
                {
                    return slot;
                }
            }

            return null;
        }

        /// <summary>把线索放到槽里；槽里原本有的线索会由笔记本收回线索页。</summary>
        public void Place(FocusNotebookSlot slot, FocusClueData clue)
        {
            if (slot == null)
            {
                return;
            }

            slot.SetClue(clue);
            RefreshSummary();
        }

        public FocusClueData ClearSlot(FocusNotebookSlot slot)
        {
            if (slot == null)
            {
                return null;
            }

            FocusClueData previous = slot.Clue;
            slot.SetClue(null);
            RefreshSummary();
            return previous;
        }

        public void ClearAll()
        {
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.SetClue(null);
            }

            RefreshSummary();
        }

        /// <summary>只亮一个槽（拖拽悬停在它上面时）。</summary>
        public void Highlight(FocusNotebookSlot slot)
        {
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.SetHighlight(slots[i] == slot);
            }
        }

        public void RefreshSummary()
        {
            FocusNotebookSlot[] slots = Slots;
            if (slots.Length == 0)
            {
                return;
            }

            int filled = FilledCount;
            _filledSnapshot = filled;

            if (_summaryLabel != null)
            {
                if (filled == 0)
                {
                    _summaryLabel.text = "把左边的线索拖到空槽里";
                }
                else if (filled >= slots.Length)
                {
                    _summaryLabel.text = "五个空都填上了";
                }
                else
                {
                    _summaryLabel.text = "已填 " + filled + " / " + slots.Length;
                }
            }

            if (_titleLabel != null)
            {
                FocusLabFont.Apply(_titleLabel);
            }

            if (_summaryLabel != null)
            {
                FocusLabFont.Apply(_summaryLabel);
            }
        }

        /// <summary>
        /// 翻页时把案件板做成「被登记在案」的样子：按槽里内容的变化重新播放图钉的落钉动画。
        /// </summary>
        public void ReplayPinAnimation()
        {
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].PlayDropAnimation(i * 0.05f);
                }
            }

            if (_titleLabel != null)
            {
                FocusLabFont.Apply(_titleLabel);
            }

            if (_summaryLabel != null)
            {
                FocusLabFont.Apply(_summaryLabel);
            }
        }

        /// <summary>槽里内容变了（包括被别处直接改），每帧对一下编号，变了就重算。</summary>
        private void LateUpdate()
        {
            if (Slots.Length == 0)
            {
                return;
            }

            int signature = 0;
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length && i < 17; i++)
            {
                if (slots[i] != null && slots[i].Clue != null)
                {
                    signature |= 1 << i;
                }
            }

            if (signature != _filledSnapshot)
            {
                RefreshSummary();
            }
        }

        private void Awake()
        {
            RefreshSummary();
        }

#if UNITY_EDITOR
        public void EditorBind(
            FocusNotebookPanel notebook,
            RectTransform slotRoot,
            FocusNotebookSlot[] slots,
            Text titleLabel,
            Text summaryLabel)
        {
            _notebook = notebook;
            _slotRoot = slotRoot;
            _slots = slots;
            _titleLabel = titleLabel;
            _summaryLabel = summaryLabel;
            RefreshSummary();
        }
#endif

        /// <summary>给测试/编辑器用：当前每个槽放的是哪条线索。</summary>
        public List<string> FilledClueIds()
        {
            var ids = new List<string>();
            FocusNotebookSlot[] slots = Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Clue != null)
                {
                    ids.Add(slots[i].Clue.ClueId);
                }
            }

            return ids;
        }
    }
}
