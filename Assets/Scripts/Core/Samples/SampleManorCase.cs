using System.Collections.Generic;

namespace CoolingGame.Core.Samples
{
    /// <summary>
    /// 一个用于验证核心机制的最小案件（「贵族庄园绑架案」极简版）。
    /// 它同时是 EditMode 测试的固定数据源和后续 UI 原型的占位关卡。
    ///
    /// 覆盖到的机制：
    /// - 三个行动（调查/交谈/前往未用，可扩展）
    /// - 新卡牌背面朝上、翻开才解锁线索
    /// - 单卡行动 与 需要附加卡槽的组合行动
    /// - 情节 flag 门禁（解密顺序）
    /// - 同一张卡 + 同一行动下的「低精度/高精度」两条配方
    /// - 线索进入笔记本、问题页五槽判定
    /// </summary>
    public static class SampleManorCase
    {
        public const string Id = "sample_manor";

        public const string Nursery = "loc_nursery";
        public const string Garden = "loc_garden";
        public const string Corridor = "loc_corridor";
        public const string Nanny = "per_nanny";
        public const string Gardener = "per_gardener";
        public const string Rope = "item_rope";
        public const string MudBoots = "item_mudboots";
        public const string Testimony = "card_testimony";

        public const string FlagHeardNanny = "heard_nanny";
        public const string FlagFoundBoots = "found_boots";
        public const string FlagFoundTrail = "found_trail";

        public static CaseDefinition Build()
        {
            var definition = new CaseDefinition
            {
                Id = Id,
                Title = "贵族庄园绑架案（示例）",
                Summary = "庄园主的女儿在午夜失踪。婴儿房、温室、保姆与园丁，五张牌说清一桩交易。",
                TimeBudget = -1
            };

            // ── 卡牌 ────────────────────────────────────────────────────────
            definition.Cards.AddRange(new[]
            {
                new CardDefinition(Nursery, "婴儿房", CardCategory.Location, "location"),
                new CardDefinition(Garden, "温室", CardCategory.Location, "location"),
                new CardDefinition(Corridor, "走廊", CardCategory.Location, "location"),
                new CardDefinition(Nanny, "保姆 艾格尼丝", CardCategory.Person, "person", "servant"),
                new CardDefinition(Gardener, "园丁 托马斯", CardCategory.Person, "person", "suspect"),
                new CardDefinition(Rope, "割断的绳索", CardCategory.Clue, "item")
                {
                    ClueId = "clue_rope"
                },
                new CardDefinition(MudBoots, "泥泞的靴子", CardCategory.Clue, "item")
                {
                    ClueId = "clue_boots"
                },
                new CardDefinition(Testimony, "保姆的证词", CardCategory.Clue, "testimony")
                {
                    ClueId = "clue_testimony"
                }
            });

            // ── 线索 ────────────────────────────────────────────────────────
            definition.Clues.AddRange(new[]
            {
                new ClueDefinition("clue_rope", "窗台上的绳索被利器割断，断口平整。", "绳索", "窗台", "割断"),
                new ClueDefinition("clue_boots", "温室后门有带泥的靴印，尺码与园丁的靴子一致。", "靴印", "温室", "园丁"),
                new ClueDefinition("clue_testimony", "保姆说午夜听见温室的门响，之后园丁很久没回屋。", "证词", "午夜", "温室"),
                new ClueDefinition("clue_damp", "绳索摸起来还有潮气，像是刚从外面拿进来。", "绳索", "潮气"),
                new ClueDefinition("clue_soil", "绳索上的泥土与靴子上的土是同一种红土。", "泥土", "绳索", "靴子"),
                new ClueDefinition("clue_confession", "园丁承认午夜去过温室，却说不清手上为什么有机油。", "园丁", "机油", "午夜"),
                new ClueDefinition("clue_corridor", "走廊地毯上有拖拽的痕迹，方向朝着温室。", "走廊", "拖拽", "痕迹")
            });

            // ── 情节 ────────────────────────────────────────────────────────
            definition.StoryNodes.AddRange(new[]
            {
                new StoryNodeDefinition
                {
                    Id = "story_open",
                    Title = "午夜之后",
                    Body = "管家在凌晨四点叫醒你。\n\n「小姐不见了。夫人已经快撑不住了。」",
                    Presentation = StoryPresentation.VisualNovel
                },
                new StoryNodeDefinition
                {
                    Id = "story_confession",
                    Title = "温室里的机油",
                    Body = "园丁的手在抖。\n\n「我只是……只是去关窗。」\n\n他袖口的机油，是温室里那台抽水机才有的。",
                    Presentation = StoryPresentation.VisualNovel,
                    SetFlagsOnPlay = new List<string> { "confession_heard" }
                }
            });

            // ── 开场手牌 ────────────────────────────────────────────────────
            definition.StartingCardIds.AddRange(new[] { Nursery, Nanny, Garden, Gardener, Corridor });

            // ── 配方 ────────────────────────────────────────────────────────
            // 调查婴儿房 → 发现割断的绳索
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_investigate_nursery",
                Action = ActionKind.Investigate,
                PrimaryCardId = Nursery,
                OutputCardIds = new List<string> { Rope },
                TimeCost = 1
            });

