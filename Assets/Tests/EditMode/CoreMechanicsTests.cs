using System.Collections.Generic;
using CoolingGame.Core;
using CoolingGame.Core.Samples;
using NUnit.Framework;

namespace CoolingGame.Tests
{
    /// <summary>
    /// 核心机制（卡牌与行动 / 配方判定 / 情节门禁 / 时间）的回归测试。
    /// 这些测试不依赖场景和资源，可直接在 EditMode 下跑。
    /// </summary>
    public class CoreMechanicsTests
    {
        private static CaseSession NewSession()
        {
            return new CaseSession(SampleManorCase.Build());
        }

        /// <summary>把示例案件推进到「全部卡牌已知且已翻开」的状态。</summary>
        private static CaseSession AdvanceToFullKnowledge()
        {
            CaseSession session = NewSession();

            ActionResult rope = session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);
            Assert.IsTrue(rope.Success, rope.Message);
            Assert.IsTrue(session.Reveal(SampleManorCase.Rope));

            ActionResult testimony = session.Perform(SampleManorCase.Nanny, ActionKind.Talk);
            Assert.IsTrue(testimony.Success, testimony.Message);
            Assert.IsTrue(session.Reveal(SampleManorCase.Testimony));

            ActionResult boots = session.Perform(SampleManorCase.Garden, ActionKind.Investigate);
            Assert.IsTrue(boots.Success, boots.Message);
            Assert.IsTrue(session.Reveal(SampleManorCase.MudBoots));

            ActionResult trail = session.Perform(SampleManorCase.Corridor, ActionKind.Go);
            Assert.IsTrue(trail.Success, trail.Message);

            return session;
        }

        [Test]
        public void StartingCards_AreKnownAndUsable()
        {
            CaseSession session = NewSession();

            Assert.AreEqual(5, session.Cards.Count);
            Assert.IsNotNull(session.GetCard(SampleManorCase.Nursery));
            Assert.IsTrue(session.GetCard(SampleManorCase.Nursery).IsFaceUp);
            Assert.AreEqual(CardZone.Hand, session.GetCard(SampleManorCase.Nursery).Zone);

            // 开场卡牌没有携带线索，笔记本应当是空的
            Assert.AreEqual(0, session.Notebook.Count);
        }

        [Test]
        public void GetActions_IsDerivedFromRecipes()
        {
            CaseSession session = NewSession();

            IReadOnlyList<ActionOption> nursery = session.GetActions(SampleManorCase.Nursery);
            Assert.AreEqual(1, nursery.Count);
            Assert.AreEqual(ActionKind.Investigate, nursery[0].Action);
            Assert.AreEqual(0, nursery[0].ContextSlotCount);
            Assert.IsTrue(nursery[0].IsAvailable);

            IReadOnlyList<ActionOption> nanny = session.GetActions(SampleManorCase.Nanny);
            Assert.AreEqual(1, nanny.Count);
            Assert.AreEqual(ActionKind.Talk, nanny[0].Action);

            // 未知卡牌没有任何行动
            Assert.AreEqual(0, session.GetActions(SampleManorCase.Rope).Count);
        }

        [Test]
        public void GetActions_ReportsContextSlotsAndFlagGate()
        {
            CaseSession session = AdvanceToFullKnowledge();

            // 绳索有两条配方：单独调查（0 槽）与带靴子调查（1 槽），UI 需要显示 1 个卡槽
            IReadOnlyList<ActionOption> rope = session.GetActions(SampleManorCase.Rope);
            Assert.AreEqual(1, rope.Count);
            Assert.AreEqual(1, rope[0].ContextSlotCount);
            Assert.IsTrue(rope[0].IsAvailable);

            // 园丁在找到靴子之前不可交谈
            Assert.IsTrue(session.HasFlag(SampleManorCase.FlagFoundBoots));
            CaseSession fresh = NewSession();
            IReadOnlyList<ActionOption> gardener = fresh.GetActions(SampleManorCase.Gardener);
            Assert.AreEqual(1, gardener.Count);
            Assert.IsFalse(gardener[0].IsAvailable);
            Assert.IsNotNull(gardener[0].UnavailableReason);
        }

