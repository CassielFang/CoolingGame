using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一条配方 = 一次「卡牌(+其他卡牌) + 行动」能产生什么。
    /// 核心机制的全部玩法内容都以配方表的形式表达：
    /// 命中配方 → 生成新卡牌 / 写入线索 / 推进情节；未命中 → 无效果反馈。
    /// </summary>
    [Serializable]
    public sealed class RecipeDefinition
    {
        public string Id;

        /// <summary>需要施加的行动。</summary>
        public ActionKind Action;

        /// <summary>作为行动对象的卡牌 ID。与 <see cref="PrimaryCardTag"/> 二选一。</summary>
        public string PrimaryCardId;

        /// <summary>按标签匹配行动对象（例如任意 person）。<see cref="PrimaryCardId"/> 为空时生效。</summary>
        public string PrimaryCardTag;

        /// <summary>
        /// 需要放进附加卡槽的卡牌 ID。数量即操作 UI 的卡槽数。
        /// 提交的附加卡必须与此集合完全一致（不多不少）。
        /// </summary>
        public List<string> RequiredContextCardIds = new List<string>();

        /// <summary>执行前必须已满足的情节 flag。</summary>
        public List<string> RequiredFlags = new List<string>();

        /// <summary>命中后生成的新卡牌（以背面朝上出现）。</summary>
        public List<string> OutputCardIds = new List<string>();

        /// <summary>命中后写入笔记本的线索。</summary>
        public List<string> OutputClueIds = new List<string>();

        /// <summary>命中后置位的情节 flag。</summary>
        public List<string> SetFlags = new List<string>();

        /// <summary>命中后要播放的情节节点（视觉小说 / 报纸等），可空。</summary>
        public string StoryNodeId;

        /// <summary>行动消耗的时间单位。（时间系统当前默认关闭，见 CaseDefinition.TimeBudget。）</summary>
        public int TimeCost = 1;

        /// <summary>命中后是否消耗掉附加卡牌（卡牌移出桌面）。</summary>
        public bool ConsumeContextCards;

        /// <summary>
        /// 匹配精度。多个配方同时命中时取最具体的那个：
        /// 精确卡牌 > 标签 > 通配，附加卡多者优先。
        /// </summary>
        public int Specificity
        {
            get
            {
                int score = 0;
                if (!string.IsNullOrEmpty(PrimaryCardId))
                {
                    score += 4;
                }
                else if (!string.IsNullOrEmpty(PrimaryCardTag))
                {
                    score += 2;
                }

                score += Math.Min(RequiredContextCardIds != null ? RequiredContextCardIds.Count : 0, 3);
                score += Math.Min(RequiredFlags != null ? RequiredFlags.Count : 0, 3);
                return score;
            }
        }

        public bool MatchesPrimary(CardDefinition card)
        {
            if (card == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(PrimaryCardId))
            {
                return PrimaryCardId == card.Id;
            }

            if (!string.IsNullOrEmpty(PrimaryCardTag))
            {
                return card.HasTag(PrimaryCardTag);
            }

            // 两者都为空：任意卡牌都可作为对象（用于“对任何东西都能做”的行动）。
            return true;
        }

        public bool HasAllFlags(ISet<string> flags)
        {
            if (RequiredFlags == null || RequiredFlags.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < RequiredFlags.Count; i++)
            {
                if (string.IsNullOrEmpty(RequiredFlags[i]))
                {
                    continue;
                }

                if (flags == null || !flags.Contains(RequiredFlags[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
