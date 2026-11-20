using System.Collections.Generic;
using CoolingGame.Core;
using UnityEngine;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 案件的 ScriptableObject 载体：策划在 Inspector 里维护数据，运行时转成纯逻辑的
    /// <see cref="CaseDefinition"/> 交给 <see cref="CaseSession"/>。
    /// 这样可以做到「加内容不改代码」，也方便后续用导入器从 Excel 生成。
    /// </summary>
    [CreateAssetMenu(menuName = "CoolingGame/案件定义", fileName = "CaseDefinition")]
    public sealed class CaseDefinitionAsset : ScriptableObject
    {
        public string caseId = "new_case";
        public string title = "新案件";
        [TextArea(2, 4)] public string summary = "";

        [Tooltip("行动时间预算；-1 表示不限时（当前默认）。")]
        public int timeBudget = -1;

        public List<CardDefinition> cards = new List<CardDefinition>();
        public List<ClueDefinition> clues = new List<ClueDefinition>();
        public List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        public List<string> startingCardIds = new List<string>();
        public List<QuestionSlotDefinition> questionSlots = new List<QuestionSlotDefinition>();
        public List<StoryNodeDefinition> storyNodes = new List<StoryNodeDefinition>();

        public CaseDefinition ToDefinition()
        {
            return new CaseDefinition
            {
                Id = caseId,
                Title = title,
                Summary = summary,
                TimeBudget = timeBudget,
                Cards = new List<CardDefinition>(cards),
                Clues = new List<ClueDefinition>(clues),
                Recipes = new List<RecipeDefinition>(recipes),
                StartingCardIds = new List<string>(startingCardIds),
                QuestionSlots = new List<QuestionSlotDefinition>(questionSlots),
                StoryNodes = new List<StoryNodeDefinition>(storyNodes)
            };
        }

        /// <summary>从代码里的案件定义回填本资产（编辑器工具/测试用）。</summary>
        public void CopyFrom(CaseDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            caseId = definition.Id;
            title = definition.Title;
            summary = definition.Summary;
            timeBudget = definition.TimeBudget;
            cards = new List<CardDefinition>(definition.Cards);
            clues = new List<ClueDefinition>(definition.Clues);
            recipes = new List<RecipeDefinition>(definition.Recipes);
            startingCardIds = new List<string>(definition.StartingCardIds);
            questionSlots = new List<QuestionSlotDefinition>(definition.QuestionSlots);
            storyNodes = new List<StoryNodeDefinition>(definition.StoryNodes);
        }
    }
}