        [Test]
        public void Investigate_ProducesFaceDownCard_RevealUnlocksClue()
        {
            CaseSession session = NewSession();

            ActionResult result = session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("r_investigate_nursery", result.RecipeId);
            Assert.AreEqual(1, result.ArrivedCards.Count);
            Assert.AreEqual(1, result.TimeCost);
            Assert.AreEqual(1, session.TimeSpent);

            CardState rope = session.GetCard(SampleManorCase.Rope);
            Assert.IsNotNull(rope);
            Assert.IsFalse(rope.IsFaceUp, "新卡牌应当背面朝上");
            Assert.IsTrue(rope.IsNew);
            Assert.IsFalse(session.Notebook.Contains("clue_rope"), "未翻开前不应写入线索");

            Assert.IsTrue(session.Reveal(SampleManorCase.Rope));

            Assert.IsTrue(rope.IsFaceUp);
            Assert.IsFalse(rope.IsNew);
            Assert.IsTrue(session.Notebook.Contains("clue_rope"));
            Assert.IsFalse(session.Reveal(SampleManorCase.Rope), "重复翻开应返回 false");
        }

        [Test]
        public void Perform_WithoutRecipe_ReturnsNoMatchingRecipe()
        {
            CaseSession session = NewSession();

            // 保姆只有「交谈」，没有「调查」
            ActionResult result = session.Perform(SampleManorCase.Nanny, ActionKind.Investigate);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ActionFailure.NoMatchingRecipe, result.Failure);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
            Assert.AreEqual(0, session.TimeSpent, "失败的尝试不应消耗时间");
        }

        [Test]
        public void Perform_FlagGated_ReturnsBlockedByFlags()
        {
            CaseSession session = NewSession();

            ActionResult result = session.Perform(SampleManorCase.Garden, ActionKind.Investigate);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ActionFailure.BlockedByFlags, result.Failure);
        }

        [Test]
        public void Perform_ContextRequiredButOmitted_ReturnsMissingContext()
        {
            // 用一个只有「带附加卡」配方的极简案件，排除基础配方的干扰
            var definition = new CaseDefinition { Id = "t_ctx", Title = "t" };
            definition.Cards.Add(new CardDefinition("a", "A", CardCategory.Item));
            definition.Cards.Add(new CardDefinition("b", "B", CardCategory.Item));
            definition.Cards.Add(new CardDefinition("c", "C", CardCategory.Item));
            definition.StartingCardIds.AddRange(new[] { "a", "b" });
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_combo",
                Action = ActionKind.Investigate,
                PrimaryCardId = "a",
                RequiredContextCardIds = new List<string> { "b" },
                OutputCardIds = new List<string> { "c" }
            });

            var session = new CaseSession(definition);

            ActionResult missing = session.Perform("a", ActionKind.Investigate);
            Assert.AreEqual(ActionFailure.MissingContextCards, missing.Failure);

            ActionResult ok = session.Perform("a", ActionKind.Investigate, new[] { "b" });
            Assert.IsTrue(ok.Success);
            Assert.AreEqual(1, ok.ArrivedCards.Count);
        }

        [Test]
        public void Perform_WithWrongContext_ReturnsMissingContext()
        {
            CaseSession session = AdvanceToFullKnowledge();

            // 绳索 + 保姆（放错卡）→ 没有匹配配方
            ActionResult result = session.Perform(
                SampleManorCase.Rope,
                ActionKind.Investigate,
                new[] { SampleManorCase.Nanny });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ActionFailure.MissingContextCards, result.Failure);
        }

        [Test]
        public void Perform_InvalidContext_IsRejected()
        {
            CaseSession session = NewSession();
            session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);

            // 背面朝上的卡不能当附加卡
            ActionResult faceDown = session.Perform(
                SampleManorCase.Nursery,
                ActionKind.Investigate,
                new[] { SampleManorCase.Rope });
            Assert.AreEqual(ActionFailure.InvalidContextCard, faceDown.Failure);

            session.Reveal(SampleManorCase.Rope);

            // 自己不能进自己的卡槽
            ActionResult self = session.Perform(
                SampleManorCase.Rope,
                ActionKind.Investigate,
                new[] { SampleManorCase.Rope });
            Assert.AreEqual(ActionFailure.InvalidContextCard, self.Failure);

            // 同一个卡槽重复放同一张卡
            ActionResult duplicate = session.Perform(
                SampleManorCase.Rope,
                ActionKind.Investigate,
                new[] { SampleManorCase.Nanny, SampleManorCase.Nanny });
            Assert.AreEqual(ActionFailure.InvalidContextCard, duplicate.Failure);
        }

        [Test]
        public void Perform_PicksMostSpecificRecipe()
        {
            CaseSession session = AdvanceToFullKnowledge();

            // 只观察绳索 → 低精度配方
            ActionResult alone = session.Perform(SampleManorCase.Rope, ActionKind.Investigate);
            Assert.IsTrue(alone.Success);
            Assert.AreEqual("r_rope_alone", alone.RecipeId);
            Assert.IsTrue(session.Notebook.Contains("clue_damp"));

            // 绳索 + 靴子 → 高精度配方
            CaseSession withBoots = AdvanceToFullKnowledge();
            ActionResult combo = withBoots.Perform(
                SampleManorCase.Rope,
                ActionKind.Investigate,
                new[] { SampleManorCase.MudBoots });

            Assert.IsTrue(combo.Success);
            Assert.AreEqual("r_rope_with_boots", combo.RecipeId);
            Assert.IsTrue(withBoots.Notebook.Contains("clue_soil"));
        }

        [Test]
        public void Perform_StoryNodeAndFlags_AreTriggered()
        {
            CaseSession session = AdvanceToFullKnowledge();

            ActionResult result = session.Perform(SampleManorCase.Gardener, ActionKind.Talk);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("story_confession", result.StoryNodeId);
            Assert.IsNotNull(session.GetStoryNode("story_confession"), "情节节点应当可以在案件数据里查到");

            Assert.IsTrue(session.HasFlag("confession_heard"), "情节节点自带的 flag 应当置位");
            Assert.IsTrue(session.Notebook.Contains("clue_confession"));
        }

        [Test]
        public void TimeBudget_BlocksActionsWhenExhausted()
        {
            CaseDefinition definition = SampleManorCase.Build();
            definition.TimeBudget = 1;
            var session = new CaseSession(definition);

            ActionResult first = session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);
            Assert.IsTrue(first.Success);
            Assert.AreEqual(1, session.TimeSpent);

            ActionResult second = session.Perform(SampleManorCase.Nanny, ActionKind.Talk);
            Assert.AreEqual(ActionFailure.NotEnoughTime, second.Failure);
            Assert.AreEqual(1, session.TimeSpent);
        }

        [Test]
        public void TimeBudget_DisabledByDefault()
        {
            CaseSession session = AdvanceToFullKnowledge();
            Assert.AreEqual(-1, session.TimeBudget);
            Assert.GreaterOrEqual(session.TimeSpent, 3);
        }

        [Test]
        public void CaseClosed_BlocksFurtherActions()
        {
            CaseSession session = AdvanceToFullKnowledge();

            Assert.IsTrue(session.SolveAutomatically());
            Assert.IsTrue(session.IsCaseFilled);
            Assert.IsTrue(session.IsCaseSolved);

            ActionResult afterClose = session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);
            Assert.AreEqual(ActionFailure.CaseClosed, afterClose.Failure);
        }

        [Test]
        public void Events_ReportArrivalAndClues()
        {
            CaseSession session = NewSession();

            var arrived = new List<string>();
            var clues = new List<string>();
            var flags = new List<string>();

            session.CardArrived += card => arrived.Add(card.Id);
            session.ClueRecorded += clue => clues.Add(clue.ClueId);
            session.FlagSet += flag => flags.Add(flag);

            session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);
            Assert.AreEqual(new[] { SampleManorCase.Rope }, arrived.ToArray());

            session.Reveal(SampleManorCase.Rope);
            Assert.AreEqual(new[] { "clue_rope" }, clues.ToArray());

            session.Perform(SampleManorCase.Nanny, ActionKind.Talk);
            Assert.AreEqual(new[] { SampleManorCase.FlagHeardNanny }, flags.ToArray());
        }

        [Test]
        public void SampleCase_HasNoDataWarnings()
        {
            CaseSession session = AdvanceToFullKnowledge();
            session.Perform(SampleManorCase.Gardener, ActionKind.Talk);

            Assert.AreEqual(0, session.DataWarnings.Count, string.Join("\n", session.DataWarnings));
        }

        [Test]
        public void MoveCardToZone_TracksBoardPlacement()
        {
            CaseSession session = NewSession();

            Assert.IsTrue(session.MoveCardToZone(SampleManorCase.Nursery, CardZone.Board));
            Assert.AreEqual(CardZone.Board, session.GetCard(SampleManorCase.Nursery).Zone);
            Assert.IsFalse(session.MoveCardToZone(SampleManorCase.Nursery, CardZone.Board));
        }
    }
}
