using System;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一个场景（地点）。桌面上「现在在哪儿」只影响两件事：背景、以及哪些配方可用。
    /// 第一章有 5 个：S00 警察局 / S01 砖瓦巷街道 / S02 私人俱乐部 / S03 青年读书沙龙 / S04 教堂。
    /// </summary>
    [Serializable]
    public sealed class SceneDefinition
    {
        /// <summary>场景 ID，例如 "S00"。</summary>
        public string Id;

        /// <summary>显示名，例如 "警察局"。</summary>
        public string DisplayName;

        /// <summary>背景图的美术资源键，可空（空则用默认桌面）。</summary>
        public string BackgroundArtKey;

        /// <summary>策划备注：这个场景里能查什么。</summary>
        public string Description;
    }
}
