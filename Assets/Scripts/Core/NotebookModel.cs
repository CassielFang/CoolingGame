using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>笔记本「线索页」里的一条记录。</summary>
    public sealed class ClueEntry
    {
        internal ClueEntry(string clueId, string text, string sourceCardId, string sourceCardName,
            IReadOnlyList<string> keywords, int sequence)
        {
            ClueId = clueId;
            Text = text;
            SourceCardId = sourceCardId;
            SourceCardName = sourceCardName;
            Keywords = keywords ?? Array.Empty<string>();
            Sequence = sequence;
            IsNew = true;
        }

        public string ClueId { get; }
        public string Text { get; }
        public string SourceCardId { get; }
        public string SourceCardName { get; }
        public IReadOnlyList<string> Keywords { get; }

        /// <summary>获得顺序，用于时间线排序。</summary>
        public int Sequence { get; }

        /// <summary>是否被玩家置顶。</summary>
        public bool Pinned { get; internal set; }

        /// <summary>是否是新获得、尚未查看（用于笔记本更新时的视觉提示）。</summary>
        public bool IsNew { get; internal set; }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            if (Contains(Text, query) || Contains(SourceCardName, query) || Contains(SourceCardId, query) || Contains(ClueId, query))
            {
                return true;
            }

            for (int i = 0; i < Keywords.Count; i++)
            {
                if (Contains(Keywords[i], query))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                   && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// 笔记本（策划草案「三、笔记本」）的数据模型。
    /// 只负责记录与检索；翻页、选项卡、动效由表现层实现。
    /// </summary>
    public sealed class NotebookModel
    {
        private readonly List<ClueEntry> _entries = new List<ClueEntry>();
        private int _sequence;

        public event Action<ClueEntry> ClueAdded;
        public event Action<ClueEntry> ClueChanged;

        /// <summary>按获得顺序排列的全部线索。</summary>
        public IReadOnlyList<ClueEntry> Clues
        {
            get { return _entries; }
        }

        public int Count
        {
            get { return _entries.Count; }
        }

        public int UnreadCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsNew)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool Contains(string clueId)
        {
            return Find(clueId) != null;
        }

        public ClueEntry Find(string clueId)
        {
            if (string.IsNullOrEmpty(clueId))
            {
                return null;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].ClueId == clueId)
                {
                    return _entries[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 写入一条线索。同 ID 已存在时返回 null（重复线索不会刷屏）。
        /// </summary>
        public ClueEntry AddClue(ClueDefinition definition, string sourceCardId = null, string sourceCardName = null)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
            {
                return null;
            }

            if (Contains(definition.Id))
            {
                return null;
            }

            var entry = new ClueEntry(
                definition.Id,
                definition.Text,
                string.IsNullOrEmpty(sourceCardId) ? definition.SourceCardId : sourceCardId,
                string.IsNullOrEmpty(sourceCardName) ? definition.SourceCardName : sourceCardName,
                definition.Keywords,
                _sequence++);

            _entries.Add(entry);
            ClueAdded?.Invoke(entry);
            return entry;
        }

        /// <summary>置顶/取消置顶。置顶的线索出现在线索页最上面。</summary>
        public bool TogglePin(string clueId)
        {
            ClueEntry entry = Find(clueId);
            if (entry == null)
            {
                return false;
            }

            entry.Pinned = !entry.Pinned;
            ClueChanged?.Invoke(entry);
            return true;
        }

        public void MarkRead(string clueId)
        {
            ClueEntry entry = Find(clueId);
            if (entry == null || !entry.IsNew)
            {
                return;
            }

            entry.IsNew = false;
            ClueChanged?.Invoke(entry);
        }

        public void MarkAllRead()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].IsNew)
                {
                    _entries[i].IsNew = false;
                    ClueChanged?.Invoke(_entries[i]);
                }
            }
        }

        /// <summary>线索页的显示顺序：置顶的在前，其余按获得顺序。</summary>
        public IReadOnlyList<ClueEntry> OrderedClues()
        {
            var pinned = new List<ClueEntry>();
            var normal = new List<ClueEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                ClueEntry entry = _entries[i];
                if (entry.Pinned)
                {
                    pinned.Add(entry);
                }
                else
                {
                    normal.Add(entry);
                }
            }

            pinned.AddRange(normal);
            return pinned;
        }

        /// <summary>关键词搜索：命中文本/来源/关键词，结果按获得顺序排列。</summary>
        public IReadOnlyList<ClueEntry> Search(string query)
        {
            var result = new List<ClueEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Matches(query))
                {
                    result.Add(_entries[i]);
                }
            }

            return result;
        }
    }
}
