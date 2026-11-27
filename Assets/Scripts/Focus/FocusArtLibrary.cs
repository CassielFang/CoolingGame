using System;
using System.Collections.Generic;
using UnityEngine;

namespace FocusTest
{
    /// <summary>
    /// 「立绘名 → 图片」的一条绑定。编剧在表格里写「实例（暗）」，这里登记「实例」即可。
    /// </summary>
    [Serializable]
    public sealed class FocusPortraitEntry
    {
        public string Name = string.Empty;

        public Sprite Sprite;
    }

    /// <summary>美术索引的一条：文件名（不带扩展名）→ 图片。背景就用这个查。</summary>
    [Serializable]
    public sealed class FocusSpriteEntry
    {
        public string Name = string.Empty;

        public Sprite Sprite;
    }

    /// <summary>
    /// 视觉小说用的美术表。
    /// 剧本资产里只存文本（立绘名 / 背景名），播放时才通过这里换成图片——
    /// 换图、改图名都只动这张表，不用重新导入剧本。
    ///
    /// 两张表：
    ///   Portraits 立绘名表（人工维护，编剧写什么就登记什么）
    ///   Sprites   美术索引（由 Tools/视觉小说/刷新美术索引 自动扫描 Assets/Art/Test 生成）
    /// </summary>
    [CreateAssetMenu(fileName = "FocusArtLibrary", menuName = "Focus Lab/Art Library")]
    public sealed class FocusArtLibrary : ScriptableObject
    {
        /// <summary>文本里代表「把这一侧清掉」的写法。</summary>
        private static readonly string[] ClearTokens = { "-", "—", "无", "none", "clear" };

        [SerializeField] private List<FocusPortraitEntry> _portraits = new List<FocusPortraitEntry>();

        [SerializeField] private List<FocusSpriteEntry> _sprites = new List<FocusSpriteEntry>();

        public IReadOnlyList<FocusPortraitEntry> Portraits => _portraits;

        public IReadOnlyList<FocusSpriteEntry> Sprites => _sprites;

        /// <summary>这批文本是不是「清空这一侧」。</summary>
        public static bool IsClearToken(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            string text = raw.Trim();
            for (int i = 0; i < ClearTokens.Length; i++)
            {
                if (string.Equals(text, ClearTokens[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>「实例（暗）」→ 立绘名「实例」，说明「暗」。</summary>
        public static string SplitNote(string raw, out string note)
        {
            note = string.Empty;
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string text = raw.Trim();
            int open = text.IndexOfAny(new[] { '（', '(' });
            if (open <= 0)
            {
                return text;
            }

            int closeFull = text.LastIndexOf('）');
            int closeHalf = text.LastIndexOf(')');
            int close = Mathf.Max(closeFull, closeHalf);
            if (close > open)
            {
                note = text.Substring(open + 1, close - open - 1).Trim();
            }

            return text.Substring(0, open).Trim();
        }

        /// <summary>背景名统一去掉扩展名，方便「bg_lab」与「bg_lab.png」混着写。</summary>
        public static string SpriteKey(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string text = raw.Trim();
            int dot = text.LastIndexOf('.');
            return dot > 0 ? text.Substring(0, dot) : text;
        }

        /// <summary>按立绘名找图；找不到时退化成正按文件名找，最后才认失败。</summary>
        public bool TryResolvePortrait(string raw, out Sprite sprite, out string key)
        {
            key = SplitNote(raw, out _);
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (int i = 0; i < _portraits.Count; i++)
            {
                FocusPortraitEntry entry = _portraits[i];
                if (entry != null && string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    sprite = entry.Sprite;
                    return sprite != null;
                }
            }

            if (TryResolveSprite(key, out sprite, out _))
            {
                return true;
            }

            return false;
        }

        /// <summary>按文件名找背景 / 图片。</summary>
        public bool TryResolveSprite(string raw, out Sprite sprite, out string key)
        {
            key = SpriteKey(raw);
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (int i = 0; i < _sprites.Count; i++)
            {
                FocusSpriteEntry entry = _sprites[i];
                if (entry != null && string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    sprite = entry.Sprite;
                    return sprite != null;
                }
            }

            return false;
        }

        /// <summary>按文件名找背景（允许带扩展名，也允许不带）。</summary>
        public bool TryResolveBackground(string raw, out Sprite sprite, out string key)
        {
            return TryResolveSprite(raw, out sprite, out key);
        }

        /// <summary>这条立绘名有没有登记过（哪怕图片还空着）。</summary>
        public bool HasPortraitEntry(string name)
        {
            for (int i = 0; i < _portraits.Count; i++)
            {
                FocusPortraitEntry entry = _portraits[i];
                if (entry != null && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void SetSprites(List<FocusSpriteEntry> sprites)
        {
            _sprites = sprites ?? new List<FocusSpriteEntry>();
        }

        public FocusPortraitEntry AddPortrait(string name)
        {
            var entry = new FocusPortraitEntry { Name = name ?? string.Empty };
            _portraits.Add(entry);
            return entry;
        }

        public void RemovePortraitAt(int index)
        {
            if (index >= 0 && index < _portraits.Count)
            {
                _portraits.RemoveAt(index);
            }
        }

        public void ResetPortraits()
        {
            _portraits.Clear();
        }
#endif
    }
}
