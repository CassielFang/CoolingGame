using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>字幕类型：台本（剧情/叙述，点击才消失）与提示（辅助信息，到时间自动消失）。</summary>
    public enum FocusSubtitleKind
    {
        Script = 0,
        Hint = 1,
    }

    /// <summary>
    /// 屏幕下方的字幕条。两种字幕共用一块面板：
    /// <list type="bullet">
    /// <item>台本字幕：一直挂在屏幕上，直到玩家点击它才消失，不会被提示字幕顶掉。</item>
    /// <item>提示字幕：显示倒计时条，时间一到自动消失，也可以点掉。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusSubtitlePanel : MonoBehaviour, IPointerClickHandler
    {
        public const string ScriptKindName = "台本";
        public const string HintKindName = "提示";
        public const float DefaultHintDuration = 30f;

        [Header("引用")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _background;
        [SerializeField] private Image _accentBar;
        [SerializeField] private Image _kindChip;
        [SerializeField] private Text _kindLabel;
        [SerializeField] private Text _speakerLabel;
        [SerializeField] private Text _bodyLabel;
        [SerializeField] private Text _footerLabel;
        [SerializeField] private Image _timerFill;

        [Header("时长")]
        [Tooltip("提示字幕默认显示多少秒后自动消失。")]
        [SerializeField, Min(0.2f)] private float _hintDuration = DefaultHintDuration;

        [Header("动画")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 14f;

        /// <summary>有字幕显示出来（含切换成另一条）。</summary>
        public event Action<FocusSubtitlePanel> Shown;

        /// <summary>字幕消失（点击或被计时器收走）。</summary>
        public event Action<FocusSubtitlePanel> Dismissed;

        /// <summary>玩家点了字幕条。视觉小说用它推进对话。</summary>
        public event Action Clicked;

        /// <summary>为 true 时点击不自己消失，而是抛 Clicked 让外部决定（视觉小说模式）。</summary>
        public bool AdvanceOnClick { get; set; }

        private FocusSubtitleKind _kind = FocusSubtitleKind.Hint;
        private string _speaker = string.Empty;
        private string _body = string.Empty;
        private bool _visible;
        private float _alpha;
        private float _remaining;
        private float _timerTotal;

        public FocusSubtitleKind Kind => _kind;

        public bool IsVisible => _visible;

        public bool IsScript => _visible && _kind == FocusSubtitleKind.Script;

        public bool IsHint => _visible && _kind == FocusSubtitleKind.Hint;

        public string SpeakerText => _speaker;

        public string BodyText => _body;

        /// <summary>提示字幕剩余秒数；台本字幕恒为 0。</summary>
        public float RemainingSeconds => IsHint ? Mathf.Max(0f, _remaining) : 0f;

        public float Alpha => _alpha;

        /// <summary>提示字幕的显示时长，测试里会临时调短。</summary>
        public float HintDuration
        {
            get => _hintDuration;
            set => _hintDuration = Mathf.Max(0.2f, value);
        }

        public string KindText => _kindLabel != null ? _kindLabel.text : string.Empty;

        private void Awake()
        {
            CacheRefs();
            HideImmediate();
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-_fadeSpeed * Time.unscaledDeltaTime);
            _alpha = Mathf.Lerp(_alpha, _visible ? 1f : 0f, k);

            if (_visible && _kind == FocusSubtitleKind.Hint)
            {
                _remaining -= Time.unscaledDeltaTime;
                if (_remaining <= 0f)
                {
                    Dismiss();
                }

                UpdateTimer();
            }

            ApplyVisual();
        }

        /// <summary>台本字幕：停在屏幕上，直到玩家点击。</summary>
        public void ShowScript(string speaker, string body)
        {
            Present(FocusSubtitleKind.Script, speaker, body);
        }

        /// <summary>视觉小说的一句台词：走台本样式，点击由播放器接管。</summary>
        public void ShowLine(string speaker, string body)
        {
            Present(FocusSubtitleKind.Script, speaker, body);
        }

        /// <summary>提示字幕：用默认时长（30 秒）显示。</summary>
        public void ShowHint(string body)
        {
            ShowHint(body, _hintDuration);
        }

        /// <summary>提示字幕：duration 秒后自动消失。</summary>
        public void ShowHint(string body, float duration)
        {
            // 台本字幕优先级更高，不会被提示顶掉，否则玩家还没读完剧情就被打断。
            if (IsScript)
            {
                return;
            }

            _timerTotal = Mathf.Max(0.2f, duration);
            _remaining = _timerTotal;
            Present(FocusSubtitleKind.Hint, string.Empty, body);
        }

        /// <summary>收起字幕（点击或逻辑调用）。</summary>
        public void Dismiss()
        {
            if (!_visible)
            {
                return;
            }

            _visible = false;
            if (_group != null)
            {
                _group.blocksRaycasts = false;
            }

            Dismissed?.Invoke(this);
        }

        /// <summary>立刻收起，不做淡出（切场景、进场时用）。</summary>
        public void HideImmediate()
        {
            _visible = false;
            _alpha = 0f;
            _remaining = 0f;

            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }

            if (gameObject.activeSelf)
            {
                ApplyVisual();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 台本与提示都可以点掉：台本是唯一消除方式，提示只是顺手的快捷方式。
            // 视觉小说模式下不自己消失，交给播放器翻下一句。
            Clicked?.Invoke();

            if (!AdvanceOnClick)
            {
                Dismiss();
            }
        }

        public void EditorBind(
            RectTransform panel,
            CanvasGroup group,
            Image background,
            Image accentBar,
            Image kindChip,
            Text kindLabel,
            Text speakerLabel,
            Text bodyLabel,
            Text footerLabel,
            Image timerFill)
        {
            _panel = panel;
            _group = group;
            _background = background;
            _accentBar = accentBar;
            _kindChip = kindChip;
            _kindLabel = kindLabel;
            _speakerLabel = speakerLabel;
            _bodyLabel = bodyLabel;
            _footerLabel = footerLabel;
            _timerFill = timerFill;

            HideImmediate();
        }

        private void Present(FocusSubtitleKind kind, string speaker, string body)
        {
            CacheRefs();

            _kind = kind;
            _speaker = speaker ?? string.Empty;
            _body = body ?? string.Empty;
            _visible = true;

            if (_kind != FocusSubtitleKind.Hint)
            {
                _remaining = 0f;
            }

            ApplyTexts();
            ApplyVisual();

            if (_group != null)
            {
                _group.blocksRaycasts = true;
            }

            Shown?.Invoke(this);
        }

        private void CacheRefs()
        {
            if (_group != null)
            {
                FocusLabFont.Apply(_kindLabel);
                FocusLabFont.Apply(_speakerLabel);
                FocusLabFont.Apply(_bodyLabel);
                FocusLabFont.Apply(_footerLabel);
            }
        }

        private void ApplyTexts()
        {
            bool hint = _kind == FocusSubtitleKind.Hint;
            Color color = hint ? new Color(0.898f, 0.760f, 0.416f, 1f) : new Color(0.545f, 0.741f, 1f, 1f);

            if (_kindLabel != null)
            {
                _kindLabel.text = hint ? HintKindName : ScriptKindName;
                _kindLabel.color = color;
            }

            if (_kindChip != null)
            {
                _kindChip.color = new Color(color.r, color.g, color.b, 0.14f);
            }

            if (_accentBar != null)
            {
                _accentBar.color = color;
            }

            if (_speakerLabel != null)
            {
                _speakerLabel.text = _speaker;
                _speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(_speaker));
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = _body;
            }

            if (_timerFill != null)
            {
                _timerFill.gameObject.SetActive(hint);
            }

            UpdateTimer();
        }

        private void UpdateTimer()
        {
            bool hint = _kind == FocusSubtitleKind.Hint && _visible;

            if (_timerFill != null)
            {
                float fill = hint && _timerTotal > 0f ? Mathf.Clamp01(_remaining / _timerTotal) : 0f;
                _timerFill.fillAmount = fill;

                // 占位条没有 sprite，Image 的 filled 模式不生效，直接收窄锚点。
                RectTransform fillRect = _timerFill.rectTransform;
                Vector2 anchorMax = fillRect.anchorMax;
                anchorMax.x = Mathf.Max(fill, 0.0001f);
                fillRect.anchorMax = anchorMax;
            }

            if (_footerLabel != null)
            {
                _footerLabel.text = hint
                    ? Mathf.CeilToInt(Mathf.Max(0f, _remaining)) + " 秒后自动消失 · 点击可关闭"
                    : "点击继续";
            }
        }

        private void ApplyVisual()
        {
            if (_group != null)
            {
                _group.alpha = _alpha;
                _group.blocksRaycasts = _visible && _alpha > 0.35f;
            }

            if (_panel != null)
            {
                // 从下方滑进来一点点，别让字幕条硬邦邦地弹出来。
                Vector2 anchored = _panel.anchoredPosition;
                anchored.y = Mathf.Lerp(anchored.y, _visible ? 0f : -28f, 1f - Mathf.Exp(-_fadeSpeed * Time.unscaledDeltaTime));
                _panel.anchoredPosition = anchored;
            }
        }
    }
}
