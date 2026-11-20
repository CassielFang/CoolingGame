using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一条线索的静态定义（笔记本「线索页」里的一条记录）。
    /// </summary>
    [Serializable]
    public sealed class ClueDefinition
    {
        public string Id;

        /// <summary>线索正文。这是玩家真正读到的信息。</summary>
        public string Text;

        /// <summary>检索关键词。搜索时与 <see cref="Text"/> 一起匹配。</summary>
        public List<string> Keywords = new List<string>();

        /// <summary>固定来源卡牌 ID（可空）。运行时以实际产出卡为准。</summary>
        public string SourceCardId;

        /// <summary>来源显示名（可空），留空时由运行时用卡名补齐。</summary>
        public string SourceCardName;

        public ClueDefinition()
        {
        }

        public ClueDefinition(string id, string text, params string[] keywords)
        {
            Id = id;
            Text = text;
            if (keywords != null && keywords.Length > 0)
            {
                Keywords.AddRange(keywords);
            }
        }
    }
}
