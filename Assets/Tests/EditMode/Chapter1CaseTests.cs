using System.Collections.Generic;
using CoolingGame.Core;
using CoolingGame.Core.Samples;
using NUnit.Framework;

namespace CoolingGame.Tests
{
    /// <summary>
    /// 第一章数据的回归测试。除了「跑通一条完整推理线」，还做全量引用校验——
    /// 策划改设定集、重新导数据后，这些测试会先一步发现笔误和软锁。
    /// 主角（办案人）是状态，不是卡牌：测里用 SetDetective 切人。
    /// </summary>
    public class Chapter1CaseTests
    {
        private static CaseSession NewSession()
        {
            return new CaseSession(Chapter1Case.Build());
        }

        private static void Do(CaseSession session, string cardId, ActionKind action, params string[] context)
        {
            ActionResult result = session.Perform(cardId, action, context);
            Assert.IsTrue(result.Success,
                string.Format("{0} / {1} 失败：{2}（{3}）", cardId, action, result.Failure, result.Message));
        }

        private static void Reveal(CaseSession session, params string[] cardIds)
        {
            for (int i = 0; i < cardIds.Length; i++)
            {
                Assert.IsTrue(session.Reveal(cardIds[i]), "应当可以翻开：" + cardIds[i]);
            }
        }

        private static void As(CaseSession session, string detectiveId)
        {
            if (session.ActiveDetectiveId != detectiveId)
            {
                Assert.IsTrue(session.SetDetective(detectiveId), "应当能切换到办案人：" + detectiveId);
            }
        }

        [Test]
        public void Build_HasNoDataWarnings()
        {
            CaseSession session = NewSession();
            Assert.IsEmpty(session.DataWarnings, "第一章数据不应有引用错误：" + string.Join(" | ", session.DataWarnings));
        }

        [Test]
        public void AllReferencesResolve()
        {
            CaseDefinition definition = Chapter1Case.Build();

            foreach (RecipeDefinition recipe in definition.Recipes)
            {
                Assert.IsNotNull(definition.FindCard(recipe.PrimaryCardId), "配方 " + recipe.Id + " 的主卡不存在");

                foreach (string contextId in recipe.RequiredContextCardIds)
                {
                    Assert.IsNotNull(definition.FindCard(contextId), "配方 " + recipe.Id + " 的附加卡不存在：" + contextId);
                }

                foreach (string cardId in recipe.OutputCardIds)
                {
                    Assert.IsNotNull(definition.FindCard(cardId), "配方 " + recipe.Id + " 产出的卡不存在：" + cardId);
                }

                foreach (string clueId in recipe.OutputClueIds)
                {
                    Assert.IsNotNull(definition.FindClue(clueId), "配方 " + recipe.Id + " 产出的线索不存在：" + clueId);
                }

                if (!string.IsNullOrEmpty(recipe.RequiredSceneId))
                {
                    Assert.IsNotNull(definition.FindScene(recipe.RequiredSceneId), "配方 " + recipe.Id + " 的场景不存在");
                }

                if (!string.IsNullOrEmpty(recipe.OutputSceneId))
                {
                    Assert.IsNotNull(definition.FindScene(recipe.OutputSceneId), "配方 " + recipe.Id + " 的产出场景不存在");
                }

                if (!string.IsNullOrEmpty(recipe.RequiredDetectiveId))
                {
                    Assert.IsNotNull(definition.FindDetective(recipe.RequiredDetectiveId),
                        "配方 " + recipe.Id + " 要求的办案人不存在：" + recipe.RequiredDetectiveId);
                }
            }

            foreach (QuestionPageDefinition page in definition.QuestionPages)
            {
                foreach (QuestionSlotDefinition slot in page.Slots)
                {
                    foreach (string cardId in slot.AcceptedCardIds)
                    {
                        Assert.IsNotNull(definition.FindCard(cardId), page.Id + "/" + slot.Id + " 的答案卡不存在：" + cardId);
                    }

                    foreach (string clueId in slot.AcceptedClueIds)
                    {
                        Assert.IsNotNull(definition.FindClue(clueId), page.Id + "/" + slot.Id + " 的答案线索不存在：" + clueId);
                    }

                    if (!string.IsNullOrEmpty(slot.AutoFilledCardId))
                    {
                        Assert.IsNotNull(definition.FindCard(slot.AutoFilledCardId), page.Id + "/" + slot.Id + " 的自动填入卡不存在");
                    }
                }

                foreach (string clueId in page.RequiredClueIds)
                {
                    Assert.IsNotNull(definition.FindClue(clueId), page.Id + " 的前置线索不存在：" + clueId);
                }
            }

            foreach (StageDefinition stage in definition.Stages)
            {
                Assert.IsNotNull(definition.FindScene(stage.SceneId), "阶段 " + stage.Id + " 的场景不存在");

                foreach (string cardId in stage.StartingCardIds)
                {
                    Assert.IsNotNull(definition.FindCard(cardId), "阶段 " + stage.Id + " 发的卡不存在：" + cardId);
                }

                foreach (string pageId in stage.AddQuestionPageIds)
                {
                    Assert.IsNotNull(definition.FindQuestionPage(pageId), "阶段 " + stage.Id + " 要加的问题页不存在：" + pageId);
                }

                foreach (string pageId in stage.RemoveQuestionPageIds)
                {
                    Assert.IsNotNull(definition.FindQuestionPage(pageId), "阶段 " + stage.Id + " 要移除的问题页不存在：" + pageId);
                }

                if (!string.IsNullOrEmpty(stage.CompletedByQuestionPageId))
                {
                    Assert.IsNotNull(definition.FindQuestionPage(stage.CompletedByQuestionPageId),
                        "阶段 " + stage.Id + " 的完成页不存在");
                }
            }
        }

