using System.Collections.Generic;
using CoolingGame.Core;
using NUnit.Framework;

namespace CoolingGame.Tests
{
    /// <summary>
    /// 第一章新结构的回归测试：场景（Scenes）、阶段（Stages）、多问题页（QuestionPages）。
    /// 对应《第一章游戏元素设定集》里的「关卡一第一阶段 / 第二阶段」「Q01–Q05」。
    /// 数据格式与用法见 README「六、场景与阶段」。
    /// </summary>
    public class StageSceneAndPageTests
    {
        private const string CardCaseFile = "I00";
        private const string CardFirstDoc = "I01";
        private const string CardSecondDoc = "I02";
        private const string CardCulprit = "P01";
        private const string CardEvidence = "I12";
        private const string CardFootprint = "I09";
        private const string CardVictim1 = "P04";
        private const string CardVictim2 = "P05";

        private const string SceneHub = "S00";
        private const string SceneStreet = "S01";

        private const string StageOne = "stage1";
        private const string StageTwo = "stage2";

        private const string PageFirst = "Q01";
        private const string PageFinal = "Q05";

        private const string ClueTalk = "C23";

        /// <summary>
        /// 最小两阶段案件：
        /// 阶段一在警察局，调查档案 →（问文件）→ 拿到凶手 → 填完 Q01 → 自动进入阶段二；
        /// 阶段二清空卡牌、换到街道、移除 Q01、加 Q05（一个自动填入的死者和一个要填的证据格）。
        /// </summary>
        private static CaseDefinition BuildCase()
        {
            var definition = new CaseDefinition
            {
                Id = "chapter1_min",
                Title = "第一章最小结构",
                StartSceneId = SceneHub,
                FinalQuestionPageId = PageFinal
            };

            definition.Scenes.Add(new SceneDefinition { Id = SceneHub, DisplayName = "警察局" });
            definition.Scenes.Add(new SceneDefinition { Id = SceneStreet, DisplayName = "砖瓦巷街道" });

            definition.Cards.Add(new CardDefinition(CardCaseFile, "案件档案", CardCategory.Item));
            definition.Cards.Add(new CardDefinition(CardFirstDoc, "死者文件一", CardCategory.Item));
            definition.Cards.Add(new CardDefinition(CardSecondDoc, "死者文件二", CardCategory.Item));
            definition.Cards.Add(new CardDefinition(CardCulprit, "塞德里克", CardCategory.Person));
            definition.Cards.Add(new CardDefinition(CardEvidence, "画师的短刀", CardCategory.Item));
            definition.Cards.Add(new CardDefinition(CardFootprint, "血脚印拓印", CardCategory.Item));
            definition.Cards.Add(new CardDefinition(CardVictim1, "珠宝店店主", CardCategory.Person));
            definition.Cards.Add(new CardDefinition(CardVictim2, "卡林顿", CardCategory.Person));

            definition.Clues.Add(new ClueDefinition(ClueTalk, "塞德里克是东区教堂的画师"));
            definition.Clues.Add(new ClueDefinition("C01", "尸体仰面倒在血泊里"));

            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_investigate_hub",
                Action = ActionKind.Investigate,
                PrimaryCardId = CardCaseFile,
                RequiredSceneId = SceneHub,
                OutputCardIds = new List<string> { CardFirstDoc, CardSecondDoc }
            });

            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_talk_doc",
                Action = ActionKind.Talk,
                PrimaryCardId = CardFirstDoc,
                OutputCardIds = new List<string> { CardCulprit },
                OutputClueIds = new List<string> { ClueTalk }
            });

            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_go_street",
                Action = ActionKind.Go,
                PrimaryCardId = CardCaseFile,
                OutputCardIds = new List<string> { CardFootprint },
                OutputSceneId = SceneStreet
            });

            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_investigate_street",
                Action = ActionKind.Investigate,
                PrimaryCardId = CardFootprint,
                RequiredSceneId = SceneStreet,
                OutputClueIds = new List<string> { "C01" }
            });

            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageFirst,
                Title = "珠宝店店主被杀页",
                Slots = new List<QuestionSlotDefinition>
                {
                    new QuestionSlotDefinition
                    {
                        Id = "culprit",
                        Label = "凶手",
                        AcceptedCardIds = new List<string> { CardCulprit }
                    }
                }
            });

            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageFinal,
                Title = "杀人案综合页",
                Slots = new List<QuestionSlotDefinition>
                {
                    new QuestionSlotDefinition
                    {
                        Id = "victim1",
                        Label = "死者",
                        AutoFilledCardId = CardVictim1,
                        AcceptedCardIds = new List<string> { CardVictim1 }
                    },
                    new QuestionSlotDefinition
                    {
                        Id = "evidence",
                        Label = "证据",
                        AcceptedCardIds = new List<string> { CardEvidence }
                    }
                }
            });

            definition.Stages.Add(new StageDefinition
            {
                Id = StageOne,
                DisplayName = "第一阶段",
                SceneId = SceneHub,
                StartingCardIds = new List<string> { CardCaseFile },
                AddQuestionPageIds = new List<string> { PageFirst },
                CompletedByQuestionPageId = PageFirst
            });

            definition.Stages.Add(new StageDefinition
            {
                Id = StageTwo,
                DisplayName = "第二阶段",
                SceneId = SceneStreet,
                ClearCardsOnEnter = true,
                StartingCardIds = new List<string> { CardCulprit, CardEvidence, CardVictim1, CardVictim2 },
                RemoveQuestionPageIds = new List<string> { PageFirst },
                AddQuestionPageIds = new List<string> { PageFinal }
            });

            return definition;
        }

        private static CaseSession NewSession()
        {
            return new CaseSession(BuildCase());
        }

        /// <summary>把阶段一推到「凶手已到手」——完成 Q01 的前置。</summary>
        private static void AdvanceToSuspect(CaseSession session)
        {
            ActionResult investigate = session.Perform(CardCaseFile, ActionKind.Investigate);
            Assert.IsTrue(investigate.Success, investigate.Message);
            Assert.IsTrue(session.Reveal(CardFirstDoc));

            ActionResult talk = session.Perform(CardFirstDoc, ActionKind.Talk);
            Assert.IsTrue(talk.Success, talk.Message);
            Assert.IsTrue(session.Reveal(CardCulprit));
        }

        [Test]
        public void FirstStage_AppliesSceneCardsAndPages()
        {
            CaseSession session = NewSession();

            Assert.AreEqual(StageOne, session.CurrentStageId);
            Assert.AreEqual(SceneHub, session.CurrentSceneId);
            Assert.AreEqual("警察局", session.CurrentScene.DisplayName);

            Assert.IsNotNull(session.GetCard(CardCaseFile), "阶段发牌应当到手");
            Assert.IsNull(session.GetCard(CardCulprit), "第二阶段的牌此时还不该出现");

            Assert.AreEqual(1, session.QuestionBook.Count);
            Assert.IsNotNull(session.QuestionBook.Find(PageFirst));
            Assert.IsNull(session.QuestionBook.Find(PageFinal));
        }

        [Test]
        public void Recipe_CanSwitchScene_AndReportsIt()
        {
            CaseSession session = NewSession();

            string reported = null;
            session.SceneChanged += sceneId => reported = sceneId;

            ActionResult result = session.Perform(CardCaseFile, ActionKind.Go);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(SceneStreet, result.SceneId, "配方应当把场景切换报给 UI");
            Assert.AreEqual(SceneStreet, session.CurrentSceneId);
            Assert.AreEqual(SceneStreet, reported);
            Assert.IsNotNull(session.GetCard(CardFootprint));
        }

        [Test]
        public void Recipe_WithRequiredScene_OnlyRunsInThatScene()
        {
            CaseSession session = NewSession();

            // 在警察局时，血脚印拓印的场景配方不可用：先出门拿卡并翻牌
            session.Perform(CardCaseFile, ActionKind.Go);
            Assert.AreEqual(SceneStreet, session.CurrentSceneId);
            Assert.IsTrue(session.Reveal(CardFootprint));

            ActionResult inStreet = session.Perform(CardFootprint, ActionKind.Investigate);
            Assert.IsTrue(inStreet.Success, inStreet.Message);
            Assert.AreEqual(1, session.Notebook.Count);

            // 同样的卡在「警察局」场景下不可用：用一个只有街道配方的独立案件验证
            CaseDefinition hubCase = BuildCase();
            hubCase.StartSceneId = SceneHub;
            hubCase.Stages.Clear(); // 不配阶段，场景单独由 StartSceneId 决定
            hubCase.StartingCardIds.Add(CardFootprint);
            var hubSession = new CaseSession(hubCase);

            Assert.AreEqual(SceneHub, hubSession.CurrentSceneId);
            Assert.AreEqual(0, hubSession.GetActions(CardFootprint).Count, "场景不符时不该给出这个行动");
            Assert.AreEqual(ActionFailure.NoMatchingRecipe, hubSession.Perform(CardFootprint, ActionKind.Investigate).Failure);
        }

        [Test]
        public void CompletingTriggerPage_AdvancesStage_AndSwapsPages()
        {
            CaseSession session = NewSession();
            AdvanceToSuspect(session);

            var entered = new List<string>();
            var completedPages = new List<string>();
            session.StageEntered += stage => entered.Add(stage.Id);
            session.QuestionPageCompleted += (pageId, correct) => completedPages.Add(pageId);

            Assert.IsTrue(session.PlaceAnswer(PageFirst, "culprit", CardCulprit));

            Assert.AreEqual(1, completedPages.Count);
            Assert.AreEqual(PageFirst, completedPages[0]);
            Assert.AreEqual(1, entered.Count, "填完 Q01 应当自动进入第二阶段");
            Assert.AreEqual(StageTwo, entered[0]);

            Assert.AreEqual(StageTwo, session.CurrentStageId, "主角卡应当随阶段切换失效");
            Assert.AreEqual(SceneStreet, session.CurrentSceneId);
            Assert.IsNull(session.GetCard(CardCaseFile), "阶段一的卡牌应当被清空");
            Assert.AreEqual(4, session.Cards.Count, "只剩阶段二发的 4 张牌");
            Assert.IsNull(session.QuestionBook.Find(PageFirst), "Q01 应当在第二阶段被移除");
            Assert.IsNotNull(session.QuestionBook.Find(PageFinal));
            Assert.AreEqual(1, session.Notebook.Count, "笔记本不随阶段清空");
        }

        [Test]
        public void AutoFilledSlot_CountsAsFilled_AndFinalPageSolvesCase()
        {
            CaseSession session = NewSession();
            AdvanceToSuspect(session);
            session.PlaceAnswer(PageFirst, "culprit", CardCulprit);

            QuestionPageModel page = session.QuestionBook.Find(PageFinal);
            Assert.IsNotNull(page);
            Assert.IsTrue(page.FindSlot("victim1").IsFilled, "死者格由数据自动填入");
            Assert.IsTrue(page.FindSlot("victim1").IsAutoFilled);
            Assert.IsFalse(session.IsCaseFilled, "证据格还空着，案件未结");

            session.PlaceAnswer(PageFinal, "evidence", CardEvidence);

            Assert.IsTrue(session.IsCaseFilled);
            Assert.IsTrue(session.IsCaseSolved);
        }

        [Test]
        public void CaseFilled_FiresOnceOnFinalPageCompletion()
        {
            CaseSession session = NewSession();
            AdvanceToSuspect(session);
            session.PlaceAnswer(PageFirst, "culprit", CardCulprit);

            var reported = new List<bool>();
            session.CaseFilled += correct => reported.Add(correct);

            session.PlaceAnswer(PageFinal, "evidence", CardEvidence);
            session.PlaceAnswer(PageFinal, "evidence", CardEvidence); // 重复放入不改变状态

            Assert.AreEqual(1, reported.Count, "结案事件只应触发一次");
            Assert.IsTrue(reported[0]);
        }

        [Test]
        public void ProgressPage_CompletesWhenEnoughCluesCollected()
        {
            var definition = new CaseDefinition { Id = "progress_page_case" };
            definition.Cards.Add(new CardDefinition(CardCaseFile, "案件档案", CardCategory.Item));
            definition.StartingCardIds.Add(CardCaseFile);
            definition.Clues.Add(new ClueDefinition("C01", "一"));
            definition.Clues.Add(new ClueDefinition("C02", "二"));

            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = "Q04",
                Title = "寻找嫌疑人页",
                RequiredClueCount = 2
            });

            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_one",
                Action = ActionKind.Investigate,
                PrimaryCardId = CardCaseFile,
                OutputClueIds = new List<string> { "C01" }
            });

            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_two",
                Action = ActionKind.Talk,
                PrimaryCardId = CardCaseFile,
                OutputClueIds = new List<string> { "C02" }
            });

            var session = new CaseSession(definition);
            QuestionPageModel page = session.QuestionBook.Find("Q04");

            Assert.IsNotNull(page);
            Assert.IsTrue(page.IsProgressPage);
            Assert.IsFalse(page.IsFilled, "线索还不够，进度页不算完成");

            session.Perform(CardCaseFile, ActionKind.Investigate);
            Assert.IsFalse(page.IsFilled, "1/2 条线索还不够");

            ActionResult second = session.Perform(CardCaseFile, ActionKind.Talk);
            Assert.IsTrue(second.Success, "第二次行动应当成功：" + second.Message);
            Assert.AreEqual(2, session.Notebook.Count, "应当记录了 2 条线索");
            Assert.AreEqual(2, page.Definition.RequiredClueCount);
            Assert.IsTrue(page.RequiredCluesMet, "线索数已达标，前置应当满足");
            Assert.IsTrue(page.IsFilled, "线索收齐后进度页完成");
        }

        [Test]
        public void MissingPageReference_ProducesDataWarning()
        {
            var definition = new CaseDefinition { Id = "bad_refs" };
            definition.Cards.Add(new CardDefinition(CardCaseFile, "案件档案", CardCategory.Item));
            definition.Stages.Add(new StageDefinition
            {
                Id = "stage1",
                StartingCardIds = new List<string> { CardCaseFile },
                AddQuestionPageIds = new List<string> { "Q_missing" }
            });

            var session = new CaseSession(definition);

            Assert.IsTrue(ContainsWarning(session.DataWarnings, "Q_missing"), "引用不存在的问题页应当报警告");
        }

        [Test]
        public void LegacyCase_WithoutStages_BehavesAsSinglePage()
        {
            var definition = new CaseDefinition { Id = "legacy_like" };
            definition.Cards.Add(new CardDefinition(CardCaseFile, "案件档案", CardCategory.Item));
            definition.StartingCardIds.Add(CardCaseFile);
            definition.QuestionSlots.Add(new QuestionSlotDefinition
            {
                Kind = QuestionSlotKind.Culprit,
                Label = "凶手",
                AcceptedCardIds = new List<string> { CardCaseFile }
            });

            var session = new CaseSession(definition);

            Assert.IsNull(session.CurrentStageId, "没有阶段数据时不应凭空造阶段");
            Assert.IsNull(session.CurrentSceneId);
            Assert.AreEqual(1, session.QuestionBook.Count);
            Assert.AreEqual(QuestionPageModel.LegacyPageId, session.QuestionPage.Id);
            Assert.IsTrue(session.PlaceAnswer(QuestionSlotKind.Culprit, CardCaseFile));
            Assert.IsTrue(session.IsCaseSolved);
        }

        private static bool ContainsWarning(IReadOnlyList<string> warnings, string fragment)
        {
            for (int i = 0; i < warnings.Count; i++)
            {
                if (warnings[i].Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
