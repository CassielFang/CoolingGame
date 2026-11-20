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

        /// <summary>问题页的五个卡槽定义。</summary>
        public List<QuestionSlotDefinition> QuestionSlots = new List<QuestionSlotDefinition>();

        public List<StoryNodeDefinition> StoryNodes = new List<StoryNodeDefinition>();

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