        [Test]
        public void EveryClueHasAProducer()
        {
            CaseDefinition definition = Chapter1Case.Build();
            var produced = new HashSet<string>();

            foreach (RecipeDefinition recipe in definition.Recipes)
            {
                foreach (string clueId in recipe.OutputClueIds)
                {
                    produced.Add(clueId);
                }
            }

            foreach (ClueDefinition clue in definition.Clues)
            {
                Assert.IsTrue(produced.Contains(clue.Id), "线索永远拿不到（没有配方产出）：" + clue.Id);
            }
        }

        [Test]
        public void Detectives_AreNotCards()
        {
            CaseDefinition definition = Chapter1Case.Build();

            Assert.IsNotNull(definition.FindDetective(Chapter1Case.Morrison));
            Assert.IsNotNull(definition.FindDetective(Chapter1Case.Shirley));

            // 主角绝不能出现在卡牌表里
            Assert.IsNull(definition.FindCard(Chapter1Case.Morrison), "莫里森不该是一张卡");
            Assert.IsNull(definition.FindCard(Chapter1Case.Shirley), "雪莉不该是一张卡");
        }

        [Test]
        public void FirstStage_StartsWithArchiveOnly_AndMorrisonOnDuty()
        {
            CaseSession session = NewSession();

            Assert.AreEqual(Chapter1Case.Stage1, session.CurrentStageId);
            Assert.AreEqual(Chapter1Case.ScenePolice, session.CurrentSceneId);
            Assert.AreEqual(Chapter1Case.Morrison, session.ActiveDetectiveId, "开局应当是莫里森在办案");

            Assert.AreEqual(1, session.Cards.Count, "开局桌上只有案件档案，主角不在桌上");
            Assert.IsNotNull(session.GetCard("I00"));
            Assert.IsNotNull(session.QuestionBook.Find(Chapter1Case.PageQ01));
            Assert.IsNull(session.QuestionBook.Find(Chapter1Case.PageQ05));
        }

        [Test]
        public void SwitchingDetective_ChangesAvailableRecipes()
        {
            CaseSession session = NewSession();

            // 拿到俱乐部地点卡
            Do(session, "I00", ActionKind.Investigate);
            Reveal(session, "I01");
            Do(session, "I01", ActionKind.Investigate);
            Reveal(session, "I04");
            Do(session, "I04", ActionKind.Investigate);
            Reveal(session, "S02");

            // 莫里森能去俱乐部
            As(session, Chapter1Case.Morrison);
            Assert.IsTrue(HasAvailableAction(session, "S02", ActionKind.Go));

            // 换成雪莉：这条「去俱乐部」要莫里森，雪莉做不了
            As(session, Chapter1Case.Shirley);
            ActionResult wrong = session.Perform("S02", ActionKind.Go);
            Assert.IsFalse(wrong.Success);
            Assert.AreEqual(ActionFailure.WrongDetective, wrong.Failure, "换人后应当提示需要换办案人");
        }

