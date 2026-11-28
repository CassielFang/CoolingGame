using CoolingGame.Presentation.ArtTuning;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools.ArtTuning
{
    /// <summary>
    /// 美术调试窗口的一个子选项卡。每新增一个调试对象就实现一个页面，
    /// 在 ArtTuningWindow.PageFactories 里注册一行即可，窗口本身不用动。
    /// 窗口对具体的参数一无所知：页面自己决定改哪些字段、怎么排版。
    ///
    /// 「保存 / 读取 / 丢弃」三个按钮是窗口统一提供的，页面只负责改工作副本。
    /// </summary>
    internal interface IArtTuningPage
    {
        /// <summary>选项卡标题。</summary>
        string Title { get; }

        /// <summary>第一次打开这个窗口时调用一次（可以在这里扫描素材）。</summary>
        void OnOpened(ArtTuningSettings settings);

        /// <summary>页面内容（已经在一个全宽的 ScrollView 里）。返回 true 表示这一帧动了数据。</summary>
        void OnDraw(ArtTuningSettings working, ArtTuningPageContext context);
    }

    /// <summary>
    /// 页面共用的上下文。页面改的一律是「正在调」的工作副本（<see cref="Working"/>），
    /// 不是资产文件 —— 什么时候落进文件由窗口顶部的「保存」决定。
    ///
    /// 编辑态和 Play 态的差别集中在这里：
    /// - 编辑态：改工作副本时记 Undo，Ctrl+Z 可回退；
    /// - Play 中：改的也是同一个工作副本，游戏下一帧就读到，所以当场生效；
    ///   退出 Play 时 Unity 会重新加载域，工作副本会从资产重新读出来 —— 没保存的试改自动作废。
    /// </summary>
    internal sealed class ArtTuningPageContext
    {
        public ArtTuningSettings Working { get; set; }

        public bool IsPlaying
        {
            get { return Application.isPlaying; }
        }

        /// <summary>
        /// 改之前调一次（拖滑条时会每帧调，Undo 会合并成一步）。
        /// 只在编辑态记；Play 中改了不落盘，也不需要回退。
        /// </summary>
        public void Undo(string label)
        {
            if (Working == null || Application.isPlaying)
            {
                return;
            }

            UnityEditor.Undo.RecordObject(Working, label);
        }

        /// <summary>改完调一次：标脏，让 Unity 知道工作副本变了。</summary>
        public void Commit()
        {
            if (Working == null)
            {
                return;
            }

            EditorUtility.SetDirty(Working);
        }
    }
}
