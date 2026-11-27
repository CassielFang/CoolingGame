using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一个阶段（关卡的一节）。第一章有「第一阶段：两起命案」「第二阶段：锁定画师」两段，
    /// 阶段之间会清空卡牌、换场景、换问题页——这些在设定集里都是策划数据，不该写死在代码里。
    ///
    /// 阶段按 <see cref="CaseDefinition.Stages"/> 的顺序推进：
    /// 当前阶段的 <see cref="CompletedByQuestionPageId"/> 指定的页完成时，
    /// 自动进入列表里的下一个阶段。
    /// </summary>
    [Serializable]
    public sealed class StageDefinition
    {
        /// <summary>阶段 ID，例如 "stage1" / "stage2"。</summary>
        public string Id;

        /// <summary>显示名，例如 "第一阶段：两起命案"。</summary>
        public string DisplayName;

        /// <summary>进入本阶段后所在的场景；空 = 沿用上一个场景。</summary>
        public string SceneId;

        /// <summary>进入本阶段时清空桌面上的所有卡牌（第一章第二阶段会清空）。</summary>
        public bool ClearCardsOnEnter;

        /// <summary>进入本阶段时正面朝上发到手里的卡牌。</summary>
        public List<string> StartingCardIds = new List<string>();

        /// <summary>进入本阶段时添加的问题页（顺序即笔记本里的顺序）。</summary>
        public List<string> AddQuestionPageIds = new List<string>();

        /// <summary>进入本阶段时移除的问题页。</summary>
        public List<string> RemoveQuestionPageIds = new List<string>();

        /// <summary>进入本阶段时播放的情节节点，可空。</summary>
        public string StoryNodeId;

        /// <summary>进入本阶段时置位的情节 flag。</summary>
        public List<string> SetFlags = new List<string>();

        /// <summary>
        /// 完成本阶段的条件：这张问题页填满即进入下一阶段（第一章是 Q03）。
        /// 空 = 不自动推进（例如最后阶段，靠结案判定）。
        /// </summary>
        public string CompletedByQuestionPageId;

        /// <summary>进入本阶段时播放的剧情（逐句 VN），可空。</summary>
        public string EnterDialogueId;

        /// <summary>进入本阶段时触发的报纸 ID，可空。</summary>
        public string EnterNewspaperId;

        /// <summary>完成本阶段时播放的剧情（逐句 VN），可空。</summary>
        public string CompletedDialogueId;

        /// <summary>完成本阶段时播放的情节节点，可空。</summary>
        public string CompletedStoryNodeId;
    }
}
