using UnityEngine;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 把 <see cref="VnDialogSettings"/> 每帧写到对话框的 RectTransform 上。
    ///
    /// 没接管（或者整组关掉）时把对话框还原成建 UI 时的原值，所以「不接管 = 和代码写死的一模一样」。
    /// 挂在对话框自己身上：对话框在视觉小说层里，层被关掉时它也不跑，打开就接着刷。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VnDialogApplier : MonoBehaviour
    {
        private RectTransform _box;
        private RectTransform _canvas;

        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalPivot;
        private Vector2 _originalPosition;
        private Vector2 _originalSize;
        private Vector3 _originalScale;
        private bool _restoreNeeded;

        /// <summary>建完对话框后调一次，记下原始值（不接管时用它还原）。</summary>
        public void Bind(RectTransform box, RectTransform canvas)
        {
            _box = box;
            _canvas = canvas;

            if (_box == null)
            {
                return;
            }

            _originalAnchorMin = _box.anchorMin;
            _originalAnchorMax = _box.anchorMax;
            _originalPivot = _box.pivot;
            _originalPosition = _box.anchoredPosition;
            _originalSize = _box.sizeDelta;
            _originalScale = _box.localScale;
        }

        /// <summary>参数刚改过：立即刷一次（不等下一帧）。</summary>
        public void MarkDirty()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_box == null)
            {
                return;
            }

            VnDialogSettings tuning = ArtTuningStore.WorkingDialog;
            if (tuning == null)
            {
                return;
            }

            if (tuning.apply)
            {
                tuning.WriteTo(_box, CanvasSize());
                _restoreNeeded = true;
                return;
            }

            if (_restoreNeeded)
            {
                _box.anchorMin = _originalAnchorMin;
                _box.anchorMax = _originalAnchorMax;
                _box.pivot = _originalPivot;
                _box.anchoredPosition = _originalPosition;
                _box.sizeDelta = _originalSize;
                _box.localScale = _originalScale;
                _restoreNeeded = false;
            }
        }

        private Vector2 CanvasSize()
        {
            if (_canvas != null && _canvas.rect.size.x > 1f)
            {
                return _canvas.rect.size;
            }

            return new Vector2(1920f, 1080f);
        }
    }
}
