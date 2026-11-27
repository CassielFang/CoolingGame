using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 视觉小说播放器：读一组 FocusVisualNovel，把每帧的背景、左右立绘、台词铺到屏幕上。
    /// 点一下画面推进一帧；遇到中断事件就停住，等外部调 Resume()。
    ///
    /// 对外入口（给别的系统调）：
    ///   FocusVnDirector.Play("prologue_01");
    ///   FocusVnDirector.Advance();
    ///   FocusVnDirector.Resume();
    /// 或者直接拿组件：FocusVisualNovelPlayer.Instance。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusVisualNovelPlayer : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _background;
        [SerializeField] private FocusVnPortrait _leftPortrait;
        [SerializeField] private FocusVnPortrait _rightPortrait;
        [SerializeField] private FocusSubtitlePanel _dialogue;
        [SerializeField] private Button _catcher;
        [SerializeField] private FocusVisualNovelLibrary _library;
        [SerializeField] private FocusArtLibrary _artLibrary;

        [Header("默认值")]
        [SerializeField] private Color _emptyBackgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.96f);
        [SerializeField] private Color _leftTint = new Color(0.86f, 0.93f, 1f, 1f);
        [SerializeField] private Color _rightTint = new Color(1f, 0.9f, 0.84f, 1f);

        private readonly List<FocusVnInterrupt> _pendingInterrupts = new List<FocusVnInterrupt>();
        private readonly HashSet<string> _missingWarned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private FocusVisualNovel _novel;
        private int _index = -1;
        private bool _playing;
        private bool _waitingForResume;
        private Sprite _currentBackground;
        private Sprite _currentLeft;
        private Sprite _currentRight;

        public static FocusVisualNovelPlayer Instance { get; private set; }

        public event Action<FocusVisualNovel> Started;

        public event Action<int, FocusVnFrame> FrameShown;

        public event Action<FocusVnInterrupt> Interrupted;

        public event Action Resumed;

        public event Action<FocusVisualNovel> Completed;

        public bool IsPlaying => _playing;

        public bool IsWaitingForResume => _waitingForResume;

        public FocusVisualNovel Novel => _novel;

        public int FrameIndex => _index;

        public FocusVnFrame? CurrentFrame
        {
            get
            {
                if (_novel == null || _index < 0 || _index >= _novel.FrameCount)
                {
                    return null;
                }

                return _novel.Frames[_index];
            }
        }

        public FocusSubtitlePanel Dialogue => _dialogue;

        public FocusVisualNovelLibrary Library => _library;

        public FocusArtLibrary ArtLibrary => _artLibrary;

        /// <summary>美术表；场景里由 FocusLabSceneBuilder 绑好。</summary>
        public FocusArtLibrary ResolveArtLibrary()
        {
            return _artLibrary;
        }

        public IReadOnlyList<FocusVisualNovel> Novels =>
            _library != null ? (IReadOnlyList<FocusVisualNovel>)_library.Novels : System.Array.Empty<FocusVisualNovel>();

        private bool _ready;

        private void Awake()
        {
            Instance = this;
            EnsureReady();
        }

        /// <summary>
        /// 幂等的初始化。进播放模式时 Awake 有可能没赶上（域重载/对象重建），
        /// 所以外部拿实例前都先调一次，避免点击推进的监听没挂上。
        /// </summary>
        public void EnsureReady()
        {
            Instance = this;
            if (_ready)
            {
                return;
            }

            _ready = true;
            CacheRefs();
            if (_catcher != null)
            {
                _catcher.onClick.RemoveListener(Advance);
                _catcher.onClick.AddListener(Advance);
            }

            if (_dialogue != null)
            {
                _dialogue.Clicked -= Advance;
                _dialogue.Clicked += Advance;
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_catcher != null)
            {
                _catcher.onClick.RemoveListener(Advance);
            }

            if (_dialogue != null)
            {
                _dialogue.Clicked -= Advance;
            }
        }

        /// <summary>按 id 找一组视觉小说并开始播。</summary>
        public bool PlayById(string novelId)
        {
            FocusVisualNovel novel = FindNovel(novelId);
            if (novel == null)
            {
                Debug.LogWarning("[FocusVn] 没有找到 id 为「" + novelId + "」的视觉小说");
                return false;
            }

            Play(novel);
            return true;
        }

        public void Play(FocusVisualNovel novel)
        {
            CacheRefs();

            if (novel == null || novel.FrameCount == 0)
            {
                Debug.LogWarning("[FocusVn] 这组视觉小说没有内容");
                return;
            }

            _novel = novel;
            _index = -1;
            _playing = true;
            _waitingForResume = false;
            _currentBackground = null;
            _currentLeft = null;
            _currentRight = null;
            _missingWarned.Clear();

            ShowScreen(true);
            _leftPortrait?.Hide();
            _rightPortrait?.Hide();

            Started?.Invoke(novel);

            // 开场前的中断（AfterFrame = -1）。
            if (TryRaiseInterrupts(-1))
            {
                return;
            }

            Advance();
        }

        /// <summary>推进一帧；等中断时点画面不会跳帧。</summary>
        public void Advance()
        {
            if (!_playing || _waitingForResume)
            {
                return;
            }

            int next = _index + 1;
            if (_novel == null || next >= _novel.FrameCount)
            {
                Finish();
                return;
            }

            _index = next;
            ApplyFrame(_novel.Frames[_index]);

            if (TryRaiseInterrupts(_index))
            {
                return;
            }
        }

        /// <summary>外部处理完中断后调用，接着往下播。</summary>
        public void Resume()
        {
            if (!_playing || !_waitingForResume)
            {
                return;
            }

            _waitingForResume = false;
            Resumed?.Invoke();
            Advance();
        }

        public void Stop()
        {
            if (!_playing)
            {
                return;
            }

            _playing = false;
            _waitingForResume = false;
            HideImmediate();
        }

        public void HideImmediate()
        {
            CacheRefs();

            _playing = false;
            _waitingForResume = false;
            _index = -1;

            ShowScreen(false);
            _leftPortrait?.HideImmediate();
            _rightPortrait?.HideImmediate();
            _dialogue?.HideImmediate();
        }

        public FocusVisualNovel FindNovel(string novelId)
        {
            if (string.IsNullOrEmpty(novelId) || _library == null)
            {
                return null;
            }

            return _library.Find(novelId);
        }

        private void ApplyFrame(FocusVnFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            // 断点事件：台词出来之前。
            frame.BeforeLine?.Invoke();

            ApplyArtwork(frame);

            if (_background != null)
            {
                _background.sprite = _currentBackground;
                _background.color = _currentBackground != null ? Color.white : _emptyBackgroundColor;
            }

            // 说话的那一侧亮一点，另一侧稍微退后——先按下说话人是否匹配卡名判断不了，
            // 所以这里只做「有图就展示」，左右亮度差留给以后接剧本字段。
            if (_leftPortrait != null)
            {
                if (_currentLeft != null)
                {
                    _leftPortrait.Show(_currentLeft, _leftTint, dimmed: false);
                }
                else
                {
                    _leftPortrait.Hide();
                }
            }

            if (_rightPortrait != null)
            {
                if (_currentRight != null)
                {
                    _rightPortrait.Show(_currentRight, _rightTint, dimmed: false);
                }
                else
                {
                    _rightPortrait.Hide();
                }
            }

            if (_dialogue != null)
            {
                _dialogue.ShowLine(frame.Speaker, frame.Line);
            }

            FrameShown?.Invoke(_index, frame);

            // 断点事件：台词出来之后。
            frame.AfterLine?.Invoke();
        }

        /// <summary>
        /// 按文本换图：留空沿用上一帧，"-"/"无" 清空，其它按立绘名/背景名去美术表里找。
        /// 找不到只打一条 warning，不影响播放。
        /// </summary>
        private void ApplyArtwork(FocusVnFrame frame)
        {
            FocusArtLibrary art = _artLibrary;

            if (!string.IsNullOrEmpty(frame.Background))
            {
                if (FocusArtLibrary.IsClearToken(frame.Background))
                {
                    _currentBackground = null;
                }
                else if (art != null && art.TryResolveBackground(frame.Background, out Sprite background, out string key))
                {
                    _currentBackground = background;
                }
                else
                {
                    WarnMissing(art, "背景", frame.Background);
                }
            }

            if (!string.IsNullOrEmpty(frame.LeftPortrait))
            {
                if (FocusArtLibrary.IsClearToken(frame.LeftPortrait))
                {
                    _currentLeft = null;
                }
                else if (art != null && art.TryResolvePortrait(frame.LeftPortrait, out Sprite left, out string key))
                {
                    _currentLeft = left;
                }
                else
                {
                    WarnMissing(art, "左侧立绘", frame.LeftPortrait);
                }
            }

            if (!string.IsNullOrEmpty(frame.RightPortrait))
            {
                if (FocusArtLibrary.IsClearToken(frame.RightPortrait))
                {
                    _currentRight = null;
                }
                else if (art != null && art.TryResolvePortrait(frame.RightPortrait, out Sprite right, out string key))
                {
                    _currentRight = right;
                }
                else
                {
                    WarnMissing(art, "右侧立绘", frame.RightPortrait);
                }
            }
        }

        /// <summary>同一个名字只警告一次，免得一长段剧本刷满控制台。</summary>
        private void WarnMissing(FocusArtLibrary art, string side, string raw)
        {
            string name = FocusArtLibrary.SplitNote(raw, out _);
            if (!_missingWarned.Add(side + "|" + name))
            {
                return;
            }

            if (art == null)
            {
                Debug.LogWarning("[FocusVn] 场景里没有绑美术表，没法解析" + side + "「" + raw + "」");
                return;
            }

            Debug.LogWarning("[FocusVn] 找不到" + side + "「" + name + "」对应的图片"
                             + (art.HasPortraitEntry(name) ? "（立绘名表里有这一条，但图片还是空的）" : "（立绘名表里没登记这个名字）"));
        }

        private bool TryRaiseInterrupts(int afterFrame)
        {
            if (_novel == null)
            {
                return false;
            }

            _novel.CollectInterrupts(afterFrame, _pendingInterrupts);
            if (_pendingInterrupts.Count == 0)
            {
                return false;
            }

            _waitingForResume = true;

            // 一次只抛一条：多条挂在同一帧时，外部每次 Resume 后再抛下一条。
            FocusVnInterrupt interrupt = _pendingInterrupts[0];
            _pendingInterrupts.RemoveAt(0);
            Interrupted?.Invoke(interrupt);

            if (!_waitingForResume)
            {
                return false;
            }

            return true;
        }

        private void Finish()
        {
            FocusVisualNovel finished = _novel;
            _playing = false;
            _waitingForResume = false;
            _index = -1;

            if (_dialogue != null)
            {
                _dialogue.AdvanceOnClick = false;
                _dialogue.Dismiss();
            }

            ShowScreen(false);
            _leftPortrait?.Hide();
            _rightPortrait?.Hide();

            Completed?.Invoke(finished);
        }

        private void ShowScreen(bool visible)
        {
            // 只靠 CanvasGroup 显隐，不关 GameObject：
            // 播放器可能就挂在同一棵树里，关掉会把自己也停掉。
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = visible;
                _group.interactable = visible;
            }

            if (_background != null)
            {
                _background.enabled = visible;
            }

            if (_catcher != null)
            {
                _catcher.gameObject.SetActive(visible);
            }

            if (_dialogue != null)
            {
                _dialogue.AdvanceOnClick = visible;
            }
        }

        private void CacheRefs()
        {
            if (_root == null)
            {
                _root = transform as RectTransform;
            }

            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }
        }

