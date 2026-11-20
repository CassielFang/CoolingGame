using System;

namespace CoolingGame.Core
{
    /// <summary>卡牌当前所在的逻辑区域。屏幕坐标属于表现层，不进 Core。</summary>
    public enum CardZone
    {
        /// <summary>手牌区（桌面下方聚集处）。</summary>
        Hand = 0,

        /// <summary>已摆到桌面上。</summary>
        Board = 1,

        /// <summary>被行动消耗掉，移出牌桌。</summary>
        Consumed = 2
    }

    /// <summary>一张卡牌的运行时状态。</summary>
    public sealed class CardState
    {
        internal CardState(CardDefinition definition, CardZone zone, bool isFaceUp)
        {
            Definition = definition;
            Zone = zone;
            IsFaceUp = isFaceUp;
            IsNew = !isFaceUp;
        }

        public CardDefinition Definition { get; }

        public string Id
        {
            get { return Definition.Id; }
        }

        public CardZone Zone { get; internal set; }

        /// <summary>是否已翻开。false = 背面朝上，等待玩家单击翻开。</summary>
        public bool IsFaceUp { get; internal set; }

        /// <summary>是否是新到手的牌（用于翻牌动效/提示）。</summary>
        public bool IsNew { get; internal set; }

        /// <summary>获得顺序。</summary>
        public int ArrivalOrder { get; internal set; }

        public bool IsConsumed
        {
            get { return Zone == CardZone.Consumed; }
        }

        public bool IsUsable
        {
            get { return IsFaceUp && Zone != CardZone.Consumed; }
        }
    }
}
