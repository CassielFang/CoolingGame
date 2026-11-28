using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation.ArtTuning
{
    /// <summary>
    /// 视觉小说背景的竖向渐变。挂在 DialogueOverlay 的黑场层上，
    /// 只在 VnStanding 的「背景」启用时打开；关掉就回落到原来的纯黑。
    ///
    /// 不需要 Material：程序化生成一张 1×256 的渐变 Sprite 给 Image 用。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class VnBackdropGradient : MonoBehaviour
    {
        private const int GradientHeight = 256;

        public Color Top = Color.white;
        public Color Bottom = Color.black;

        private Image _image;
        private Sprite _gradient;
        private Color _builtTop;
        private Color _builtBottom;
        private bool _hasBuilt;

        public void SetColors(Color top, Color bottom)
        {
            Top = top;
            Bottom = bottom;
        }

        /// <summary>颜色有变化（或第一次）就重画。返回是否真的重画了。</summary>
        public bool RefreshIfNeeded()
        {
            if (_hasBuilt && ColorsClose(Top, _builtTop) && ColorsClose(Bottom, _builtBottom))
            {
                return false;
            }

            Rebuild();
            return true;
        }

        private void OnEnable()
        {
            // 用缓存的颜色重建（可能一直是默认白色，等 Applier 第一次 SetColors 后会立即重画）
            Rebuild();
        }

        private void Rebuild()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (_image == null)
            {
                return;
            }

            if (_gradient == null)
            {
                var texture = new Texture2D(1, GradientHeight, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "VnBackdropGradient"
                };

                _gradient = Sprite.Create(texture, new Rect(0, 0, 1, GradientHeight), new Vector2(0.5f, 0.5f), 100f);
                _gradient.name = "VnBackdropGradient";
            }

            Texture2D tex = _gradient.texture;
            for (int y = 0; y < GradientHeight; y++)
            {
                float t = y / (float)(GradientHeight - 1); // 0 = 底，1 = 顶
                tex.SetPixel(0, y, Color.Lerp(Bottom, Top, t));
            }

            tex.Apply(false, false);

            _builtTop = Top;
            _builtBottom = Bottom;
            _hasBuilt = true;

            _image.sprite = _gradient;
            _image.type = Image.Type.Simple;
            _image.color = Color.white;
            _image.preserveAspect = false;
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                   && Mathf.Abs(a.g - b.g) < 0.002f
                   && Mathf.Abs(a.b - b.b) < 0.002f
                   && Mathf.Abs(a.a - b.a) < 0.002f;
        }

        private void OnDestroy()
        {
            if (_gradient == null)
            {
                return;
            }

            Texture2D tex = _gradient.texture;
            Destroy(_gradient);
            if (tex != null)
            {
                Destroy(tex);
            }
        }
    }
}
