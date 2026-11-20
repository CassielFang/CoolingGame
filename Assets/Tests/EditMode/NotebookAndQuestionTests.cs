using System.Linq;
using CoolingGame.Core;
using CoolingGame.Core.Samples;
using NUnit.Framework;

namespace CoolingGame.Tests
{
    /// <summary>笔记本（线索页 / 问题页）的回归测试。</summary>
    public class NotebookAndQuestionTests
    {
        private static CaseSession Advance()
        {
            CaseSession session = new CaseSession(SampleManorCase.Build());

            session.Perform(SampleManorCase.Nursery, ActionKind.Investigate);
            session.Reveal(SampleManorCase.Rope);

            session.Perform(SampleManorCase.Nanny, ActionKind.Talk);
            session.Reveal(SampleManorCase.Testimony);

            session.Perform(SampleManorCase.Garden, ActionKind.Investigate);
            session.Reveal(SampleManorCase.MudBoots);

            session.Perform(SampleManorCase.Corridor, ActionKind.Go);

            return session;
        }

        private static void FillAll(CaseSession session, string culprit)
        {
            session.PlaceAnswer(QuestionSlotKind.Case, SampleManorCase.Nursery);
            session.PlaceAnswer(QuestionSlotKind.Culprit, culprit);
            session.PlaceAnswer(QuestionSlotKind.Weapon, SampleManorCase.Rope);
            session.PlaceAnswer(QuestionSlotKind.PhysicalEvidence, SampleManorCase.MudBoots);
            session.PlaceAnswer(QuestionSlotKind.Testimony, SampleManorCase.Testimony);
        }

        [Test]
        public void AddClue_Deduplicates()
        {
            var session = new CaseSession(SampleManorCase.Build());
            var clue = new ClueDefinition("clue_dup", "重复的线索");

            ClueEntry first = session.Notebook.AddClue(clue);
            ClueEntry second = session.Notebook.AddClue(clue);

            Assert.IsNotNull(first);
            Assert.IsNull(second, "同 ID 线索不应重复写入");
            Assert.AreEqual(1, session.Notebook.Count);
            Assert.IsTrue(first.IsNew);
        }

        [Test]
        public void Pin_MovesClueToTopWithoutChangingTimeline()
        {
            var session = new CaseSession(SampleManorCase.Build());
            session.Notebook.AddClue(new ClueDefinition("c1", "第一条"));
            session.Notebook.AddClue(new ClueDefinition("c2", "第二条"));
            session.Notebook.AddClue(new ClueDefinition("c3", "第三条"));

            Assert.IsTrue(session.Notebook.TogglePin("c2"));

            var ordered = session.Notebook.OrderedClues();
            Assert.AreEqual(3, ordered.Count);
            Assert.AreEqual("c2", ordered[0].ClueId, "置顶线索应排最上面");
            Assert.AreEqual("c1", ordered[1].ClueId);

            // 时间线本身不受置顶影响
            Assert.AreEqual("c1", session.Notebook.Clues[0].ClueId);

            Assert.IsTrue(session.Notebook.TogglePin("c2"));
            Assert.AreEqual("c1", session.Notebook.OrderedClues()[0].ClueId);
        }

        [Test]
        public void Search_MatchesTextKeywordsAndSource()
        {
            var session = new CaseSession(SampleManorCase.Build());
            session.Notebook.AddClue(new ClueDefinition("k1", "绳索上有红土", "泥土"));
            session.Notebook.AddClue(new ClueDefinition("k2", "保姆的证词", "午夜", "温室"));

            Assert.AreEqual(1, session.Notebook.Search("红土").Count);
            Assert.AreEqual(1, session.Notebook.Search("午夜").Count);
            Assert.AreEqual(2, session.Notebook.Search("").Count);
            Assert.AreEqual(0, session.Notebook.Search("不存在的关键词").Count);
        }

        [Test]
        public void UnreadTracking_Works()
        {
            var session = new CaseSession(SampleManorCase.Build());
            session.Notebook.AddClue(new ClueDefinition("u1", "一"));
            session.Notebook.AddClue(new ClueDefinition("u2", "二"));

            Assert.AreEqual(2, session.Notebook.UnreadCount);

            session.Notebook.MarkRead("u1");
            Assert.AreEqual(1, session.Notebook.UnreadCount);

            session.Notebook.MarkAllRead();
            Assert.AreEqual(0, session.Notebook.UnreadCount);
        }

        [Test]
        public void QuestionPage_PlaceAndClear()
        {
            var session = new CaseSession(SampleManorCase.Build());

            Assert.AreEqual(5, session.QuestionPage.SlotCount);

            Assert.IsTrue(session.PlaceAnswer(QuestionSlotKind.Case, SampleManorCase.Nursery));
            Assert.AreEqual(1, session.QuestionPage.FilledCount);
            Assert.IsFalse(session.QuestionPage.IsFilled);

            Assert.IsTrue(session.ClearAnswer(QuestionSlotKind.Case));
            Assert.AreEqual(0, session.QuestionPage.FilledCount);

            // 未知卡牌不能放进问题页
            Assert.IsFalse(session.PlaceAnswer(QuestionSlotKind.Weapon, "item_unknown"));
        }

        [Test]
        public void QuestionPage_WrongAnswer_IsFilledButNotCorrect()
        {
            CaseSession session = Advance();
            FillAll(session, SampleManorCase.Nanny);

            Assert.IsTrue(session.QuestionPage.IsFilled, "五个槽填满即完成关卡");
            Assert.IsFalse(session.QuestionPage.IsCorrect);
            Assert.IsFalse(session.IsCaseSolved);

            var evaluation = session.QuestionPage.Evaluate();
            Assert.IsTrue(evaluation.Any(e => e.Kind == QuestionSlotKind.Culprit && !e.IsCorrect));
            Assert.IsTrue(evaluation.Any(e => e.Kind == QuestionSlotKind.Case && e.IsCorrect));
        }

        [Test]
        public void QuestionPage_CorrectAnswers_SolveCase()
        {
            CaseSession session = Advance();
            FillAll(session, SampleManorCase.Gardener);

            Assert.IsTrue(session.QuestionPage.IsFilled);
            Assert.IsTrue(session.QuestionPage.IsCorrect);
            Assert.IsTrue(session.IsCaseSolved);
        }

        [Test]
        public void CaseFilled_EventReportsCorrectness()
        {
            CaseSession session = Advance();

            bool? reported = null;
            session.CaseFilled += correct => reported = correct;

            FillAll(session, SampleManorCase.Gardener);

            Assert.AreEqual(true, reported);
        }

        [Test]
        public void SolveAutomatically_FillsAllSlots()
        {
            CaseSession session = Advance();

            Assert.IsTrue(session.SolveAutomatically());
            Assert.IsTrue(session.IsCaseSolved);
            Assert.AreEqual(5, session.QuestionPage.FilledCount);
        }
    }
}
