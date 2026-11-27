using System;
using System.Collections.Generic;

namespace CoolingGame.Core
{
    /// <summary>
    /// 问题页的集合：一局案件里可能同时有多张页（第一章 Q01–Q05），
    /// 页会随阶段被添加/移除。每张页自己记判定，这里只管「有哪些页、当前看哪张」。
    /// </summary>
    public sealed class QuestionBookModel
    {
        private readonly List<QuestionPageModel> _pages = new List<QuestionPageModel>();

        public event Action Changed;

        public event Action<QuestionPageModel> PageAdded;

        public event Action<QuestionPageModel> PageRemoved;

        public IReadOnlyList<QuestionPageModel> Pages
        {
            get { return _pages; }
        }

        public int Count
        {
            get { return _pages.Count; }
        }

        /// <summary>第一张页（旧案件即唯一一页；笔记本默认翻到这里）。</summary>
        public QuestionPageModel Primary
        {
            get { return _pages.Count > 0 ? _pages[0] : null; }
        }

        /// <summary>最后一张页（第一章里即 Q05 综合页）。</summary>
        public QuestionPageModel Last
        {
            get { return _pages.Count > 0 ? _pages[_pages.Count - 1] : null; }
        }

        public QuestionPageModel Find(string pageId)
        {
            if (string.IsNullOrEmpty(pageId))
            {
                return null;
            }

            for (int i = 0; i < _pages.Count; i++)
            {
                if (_pages[i].Id == pageId)
                {
                    return _pages[i];
                }
            }

            return null;
        }

        public bool Contains(string pageId)
        {
            return Find(pageId) != null;
        }

        /// <summary>添加一页；已存在同 ID 时返回已有页。</summary>
        public QuestionPageModel Add(QuestionPageDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            QuestionPageModel existing = Find(definition.Id);
            if (existing != null)
            {
                return existing;
            }

            return Add(new QuestionPageModel(definition));
        }

        /// <summary>直接接管一个已构造好的页（旧案件的五槽页走这里）。</summary>
        public QuestionPageModel Add(QuestionPageModel page)
        {
            if (page == null)
            {
                return null;
            }

            QuestionPageModel existing = Find(page.Id);
            if (existing != null)
            {
                return existing;
            }

            page.Changed += OnPageChanged;
            _pages.Add(page);
            PageAdded?.Invoke(page);
            Changed?.Invoke();
            return page;
        }

        public bool Remove(string pageId)
        {
            QuestionPageModel page = Find(pageId);
            if (page == null)
            {
                return false;
            }

            page.Changed -= OnPageChanged;
            _pages.Remove(page);
            PageRemoved?.Invoke(page);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Changed -= OnPageChanged;
            }

            _pages.Clear();
            Changed?.Invoke();
        }

        private void OnPageChanged()
        {
            Changed?.Invoke();
        }
    }
}
