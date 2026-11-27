using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一个案件的全部静态数据。相当于一张「关卡表」：
    /// 卡牌、线索、配方、开场手牌、问题页答案、情节节点。
    /// 表现层只读这张表，不承担任何规则判定。
    /// </summary>
    [Serializable]
    public sealed class CaseDefinition
    {
        public string Id;

        public string Title;

        public string Summary;

        /// <summary>
        /// 行动预算（时间系统）。-1 表示不限时——当前版本默认不启用时间压力，
        /// 与策划草案一致；打开后每次行动按配方 TimeCost 扣减。
        /// </summary>
        public int TimeBudget = -1;

        public List<CardDefinition> Cards = new List<CardDefinition>();

        public List<ClueDefinition> Clues = new List<ClueDefinition>();

        public List<RecipeDefinition> Recipes = new List<RecipeDefinition>();

        /// <summary>开局就在手上的卡牌（正面朝上）。</summary>
        public List<string> StartingCardIds = new List<string>();

        /// <summary>
        /// 旧版问题页的五个卡槽。只在 <see cref="QuestionPages"/> 为空时使用，
        /// 视为一张 ID 为 <see cref="QuestionPageModel.LegacyPageId"/> 的页。
        /// </summary>
        public List<QuestionSlotDefinition> QuestionSlots = new List<QuestionSlotDefinition>();

        /// <summary>多问题页（第一章 Q01–Q05）。为空时回退到 <see cref="QuestionSlots"/>。</summary>
        public List<QuestionPageDefinition> QuestionPages = new List<QuestionPageDefinition>();

        /// <summary>结案页：这一页完成 = 案件有结论。空则取最后一张页。</summary>
        public string FinalQuestionPageId;

        /// <summary>破案（结案页全对）时播放的情节节点，可空。</summary>
        public string ClosingStoryNodeId;

        /// <summary>破案时播放的剧情（逐句 VN），可空。</summary>
        public string ClosingDialogueId;

        /// <summary>场景表（第一章 S00–S04）。</summary>
        public List<SceneDefinition> Scenes = new List<SceneDefinition>();

        /// <summary>开局场景；空 = 由第一个阶段的 SceneId 决定。</summary>
        public string StartSceneId;

        /// <summary>阶段表，按顺序推进（第一章两段）。为空 = 单阶段，行为与旧版本完全一致。</summary>
        public List<StageDefinition> Stages = new List<StageDefinition>();

        /// <summary>办案人（主角）。不是卡牌，是「谁在办这个案」。</summary>
        public List<DetectiveDefinition> Detectives = new List<DetectiveDefinition>();

        /// <summary>开局由谁办案；空 = 用 Detectives 的第一个。</summary>
        public string StartingDetectiveId;

        public List<StoryNodeDefinition> StoryNodes = new List<StoryNodeDefinition>();

        /// <summary>逐句剧情（策划表的脚本ID VN01–VN09）。</summary>
        public List<DialogueScript> Dialogues = new List<DialogueScript>();

        /// <summary>开局自动播放的剧情；空 = 直接进桌面。</summary>
        public string OpeningDialogueId;

        /// <summary>报纸（策划表「报纸新闻」）。</summary>
        public List<NewspaperDefinition> Newspapers = new List<NewspaperDefinition>();

        public NewspaperDefinition FindNewspaper(string newspaperId)
        {
            if (string.IsNullOrEmpty(newspaperId) || Newspapers == null)
            {
                return null;
            }

            for (int i = 0; i < Newspapers.Count; i++)
            {
                if (Newspapers[i] != null && Newspapers[i].Id == newspaperId)
                {
                    return Newspapers[i];
                }
            }

            return null;
        }

        public DialogueScript FindDialogue(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId) || Dialogues == null)
            {
                return null;
            }

            for (int i = 0; i < Dialogues.Count; i++)
            {
                if (Dialogues[i] != null && Dialogues[i].Id == dialogueId)
                {
                    return Dialogues[i];
                }
            }

            return null;
        }

        public CardDefinition FindCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId) || Cards == null)
            {
                return null;
            }

            for (int i = 0; i < Cards.Count; i++)
            {
                if (Cards[i] != null && Cards[i].Id == cardId)
                {
                    return Cards[i];
                }
            }

            return null;
        }

        public ClueDefinition FindClue(string clueId)
        {
            if (string.IsNullOrEmpty(clueId) || Clues == null)
            {
                return null;
            }

            for (int i = 0; i < Clues.Count; i++)
            {
                if (Clues[i] != null && Clues[i].Id == clueId)
                {
                    return Clues[i];
                }
            }

            return null;
        }

        public StoryNodeDefinition FindStoryNode(string storyNodeId)
        {
            if (string.IsNullOrEmpty(storyNodeId) || StoryNodes == null)
            {
                return null;
            }

            for (int i = 0; i < StoryNodes.Count; i++)
            {
                if (StoryNodes[i] != null && StoryNodes[i].Id == storyNodeId)
                {
                    return StoryNodes[i];
                }
            }

            return null;
        }

        public QuestionSlotDefinition FindQuestionSlot(QuestionSlotKind kind)
        {
            if (QuestionSlots == null)
            {
                return null;
            }

            for (int i = 0; i < QuestionSlots.Count; i++)
            {
                if (QuestionSlots[i] != null && QuestionSlots[i].Kind == kind)
                {
                    return QuestionSlots[i];
                }
            }

            return null;
        }

        public SceneDefinition FindScene(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId) || Scenes == null)
            {
                return null;
            }

            for (int i = 0; i < Scenes.Count; i++)
            {
                if (Scenes[i] != null && Scenes[i].Id == sceneId)
                {
                    return Scenes[i];
                }
            }

            return null;
        }

        public StageDefinition FindStage(string stageId)
        {
            if (string.IsNullOrEmpty(stageId) || Stages == null)
            {
                return null;
            }

            for (int i = 0; i < Stages.Count; i++)
            {
                if (Stages[i] != null && Stages[i].Id == stageId)
                {
                    return Stages[i];
                }
            }

            return null;
        }

        /// <summary>阶段在表里的序号；找不到返回 -1。</summary>
        public int IndexOfStage(string stageId)
        {
            if (string.IsNullOrEmpty(stageId) || Stages == null)
            {
                return -1;
            }

            for (int i = 0; i < Stages.Count; i++)
            {
                if (Stages[i] != null && Stages[i].Id == stageId)
                {
                    return i;
                }
            }

            return -1;
        }

        public StageDefinition NextStage(string stageId)
        {
            int index = IndexOfStage(stageId);
            if (index < 0 || index + 1 >= Stages.Count)
            {
                return null;
            }

            return Stages[index + 1];
        }

        public DetectiveDefinition FindDetective(string detectiveId)
        {
            if (string.IsNullOrEmpty(detectiveId) || Detectives == null)
            {
                return null;
            }

            for (int i = 0; i < Detectives.Count; i++)
            {
                if (Detectives[i] != null && Detectives[i].Id == detectiveId)
                {
                    return Detectives[i];
                }
            }

            return null;
        }

        public QuestionPageDefinition FindQuestionPage(string pageId)
        {            if (string.IsNullOrEmpty(pageId) || QuestionPages == null)
            {
                return null;
            }

            for (int i = 0; i < QuestionPages.Count; i++)
            {
                if (QuestionPages[i] != null && QuestionPages[i].Id == pageId)
                {
                    return QuestionPages[i];
                }
            }

            return null;
        }

        /// <summary>数据里是否真的配了多问题页（否则回退到旧的五槽页）。</summary>
        public bool HasQuestionPages
        {
            get { return QuestionPages != null && QuestionPages.Count > 0; }
        }

        /// <summary>找出「某张卡 + 某种行动」下所有配方（按表内顺序）。</summary>
        public List<RecipeDefinition> FindRecipes(CardDefinition card, ActionKind action)
        {
            var result = new List<RecipeDefinition>();
            if (card == null || Recipes == null)
            {
                return result;
            }

            for (int i = 0; i < Recipes.Count; i++)
            {
                RecipeDefinition recipe = Recipes[i];
                if (recipe == null || recipe.Action != action)
                {
                    continue;
                }

                if (recipe.MatchesPrimary(card))
                {
                    result.Add(recipe);
                }
            }

            return result;
        }
    }
}
