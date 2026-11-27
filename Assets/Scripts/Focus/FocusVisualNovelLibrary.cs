using System.Collections.Generic;
using UnityEngine;

namespace FocusTest
{
    /// <summary>
    /// 视觉小说目录：播放器只认这一个资产，导入脚本时刷新它，场景里的引用不用改。
    /// </summary>
    [CreateAssetMenu(fileName = "VisualNovelLibrary", menuName = "Focus Lab/Visual Novel Library")]
    public sealed class FocusVisualNovelLibrary : ScriptableObject
    {
        [SerializeField] private List<FocusVisualNovel> _novels = new List<FocusVisualNovel>();

        public IReadOnlyList<FocusVisualNovel> Novels => _novels;

        public int Count => _novels.Count;

        /// <summary>按组 id 找一组对话，找不到返回 null。</summary>
        public FocusVisualNovel Find(string novelId)
        {
            if (string.IsNullOrEmpty(novelId))
            {
                return null;
            }

            for (int i = 0; i < _novels.Count; i++)
            {
                FocusVisualNovel novel = _novels[i];
                if (novel != null && novel.NovelId == novelId)
                {
                    return novel;
                }
            }

            return null;
        }

        public IReadOnlyList<string> NovelIds
        {
            get
            {
                var ids = new List<string>(_novels.Count);
                for (int i = 0; i < _novels.Count; i++)
                {
                    if (_novels[i] != null)
                    {
                        ids.Add(_novels[i].NovelId);
                    }
                }

                return ids;
            }
        }

#if UNITY_EDITOR
        /// <summary>导入器用：整体替换目录内容。</summary>
        public void SetNovels(List<FocusVisualNovel> novels)
        {
            _novels.Clear();

            if (novels != null)
            {
                for (int i = 0; i < novels.Count; i++)
                {
                    if (novels[i] != null)
                    {
                        _novels.Add(novels[i]);
                    }
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>把一组从目录里拿掉（不删资产文件）。</summary>
        public bool Remove(FocusVisualNovel novel)
        {
            if (novel == null)
            {
                return false;
            }

            bool removed = _novels.Remove(novel);
            if (removed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }

            return removed;
        }
#endif
    }
}
