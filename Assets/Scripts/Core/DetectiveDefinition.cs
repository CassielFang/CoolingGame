using System;

namespace CoolingGame.Core
{
    /// <summary>
    /// 办案人（主角）。他不是桌上的卡牌，而是「正在操作这些资料的人」——
    /// 立绘站在桌边，玩家切换他等于换一双眼睛。
    ///
    /// 配方用 <see cref="RecipeDefinition.RequiredDetectiveId"/> 指定「必须谁来办」：
    /// 莫里森擅长审讯和现场，雪莉擅长观察和文字比对，同一张卡换个人问，得到的东西不一样。
    /// </summary>
    [Serializable]
    public sealed class DetectiveDefinition
    {
        /// <summary>办案人 ID，例如 "MP01"。</summary>
        public string Id;

        /// <summary>显示名，例如 "莫里森警长"。</summary>
        public string DisplayName;

        /// <summary>立绘美术键（ArtLibrary 约定，例如 "char/mp01_default"）。</summary>
        public string PortraitArtKey;

        /// <summary>微笑表情的立绘键（剧情里用）。</summary>
        public string PortraitSmileKey;

        /// <summary>质问/严肃表情的立绘键（剧情里用）。</summary>
        public string PortraitQuestionKey;

        /// <summary>一句话人设，显示在立绘旁。</summary>
        public string Description;
    }
}