        [Test]
        public void FullPlaythrough_SolvesTheCase()
        {
            CaseSession session = NewSession();

            // ── 第一阶段：警察局翻档案（莫里森） ────────────────────────
            As(session, Chapter1Case.Morrison);

            Do(session, "I00", ActionKind.Investigate);
            Reveal(session, "I01", "I02");

            Do(session, "I01", ActionKind.Investigate);
            Reveal(session, "I03", "I04", "I05");

            Do(session, "I02", ActionKind.Investigate);
            Reveal(session, "I06", "I07", "I08");

            Do(session, "I03", ActionKind.Investigate);          // C01-C03
            Do(session, "I05", ActionKind.Investigate);          // C07,C08（莫里森）
            Do(session, "I08", ActionKind.Investigate);          // C15,C16

            Do(session, "I04", ActionKind.Investigate);          // 俱乐部 + C04-C06
            Reveal(session, "S02");
            Assert.IsTrue(session.PlaceClueAnswer(Chapter1Case.PageQ03, "motive_robbery", "C04"));
            Assert.IsTrue(session.PlaceClueAnswer(Chapter1Case.PageQ03, "motive_revenge", "C05"));

            Do(session, "I06", ActionKind.Investigate);          // 血脚印 + C09-C11
            Reveal(session, "I09");
            Do(session, "I09", ActionKind.Investigate);          // C17（莫里森）
            Assert.IsTrue(session.PlaceClueAnswer(Chapter1Case.PageQ03, "profile", "C17"));

            Assert.AreEqual(Chapter1Case.Stage2, session.CurrentStageId);
            Assert.AreEqual(Chapter1Case.SceneChurch, session.CurrentSceneId);
            Assert.IsNull(session.QuestionBook.Find(Chapter1Case.PageQ05), "Q05 要到第三阶段才出现");

            // ── 第二阶段：教堂，两个办案人分头问 ────────────────────────
            Do(session, Chapter1Case.SceneChurch, ActionKind.Go);   // 莫里森进教堂
            Reveal(session, "P01", "P02");

            Do(session, "P01", ActionKind.Talk);                    // 莫里森：I11 + C23,C24
            Reveal(session, "I11");

            As(session, Chapter1Case.Shirley);
            Do(session, "P01", ActionKind.Talk, "I10");             // 雪莉：C25
            Do(session, "P02", ActionKind.Talk, "I10");             // 雪莉：C29

            As(session, Chapter1Case.Morrison);
            Do(session, "P02", ActionKind.Talk);                    // 莫里森：C26,C27,C28

            // Q04 收齐 7 条线索 → 自动进入第三阶段
            Assert.AreEqual(Chapter1Case.Stage3, session.CurrentStageId);
            Assert.AreEqual(Chapter1Case.ScenePolice, session.CurrentSceneId);
            Assert.IsNotNull(session.QuestionBook.Find(Chapter1Case.PageQ05));
            Assert.IsNull(session.QuestionBook.Find(Chapter1Case.PageQ01), "第一阶段的问题页应当被移除");

            // ── 第三阶段：警局结案 ──────────────────────────────────────
            Do(session, "P08", ActionKind.Talk);                    // 莫里森：I00 + C31
            Do(session, "I00", ActionKind.Investigate);             // 莫里森：C32

            As(session, Chapter1Case.Shirley);
            Do(session, "I00", ActionKind.Investigate, "I11");      // 雪莉：C34

            Assert.IsTrue(session.PlaceClueAnswer(Chapter1Case.PageQ05, "culprit", "C23"));
            Assert.IsTrue(session.PlaceClueAnswer(Chapter1Case.PageQ05, "weapon", "C28"));
            Assert.IsTrue(session.PlaceClueAnswer(Chapter1Case.PageQ05, "evidence", "C34"));

            Assert.IsTrue(session.IsCaseSolved, "按设定集的证据链应当能破案");
        }

        [Test]
        public void GoRecipe_SwitchesScene_AndProducesReturnCard()
        {
            CaseSession session = NewSession();
            As(session, Chapter1Case.Morrison);

            Do(session, "I00", ActionKind.Investigate);
            Reveal(session, "I01");
            Do(session, "I01", ActionKind.Investigate);
            Reveal(session, "I04");
            Do(session, "I04", ActionKind.Investigate);
            Reveal(session, "S02");

            Assert.AreEqual(Chapter1Case.ScenePolice, session.CurrentSceneId);

            ActionResult go = session.Perform("S02", ActionKind.Go);
            Assert.IsTrue(go.Success, go.Message);
            Assert.AreEqual(Chapter1Case.SceneClub, session.CurrentSceneId);
            Assert.AreEqual(Chapter1Case.SceneClub, go.SceneId);
            Assert.IsNotNull(session.GetCard("P06"), "俱乐部里应当出现舞女");

            Reveal(session, "P06", Chapter1Case.ScenePolice);
            ActionResult back = session.Perform(Chapter1Case.ScenePolice, ActionKind.Go);
            Assert.IsTrue(back.Success, back.Message);
            Assert.AreEqual(Chapter1Case.ScenePolice, session.CurrentSceneId);
        }

        [Test]
        public void QuestionPage_RejectsClueNotInNotebook()
        {
            CaseSession session = NewSession();

            // C17 还没拿到，不能填进 Q03
            Assert.IsFalse(session.PlaceClueAnswer(Chapter1Case.PageQ03, "profile", "C17"));
            Assert.IsFalse(session.QuestionBook.Find(Chapter1Case.PageQ03).FindSlot("profile").IsFilled);
        }

        [Test]
        public void AutoFilledVictimSlots_CountAsFilled()
        {
            CaseSession session = NewSession();

            QuestionPageModel page = session.QuestionBook.Find(Chapter1Case.PageQ01);
            Assert.IsTrue(page.FindSlot("victim").IsFilled);
            Assert.IsTrue(page.FindSlot("victim").IsAutoFilled);
            Assert.IsFalse(page.FindSlot("culprit").IsFilled);
        }

        private static bool HasAvailableAction(CaseSession session, string cardId, ActionKind action)
        {
            IReadOnlyList<ActionOption> options = session.GetActions(cardId);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Action == action && options[i].IsAvailable)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
