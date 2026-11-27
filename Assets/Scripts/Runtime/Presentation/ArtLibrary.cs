using System.Collections.Generic;
using UnityEngine;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 美术资源入口：按约定路径从 Resources 取图，找不到就返回 null。
    ///
    /// 约定（美术交付 → 放进 Assets/Resources/Art/）：
    /// - Art/desk              桌面背景（16:9）
    /// - Art/ui/card_back      卡背
    /// - Art/ui/action_panel   行动面板（九宫格）
    /// - Art/ui/dialog         对话框（九宫格，留给视觉小说演出）
    /// - Art/char/xxx          角色立绘；卡牌的 ArtKey 填 "char/xxx" 即可
    ///
    /// 有图就用图，没有就回落到程序化的占位样式——所以素材可以一张一张地接，
    /// 不会出现"图没到就白屏"。
    /// </summary>
    public static class ArtLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Desk
        {
            get { return Load("Art/desk"); }
        }

        public static Sprite CardBack
        {
            get { return Load("Art/ui/card_back"); }
        }

        public static Sprite ActionPanel
        {
            get { return Load("Art/ui/action_panel"); }
        }

        public static Sprite Dialog
        {
            get { return Load("Art/ui/dialog"); }
        }

        public static Sprite NotebookCover
        {
            get { return Load("Art/ui/notebook_cover"); }
        }

        public static Sprite Newspaper
        {
            get { return Load("Art/ui/newspaper"); }
        }

        /// <summary>卡牌的 ArtKey（例如 "char/p07_salon"），空则返回 null。</summary>
        public static Sprite LoadCardArt(string artKey)
        {
            if (string.IsNullOrEmpty(artKey))
            {
                return null;
            }

            return Load("Art/" + artKey);
        }

        public static Sprite Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            Sprite cached;
            if (Cache.TryGetValue(resourcePath, out cached))
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            Cache[resourcePath] = sprite;
            return sprite;
        }
    }
}
