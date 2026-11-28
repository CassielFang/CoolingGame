using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 运行时执行「图怎么摆进框」：按 <see cref="VnStandingSettings.ResolveArt"/> 算好的结果把图摆上去。
    /// 立绘的 RectTransform 就是框（尺寸由 VnStandingSide 控制，这里不碰）；
    /// 框上的 Image 只当框用，真正的图放到子物体 Art 上，超出框的部分用 RectMask2D 裁掉。
    ///
    /// 只由 <see cref="VnStandingApplier"/> 在「接管图」启用时挂上；禁用即回到原始 Image 行为。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class VnPortraitMatcher : MonoBehaviour
    {
        /// <summary>默认：超过框的部分裁掉（框就是画框）。</summary>
        public bool ClipToFrame = true;

        private Image _root;
        private Image _art;
        private RectMask2D _mask;
        private ArtImagePlacement _placement;
        private Vector2 _native;

        /// <summary>外部算好摆放结果后调一次。</summary>
        public void SetPlacement(ArtImagePlacement placement, Vector2 native, bool clipToFrame)
        {
            _placement = placement;
            _native = native;
            ClipToFrame = clipToFrame;

            if (isActiveAndEnabled)
            {
                Build();
            }
        }

        /// <summary>参数或贴图刚变过：立即重摆一次（不等下一帧）。</summary>
        public void MarkDirty()
        {
            _root = _root != null ? _root : GetComponent<Image>();
            Build();
        }

        private void OnEnable()
        {
            _root = GetComponent<Image>();
            Build();
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                Build();
            }
        }

        private void LateUpdate()
        {
            // 换句换人：sprite 变了要重画
            if (_root != null && _art != null && _art.enabled && _art.sprite != _root.sprite)
            {
                Build();
            }
        }

        private void Build()
        {
            if (_root == null)
            {
                _root = GetComponent<Image>();
            }

            EnsureArt();

            Sprite sprite = _root != null ? _root.sprite : null;
            if (!isActiveAndEnabled || _root == null || sprite == null || _art == null)
            {
                Teardown();
                return;
            }

            // 根只当框用，不再画图
            _root.enabled = false;

            _art.color = _root.color;
            _art.sprite = sprite;
            _art.enabled = true;
            ArtImagePlacement.Layer(_art, _placement, _native.sqrMagnitude > 0f ? _native : sprite.rect.size);

            if (_mask == null)
            {
                _mask = gameObject.AddComponent<RectMask2D>();
                _mask.hideFlags = HideFlags.DontSave;
            }

            _mask.enabled = ClipToFrame;
        }

        private void EnsureArt()
        {
            if (_art != null)
            {
                return;
            }

            Transform existing = transform.Find("Art");
            if (existing == null || existing.GetComponent<Image>() == null)
            {
                var go = new GameObject("Art", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                go.hideFlags = HideFlags.DontSave;
                existing = go.transform;
            }

            _art = existing.GetComponent<Image>();
            _art.raycastTarget = false;
        }

        /// <summary>回到原始行为：根 Image 显示，子物体藏起来。</summary>
        private void Teardown()
        {
            if (_art != null)
            {
                _art.enabled = false;
            }

            if (_mask != null)
            {
                _mask.enabled = false;
            }

            if (_root != null)
            {
                _root.enabled = true;
                _root.preserveAspect = true;
                _root.raycastTarget = false;
            }
        }
    }
}
