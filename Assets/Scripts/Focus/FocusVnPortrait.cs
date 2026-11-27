using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 视觉小说一侧的立绘层。左、右各挂一个，各自带 Canvas + CanvasGroup，
    /// 所以之后想给某一侧单独加光线、遮罩或调色，都不会牵连另一侧。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusVnPortrait : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _image;

        [Header("表现")]
        [SerializeField] private float _fadeSpeed = 10f;
        [SerializeField] private float _slideDistance = 28f;
        [SerializeField, Range(0f, 1f)] private float _dimmedAlpha = 0.72f;

        private Vector2 _homePosition;
        private bool _homeKnown;
        private float _alpha;
        private bool _visible;
        private Sprite _sprite;

        /// <summary>这一侧自己的 Canvas，将来加效果直接往它上面挂。</summary>
        public Canvas LayerCanvas => GetComponent<Canvas>();

        public CanvasGroup Group => _group;

        public bool IsVisible => _visible;

        public Sprite Sprite => _sprite;

        public float Alpha => _alpha;

        /// <summary>换人/换图；dimmed 用来让「不说话的那一侧」稍微退后一点。</summary>
        public void Show(Sprite sprite, Color tint, bool dimmed)
        {
            CacheRefs();

            _sprite = sprite;
            _visible = sprite != null;

            if (_image != null)
            {
                _image.sprite = sprite;
                _image.enabled = sprite != null;
                _image.color = tint;
            }

            _targetAlpha = dimmed ? _dimmedAlpha : 1f;
        }

        public void Hide()
        {
            _visible = false;
            _targetAlpha = 0f;
        }

        public void HideImmediate()
        {
            CacheRefs();

            _visible = false;
            _alpha = 0f;
            _targetAlpha = 0f;
            _sprite = null;

            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }

            if (_image != null)
            {
                _image.enabled = false;
            }

            if (_root != null)
            {
                _root.anchoredPosition = HomePosition + Vector2.left * _slideDistance * (IsLeftSide ? 1f : -1f);
            }
        }

        private bool IsLeftSide
        {
            get
            {
                if (_root == null)
                {
                    return true;
                }

                return _root.anchorMin.x < 0.5f;
            }
        }

        private float _targetAlpha;

        private Vector2 HomePosition
        {
            get
            {
                if (!_homeKnown && _root != null)
                {
                    _homePosition = _root.anchoredPosition;
                    _homeKnown = true;
                }

                return _homePosition;
            }
        }

        private void Awake()
        {
            CacheRefs();
        }

        private void Update()
        {
            if (_group == null || _root == null)
            {
                return;
            }

            float k = 1f - Mathf.Exp(-_fadeSpeed * Time.unscaledDeltaTime);
            _alpha = Mathf.Lerp(_alpha, _targetAlpha, k);
            if (Mathf.Abs(_alpha - _targetAlpha) < 0.002f)
            {
                _alpha = _targetAlpha;
            }

            _group.alpha = _alpha;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            float sign = IsLeftSide ? 1f : -1f;
            Vector2 target = HomePosition + Vector2.left * _slideDistance * sign * (1f - _alpha);
            _root.anchoredPosition = Vector2.Lerp(_root.anchoredPosition, target, k);
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

            if (_image == null)
            {
                _image = GetComponentInChildren<Image>(true);
            }
        }

#if UNITY_EDITOR
        public void EditorBind(RectTransform root, CanvasGroup group, Image image)
        {
            _root = root;
            _group = group;
            _image = image;
            _homeKnown = false;
        }
#endif
    }
}
