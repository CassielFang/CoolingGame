using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 旧版问题页的五个固定卡槽（策划草案「三、笔记本 / 2.问题页」）。
    /// 第一章开始出现「一页四个空」的多张问题页，新数据请用 <see cref="QuestionSlotDefinition.SlotId"/>，
    /// 这里保留枚举只为兼容已有案件数据与测试。
    /// </summary>
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

    /// <summary>问题页上一个卡槽的静态定义。</summary>
    [Serializable]
    public sealed class QuestionSlotDefinition
    {
        /// <summary>
        /// 页内唯一 ID（例如 "dead"、"culprit"、"motive_revenge"）。
        /// 留空时回退到 <see cref="Kind"/> 的名称，兼容旧案件数据。
        /// </summary>
        public string Id;

        /// <summary>旧版的固定槽位类型；多问题页的数据可以一律留 Case。</summary>
        public QuestionSlotKind Kind;

        /// <summary>卡槽标题，例如 "凶手"。</summary>
        public string Label;

        /// <summary>引导文字，例如 "用于放置嫌疑人"。</summary>
        public string Hint;

        /// <summary>正确答案允许的卡牌 ID 列表（多解）。</summary>
        public List<string> AcceptedCardIds = new List<string>();

        /// <summary>
        /// 正确答案允许的**线索** ID 列表（多解）。第一章的「凶手是 C23~C27」这类格子
        /// 填的是笔记本里的线索，而不是桌上的卡牌。
        /// </summary>
        public List<string> AcceptedClueIds = new List<string>();

        /// <summary>接受任意线索（调试或特殊关卡用）。</summary>
        public bool AcceptClueAny;

        /// <summary>是否接受任意卡牌（调试或特殊关卡用）。</summary>
        public bool AcceptsAnyCard;

        /// <summary>
        /// 非空表示「进入页面时自动填入这张卡」，玩家不需要自己填。
        /// 用于 Q05「杀人案综合页」的两个死者格。
        /// </summary>
        public string AutoFilledCardId;

        /// <summary>答对后划掉这一条（Q03「仇杀？」「劫财？」）。由表现层渲染，不参与判定。</summary>
        public bool StrikeThroughWhenCorrect;

        /// <summary>用于查找的实际槽位 ID。</summary>
        public string SlotId
        {
            get { return string.IsNullOrEmpty(Id) ? Kind.ToString() : Id; }
        }
    }

    /// <summary>
    /// 一张问题页的静态定义。第一章有 5 张（Q01 珠宝店店主 / Q02 议员之子 / Q03 凶手页 /
    /// Q04 寻找嫌疑人 / Q05 综合页），会随阶段增删。
    /// </summary>
    [Serializable]
    public sealed class QuestionPageDefinition
    {
        public string Id;

        /// <summary>页标题，例如 "珠宝店店主被杀页"。</summary>
        public string Title;

        public string Description;

        /// <summary>页底图的美术资源键，可空。</summary>
        public string BackgroundArtKey;

        public List<QuestionSlotDefinition> Slots = new List<QuestionSlotDefinition>();

        /// <summary>收齐这些线索才算完成本页（可选）。</summary>
        public List<string> RequiredClueIds = new List<string>();

        /// <summary>需要收集到的线索条数（Q04「线索 0/7」）；0 表示不按数量要求。</summary>
        public int RequiredClueCount;

        /// <summary>本页完成时播放的情节节点，可空。</summary>
        public string CompletedStoryNodeId;

        /// <summary>没有卡槽、只按线索进度判定的页（Q04）。</summary>
        public bool IsProgressPage
        {
            get
            {
                bool hasSlots = Slots != null && Slots.Count > 0;
                bool hasClueGoal = RequiredClueCount > 0 || (RequiredClueIds != null && RequiredClueIds.Count > 0);
                return !hasSlots && hasClueGoal;
            }
        }
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

        /// <summary>已放入的线索 ID（与卡牌二选一）。</summary>
        public string PlacedClueId { get; internal set; }

        /// <summary>是否由数据自动填入（玩家没有操作过）。</summary>
        public bool IsAutoFilled { get; internal set; }

        public bool IsFilled
        {
            get { return !string.IsNullOrEmpty(PlacedCardId) || !string.IsNullOrEmpty(PlacedClueId); }
        }

        /// <summary>槽里放的是线索而不是卡牌。</summary>
        public bool HoldsClue
        {
            get { return !string.IsNullOrEmpty(PlacedClueId); }
        }

        public string SlotId
        {
            get { return Definition.SlotId; }
        }

        public QuestionSlotKind Kind
        {
            get { return Definition.Kind; }
        }
    }

    /// <summary>单个卡槽的判定结果。</summary>
    public readonly struct SlotEvaluation
    {
        public SlotEvaluation(QuestionSlotKind kind, bool isFilled, bool isCorrect, string placedCardId, string slotId = null, string placedClueId = null)
        {
            Kind = kind;
            IsFilled = isFilled;
            IsCorrect = isCorrect;
            PlacedCardId = placedCardId;
            SlotId = slotId;
            PlacedClueId = placedClueId;
        }

        public QuestionSlotKind Kind { get; }
        public bool IsFilled { get; }
        public bool IsCorrect { get; }
        public string PlacedCardId { get; }

        /// <summary>问题页内的槽位 ID（旧数据为 Kind 名称）。</summary>
        public string SlotId { get; }

        /// <summary>填入的线索 ID（填的是线索时非空）。</summary>
        public string PlacedClueId { get; }
    }

    /// <summary>
    /// 一张问题页：玩家的推理板。卡槽填满即完成本页；填满但填错 = 推理错误，
    /// 全对 = 本页推理成立。多张页由 <see cref="QuestionBookModel"/> 管理。
    /// </summary>
    public sealed class QuestionPageModel
    {
        /// <summary>没有 <see cref="QuestionPageDefinition"/> 的旧案件使用这个页 ID。</summary>
        public const string LegacyPageId = "main";

        private readonly List<QuestionSlotState> _slots = new List<QuestionSlotState>();
        private bool _requiredCluesMet = true;

        /// <summary>旧案件：只有一组卡槽，没有页定义。</summary>
        public QuestionPageModel(IEnumerable<QuestionSlotDefinition> definitions)
            : this(null, definitions)
        {
        }

        public QuestionPageModel(QuestionPageDefinition definition)
            : this(definition, definition != null ? definition.Slots : null)
        {
        }

        private QuestionPageModel(QuestionPageDefinition definition, IEnumerable<QuestionSlotDefinition> definitions)
        {
            Definition = definition;
            if (definitions == null)
            {
                return;
            }

            foreach (QuestionSlotDefinition slot in definitions)
            {
                if (slot == null)
                {
                    continue;
                }

                _slots.Add(new QuestionSlotState(slot));
            }
        }

        public event Action Changed;

        public QuestionPageDefinition Definition { get; }

        public string Id
        {
            get { return Definition != null && !string.IsNullOrEmpty(Definition.Id) ? Definition.Id : LegacyPageId; }
        }

        public string Title
        {
            get { return Definition != null ? Definition.Title : null; }
        }

        public IReadOnlyList<QuestionSlotState> Slots
        {
            get { return _slots; }
        }

        /// <summary>只按线索进度判定的页（没有卡槽）。</summary>
        public bool IsProgressPage
        {
            get { return Definition != null && Definition.IsProgressPage; }
        }

        /// <summary>线索收集要求是否已满足；没有要求时恒为 true。</summary>
        public bool RequiredCluesMet
        {
            get { return _requiredCluesMet; }
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

        /// <summary>本页是否完成：卡槽填满（或进度页线索达标），且线索前置满足。</summary>
        public bool IsFilled
        {
            get
            {
                if (!_requiredCluesMet)
                {
                    return false;
                }

                if (_slots.Count == 0)
                {
                    return IsProgressPage;
                }

                return FilledCount == _slots.Count;
            }
        }

        /// <summary>完成且全部正确。</summary>
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

        /// <summary>按旧版槽位类型查找（兼容旧案件）。</summary>
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

        /// <summary>按槽位 ID 查找（多问题页数据用这个）。</summary>
        public QuestionSlotState FindSlot(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return null;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].SlotId == slotId)
                {
                    return _slots[i];
                }
            }

            return null;
        }

        /// <summary>放入一张卡。同槽已有内容时替换。</summary>
        public bool Place(QuestionSlotKind kind, string cardId)
        {
            return PlaceSlot(Find(kind), cardId, false);
        }

        /// <summary>按槽位 ID 放入一张卡。</summary>
        public bool Place(string slotId, string cardId)
        {
            return PlaceSlot(FindSlot(slotId), cardId, false);
        }

        /// <summary>按槽位 ID 放入一条线索（第一章的问题页大多填线索）。</summary>
        public bool PlaceClue(string slotId, string clueId)
        {
            QuestionSlotState slot = FindSlot(slotId);
            if (slot == null || string.IsNullOrEmpty(clueId))
            {
                return false;
            }

            if (slot.PlacedClueId == clueId)
            {
                return false;
            }

            slot.PlacedClueId = clueId;
            slot.PlacedCardId = null;
            slot.IsAutoFilled = false;
            Changed?.Invoke();
            return true;
        }

        public bool Clear(QuestionSlotKind kind)
        {
            return ClearSlot(Find(kind));
        }

        public bool Clear(string slotId)
        {
            return ClearSlot(FindSlot(slotId));
        }

        public void ClearAll()
        {
            bool changed = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsFilled)
                {
                    _slots[i].PlacedCardId = null;
                    _slots[i].PlacedClueId = null;
                    _slots[i].IsAutoFilled = false;
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// 自动填入声明了 <see cref="QuestionSlotDefinition.AutoFilledCardId"/> 的卡槽。
        /// <paramref name="cardExists"/> 用于判断这张卡在当前案件里是否存在。
        /// </summary>
        public int AutoFill(Func<string, bool> cardExists)
        {
            int filled = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                QuestionSlotState slot = _slots[i];
                string cardId = slot.Definition != null ? slot.Definition.AutoFilledCardId : null;
                if (string.IsNullOrEmpty(cardId) || slot.IsFilled)
                {
                    continue;
                }

                if (cardExists != null && !cardExists(cardId))
                {
                    continue;
                }

                slot.PlacedCardId = cardId;
                slot.IsAutoFilled = true;
                filled++;
            }

            if (filled > 0)
            {
                Changed?.Invoke();
            }

            return filled;
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

        public bool IsSlotCorrect(string slotId, string cardId)
        {
            QuestionSlotState slot = FindSlot(slotId);
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
                result.Add(new SlotEvaluation(slot.Kind, slot.IsFilled, IsSlotCorrect(slot), slot.PlacedCardId, slot.SlotId, slot.PlacedClueId));
            }

            return result;
        }

        /// <summary>线索前置是否满足（由 CaseSession 维护）。</summary>
        internal void SetRequiredCluesMet(bool met)
        {
            _requiredCluesMet = met;
        }

        private bool PlaceSlot(QuestionSlotState slot, string cardId, bool autoFilled)
        {
            if (slot == null || string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            if (slot.PlacedCardId == cardId)
            {
                return false;
            }

            slot.PlacedCardId = cardId;
            slot.PlacedClueId = null;
            slot.IsAutoFilled = autoFilled;
            Changed?.Invoke();
            return true;
        }

        private bool ClearSlot(QuestionSlotState slot)
        {
            if (slot == null || !slot.IsFilled)
            {
                return false;
            }

            slot.PlacedCardId = null;
            slot.PlacedClueId = null;
            slot.IsAutoFilled = false;
            Changed?.Invoke();
            return true;
        }

        private static bool IsSlotCorrect(QuestionSlotState slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (slot.HoldsClue)
            {
                return IsClueCorrect(slot, slot.PlacedClueId);
            }

            return IsSlotCorrect(slot, slot.PlacedCardId);
        }

        private static bool IsClueCorrect(QuestionSlotState slot, string clueId)
        {
            if (slot == null || string.IsNullOrEmpty(clueId))
            {
                return false;
            }

            QuestionSlotDefinition definition = slot.Definition;
            if (definition == null)
            {
                return false;
            }

            if (definition.AcceptClueAny)
            {
                return true;
            }

            if (definition.AcceptedClueIds == null)
            {
                return false;
            }

            for (int i = 0; i < definition.AcceptedClueIds.Count; i++)
            {
                if (definition.AcceptedClueIds[i] == clueId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSlotCorrect(QuestionSlotState slot, string cardId)
        {
            if (slot == null || string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            QuestionSlotDefinition definition = slot.Definition;
            if (definition == null)
            {
                return false;
            }

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
