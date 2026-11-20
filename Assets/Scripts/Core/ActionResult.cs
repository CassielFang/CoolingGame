using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>一次行动失败的原因。UI 据此播放不同的「无效果」反馈。</summary>
    public enum ActionFailure
    {
        None = 0,

        /// <summary>卡牌不存在或还背面朝上。</summary>
        UnknownCard,

        /// <summary>附加卡槽里的卡不可用（背面、不存在、重复、或把卡放进自己的槽）。</summary>
        InvalidContextCard,

        /// <summary>这个「卡牌 + 行动」没有任何配方 —— 播放无效果反馈。</summary>
        NoMatchingRecipe,

        /// <summary>配方存在，但附加卡槽放错了卡或还缺卡。</summary>
        MissingContextCards,

        /// <summary>需要的前置情节还没有发生。</summary>
        BlockedByFlags,

        /// <summary>时间预算不够。</summary>
        NotEnoughTime,

        /// <summary>案件已经填完，不能再行动。</summary>
        CaseClosed
    }

    /// <summary>
    /// 一次行动的结果。无论成败都返回，表现层据此更新桌面、笔记本与提示。
    /// </summary>
    public sealed class ActionResult
    {
        /// <summary>行动对象卡牌。</summary>
        public string PrimaryCardId { get; internal set; }

        public ActionKind Action { get; internal set; }

        public bool Success { get; internal set; }

        public ActionFailure Failure { get; internal set; }

        /// <summary>命中的配方（仅成功时有值）。</summary>
        public string RecipeId { get; internal set; }

        /// <summary>本次行动到手的新卡牌（背面朝上）。</summary>
        public List<CardState> ArrivedCards { get; } = new List<CardState>();

        /// <summary>本次行动写入笔记本的线索。</summary>
        public List<ClueEntry> NewClues { get; } = new List<ClueEntry>();

        /// <summary>本次行动置位的情节 flag。</summary>
        public List<string> NewFlags { get; } = new List<string>();

        /// <summary>被消耗掉的卡牌。</summary>
        public List<string> ConsumedCardIds { get; } = new List<string>();

        /// <summary>需要播放的情节节点，可空。</summary>
        public string StoryNodeId { get; internal set; }

        /// <summary>本次行动消耗的时间单位。</summary>
        public int TimeCost { get; internal set; }

        /// <summary>给 UI 的反馈文案（失败时即「无效果的对应显示」）。</summary>
        public string Message { get; internal set; }

        public static ActionResult Fail(string cardId, ActionKind action, ActionFailure failure, string message)
        {
            return new ActionResult
            {
                PrimaryCardId = cardId,
                Action = action,
                Success = false,
                Failure = failure,
                Message = message
            };
        }
    }
}
