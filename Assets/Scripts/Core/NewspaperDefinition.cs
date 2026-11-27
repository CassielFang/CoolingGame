using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一张报纸（策划表「报纸新闻」）。报纸是叙事节奏点：它们不推进玩法，
    /// 只告诉玩家「外面的人怎么看这案子」，顺便把舆论压力塞给玩家。
    /// </summary>
    [Serializable]
    public sealed class NewspaperDefinition
    {
        public string Id;

        /// <summary>大标题，例如《斯威克的连环杀手》。</summary>
        public string Title;

        /// <summary>正文，段落之间用空行分隔。</summary>
        public string Body;
    }
}
