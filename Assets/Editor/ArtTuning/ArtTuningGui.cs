using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools.ArtTuning
{
    /// <summary>
    /// 美术调试窗口里的常用控件：带 tooltip 的标签、浅色分隔线、缩进的参数块。
    /// 参数名一律用 LabelField + 固定宽度，这样标签宽度可控，
    /// 长参数名也不会把右边的输入框挤成一个点（上一版的问题）。
    /// </summary>
    internal static class ArtTuningGui
    {
        private static readonly GUIContent Content = new GUIContent();

        public const float LabelWidth = 132f;

        public static void Label(string text, string tooltip = null, float width = LabelWidth)
        {
            Content.text = text;
            Content.tooltip = tooltip;
            Content.image = null;
            EditorGUILayout.LabelField(Content, GUILayout.Width(width));
        }

        public static void SmallLabel(string text, string tooltip = null)
        {
            Content.text = text;
            Content.tooltip = tooltip;
            Content.image = null;
            EditorGUILayout.LabelField(Content, EditorStyles.miniLabel);
        }

        public static void Bold(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        public static void Section(string text, string tooltip = null)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(new GUIContent(text, tooltip), EditorStyles.boldLabel);
            Separator();
        }

        public static void Separator()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.22f));
            EditorGUILayout.Space(2f);
        }

        public static void HelpBox(string message, MessageType type)
        {
            EditorGUILayout.HelpBox(message, type, true);
        }

        /// <summary>横排一行（用 using 包住）。</summary>
        public sealed class Row : System.IDisposable
        {
            public Row()
            {
                EditorGUILayout.BeginHorizontal();
            }

            public void Dispose()
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>多缩进一级（用 using 包住）。</summary>
        public sealed class Indent : System.IDisposable
        {
            public Indent()
            {
                EditorGUI.indentLevel++;
            }

            public void Dispose()
            {
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>一行文字（贴在数值旁边用）。</summary>
        public static void Text(string text, float width = 90f)
        {
            Content.text = text;
            Content.tooltip = null;
            Content.image = null;
            EditorGUILayout.LabelField(Content, GUILayout.Width(width));
        }

        public static string Format(Vector2 value, string unit = "")
        {
            return value.x.ToString("0.##") + ", " + value.y.ToString("0.##") +
                   (string.IsNullOrEmpty(unit) ? string.Empty : " " + unit);
        }

        public static string Format(float value, string format = "0.##")
        {
            return value.ToString(format);
        }
    }
}
