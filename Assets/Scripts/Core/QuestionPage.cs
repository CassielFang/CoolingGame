using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>问题页的五个卡槽（策划草案「三、笔记本 / 2.问题页」）。</summary>
    public enum QuestionSlotKind
    {
        /// <summary>案件：可以是现场、受害者等。</summary>
        Case = 0,

        /// <summary>凶手：用于放置嫌疑人。</summary>
        Culprit = 1,

        /// <summary>凶器：放置凶器或作案手法。</summary>
        Weapon = 2,

        /// <summary>物证：能指正凶手的核心证据。</summary>
        PhysicalEvidence = 3,

        /// <summary>人证：指正凶手的目击者或证言。</summary>
        Testimony = 4
    }

    /// <summary>问题页卡槽的静态定义。</summary>
    [Serializable]
    public sealed class QuestionSlotDefinition
    {
        public QuestionSlotKind Kind;

        /// <summary>卡槽标题，例如 "凶手"。</summary>
        public string Label;

        /// <summary>引导文字，例如 "用于放置嫌疑人"。</summary>
        public string Hint;

        /// <summary>正确答案允许的卡牌 ID 列表（多解）。</summary>
        public List<string> AcceptedCardIds = new List<string>();

        /// <summary>是否接受任意卡牌（调试或特殊关卡用）。</summary>
        public bool AcceptsAnyCard;
    }

    /// <summary>问题页卡槽的运行时状态。</summary>
    public sealed class QuestionSlotState
    {
        internal QuestionSlotState(QuestionSlotDefinition definition)
        {
            Definition = definition;
        }

        public QuestionSlotDefinition Definition { get; }

        /// <summary>已放入的卡牌 ID，空表示未填。</summary>
        public string PlacedCardId { get; internal set; }

        public bool IsFilled
        {
            get { return !string.IsNullOrEmpty(PlacedCardId); }
        }

        public QuestionSlotKind Kind
        {
            get { return Definition.Kind; }
        }
    }

    /// <summary>单个卡槽的判定结果。</summary>
    public readonly struct SlotEvaluation
    {
        public SlotEvaluation(QuestionSlotKind kind, bool isFilled, bool isCorrect, string placedCardId)
        {
            Kind = kind;
            IsFilled = isFilled;
            IsCorrect = isCorrect;
            PlacedCardId = placedCardId;
        }

        public QuestionSlotKind Kind { get; }
        public bool IsFilled { get; }
        public bool IsCorrect { get; }
        public string PlacedCardId { get; }
    }

    /// <summary>
    /// 问题页：玩家的推理板。把五个卡槽填满即完成关卡；
    /// 填满但填错 = 推理错误结局，全对 = 破案。
    /// </summary>
    public sealed class QuestionPageModel
    {
        private readonly List<QuestionSlotState> _slots = new List<QuestionSlotState>();

        public QuestionPageModel(IEnumerable<QuestionSlotDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (QuestionSlotDefinition definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                _slots.Add(new QuestionSlotState(definition));
            }
        }

        public event Action Changed;

        public IReadOnlyList<QuestionSlotState> Slots
        {
            get { return _slots; }
        }

        public int FilledCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].IsFilled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int SlotCount
        {
            get { return _slots.Count; }
        }

        /// <summary>五个卡槽是否都已填满（= 可提交结论）。</summary>
        public bool IsFilled
        {
            get { return _slots.Count > 0 && FilledCount == _slots.Count; }
        }

        /// <summary>填满且全部正确（= 真正破案）。</summary>
        public bool IsCorrect
        {
            get
            {
                if (!IsFilled)
                {
                    return false;
                }

                for (int i = 0; i < _slots.Count; i++)
                {
                    if (!IsSlotCorrect(_slots[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public QuestionSlotState Find(QuestionSlotKind kind)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Kind == kind)
                {
                    return _slots[i];
                }
            }

            return null;
        }

        /// <summary>放入一张卡。同槽已有内容时替换。</summary>
        public bool Place(QuestionSlotKind kind, string cardId)
        {
            QuestionSlotState slot = Find(kind);
            if (slot == null || string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            if (slot.PlacedCardId == cardId)
            {
                return false;
            }

            slot.PlacedCardId = cardId;
            Changed?.Invoke();
            return true;
        }

        public bool Clear(QuestionSlotKind kind)
        {
            QuestionSlotState slot = Find(kind);
            if (slot == null || !slot.IsFilled)
            {
                return false;
            }

            slot.PlacedCardId = null;
            Changed?.Invoke();
            return true;
        }

        public void ClearAll()
        {
            bool changed = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsFilled)
                {
                    _slots[i].PlacedCardId = null;
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public bool IsSlotCorrect(QuestionSlotKind kind, string cardId)
        {
            QuestionSlotState slot = Find(kind);
            if (slot == null || string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            return IsSlotCorrect(slot, cardId);
        }

        public IReadOnlyList<SlotEvaluation> Evaluate()
        {
            var result = new List<SlotEvaluation>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
            {
                QuestionSlotState slot = _slots[i];
                result.Add(new SlotEvaluation(slot.Kind, slot.IsFilled, IsSlotCorrect(slot), slot.PlacedCardId));
            }

            return result;
        }

        private static bool IsSlotCorrect(QuestionSlotState slot)
        {
            return IsSlotCorrect(slot, slot.PlacedCardId);
        }

        private static bool IsSlotCorrect(QuestionSlotState slot, string cardId)
        {
            if (slot == null || string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            QuestionSlotDefinition definition = slot.Definition;
            if (definition.AcceptsAnyCard)
            {
                return true;
            }

            if (definition.AcceptedCardIds == null)
            {
                return false;
            }

            for (int i = 0; i < definition.AcceptedCardIds.Count; i++)
            {
                if (definition.AcceptedCardIds[i] == cardId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
