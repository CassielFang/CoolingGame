using System;
using System.Collections.Generic;
using System.Text;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一局案件的全部运行时状态：牌桌、笔记本、问题页、情节 flag、时间。
    /// 这是核心机制的唯一入口，表现层（卡牌、笔记本、UI）只调用这里的 API。
    ///
    /// 设计约束：
    /// 1. 不带任何 UnityEngine 依赖 —— 可在 EditMode 测试与 CI 中直接跑。
    /// 2. 规则全部由 <see cref="CaseDefinition"/> 的数据驱动 —— 加内容不改代码。
    /// 3. 每次行动都返回 <see cref="ActionResult"/> —— UI 据此播放成功演出或「无效果」反馈。
    /// </summary>
    public sealed class CaseSession
    {
        private readonly Dictionary<string, CardState> _cards = new Dictionary<string, CardState>();
        private readonly List<CardState> _orderedCards = new List<CardState>();
        private readonly HashSet<string> _flags = new HashSet<string>();
        private readonly List<string> _warnings = new List<string>();

        public CaseSession(CaseDefinition definition)
        {
            Case = definition ?? throw new ArgumentNullException(nameof(definition));
            Notebook = new NotebookModel();
            QuestionPage = new QuestionPageModel(Case.QuestionSlots);

            if (Case.StartingCardIds != null)
            {
                for (int i = 0; i < Case.StartingCardIds.Count; i++)
                {
                    CardState state = AddCard(Case.StartingCardIds[i], CardZone.Hand, true);
                    if (state == null)
                    {
                        continue;
                    }

                    RecordCardClue(state);
                }
            }
        }

        public CaseDefinition Case { get; }

        public NotebookModel Notebook { get; }

        public QuestionPageModel QuestionPage { get; }

        /// <summary>已消耗的时间单位。</summary>
        public int TimeSpent { get; private set; }

        /// <summary>时间预算。-1 = 不限时。</summary>
        public int TimeBudget
        {
            get { return Case.TimeBudget; }
        }

        /// <summary>按获得顺序排列的所有卡牌（含已消耗）。</summary>
        public IReadOnlyList<CardState> Cards
        {
            get { return _orderedCards; }
        }

        /// <summary>问题页是否已填满（= 可以收尾）。</summary>
        public bool IsCaseFilled
        {
            get { return QuestionPage.IsFilled; }
        }

        /// <summary>问题页是否填满且全对（= 破案）。</summary>
        public bool IsCaseSolved
        {
            get { return QuestionPage.IsCorrect; }
        }

        /// <summary>数据引用错误（配方指向不存在的卡/线索等），用于开发期发现策划表笔误。</summary>
        public IReadOnlyList<string> DataWarnings
        {
            get { return _warnings; }
        }

        public event Action<CardState> CardArrived;
        public event Action<CardState> CardRevealed;
        public event Action<ClueEntry> ClueRecorded;
        public event Action<string> FlagSet;
        public event Action<int> TimeChanged;
        public event Action<ActionResult> ActionResolved;
        public event Action<bool> CaseFilled;

        // ── 查询 ────────────────────────────────────────────────────────────

        public CardState GetCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            CardState state;
            return _cards.TryGetValue(cardId, out state) ? state : null;
        }

        public bool HasFlag(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _flags.Contains(flag);
        }

        /// <summary>当前时间里可用的手牌/桌面牌。</summary>
        public List<CardState> GetUsableCards(CardZone? zone = null)
        {
            var result = new List<CardState>();
            for (int i = 0; i < _orderedCards.Count; i++)
            {
                CardState card = _orderedCards[i];
                if (!card.IsUsable)
                {
                    continue;
                }

                if (zone.HasValue && card.Zone != zone.Value)
                {
                    continue;
                }

                result.Add(card);
            }

            return result;
        }

        /// <summary>还没翻开的新牌。</summary>
        public List<CardState> GetFaceDownCards()
        {
            var result = new List<CardState>();
            for (int i = 0; i < _orderedCards.Count; i++)
            {
                CardState card = _orderedCards[i];
                if (!card.IsFaceUp && card.Zone != CardZone.Consumed)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        /// <summary>
        /// 这张卡现在能用哪些行动，以及每个行动需要几个附加卡槽。
        /// 完全由配方表反推，卡牌本身不需要配置任何行动。
        /// </summary>
        public IReadOnlyList<ActionOption> GetActions(string cardId)
        {
            CardState card = GetCard(cardId);
            if (card == null || !card.IsUsable)
            {
                return Array.Empty<ActionOption>();
            }

            var options = new List<ActionOption>();
            ActionKind[] kinds = ActionKindText.All;
            for (int i = 0; i < kinds.Length; i++)
            {
                List<RecipeDefinition> recipes = Case.FindRecipes(card.Definition, kinds[i]);
                if (recipes.Count == 0)
                {
                    continue;
                }

                int slotCount = 0;
                bool anyAvailable = false;
                bool anyBlocked = false;

                for (int r = 0; r < recipes.Count; r++)
                {
                    RecipeDefinition recipe = recipes[r];
                    int required = recipe.RequiredContextCardIds != null ? recipe.RequiredContextCardIds.Count : 0;
                    if (required > slotCount)
                    {
                        slotCount = required;
                    }

                    if (recipe.HasAllFlags(_flags))
                    {
                        anyAvailable = true;
                    }
                    else
                    {
                        anyBlocked = true;
                    }
                }

                string reason = anyAvailable ? null : (anyBlocked ? "还缺少某些前提。" : "暂时用不了。");
                options.Add(new ActionOption(kinds[i], slotCount, anyAvailable, reason));
            }

            return options;
        }

        public StoryNodeDefinition GetStoryNode(string storyNodeId)
        {
            return Case.FindStoryNode(storyNodeId);
        }

        // ── 核心行动 ────────────────────────────────────────────────────────

        /// <summary>
        /// 执行一次「卡牌 + 行动 (+ 附加卡)」。这是整个游戏唯一的规则推进点。
        /// </summary>
        public ActionResult Perform(string primaryCardId, ActionKind action, IReadOnlyList<string> contextCardIds = null)
        {
            contextCardIds = contextCardIds ?? Array.Empty<string>();

            if (QuestionPage.IsFilled)
            {
                return Fail(primaryCardId, action, ActionFailure.CaseClosed, "案件已经有了结论，无法再行动。");
            }

            CardState primary = GetCard(primaryCardId);
            if (primary == null || !primary.IsUsable)
            {
                return Fail(primaryCardId, action, ActionFailure.UnknownCard, "这张卡现在无法使用。");
            }

            // 1) 校验附加卡
            var context = new List<CardState>(contextCardIds.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < contextCardIds.Count; i++)
            {
                string id = contextCardIds[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    return Fail(primaryCardId, action, ActionFailure.InvalidContextCard, "同一个卡槽里重复放入了同一张卡。");
                }

                if (id == primary.Id)
                {
                    return Fail(primaryCardId, action, ActionFailure.InvalidContextCard, "不能把卡牌放进它自己的卡槽。");
                }

                CardState extra = GetCard(id);
                if (extra == null || !extra.IsUsable)
                {
                    return Fail(primaryCardId, action, ActionFailure.InvalidContextCard, "有一张卡现在无法使用。");
                }

                context.Add(extra);
            }

            // 2) 找配方：flag 满足 + 附加卡数量一致 + 附加卡集合完全一致
            List<RecipeDefinition> all = Case.FindRecipes(primary.Definition, action);
            RecipeDefinition recipe = null;
            int bestSpecificity = int.MinValue;

            for (int i = 0; i < all.Count; i++)
            {
                RecipeDefinition candidate = all[i];
                int required = candidate.RequiredContextCardIds != null ? candidate.RequiredContextCardIds.Count : 0;
                if (required != context.Count)
                {
                    continue;
                }

                if (!MatchesContext(candidate, context))
                {
                    continue;
                }

                if (!candidate.HasAllFlags(_flags))
                {
                    continue;
                }

                if (recipe == null || candidate.Specificity > bestSpecificity)
                {
                    recipe = candidate;
                    bestSpecificity = candidate.Specificity;
                }
            }

            if (recipe == null)
            {
                // 区分三种「无效果」，让提示更有指向性
                if (all.Count == 0)
                {
                    return Fail(primaryCardId, action, ActionFailure.NoMatchingRecipe, "这样似乎行不通。");
                }

                for (int i = 0; i < all.Count; i++)
                {
                    RecipeDefinition candidate = all[i];
                    int required = candidate.RequiredContextCardIds != null ? candidate.RequiredContextCardIds.Count : 0;
                    if (required == context.Count && MatchesContext(candidate, context) && !candidate.HasAllFlags(_flags))
                    {
                        return Fail(primaryCardId, action, ActionFailure.BlockedByFlags, "现在还查不到这一步，得先弄清楚别的事情。");
                    }
                }

                return Fail(primaryCardId, action, ActionFailure.MissingContextCards, "这个组合还缺一张卡，或者放错了卡。");
            }

            // 3) 时间校验
            int cost = Math.Max(0, recipe.TimeCost);
            if (Case.TimeBudget >= 0 && TimeSpent + cost > Case.TimeBudget)
            {
                return Fail(primaryCardId, action, ActionFailure.NotEnoughTime, "时间来不及了。");
            }

            // 4) 结算
            var result = new ActionResult
            {
                PrimaryCardId = primaryCardId,
                Action = action,
                Success = true,
                Failure = ActionFailure.None,
                RecipeId = recipe.Id,
                StoryNodeId = recipe.StoryNodeId,
                TimeCost = cost
            };

            if (cost > 0)
            {
                TimeSpent += cost;
                TimeChanged?.Invoke(TimeSpent);
            }

            ApplyFlags(recipe.SetFlags, result);

            if (recipe.OutputClueIds != null)
            {
                for (int i = 0; i < recipe.OutputClueIds.Count; i++)
                {
                    string clueId = recipe.OutputClueIds[i];
                    ClueDefinition clue = Case.FindClue(clueId);
                    if (clue == null)
                    {
                        AddWarning(string.Format("配方「{0}」引用了不存在的线索「{1}」。", recipe.Id, clueId));
                        continue;
                    }

                    ClueEntry entry = Notebook.AddClue(clue, primary.Id, primary.Definition.DisplayName);
                    if (entry != null)
                    {
                        result.NewClues.Add(entry);
                        ClueRecorded?.Invoke(entry);
                    }
                }
            }

            if (recipe.OutputCardIds != null)
            {
                for (int i = 0; i < recipe.OutputCardIds.Count; i++)
                {
                    CardState arrived = AddCard(recipe.OutputCardIds[i], CardZone.Hand, false);
                    if (arrived == null)
                    {
                        continue;
                    }

                    result.ArrivedCards.Add(arrived);
                    CardArrived?.Invoke(arrived);
                }
            }

            // 情节节点自带的 flag 在触发时就置位，保证剧情与解锁顺序一致
            if (!string.IsNullOrEmpty(recipe.StoryNodeId))
            {
                StoryNodeDefinition node = Case.FindStoryNode(recipe.StoryNodeId);
                if (node == null)
                {
                    AddWarning(string.Format("配方「{0}」引用了不存在的情节节点「{1}」。", recipe.Id, recipe.StoryNodeId));
                }
                else
                {
                    ApplyFlags(node.SetFlagsOnPlay, result);
                }
            }

            if (recipe.ConsumeContextCards)
            {
                for (int i = 0; i < context.Count; i++)
                {
                    context[i].Zone = CardZone.Consumed;
                    result.ConsumedCardIds.Add(context[i].Id);
                }
            }

            result.Message = BuildSuccessMessage(primary, action, result);
            ActionResolved?.Invoke(result);
            return result;
        }

        /// <summary>翻开一张背面朝上的新卡；翻开时解锁该卡自带的线索。</summary>
        public bool Reveal(string cardId)
        {
            CardState card = GetCard(cardId);
            if (card == null || card.IsFaceUp || card.Zone == CardZone.Consumed)
            {
                return false;
            }

            card.IsFaceUp = true;
            card.IsNew = false;
            RecordCardClue(card);
            CardRevealed?.Invoke(card);
            return true;
        }

        /// <summary>翻开全部新牌（给「一键翻开」之类的便利入口）。</summary>
        public int RevealAllNew()
        {
            List<CardState> faceDown = GetFaceDownCards();
            int revealed = 0;
            for (int i = 0; i < faceDown.Count; i++)
            {
                if (Reveal(faceDown[i].Id))
                {
                    revealed++;
                }
            }

            return revealed;
        }

        /// <summary>把卡牌摆到桌面上（纯表现位置变化，但记在 Core 里以便存档）。</summary>
        public bool MoveCardToZone(string cardId, CardZone zone)
        {
            CardState card = GetCard(cardId);
            if (card == null || card.Zone == zone || card.Zone == CardZone.Consumed)
            {
                return false;
            }

            card.Zone = zone;
            return true;
        }

        // ── 问题页（推理板） ────────────────────────────────────────────────

        /// <summary>把一张已知卡牌放进问题页的某个卡槽。</summary>
        public bool PlaceAnswer(QuestionSlotKind kind, string cardId)
        {
            CardState card = GetCard(cardId);
            if (card == null || !card.IsUsable)
            {
                return false;
            }

            if (!QuestionPage.Place(kind, cardId))
            {
                return false;
            }

            if (QuestionPage.IsFilled)
            {
                CaseFilled?.Invoke(QuestionPage.IsCorrect);
            }

            return true;
        }

        public bool ClearAnswer(QuestionSlotKind kind)
        {
            return QuestionPage.Clear(kind);
        }

        /// <summary>一次把所有卡槽填满（调试/自动结算用）。</summary>
        public bool SolveAutomatically()
        {
            bool placedAny = false;
            for (int i = 0; i < QuestionPage.Slots.Count; i++)
            {
                QuestionSlotState slot = QuestionPage.Slots[i];
                QuestionSlotDefinition definition = slot.Definition;
                if (definition.AcceptedCardIds == null || definition.AcceptedCardIds.Count == 0)
                {
                    continue;
                }

                for (int c = 0; c < definition.AcceptedCardIds.Count; c++)
                {
                    string cardId = definition.AcceptedCardIds[c];
                    if (GetCard(cardId) != null)
                    {
                        placedAny |= QuestionPage.Place(definition.Kind, cardId);
                        break;
                    }
                }
            }

            if (QuestionPage.IsFilled)
            {
                CaseFilled?.Invoke(QuestionPage.IsCorrect);
            }

            return QuestionPage.IsFilled;
        }

        // ── 内部 ────────────────────────────────────────────────────────────

        private ActionResult Fail(string cardId, ActionKind action, ActionFailure failure, string message)
        {
            ActionResult result = ActionResult.Fail(cardId, action, failure, message);
            ActionResolved?.Invoke(result);
            return result;
        }

        private CardState AddCard(string cardId, CardZone zone, bool faceUp)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            CardState existing;
            if (_cards.TryGetValue(cardId, out existing))
            {
                return null; // 已经拥有，不重复生成
            }

            CardDefinition definition = Case.FindCard(cardId);
            if (definition == null)
            {
                AddWarning(string.Format("案件「{0}」引用了不存在的卡牌「{1}」。", Case.Id, cardId));
                return null;
            }

            var state = new CardState(definition, zone, faceUp)
            {
                ArrivalOrder = _orderedCards.Count
            };

            _cards[cardId] = state;
            _orderedCards.Add(state);
            return state;
        }

        private void RecordCardClue(CardState card)
        {
            if (card == null || string.IsNullOrEmpty(card.Definition.ClueId))
            {
                return;
            }

            ClueDefinition clue = Case.FindClue(card.Definition.ClueId);
            if (clue == null)
            {
                AddWarning(string.Format("卡牌「{0}」引用了不存在的线索「{1}」。", card.Id, card.Definition.ClueId));
                return;
            }

            ClueEntry entry = Notebook.AddClue(clue, card.Id, card.Definition.DisplayName);
            if (entry != null)
            {
                ClueRecorded?.Invoke(entry);
            }
        }

        private void ApplyFlags(List<string> flags, ActionResult result)
        {
            if (flags == null)
            {
                return;
            }

            for (int i = 0; i < flags.Count; i++)
            {
                string flag = flags[i];
                if (string.IsNullOrEmpty(flag) || !_flags.Add(flag))
                {
                    continue;
                }

                result.NewFlags.Add(flag);
                FlagSet?.Invoke(flag);
            }
        }

        private static bool MatchesContext(RecipeDefinition recipe, List<CardState> context)
        {
            if (recipe.RequiredContextCardIds == null || recipe.RequiredContextCardIds.Count == 0)
            {
                return context.Count == 0;
            }

            if (context.Count != recipe.RequiredContextCardIds.Count)
            {
                return false;
            }

            var required = new HashSet<string>(recipe.RequiredContextCardIds);
            for (int i = 0; i < context.Count; i++)
            {
                if (!required.Contains(context[i].Id))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildSuccessMessage(CardState primary, ActionKind action, ActionResult result)
        {
            var builder = new StringBuilder();
            builder.Append("对「").Append(primary.Definition.DisplayName).Append("」的").Append(action.ToChinese()).Append("有了结果。");

            if (result.NewClues.Count > 0)
            {
                builder.Append("（新线索 ").Append(result.NewClues.Count).Append(" 条）");
            }

            if (result.ArrivedCards.Count > 0)
            {
                builder.Append("（新卡牌 ").Append(result.ArrivedCards.Count).Append(" 张）");
            }

            return builder.ToString();
        }

        private void AddWarning(string message)
        {
            if (!_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }
    }
}
