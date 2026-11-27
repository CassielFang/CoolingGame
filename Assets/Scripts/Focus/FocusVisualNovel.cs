using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FocusTest
{
    /// <summary>
    /// 视觉小说里的一帧。
    /// 注意：这里存的是**导入文件里写的文本**，不是图片引用——图片在播放时才按名字去美术表里找。
    /// 这样编剧改名、换图都不用动剧本资产。
    ///
    /// 三条留空规则（背景 / 左立绘 / 右立绘 都一样）：
    ///   留空      = 沿用上一帧
    ///   -  或 无  = 这一帧把它清掉
    ///   其它文本  = 按名字去美术表里找
    /// </summary>
    [Serializable]
    public sealed class FocusVnFrame
    {
        [Tooltip("背景图名：Assets/Art/Test 下的文件名，带不带扩展名都行；留空 = 沿用上一帧")]
        public string Background = string.Empty;

        [Tooltip("左侧立绘名：立绘名表里登记的名字；留空 = 沿用上一帧，\"-\" = 清掉这一侧")]
        public string LeftPortrait = string.Empty;

        [Tooltip("右侧立绘名；规则同左侧")]
        public string RightPortrait = string.Empty;

        [Tooltip("说话人；留空表示旁白")]
        public string Speaker = string.Empty;

        [TextArea(2, 5)]
        public string Line = string.Empty;

        [Tooltip("这一句显示之前触发（断点事件，可挂多个调用）")]
        public UnityEvent BeforeLine = new UnityEvent();

        [Tooltip("这一句显示之后触发")]
        public UnityEvent AfterLine = new UnityEvent();
    }

    /// <summary>
    /// 中断事件：剧本写完之后，在导入好的资产上手动插一条。
    /// 播完 AfterFrame 这一帧（0 基，-1 表示开场前）就暂停，把 EventKey 抛给外部。
    /// 与帧上的 UnityEvent 的区别：UnityEvent 立刻执行、不暂停；这里会停下来等 Resume。
    /// </summary>
    [Serializable]
    public class FocusVnInterrupt
    {
        [Tooltip("播完第几帧后触发（0 基）；-1 = 开场前")]
        public int AfterFrame = -1;

        [Tooltip("给外部认的键名，例如 give_card / play_clock_sound")]
        public string EventKey = string.Empty;

        [Tooltip("给自己看的备注")]
        public string Note = string.Empty;
    }

    /// <summary>
    /// 一组连续的视觉小说。xlsx 批量导入生成，中断事件与逐帧事件由人工在 Inspector 里补。
    /// </summary>
    [CreateAssetMenu(fileName = "VisualNovel", menuName = "Focus Lab/Visual Novel")]
    public sealed class FocusVisualNovel : ScriptableObject
    {
        [SerializeField] private string _novelId = string.Empty;

        [SerializeField] private List<FocusVnFrame> _frames = new List<FocusVnFrame>();

        [SerializeField] private List<FocusVnInterrupt> _interrupts = new List<FocusVnInterrupt>();

        public string NovelId => _novelId;

        public IReadOnlyList<FocusVnFrame> Frames => _frames;

        public IReadOnlyList<FocusVnInterrupt> Interrupts => _interrupts;

        public int FrameCount => _frames.Count;

        public int InterruptCount => _interrupts.Count;

        /// <summary>这一帧播完之后要触发的中断（可能不止一条）。</summary>
        public void CollectInterrupts(int frameIndex, List<FocusVnInterrupt> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            for (int i = 0; i < _interrupts.Count; i++)
            {
                FocusVnInterrupt interrupt = _interrupts[i];
                if (interrupt != null && interrupt.AfterFrame == frameIndex)
                {
                    into.Add(interrupt);
                }
            }
        }

        /// <summary>这一帧上挂了几条中断（窗口里显示用）。</summary>
        public int CountInterruptsAt(int frameIndex)
        {
            int count = 0;
            for (int i = 0; i < _interrupts.Count; i++)
            {
                if (_interrupts[i] != null && _interrupts[i].AfterFrame == frameIndex)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>剧本里出现过的所有立绘名（含括号说明的原文），窗口里用来提示未绑定。</summary>
        public void CollectPortraitNames(List<string> into)
        {
            if (into == null)
            {
                return;
            }

            for (int i = 0; i < _frames.Count; i++)
            {
                FocusVnFrame frame = _frames[i];
                if (frame == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(frame.LeftPortrait) && !into.Contains(frame.LeftPortrait))
                {
                    into.Add(frame.LeftPortrait);
                }

                if (!string.IsNullOrEmpty(frame.RightPortrait) && !into.Contains(frame.RightPortrait))
                {
                    into.Add(frame.RightPortrait);
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>批量导入时重建内容；帧上的 UnityEvent 与组级中断尽量保留。</summary>
        public void Setup(string novelId, List<FocusVnFrame> frames)
        {
            _novelId = novelId;

            // 旧帧上挂的事件按「序号」搬过来，免得每次导入都得重新连一遍。
            var previousEvents = new List<(UnityEvent Before, UnityEvent After)>();
            for (int i = 0; i < _frames.Count; i++)
            {
                FocusVnFrame old = _frames[i];
                previousEvents.Add(old == null
                    ? (null, null)
                    : (old.BeforeLine, old.AfterLine));
            }

            _frames = frames ?? new List<FocusVnFrame>();

            for (int i = 0; i < _frames.Count && i < previousEvents.Count; i++)
            {
                if (_frames[i] == null)
                {
                    _frames[i] = new FocusVnFrame();
                }

                (UnityEvent before, UnityEvent after) = previousEvents[i];
                if (before != null)
                {
                    _frames[i].BeforeLine = before;
                }

                if (after != null)
                {
                    _frames[i].AfterLine = after;
                }
            }

            // 帧数变了就丢掉越界的中断，免得播到一半抛空。
            for (int i = _interrupts.Count - 1; i >= 0; i--)
            {
                FocusVnInterrupt interrupt = _interrupts[i];
                if (interrupt == null || interrupt.AfterFrame >= _frames.Count)
                {
                    _interrupts.RemoveAt(i);
                }
            }
        }

        public void RemoveFrameAt(int index)
        {
            if (index < 0 || index >= _frames.Count)
            {
                return;
            }

            _frames.RemoveAt(index);

            // 帧删掉之后，后面的序号整体前移，挂在原位置的断点跟着挪；越界的直接丢掉。
            for (int i = _interrupts.Count - 1; i >= 0; i--)
            {
                FocusVnInterrupt interrupt = _interrupts[i];
                if (interrupt == null)
                {
                    _interrupts.RemoveAt(i);
                    continue;
                }

                if (interrupt.AfterFrame > index)
                {
                    interrupt.AfterFrame--;
                }
                else if (interrupt.AfterFrame < -1 || interrupt.AfterFrame >= _frames.Count)
                {
                    _interrupts.RemoveAt(i);
                }
            }
        }

        public void RemoveInterruptAt(int index)
        {
            if (index >= 0 && index < _interrupts.Count)
            {
                _interrupts.RemoveAt(index);
            }
        }

        public void AddInterrupt(FocusVnInterrupt interrupt)
        {
            if (interrupt != null)
            {
                _interrupts.Add(interrupt);
            }
        }
#endif
    }
}
