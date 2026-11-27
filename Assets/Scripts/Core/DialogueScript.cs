using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 一句剧情台词。左右立绘用美术键，空字符串表示这一侧没人。
    /// 说话人那一侧亮、另一侧暗（策划表里的「（暗）」）。
    /// </summary>
    [Serializable]
    public sealed class DialogueLine
    {
        /// <summary>左侧立绘美术键，例如 "char/mp01_default"。</summary>
        public string LeftArtKey = string.Empty;

        /// <summary>右侧立绘美术键。</summary>
        public string RightArtKey = string.Empty;

        /// <summary>左侧是否变暗（不是说话方）。</summary>
        public bool LeftDimmed;

        /// <summary>右侧是否变暗。</summary>
        public bool RightDimmed;

        /// <summary>说话人显示名，例如「莫里森」「旁白」「信件文本」。</summary>
        public string Speaker = string.Empty;

        /// <summary>台词正文。</summary>
        public string Text = string.Empty;
    }

    /// <summary>
    /// 一段剧情（策划表的「脚本ID」），按顺序逐句播放。
    /// </summary>
    [Serializable]
    public sealed class DialogueScript
    {
        public string Id;

        public string Title;

        public List<DialogueLine> Lines = new List<DialogueLine>();

        public DialogueScript()
        {
        }

        public DialogueScript(string id, string title, params DialogueLine[] lines)
        {
            Id = id;
            Title = title;
            if (lines != null && lines.Length > 0)
            {
                Lines.AddRange(lines);
            }
        }
    }
}
