using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 卡牌的三个基本类别。每类对应一种边框（由表现层按类别取样式）。
    /// </summary>
    public enum CardCategory
    {
        Location = 0,
        Item = 1,
        Person = 2,
        Clue = 3,
        Action = 4,
        Other = 5
    }

    /// <summary>
    /// 一张卡牌的静态定义（策划数据）。运行时状态见 <see cref="CardState"/>。
    /// 约定：本类型只承载只读数据，不持有任何表现层引用。
    /// </summary>
    [Serializable]
    public sealed class CardDefinition
    {
        /// <summary>唯一 ID，例如 "per_innkeeper"。策划表与代码共用这一个键。</summary>
        public string Id;

        /// <summary>牌面显示名，例如 "旅店老板"。</summary>
        public string DisplayName;

        public CardCategory Category;

        /// <summary>表现层用来取美术资源的键，可空。</summary>
        public string ArtKey;

        /// <summary>查看卡牌时的说明文字（可选）。</summary>
        public string Description;

        /// <summary>
        /// 翻开这张卡时自动记入笔记本的线索 ID（可空）。
        /// 用于“线索卡”——卡牌本身就是一条线索。
        /// </summary>
        public string ClueId;

        /// <summary>标签，用于按类匹配配方（例如 "person"、"locked"）。</summary>
        public List<string> Tags = new List<string>();

        public CardDefinition()
        {
        }

        public CardDefinition(string id, string displayName, CardCategory category, params string[] tags)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            if (tags != null && tags.Length > 0)
            {
                Tags.AddRange(tags);
            }
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || Tags == null)
            {
                return false;
            }

            for (int i = 0; i < Tags.Count; i++)
            {
                if (Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
