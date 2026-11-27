using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 左侧人物立绘：选中人物卡时滑出，换成另一张人物卡时换图并换色。
    /// 占位美术只有一张立绘，不同人物靠 FocusCardData.PortraitTint 染色区分。
    /// 立绘不吃点击（blocksRaycasts 恒为 false），免得挡住左侧的牌。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusPortraitPanel : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _frame;
        [SerializeField] private Image _portrait;
        [SerializeField] private Image _tintOverlay;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _roleLabel;

        [Header("表现")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 12f;
        [SerializeField, Min(0f)] private float _slideOffset = 34f;
        [SerializeField, Range(0f, 1f)] private float _tintStrength = 0.24f;

        private float _alpha;
        private Vector2 _homePosition;
        private bool _homeKnown;
        private bool _visible;
        private Sprite _sprite;
        private Color _tint = Color.white;
        private string _displayName = string.Empty;

        public bool IsVisible => _visible;

        public float Alpha => _alpha;

        public Sprite PortraitSprite => _sprite;

        public string DisplayName => _displayName;

        public CanvasGroup Group => _group;

        private void Awake()
        {
            CacheRefs();
            HideImmediate();
        }

        private void OnEnable()
        {
            CacheRefs();
        }

        /// <summary>按人物卡显示立绘。</summary>
        public void Show(FocusCardView card)
        {
            if (card == null)
            {
                Hide();
                return;
            }

            Show(card.Data != null ? card.Data.Portrait : null,
                card.Data != null ? card.Data.PortraitTint : Color.white,
                card.DisplayName);
        }

        /// <summary>直接给图、颜色和名字（截图与测试用）。</summary>
        public void Show(Sprite portrait, Color tint, string displayName)
        {
            CacheRefs();

            _sprite = portrait;
            _tint = tint;
            _displayName = string.IsNullOrEmpty(displayName) ? "未知人物" : displayName;
            _visible = true;

            if (_portrait != null)
            {
                _portrait.sprite = _sprite;
                _portrait.enabled = _sprite != null;
            }

            if (_frame != null)
            {
                // 边框跟人物走色，图本身只压一层很淡的色，免得整张纸底发灰。
                _frame.color = Color.Lerp(new Color(0.05f, 0.07f, 0.11f, 0.92f), _tint, 0.5f);
            }

            if (_tintOverlay != null)
            {
                _tintOverlay.color = new Color(_tint.r, _tint.g, _tint.b, _tintStrength * 0.5f);
                _tintOverlay.enabled = true;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = _displayName;
                _nameLabel.color = Color.Lerp(_tint, Color.white, 0.35f);
            }

            if (_roleLabel != null)
            {
                _roleLabel.text = "角色";
                _roleLabel.color = new Color(0.95f, 0.98f, 1f, 0.95f);
            }
        }

        public void Hide()
        {
            _visible = false;
        }

        /// <summary>立刻收起（不播淡出），进播放模式与截图前用。</summary>
        public void HideImmediate()
        {
            CacheRefs();

            _visible = false;
            _alpha = 0f;
            _displayName = string.Empty;
            _sprite = null;

            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }

            if (_panel != null)
            {
                _panel.anchoredPosition = HomePosition + Vector2.left * _slideOffset;
            }
        }

        private Vector2 HomePosition
        {
            get
            {
                if (!_homeKnown && _panel != null)
                {
                    _homePosition = _panel.anchoredPosition + Vector2.right * _slideOffset;
                    _homeKnown = true;
                }

                return _homePosition;
            }
        }

        private void Update()
        {
            if (_group == null || _panel == null)
            {
                return;
            }

            float target = _visible ? 1f : 0f;
            float k = 1f - Mathf.Exp(-_fadeSpeed * Time.unscaledDeltaTime);
            _alpha = Mathf.Lerp(_alpha, target, k);

            if (Mathf.Abs(_alpha - target) < 0.002f)
            {
                _alpha = target;
            }

            _group.alpha = _alpha;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Vector2 home = HomePosition;
            Vector2 targetPosition = _alpha > 0.001f ? home : home + Vector2.left * _slideOffset;
            _panel.anchoredPosition = Vector2.Lerp(_panel.anchoredPosition, targetPosition, k);
        }

        private void CacheRefs()
        {
            if (_panel == null)
            {
                _panel = transform as RectTransform;
            }

            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
            }

            if (!_homeKnown && _panel != null)
            {
                _homePosition = _panel.anchoredPosition;
                _homeKnown = true;
            }
        }

#if UNITY_EDITOR
        /// <summary>编辑器构建场景时注入引用（运行时不使用）。</summary>
        public void EditorBind(
            RectTransform panel,
            CanvasGroup group,
            Image frame,
            Image portrait,
            Image tintOverlay,
            Text nameLabel,
            Text roleLabel)
        {
            _panel = panel;
            _group = group;
            _frame = frame;
            _portrait = portrait;
            _tintOverlay = tintOverlay;
            _nameLabel = nameLabel;
            _roleLabel = roleLabel;

            _homeKnown = false;
            CacheRefs();
            HideImmediate();
        }
#endif
    }
}
