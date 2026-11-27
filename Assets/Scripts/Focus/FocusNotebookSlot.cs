using UnityEngine;
using UnityEngine.UI;

namespace FocusTest
{
    /// <summary>
    /// 案件板上的一个空槽（案件 / 凶手 / 凶器 / 物证 / 人证）。
    /// 空着时显示虚线框、槽名、说明和例子；放上线索后槽名和线索短语都亮着。
    /// 同一个槽只放一条线索，放新的会把旧的换回线索页。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusNotebookSlot : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private FocusQuestionData _question;
        [SerializeField] private string _pendingQuestionId;
        [SerializeField] private RectTransform _body;
        [SerializeField] private Image _frame;
        [SerializeField] private Image _ghost;
        [SerializeField] private Image _pin;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _hintLabel;
        [SerializeField] private Text _clueLabel;
        [SerializeField] private RectTransform _dropZone;

        [Header("配色")]
        [SerializeField] private Color _emptyColor = new Color(0.28f, 0.55f, 0.62f, 0.85f);
        [SerializeField] private Color _filledColor = new Color(0.78f, 0.62f, 0.30f, 1f);

        private FocusClueData _clue;
        private string _clueText = string.Empty;
        private string _slotKey = "slot";
        private float _dropAge = 99f;
        private RectTransform _bodyRect;
        private Vector3 _bodyScale = Vector3.one;

        public FocusQuestionData Question => _question;

        public FocusClueData Clue => _clue;

        public bool IsFilled => _clue != null;

        public RectTransform DropZone => _dropZone != null ? _dropZone : (RectTransform)transform;

        public string ClueText => _clueText;

        public void SetHighlight(bool highlight)
        {
            if (_frame != null)
            {
                _frame.color = IsFilled ? _filledColor : _emptyColor;
                _frame.canvasRenderer.SetAlpha(highlight || IsFilled ? 1f : 0.45f);
            }

            if (_body != null)
            {
                _body.localScale = _bodyScale * (highlight ? 1.03f : 1f);
            }
        }

        /// <summary>放进一条线索（null = 清空）。放进来时播一下落定时动画。</summary>
        public void SetClue(FocusClueData clue)
        {
            bool newlyFilled = clue != null && _clue != clue;
            _clue = clue;
            ApplyVisual();

            if (newlyFilled)
            {
                _dropAge = 0f;
            }
        }

        /// <summary>翻页/重开笔记本时补播一次落定动画（delay 秒后开始）。</summary>
        public void PlayDropAnimation(float delay)
        {
            _dropAge = -Mathf.Max(0f, delay);
        }

        /// <summary>屏幕坐标是不是落在槽的投放范围内。</summary>
        public bool ContainsScreenPoint(Vector2 screenPosition, Camera camera)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(DropZone, screenPosition, camera);
        }

        /// <summary>落定动画：刚放进去时略微放大一下再收回来。</summary>
        private void Update()
        {
            if (_dropAge > 1f || _bodyRect == null)
            {
                return;
            }

            _dropAge += Time.unscaledDeltaTime;
            if (_dropAge < 0f)
            {
                return;
            }

            float t = Mathf.Clamp01(_dropAge / 0.18f);
            float punch = Mathf.Sin(t * Mathf.PI) * 0.06f;
            _bodyRect.localScale = _bodyScale * (1f + punch);
        }

        private void ApplyVisual()
        {
            bool filled = _clue != null;

            _clueText = filled ? _clue.Text : string.Empty;

            if (_titleLabel != null)
            {
                _titleLabel.text = _question != null ? _question.Title : _slotKey;
                _titleLabel.color = filled ? new Color(0.40f, 0.29f, 0.16f, 1f) : new Color(0.30f, 0.20f, 0.11f, 1f);
            }

            if (_clueLabel != null)
            {
                FocusLabFont.Apply(_clueLabel);
                _clueLabel.text = _clueText;
                _clueLabel.color = _clue != null ? FocusClueKinds.Accent(_clue.Kind) : Color.white;
                _clueLabel.gameObject.SetActive(filled);
            }

            if (_hintLabel != null)
            {
                _hintLabel.text = filled ? string.Empty : BuildHint();
                _hintLabel.gameObject.SetActive(!filled);
            }

            if (_ghost != null)
            {
                _ghost.enabled = !filled;
            }

            if (_pin != null)
            {
                _pin.enabled = filled;
            }

            SetHighlight(false);
        }

        private void Composite()
        {
            _slotKey = SlotKey();

            if (_hintLabel != null)
            {
                FocusLabFont.Apply(_hintLabel);
                _hintLabel.text = BuildHint();
            }

            if (_titleLabel != null)
            {
                FocusLabFont.Apply(_titleLabel);
                _titleLabel.text = _question != null ? _question.Title : _slotKey;
            }

            if (_clueLabel != null)
            {
                FocusLabFont.Apply(_clueLabel);
            }

            ApplyVisual();
        }

        private string SlotKey()
        {
            if (_question != null && !string.IsNullOrEmpty(_question.QuestionId))
            {
                return _question.QuestionId;
            }

            return gameObject.name;
        }

        private string BuildHint()
        {
            if (_question == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(_question.Example))
            {
                return _question.Hint;
            }

            return string.IsNullOrEmpty(_question.Hint)
                ? "例如：" + _question.Example
                : _question.Hint + "\n例如：" + _question.Example;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 场景构建时先挂脚本再设引用；某些情况下反序列化后字段会丢，这里自愈一次。
            if (_question == null && !string.IsNullOrEmpty(_pendingQuestionId))
            {
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:FocusQuestionData"))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<FocusQuestionData>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null && asset.QuestionId == _pendingQuestionId)
                    {
                        _question = asset;
                        break;
                    }
                }
            }
        }
#endif

        private void Awake()
        {
            if (_dropZone == null)
            {
                _dropZone = (RectTransform)transform;
            }

            _bodyRect = _body != null ? _body : (RectTransform)transform;
            _bodyScale = _bodyRect.localScale;
            Composite();
        }

#if UNITY_EDITOR
        /// <summary>编辑器构建场景时注入引用（运行时不使用）。</summary>
        public void EditorBind(
            FocusQuestionData question,
            RectTransform body,
            Image frame,
            Image ghost,
            Image pin,
            Text titleLabel,
            Text hintLabel,
            Text clueLabel,
            RectTransform dropZone)
        {
            _question = question;
            _pendingQuestionId = question != null ? question.QuestionId : string.Empty;
            _body = body;
            _frame = frame;
            _ghost = ghost;
            _pin = pin;
            _titleLabel = titleLabel;
            _hintLabel = hintLabel;
            _clueLabel = clueLabel;
            _dropZone = dropZone;
            _bodyRect = body != null ? body : (RectTransform)transform;
            _bodyScale = _bodyRect.localScale;
            Composite();
        }
#endif
    }
}
