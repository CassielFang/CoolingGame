using System;

namespace CoolingGame.Core
{
    /// <summary>
    /// 游戏中的三种基本行动（策划草案「二、卡牌与行动 / 2.行动」）。
    /// 行动不是卡牌，而是玩家对某张卡牌施加的行为。
    /// </summary>
    public enum ActionKind
    {
        /// <summary>调查：搜查物品、探索场景、观察人物。</summary>
        Investigate = 0,

        /// <summary>交谈：对话、讯问、请教、探讨。</summary>
        Talk = 1,

        /// <summary>前往：前往某处。</summary>
        Go = 2
    }

    /// <summary>
    /// 某个卡牌当前可切换的行动，以及该行动需要几个额外卡槽。
    /// 由 <see cref="CaseSession.GetActions"/> 从配方表反推得出，不需要在卡牌上手工配置。
    /// </summary>
    public readonly struct ActionOption
    {
        public ActionOption(ActionKind action, int contextSlotCount, bool isAvailable, string unavailableReason)
        {
            Action = action;
            ContextSlotCount = contextSlotCount;
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason;
        }

        public ActionKind Action { get; }

        /// <summary>操作 UI 需要显示的附加卡槽数量（0 表示该行动不需要再拖卡）。</summary>
        public int ContextSlotCount { get; }

        /// <summary>前置情节（flag）是否已满足。未满足时按钮可显示但不可用。</summary>
        public bool IsAvailable { get; }

        /// <summary>不可用原因，用于 UI 提示。</summary>
        public string UnavailableReason { get; }
    }

    public static class ActionKindText
    {
        public static string ToChinese(this ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Investigate:
                    return "调查";
                case ActionKind.Talk:
                    return "交谈";
                case ActionKind.Go:
                    return "前往";
                default:
                    return kind.ToString();
            }
        }

        public static readonly ActionKind[] All =
        {
            ActionKind.Investigate,
            ActionKind.Talk,
            ActionKind.Go
        };
    }
}