#if UNITY_EDITOR
        public void EditorBind(
            RectTransform root,
            CanvasGroup group,
            Image background,
            FocusVnPortrait leftPortrait,
            FocusVnPortrait rightPortrait,
            FocusSubtitlePanel dialogue,
            Button catcher,
            FocusVisualNovelLibrary library,
            FocusArtLibrary artLibrary)
        {
            _root = root;
            _group = group;
            _background = background;
            _leftPortrait = leftPortrait;
            _rightPortrait = rightPortrait;
            _dialogue = dialogue;
            _catcher = catcher;
            _library = library;
            _artLibrary = artLibrary;
        }
#endif
    }

    /// <summary>给别的系统用的一层静态入口，免得处处 Find。</summary>
    public static class FocusVnDirector
    {
        public static bool IsPlaying => Resolve() != null && Resolve().IsPlaying;

        public static bool Play(string novelId)
        {
            FocusVisualNovelPlayer player = Resolve();
            if (player == null)
            {
                Debug.LogWarning("[FocusVn] 场景里没有 FocusVisualNovelPlayer");
                return false;
            }

            return player.PlayById(novelId);
        }

        public static void Advance()
        {
            Resolve()?.Advance();
        }

        public static void Resume()
        {
            Resolve()?.Resume();
        }

        public static void Stop()
        {
            Resolve()?.Stop();
        }

        /// <summary>Instance 可能因为 Awake 没跑到而为空，这里兜底查一次并补齐初始化。</summary>
        private static FocusVisualNovelPlayer Resolve()
        {
            FocusVisualNovelPlayer player = FocusVisualNovelPlayer.Instance;
            if (player == null)
            {
                player = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
            }

            if (player != null)
            {
                player.EnsureReady();
            }

            return player;
        }
    }
}
