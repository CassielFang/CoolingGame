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

        [Header("内容表")]
        public List<CardDefinition> cards = new List<CardDefinition>();
        public List<ClueDefinition> clues = new List<ClueDefinition>();
        public List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        public List<StoryNodeDefinition> storyNodes = new List<StoryNodeDefinition>();

        [Header("场景与阶段（第一章用；留空 = 单阶段、无场景）")]
        public List<SceneDefinition> scenes = new List<SceneDefinition>();
        public string startSceneId = "";
        public List<StageDefinition> stages = new List<StageDefinition>();

        [Header("办案人（主角，不是卡牌）")]
        public List<DetectiveDefinition> detectives = new List<DetectiveDefinition>();
        public string startingDetectiveId = "";

        [Header("开场手牌（配了阶段时由阶段的发牌接管）")]
        public List<string> startingCardIds = new List<string>();

        [Header("报纸")]
        public List<NewspaperDefinition> newspapers = new List<NewspaperDefinition>();

        [Header("剧情对话（逐句 VN）")]
        public List<DialogueScript> dialogues = new List<DialogueScript>();
        [Tooltip("开局自动播放的剧情脚本 ID；留空 = 直接进桌面。")]
        public string openingDialogueId = "";

        [Header("问题页")]        [Tooltip("多问题页（第一章 Q01–Q05）。留空则回退到下面的 questionSlots 五槽页。")]
        public List<QuestionPageDefinition> questionPages = new List<QuestionPageDefinition>();
        [Tooltip("结案页 ID（例如 Q05）；留空取最后一张页。")]
        public string finalQuestionPageId = "";
        [Tooltip("破案时播放的情节节点 ID，可空。")]
        public string closingStoryNodeId = "";
        [Tooltip("破案时播放的剧情脚本 ID，可空。")]
        public string closingDialogueId = "";
        [Tooltip("旧版单页的五个卡槽；questionPages 为空时才生效。")]
        public List<QuestionSlotDefinition> questionSlots = new List<QuestionSlotDefinition>();

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
                StoryNodes = new List<StoryNodeDefinition>(storyNodes),
                Dialogues = new List<DialogueScript>(dialogues),
                Newspapers = new List<NewspaperDefinition>(newspapers),
                OpeningDialogueId = openingDialogueId,
                Scenes = new List<SceneDefinition>(scenes),
                StartSceneId = startSceneId,
                Stages = new List<StageDefinition>(stages),
                Detectives = new List<DetectiveDefinition>(detectives),
                StartingDetectiveId = startingDetectiveId,
                StartingCardIds = new List<string>(startingCardIds),
                QuestionPages = new List<QuestionPageDefinition>(questionPages),
                FinalQuestionPageId = finalQuestionPageId,
                ClosingStoryNodeId = closingStoryNodeId,
                ClosingDialogueId = closingDialogueId,
                QuestionSlots = new List<QuestionSlotDefinition>(questionSlots)
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
            storyNodes = new List<StoryNodeDefinition>(definition.StoryNodes);
            dialogues = new List<DialogueScript>(definition.Dialogues);
            newspapers = new List<NewspaperDefinition>(definition.Newspapers);
            openingDialogueId = definition.OpeningDialogueId;
            scenes = new List<SceneDefinition>(definition.Scenes);
            startSceneId = definition.StartSceneId;
            stages = new List<StageDefinition>(definition.Stages);
            detectives = new List<DetectiveDefinition>(definition.Detectives);
            startingDetectiveId = definition.StartingDetectiveId;
            startingCardIds = new List<string>(definition.StartingCardIds);
            questionPages = new List<QuestionPageDefinition>(definition.QuestionPages);
            finalQuestionPageId = definition.FinalQuestionPageId;
            closingStoryNodeId = definition.ClosingStoryNodeId;
            closingDialogueId = definition.ClosingDialogueId;
            questionSlots = new List<QuestionSlotDefinition>(definition.QuestionSlots);
        }
    }
}