            // 与保姆交谈 → 拿到证词卡（翻开才进笔记本）
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_talk_nanny",
                Action = ActionKind.Talk,
                PrimaryCardId = Nanny,
                OutputCardIds = new List<string> { Testimony },
                SetFlags = new List<string> { FlagHeardNanny },
                TimeCost = 1
            });

            // 调查温室（需要先听过保姆的话）→ 泥靴子
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_investigate_garden",
                Action = ActionKind.Investigate,
                PrimaryCardId = Garden,
                OutputCardIds = new List<string> { MudBoots },
                RequiredFlags = new List<string> { FlagHeardNanny },
                SetFlags = new List<string> { FlagFoundBoots },
                TimeCost = 1
            });

            // 前往走廊 → 发现拖拽痕迹（演示「前往」行动）
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_go_corridor",
                Action = ActionKind.Go,
                PrimaryCardId = Corridor,
                OutputClueIds = new List<string> { "clue_corridor" },
                SetFlags = new List<string> { FlagFoundTrail },
                TimeCost = 1
            });

            // 与园丁交谈（需要先找到靴子、也要先看过走廊）→ 关键线索 + 情节
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_talk_gardener",
                Action = ActionKind.Talk,
                PrimaryCardId = Gardener,
                OutputClueIds = new List<string> { "clue_confession" },
                RequiredFlags = new List<string> { FlagFoundBoots, FlagFoundTrail },
                StoryNodeId = "story_confession",
                TimeCost = 1
            });

            // 低精度：单独观察绳索 → 潮气
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_rope_alone",
                Action = ActionKind.Investigate,
                PrimaryCardId = Rope,
                OutputClueIds = new List<string> { "clue_damp" },
                TimeCost = 1
            });

            // 高精度：绳索 + 靴子 → 红土比对
            definition.Recipes.Add(new RecipeDefinition
            {
                Id = "r_rope_with_boots",
                Action = ActionKind.Investigate,
                PrimaryCardId = Rope,
                RequiredContextCardIds = new List<string> { MudBoots },
                OutputClueIds = new List<string> { "clue_soil" },
                TimeCost = 1
            });

            // ── 问题页五槽 ──────────────────────────────────────────────────
            definition.QuestionSlots.AddRange(new[]
            {
                new QuestionSlotDefinition
                {
                    Kind = QuestionSlotKind.Case,
                    Label = "案件",
                    Hint = "现场、受害者",
                    AcceptedCardIds = new List<string> { Nursery }
                },
                new QuestionSlotDefinition
                {
                    Kind = QuestionSlotKind.Culprit,
                    Label = "凶手",
                    Hint = "放置嫌疑人",
                    AcceptedCardIds = new List<string> { Gardener }
                },
                new QuestionSlotDefinition
                {
                    Kind = QuestionSlotKind.Weapon,
                    Label = "凶器",
                    Hint = "凶器或作案手法",
                    AcceptedCardIds = new List<string> { Rope }
                },
                new QuestionSlotDefinition
                {
                    Kind = QuestionSlotKind.PhysicalEvidence,
                    Label = "物证",
                    Hint = "能指正凶手的核心证据",
                    AcceptedCardIds = new List<string> { MudBoots }
                },
                new QuestionSlotDefinition
                {
                    Kind = QuestionSlotKind.Testimony,
                    Label = "人证",
                    Hint = "目击者或证言",
                    AcceptedCardIds = new List<string> { Testimony }
                }
            });

            return definition;
        }
    }
}
