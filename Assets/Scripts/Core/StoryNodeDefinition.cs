using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>情节演出形式（策划草案「四、情节控制」）。</summary>
    public enum StoryPresentation
    {
        VisualNovel = 0,
        Newspaper = 1,
        Comic = 2,
        Cg = 3
    }

    /// <summary>
    /// 一个情节节点。由配方或案件开始触发，负责该段剧情演出，
    /// 并可以把情节 flag 置位，从而解锁后续配方。
    /// </summary>
    [Serializable]
    public sealed class StoryNodeDefinition
    {
        public string Id;

        public string Title;

        /// <summary>正文/台词（可含空行分段）。</summary>
        public string Body;

        public StoryPresentation Presentation;

        /// <summary>演出开始时置位的 flag，用于推进情节（例如 "act1_done"）。</summary>
        public List<string> SetFlagsOnPlay = new List<string>();
    }
}
