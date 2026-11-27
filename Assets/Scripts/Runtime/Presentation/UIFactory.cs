using System;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>临时美术：所有颜色与程序化贴图都集中在这里，换正式美术时只改这一个文件。</summary>
    public static class Palette
    {
        public static readonly Color Wood = new Color(0.18f, 0.13f, 0.10f);
        public static readonly Color Paper = new Color(0.91f, 0.85f, 0.71f);
        public static readonly Color PaperDark = new Color(0.78f, 0.70f, 0.55f);
        public static readonly Color Ink = new Color(0.16f, 0.12f, 0.09f);
        public static readonly Color Gold = new Color(0.86f, 0.70f, 0.40f);
        public static readonly Color Red = new Color(0.62f, 0.22f, 0.16f);
        public static readonly Color Green = new Color(0.29f, 0.45f, 0.31f);
        public static readonly Color Shadow = new Color(0.05f, 0.04f, 0.03f, 0.78f);

        public static Color CategoryColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Location:
                    return new Color(0.36f, 0.49f, 0.33f);
                case CardCategory.Person:
                    return new Color(0.60f, 0.30f, 0.24f);
                case CardCategory.Item:
                    return new Color(0.62f, 0.45f, 0.24f);
                case CardCategory.Clue:
                    return new Color(0.45f, 0.38f, 0.55f);
                case CardCategory.Action:
                    return new Color(0.66f, 0.42f, 0.45f);
                default:
                    return new Color(0.35f, 0.33f, 0.30f);
            }
        }

        public static string CategoryGlyph(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Location:
                    return "地";
                case CardCategory.Person:
                    return "人";
                case CardCategory.Item:
                    return "物";
                case CardCategory.Clue:
                    return "线";
                case CardCategory.Action:
                    return "行";
                default:
                    return "？";
            }
        }
    }

    /// <summary>纯代码搭 UI 的工具箱，避免为了原型去维护一堆 prefab。</summary>
    public static class UIFactory
    {
        private static Font _font;
        private static Sprite _rounded;
        private static Sprite _white;
        private static Sprite _wood;
        private static Sprite _vignette;
        private static Sprite _lightPool;
        private static Sprite _softShadow;
        private static Sprite _lightGradient;

        public static Font Font
        {
            get
            {
                if (_font != null)
                {
                    return _font;
                }

                _font = UnityEngine.Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 24);

                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _font;
            }
        }

        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    _white = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                }

                return _white;
            }
        }

        /// <summary>9 宫格圆角白图，供面板/卡牌使用。</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null)
                {
                    return _rounded;
                }

                const int size = 64;
                const float radius = 14f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Min(x, size - 1 - x);
                        float dy = Mathf.Min(y, size - 1 - y);
                        float alpha = 1f;
                        if (dx < radius && dy < radius)
                        {
                            float distance = Mathf.Sqrt((radius - dx) * (radius - dx) + (radius - dy) * (radius - dy));
                            alpha = Mathf.Clamp01(radius - distance + 0.5f);
                        }

                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return _rounded;
            }
        }

        /// <summary>程序化木纹桌面（双向无缝）。</summary>
        public static Sprite Wood
        {
            get
            {
                if (_wood != null)
                {
                    return _wood;
                }

                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };

                var random = new System.Random(17);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = x / (float)size;
                        float v = y / (float)size;
                        float warp = Mathf.Sin(u * Mathf.PI * 2f) * 0.32f;
                        float grain = Mathf.Sin((v * 46f + warp * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                        float noise = (float)random.NextDouble() * 0.03f;
                        float value = Mathf.Clamp01(0.145f + grain * 0.045f + noise);
                        texture.SetPixel(x, y, new Color(value * 1.65f, value * 1.10f, value * 0.72f, 1f));
                    }
                }

                texture.Apply();
                _wood = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _wood;
            }
        }

        /// <summary>桌面压角暗边，让卡牌更突出。</summary>
        public static Sprite Vignette
        {
            get
            {
                if (_vignette != null)
                {
                    return _vignette;
                }

                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x / (float)(size - 1) - 0.5f) * 2f;
                        float dy = (y / (float)(size - 1) - 0.5f) * 2f;
                        float distance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.42f);
                        float alpha = distance * distance * 0.85f;
                        texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                    }
                }

                texture.Apply();
                _vignette = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _vignette;
            }
        }

        /// <summary>台灯光池：中心亮、边缘暗的径向渐变，用来压出「桌上有一盏灯」。</summary>
        public static Sprite LightPool
        {
            get
            {
                if (_lightPool != null)
                {
                    return _lightPool;
                }

                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float maxDistance = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                        float falloff = Mathf.Clamp01(1f - distance);
                        float alpha = falloff * falloff * falloff;
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                _lightPool = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _lightPool;
            }
        }

        /// <summary>软边圆角矩形：卡片的投影用（硬边会像"第二张黑卡"）。</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (_softShadow != null)
                {
                    return _softShadow;
                }

                const int size = 128;
                const float corner = 24f;
                const float feather = 11f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                float half = size * 0.5f - 6f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // 圆角矩形的有符号距离场，再对边缘做羽化
                        float px = Mathf.Abs(x - size * 0.5f + 0.5f) - (half - corner);
                        float py = Mathf.Abs(y - size * 0.5f + 0.5f) - (half - corner);
                        float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                                                   Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
                        float dist = outside + Mathf.Min(Mathf.Max(px, py), 0f) - corner;
                        float alpha = Mathf.Clamp01(1f - (dist + feather * 0.5f) / feather);
                        alpha = alpha * alpha * (3f - 2f * alpha);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                _softShadow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _softShadow;
            }
        }

        /// <summary>卡片受光渐变：左下暗、右上亮（灯在右上）。</summary>
        public static Sprite LightGradient
        {
            get
            {
                if (_lightGradient != null)
                {
                    return _lightGradient;
                }

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float t = (x + y) / (2f * (size - 1));
                        float alpha = t * t * 0.75f;
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                _lightGradient = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _lightGradient;
            }
        }

        public static RectTransform MakePanel(Transform parent, string name, Color color, bool rounded = true, float radius = 14f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }

            return (RectTransform)go.transform;
        }

        public static Text MakeText(Transform parent, string name, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string label, Action onClick,
            Color background, Color foreground, int fontSize = 21)
        {
            RectTransform rect = MakePanel(parent, name, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            Text text = MakeText(rect, "Label", label, fontSize, foreground, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8, 8, 4, 4);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        public static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>以父物体左上角为基准摆放（x 向右、y 向下）。</summary>
        public static RectTransform PlaceLeftTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>以父物体左下角为基准摆放。</summary>
        public static RectTransform PlaceLeftBottom(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform PlaceRightBottom(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform PlaceRightTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static void ClearChildren(Transform parent)
        {
            // 先把子物体移出层级再销毁：Destroy 是延迟到帧末的，
            // 不移出去的话同一帧里旧对象还挂在 parent 下，会被误当成现存元素
            // （重复计数、位置写到已销毁的对象上，牌就「弹回原位」）。
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static ScrollRect MakeScroll(Transform parent, out RectTransform content, Color background)
        {
            RectTransform root = MakePanel(parent, "Scroll", background);
            Stretch(root);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            RectTransform viewport = MakePanel(root, "Viewport", new Color(0f, 0f, 0f, 0f), false);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = MakePanel(viewport, "Content", new Color(0f, 0f, 0f, 0f), false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }
    }
}
