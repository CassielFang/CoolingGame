using System;
using System.Collections.Generic;
using System.Text;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一局案件的全部运行时状态：牌桌、场景、阶段、笔记本、问题页、情节 flag、时间。
    /// 这是核心机制的唯一入口，表现层（卡牌、笔记本、UI）只调用这里的 API。
    ///
    /// 设计约束：
    /// 1. 不带任何 UnityEngine 依赖 —— 可在 EditMode 测试与 CI 中直接跑。
    /// 2. 规则全部由 <see cref="CaseDefinition"/> 的数据驱动 —— 加内容不改代码。
    /// 3. 每次行动都返回 <see cref="ActionResult"/> —— UI 据此播放成功演出或「无效果」反馈。
    /// 4. 场景（在哪）、阶段（第几段）、问题页（几张板）都是数据：没有配 Stages 的案件，
    ///    行为与旧版本完全一致（单阶段、单问题页、无场景）。
    /// </summary>
    public sealed class CaseSession
    {
        private readonly Dictionary<string, CardState> _cards = new Dictionary<string, CardState>();
        private readonly List<CardState> _orderedCards = new List<CardState>();
        private readonly HashSet<string> _flags = new HashSet<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly HashSet<string> _completedStageIds = new HashSet<string>();
        private readonly HashSet<string> _completedPageIds = new HashSet<string>();

        private string _currentStageId;
        private string _currentSceneId;
        private string _activeDetectiveId;
        private bool _enteringStage;

        private readonly HashSet<string> _shownNewspapers = new HashSet<string>();
        private bool _finalPageWasFilled;

        public CaseSession(CaseDefinition definition)
        {
            Case = definition ?? throw new ArgumentNullException(nameof(definition));
            Notebook = new NotebookModel();
            QuestionBook = new QuestionBookModel();
            BuildQuestionBook();

            if (!string.IsNullOrEmpty(Case.StartSceneId))
            {
                _currentSceneId = Case.StartSceneId;
            }

            // 办案人：数据指定优先，否则用第一个
            if (Case.Detectives != null && Case.Detectives.Count > 0)
            {
                string starting = Case.StartingDetectiveId;
                if (string.IsNullOrEmpty(starting) || Case.FindDetective(starting) == null)
                {
                    starting = Case.Detectives[0] != null ? Case.Detectives[0].Id : null;
                }

                _activeDetectiveId = starting;
            }

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

            // 配了阶段表就进入第一个阶段：场景、发牌、加问题页都由数据决定。
            if (Case.Stages != null && Case.Stages.Count > 0)
            {
                EnterStage(Case.Stages[0].Id);
            }

            SyncClueRequirements();
            SyncFinalPageState();
        }

        public CaseDefinition Case { get; }

        public NotebookModel Notebook { get; }

        /// <summary>所有问题页（第一章 5 张；旧案件只有 1 张）。</summary>
        public QuestionBookModel QuestionBook { get; }

        /// <summary>默认操作的那张问题页（旧案件即唯一一页）。</summary>
        public QuestionPageModel QuestionPage
        {
            get { return QuestionBook.Primary; }
        }

        /// <summary>已消耗的时间单位。</summary>
        public int TimeSpent { get; private set; }

        /// <summary>时间预算。-1 = 不限时。</summary>
        public int TimeBudget
        {
            get { return Case.TimeBudget; }
        }

        /// <summary>当前场景 ID；无场景数据时为 null。</summary>
        public string CurrentSceneId
        {
            get { return _currentSceneId; }
        }

        /// <summary>当前办案人 ID；没有主角数据时为 null。</summary>
        public string ActiveDetectiveId
        {
            get { return _activeDetectiveId; }
        }

        public DetectiveDefinition ActiveDetective
        {
            get { return Case.FindDetective(_activeDetectiveId); }
        }

        /// <summary>切换办案人（主角是「切换」，不是同时在场）。</summary>
        public bool SetDetective(string detectiveId)
        {
            DetectiveDefinition detective = Case.FindDetective(detectiveId);
            if (detective == null)
            {
                AddWarning(string.Format("案件「{0}」引用了不存在的办案人「{1}」。", Case.Id, detectiveId));
                return false;
            }

            if (_activeDetectiveId == detectiveId)
            {
                return false;
            }

            _activeDetectiveId = detectiveId;
            DetectiveChanged?.Invoke(detective);
            return true;
        }

        public SceneDefinition CurrentScene
        {
            get { return Case.FindScene(_currentSceneId); }
        }

        /// <summary>当前阶段 ID；无阶段数据时为 null。</summary>
        public string CurrentStageId
        {
            get { return _currentStageId; }
        }

        public StageDefinition CurrentStage
        {
            get { return Case.FindStage(_currentStageId); }
        }

        /// <summary>按获得顺序排列的所有卡牌（含已消耗）。</summary>
        public IReadOnlyList<CardState> Cards
        {
            get { return _orderedCards; }
        }

        /// <summary>结案页是否已填满。</summary>
        public bool IsCaseFilled
        {
            get
            {
                QuestionPageModel final = FinalPage;
                return final != null && final.IsFilled;
            }
        }

        /// <summary>结案页是否填满且全对（= 破案）。</summary>
        public bool IsCaseSolved
        {
            get
            {
                QuestionPageModel final = FinalPage;
                return final != null && final.IsCorrect;
            }
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

        /// <summary>切换办案人时触发（带新的办案人定义）。</summary>
        public event Action<DetectiveDefinition> DetectiveChanged;

        /// <summary>场景切换（「前往」或阶段切换触发）。</summary>
        public event Action<string> SceneChanged;

        /// <summary>进入新阶段（阶段数据 + 发牌 + 问题页增删都已应用完）。</summary>
        public event Action<StageDefinition> StageEntered;

        /// <summary>该给玩家看一张报纸了（同一张只发一次）。</summary>
        public event Action<string> NewspaperTriggered;

        /// <summary>一张问题页第一次被填满（参数：页 ID、是否全对）。</summary>
        public event Action<string, bool> QuestionPageCompleted;

        /// <summary>问题页被加入/移除（阶段切换时）。</summary>
        public event Action<QuestionPageModel> QuestionPageAdded;
        public event Action<QuestionPageModel> QuestionPageRemoved;

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
                List<RecipeDefinition> recipes = FilterByScene(Case.FindRecipes(card.Definition, kinds[i]));
                if (recipes.Count == 0)
                {
                    continue;
                }

                int slotCount = 0;
                bool anyAvailable = false;
                bool anyBlocked = false;
                bool anyOtherDetective = false;

                for (int r = 0; r < recipes.Count; r++)
                {
                    RecipeDefinition recipe = recipes[r];
                    int required = recipe.RequiredContextCardIds != null ? recipe.RequiredContextCardIds.Count : 0;
                    if (required > slotCount)
                    {
                        slotCount = required;
                    }

                    if (!MatchesDetective(recipe))
                    {
                        anyOtherDetective = true;
                        continue;
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

                string reason = anyAvailable
                    ? null
                    : anyBlocked
                        ? "还缺少某些前提。"
                        : anyOtherDetective ? "这件事得换个办案人。" : "暂时用不了。";
                options.Add(new ActionOption(kinds[i], slotCount, anyAvailable, reason));
            }

            return options;
        }

        public StoryNodeDefinition GetStoryNode(string storyNodeId)
        {
            return Case.FindStoryNode(storyNodeId);
        }

        // ── 阶段 / 场景 ─────────────────────────────────────────────────────

        /// <summary>同一张报纸只发一次；数据没配或不存在就静默跳过。</summary>
        private void TriggerNewspaper(string newspaperId)
        {
            if (string.IsNullOrEmpty(newspaperId) || Case.FindNewspaper(newspaperId) == null)
            {
                return;
            }

            if (!_shownNewspapers.Add(newspaperId))
            {
                return;
            }

            NewspaperTriggered?.Invoke(newspaperId);
        }

        /// <summary>
        /// 进入一个阶段：切场景、清牌/发牌、加删问题页、置位 flag。
        /// 这是关卡分段的唯一入口，只由数据（Stages 表）和「完成条件」驱动。
        /// </summary>
        public bool EnterStage(string stageId)
        {
            StageDefinition stage = Case.FindStage(stageId);
            if (stage == null)
            {
                AddWarning(string.Format("案件「{0}」引用了不存在的阶段「{1}」。", Case.Id, stageId));
                return false;
            }

            if (_enteringStage)
            {
                return false;
            }

            _enteringStage = true;
            try
            {
                _currentStageId = stage.Id;

                if (stage.ClearCardsOnEnter)
                {
                    ClearCards();
                }

                ApplyScene(stage.SceneId, null);

                if (stage.RemoveQuestionPageIds != null)
                {
                    for (int i = 0; i < stage.RemoveQuestionPageIds.Count; i++)
                    {
                        string pageId = stage.RemoveQuestionPageIds[i];
                        if (!QuestionBook.Remove(pageId))
                        {
                            AddWarning(string.Format("阶段「{0}」要移除的问题页「{1}」不存在。", stage.Id, pageId));
                        }
                    }
                }

                if (stage.AddQuestionPageIds != null)
                {
                    for (int i = 0; i < stage.AddQuestionPageIds.Count; i++)
                    {
                        string pageId = stage.AddQuestionPageIds[i];
                        QuestionPageDefinition pageDefinition = Case.FindQuestionPage(pageId);
                        if (pageDefinition == null)
                        {
                            AddWarning(string.Format("阶段「{0}」要添加的问题页「{1}」不存在。", stage.Id, pageId));
                            continue;
                        }

                        QuestionPageModel page = QuestionBook.Add(pageDefinition);
                        if (page != null)
                        {
                            // Q05 的两个死者格是数据自动填的，玩家不用填。
                            page.AutoFill(id => Case.FindCard(id) != null);
                            QuestionPageAdded?.Invoke(page);
                        }
                    }
                }

                if (stage.StartingCardIds != null)
                {
                    for (int i = 0; i < stage.StartingCardIds.Count; i++)
                    {
                        CardState state = AddCard(stage.StartingCardIds[i], CardZone.Hand, true);
                        if (state == null)
                        {
                            continue;
                        }

                        RecordCardClue(state);
                        CardArrived?.Invoke(state);
                    }
                }

                ApplyFlags(stage.SetFlags, null);

                if (!string.IsNullOrEmpty(stage.StoryNodeId) && Case.FindStoryNode(stage.StoryNodeId) == null)
                {
                    AddWarning(string.Format("阶段「{0}」引用了不存在的情节节点「{1}」。", stage.Id, stage.StoryNodeId));
                }

                SyncClueRequirements();
                TriggerNewspaper(stage.EnterNewspaperId);
                StageEntered?.Invoke(stage);
            }
            finally
            {
                _enteringStage = false;
            }

            return true;
        }

        /// <summary>推进到表里的下一个阶段；已是最后一个阶段时返回 false。</summary>
        public bool AdvanceStage()
        {
            StageDefinition next = Case.NextStage(_currentStageId);
            return next != null && EnterStage(next.Id);
        }

        // ── 核心行动 ────────────────────────────────────────────────────────

        /// <summary>
        /// 执行一次「卡牌 + 行动 (+ 附加卡)」。这是整个游戏唯一的规则推进点。
        /// </summary>
        public ActionResult Perform(string primaryCardId, ActionKind action, IReadOnlyList<string> contextCardIds = null)
        {
            contextCardIds = contextCardIds ?? Array.Empty<string>();

            if (IsCaseFilled)
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

            // 2) 找配方：场景 + 办案人 + flag 满足 + 附加卡数量一致 + 附加卡集合完全一致
            List<RecipeDefinition> all = FilterByScene(Case.FindRecipes(primary.Definition, action));
            RecipeDefinition recipe = null;
            int bestSpecificity = int.MinValue;

            for (int i = 0; i < all.Count; i++)
            {
                RecipeDefinition candidate = all[i];
                if (!MatchesDetective(candidate))
                {
                    continue;
                }

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
                // 区分四种「无效果」，让提示更有指向性
                if (all.Count == 0)
                {
                    return Fail(primaryCardId, action, ActionFailure.NoMatchingRecipe, "这样似乎行不通。");
                }

                bool anyWrongDetective = false;
                bool anyFlagBlocked = false;
                for (int i = 0; i < all.Count; i++)
                {
                    RecipeDefinition candidate = all[i];
                    int required = candidate.RequiredContextCardIds != null ? candidate.RequiredContextCardIds.Count : 0;
                    if (required != context.Count || !MatchesContext(candidate, context))
                    {
                        continue;
                    }

                    if (!MatchesDetective(candidate))
                    {
                        anyWrongDetective = true;
                    }
                    else if (!candidate.HasAllFlags(_flags))
                    {
                        anyFlagBlocked = true;
                    }
                }

                if (anyFlagBlocked)
                {
                    return Fail(primaryCardId, action, ActionFailure.BlockedByFlags, "现在还查不到这一步，得先弄清楚别的事情。");
                }

                if (anyWrongDetective)
                {
                    return Fail(primaryCardId, action, ActionFailure.WrongDetective, "这件事得换个办案人来做。");
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
                DialogueId = recipe.DialogueId,
                NewspaperId = recipe.OutputNewspaperId,
                TimeCost = cost
            };

            if (cost > 0)
            {
                TimeSpent += cost;
                TimeChanged?.Invoke(TimeSpent);
            }

            TriggerNewspaper(recipe.OutputNewspaperId);

            ApplyFlags(recipe.SetFlags, result);

            // 场景切换是配方产物（「前往」类配方）
            ApplyScene(recipe.OutputSceneId, result);

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

            // 线索可能刚好喂饱某张问题页（Q04 的「线索 0/7」）
            RefreshPagesAfterClues();

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
            RefreshPagesAfterClues();
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

        /// <summary>把一张已知卡牌放进默认问题页的某个卡槽（旧 API，兼容单页案件）。</summary>
        public bool PlaceAnswer(QuestionSlotKind kind, string cardId)
        {
            return PlaceAnswerOnPage(QuestionPage, kind, cardId);
        }

        /// <summary>把一张已知卡牌放进指定问题页的指定卡槽（多问题页用这个）。</summary>
        public bool PlaceAnswer(string pageId, string slotId, string cardId)
        {
            QuestionPageModel page = QuestionBook.Find(pageId);
            if (page == null)
            {
                return false;
            }

            CardState card = GetCard(cardId);
            if (card == null || !card.IsUsable)
            {
                return false;
            }

            if (!page.Place(slotId, cardId))
            {
                return false;
            }

            AfterPageChanged(page);
            return true;
        }

        /// <summary>把一条已收集的线索放进指定问题页的指定卡槽（第一章用法）。</summary>
        public bool PlaceClueAnswer(string pageId, string slotId, string clueId)
        {
            QuestionPageModel page = QuestionBook.Find(pageId);
            if (page == null || string.IsNullOrEmpty(clueId))
            {
                return false;
            }

            if (!Notebook.Contains(clueId))
            {
                return false;
            }

            if (!page.PlaceClue(slotId, clueId))
            {
                return false;
            }

            AfterPageChanged(page);
            return true;
        }

        public bool ClearAnswer(QuestionSlotKind kind)
        {
            QuestionPageModel page = QuestionPage;
            if (page == null || !page.Clear(kind))
            {
                return false;
            }

            AfterPageChanged(page);
            return true;
        }

        public bool ClearAnswer(string pageId, string slotId)
        {
            QuestionPageModel page = QuestionBook.Find(pageId);
            if (page == null || !page.Clear(slotId))
            {
                return false;
            }

            AfterPageChanged(page);
            return true;
        }

        /// <summary>一次把所有问题页填满（调试/自动结算用）。</summary>
        public bool SolveAutomatically()
        {
            for (int p = 0; p < QuestionBook.Pages.Count; p++)
            {
                QuestionPageModel page = QuestionBook.Pages[p];
                IReadOnlyList<QuestionSlotState> slots = page.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    QuestionSlotDefinition definition = slots[i].Definition;
                    if (definition == null)
                    {
                        continue;
                    }

                    bool placed = false;
                    if (definition.AcceptedCardIds != null)
                    {
                        for (int c = 0; c < definition.AcceptedCardIds.Count; c++)
                        {
                            string cardId = definition.AcceptedCardIds[c];
                            if (GetCard(cardId) != null)
                            {
                                placed = page.Place(definition.SlotId, cardId);
                                break;
                            }
                        }
                    }

                    if (!placed && definition.AcceptedClueIds != null)
                    {
                        for (int c = 0; c < definition.AcceptedClueIds.Count; c++)
                        {
                            string clueId = definition.AcceptedClueIds[c];
                            if (Notebook.Contains(clueId))
                            {
                                page.PlaceClue(definition.SlotId, clueId);
                                break;
                            }
                        }
                    }
                }

                AfterPageChanged(page);
            }

            return IsCaseFilled;
        }

        // ── 内部 ────────────────────────────────────────────────────────────

        /// <summary>结案页：数据指定优先（指向还不存在的页 = 还没有结案页），否则取最后一张页。</summary>
        private QuestionPageModel FinalPage
        {
            get
            {
                if (!string.IsNullOrEmpty(Case.FinalQuestionPageId))
                {
                    return Case.HasQuestionPages ? QuestionBook.Find(Case.FinalQuestionPageId) : null;
                }

                return QuestionBook.Last;
            }
        }

        private void BuildQuestionBook()
        {
            bool hasStages = Case.Stages != null && Case.Stages.Count > 0;

            if (Case.HasQuestionPages)
            {
                // 配了阶段表时，QuestionPages 只是「页的目录」，真正上场哪些页由阶段的
                // AddQuestionPageIds / RemoveQuestionPageIds 决定（第一章 Q01–Q05 就是这样）。
                if (hasStages)
                {
                    return;
                }

                for (int i = 0; i < Case.QuestionPages.Count; i++)
                {
                    QuestionPageDefinition definition = Case.QuestionPages[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    QuestionPageModel page = QuestionBook.Add(definition);
                    if (page != null)
                    {
                        page.AutoFill(id => Case.FindCard(id) != null);
                    }
                }

                return;
            }

            // 旧案件：QuestionSlots 就是唯一一张页
            var legacyPage = new QuestionPageModel(Case.QuestionSlots);
            QuestionBook.Add(legacyPage);
        }

        private bool PlaceAnswerOnPage(QuestionPageModel page, QuestionSlotKind kind, string cardId)
        {
            if (page == null)
            {
                return false;
            }

            CardState card = GetCard(cardId);
            if (card == null || !card.IsUsable)
            {
                return false;
            }

            if (!page.Place(kind, cardId))
            {
                return false;
            }

            AfterPageChanged(page);
            return true;
        }

        private void AfterPageChanged(QuestionPageModel page)
        {
            if (page == null)
            {
                return;
            }

            if (page.IsFilled)
            {
                if (_completedPageIds.Add(page.Id))
                {
                    QuestionPageCompleted?.Invoke(page.Id, page.IsCorrect);

                    QuestionPageDefinition definition = page.Definition;
                    if (definition != null && !string.IsNullOrEmpty(definition.CompletedStoryNodeId)
                        && Case.FindStoryNode(definition.CompletedStoryNodeId) == null)
                    {
                        AddWarning(string.Format("问题页「{0}」引用了不存在的情节节点「{1}」。", page.Id, definition.CompletedStoryNodeId));
                    }
                }
            }
            else
            {
                _completedPageIds.Remove(page.Id);
            }

            SyncFinalPageState();
            CheckStageCompletion();
        }

        private void SyncFinalPageState()
        {
            QuestionPageModel final = FinalPage;
            bool filled = final != null && final.IsFilled;

            if (filled && !_finalPageWasFilled)
            {
                _finalPageWasFilled = true;
                CaseFilled?.Invoke(final.IsCorrect);
            }
            else if (!filled)
            {
                _finalPageWasFilled = false;
            }
        }

        private void CheckStageCompletion()
        {
            if (_enteringStage)
            {
                return;
            }

            StageDefinition stage = CurrentStage;
            if (stage == null || string.IsNullOrEmpty(stage.CompletedByQuestionPageId))
            {
                return;
            }

            QuestionPageModel page = QuestionBook.Find(stage.CompletedByQuestionPageId);
            if (page == null || !page.IsFilled)
            {
                return;
            }

            if (!_completedStageIds.Add(stage.Id))
            {
                return;
            }

            StageDefinition next = Case.NextStage(stage.Id);
            if (next != null)
            {
                EnterStage(next.Id);
            }
        }

        /// <summary>线索变化后，重算每张页的线索前置；收齐的页要立刻算完成。</summary>
        private void RefreshPagesAfterClues()
        {
            SyncClueRequirements();

            for (int i = 0; i < QuestionBook.Pages.Count; i++)
            {
                QuestionPageModel page = QuestionBook.Pages[i];
                if (page.IsFilled)
                {
                    AfterPageChanged(page);
                }
            }
        }

        private void SyncClueRequirements()
        {
            for (int i = 0; i < QuestionBook.Pages.Count; i++)
            {
                QuestionPageModel page = QuestionBook.Pages[i];
                QuestionPageDefinition definition = page.Definition;
                if (definition == null)
                {
                    page.SetRequiredCluesMet(true);
                    continue;
                }

                bool met = true;

                if (definition.RequiredClueCount > 0 && Notebook.Count < definition.RequiredClueCount)
                {
                    met = false;
                }

                if (met && definition.RequiredClueIds != null)
                {
                    for (int c = 0; c < definition.RequiredClueIds.Count; c++)
                    {
                        string clueId = definition.RequiredClueIds[c];
                        if (!Notebook.Contains(clueId))
                        {
                            met = false;
                            break;
                        }
                    }
                }

                page.SetRequiredCluesMet(met);
            }
        }

        private void ApplyScene(string sceneId, ActionResult result)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return;
            }

            if (Case.FindScene(sceneId) == null)
            {
                AddWarning(string.Format("案件「{0}」引用了不存在的场景「{1}」。", Case.Id, sceneId));
            }

            if (_currentSceneId == sceneId)
            {
                return;
            }

            _currentSceneId = sceneId;

            if (result != null)
            {
                result.SceneId = sceneId;
            }

            SceneChanged?.Invoke(sceneId);
        }

        private void ClearCards()
        {
            _cards.Clear();
            _orderedCards.Clear();
        }

        /// <summary>配方只在自己的场景里可用；没声明的到处可用。</summary>
        private List<RecipeDefinition> FilterByScene(List<RecipeDefinition> recipes)        {
            if (recipes == null || recipes.Count == 0)
            {
                return recipes ?? new List<RecipeDefinition>();
            }

            bool anyFiltered = false;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (!string.IsNullOrEmpty(recipes[i].RequiredSceneId))
                {
                    anyFiltered = true;
                    break;
                }
            }

            if (!anyFiltered)
            {
                return recipes;
            }

            var result = new List<RecipeDefinition>(recipes.Count);
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (string.IsNullOrEmpty(recipe.RequiredSceneId) || recipe.RequiredSceneId == _currentSceneId)
                {
                    result.Add(recipe);
                }
            }

            return result;
        }

        private ActionResult Fail(string cardId, ActionKind action, ActionFailure failure, string message)
        {
            ActionResult result = ActionResult.Fail(cardId, action, failure, message);
            ActionResolved?.Invoke(result);
            return result;
        }

        /// <summary>配方对办案人的要求：空 = 谁都能办。</summary>
        private bool MatchesDetective(RecipeDefinition recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.RequiredDetectiveId))
            {
                return true;
            }

            return recipe.RequiredDetectiveId == _activeDetectiveId;
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

                if (result != null)
                {
                    result.NewFlags.Add(flag);
                }

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
