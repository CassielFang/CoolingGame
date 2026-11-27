using UnityEngine;

namespace FocusTest
{
    /// <summary>
    /// 测试场用的中文字体：优先取系统字体，退化到内置字体。
    /// 与项目里 UIFactory 的做法一致——不把字体文件塞进仓库。
    /// </summary>
    public static class FocusLabFont
    {
        private static Font _font;

        public static Font UiFont
        {
            get
            {
                if (_font != null)
                {
                    return _font;
                }

                _font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Noto Sans CJK SC", "PingFang SC", "Arial" },
                    32);

                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _font;
            }
        }

        /// <summary>
        /// 运行时换成中文字体。场景里存的是内置 LegacyRuntime.ttf（没有中文），
        /// 动态字体不能序列化，所以只在播放时替换。
        /// </summary>
        public static void Apply(UnityEngine.UI.Text text)
        {
            if (text == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Font font = UiFont;
                if (font != null && text.font != font)
                {
                    text.font = font;
                }
            }
        }
    }
}
