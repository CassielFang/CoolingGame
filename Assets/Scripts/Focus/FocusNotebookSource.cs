using System;
using UnityEngine;

namespace FocusTest
{
    /// <summary>
    /// 笔记本的入口：挂在桌上的笔记本道具上，按钮点一下就让笔记本打开。
    /// 道具本身随桌上的其它物件一起被抬起的笔记本挡住，所以不再处理「笔记本开着时不能点」。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusNotebookSource : MonoBehaviour
    {
        /// <summary>点开了笔记本。</summary>
        public event Action Opening;

        public FocusNotebookPanel Notebook { get; private set; }

        [SerializeField] private FocusNotebookPanel _notebook;

        private void Awake()
        {
            if (_notebook != null)
            {
                Notebook = _notebook;
            }

            CacheNotebook();
        }

        public void Open()
        {
            FocusNotebookPanel notebook = CacheNotebook();
            if (notebook == null)
            {
                Debug.LogWarning("[FocusNotebook] 场景里没有 FocusNotebookPanel，笔记本打不开");
                return;
            }

            Opening?.Invoke();
            notebook.Open();
        }

        public void Close()
        {
            CacheNotebook()?.Close();
        }

        private FocusNotebookPanel CacheNotebook()
        {
            if (Notebook == null)
            {
                Notebook = FindAnyObjectByType<FocusNotebookPanel>();
            }

            return Notebook;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_notebook == null)
            {
                _notebook = FindAnyObjectByType<FocusNotebookPanel>();
            }

            Notebook = _notebook;
        }
#endif

#if UNITY_EDITOR
        public void EditorBind(FocusNotebookPanel notebook)
        {
            _notebook = notebook;
            Notebook = notebook;
        }
#endif
    }
}
