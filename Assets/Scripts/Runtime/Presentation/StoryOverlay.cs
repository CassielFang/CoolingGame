using System;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 情节演出（视觉小说 / 报纸 / 漫画 / CG 的统一占位）。
    /// 现在只画文字，后续把正文换成美术与排版即可，接口不变。
    /// </summary>
    public sealed class StoryOverlay : MonoBehaviour
    {
        private Text _title;
        private Text _body;
        private Text _tag;
        private Action _onClosed;

        public bool Visible
        {
            get { return gameObject.activeSelf; }
        }

        public static StoryOverlay Create(Transform parent)
        {
            RectTransform root = UIFactory.MakePanel(parent, "StoryOverlay", new Color(0f, 0f, 0f, 0.9f), false);
            UIFactory.Stretch(root);

            var overlay = root.gameObject.AddComponent<StoryOverlay>();
            overlay.Build(root);
            root.gameObject.SetActive(false);
            return overlay;
        }

        public void Show(StoryNodeDefinition node, Action onClosed)
        {
            if (node == null)
            {
                if (onClosed != null)
                {
                    onClosed();
                }

                return;
            }

            _title.text = node.Title;
            _body.text = node.Body;
            _tag.text = PresentationLabel(node.Presentation);
            _onClosed = onClosed;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Build(RectTransform root)
        {
            RectTransform panel = UIFactory.MakePanel(root, "StoryPanel", Palette.Paper);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1180f, 660f);

            _tag = UIFactory.MakeText(panel, "Tag", "", 16,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.5f), TextAnchor.MiddleLeft);
            UIFactory.PlaceLeftTop(_tag.rectTransform, 44, 26, 420, 26);

            _title = UIFactory.MakeText(panel, "Title", "", 42, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(_title.rectTransform, 44, 58, 1080, 62);

            _body = UIFactory.MakeText(panel, "Body", "", 26, Palette.Ink, TextAnchor.UpperLeft);
            RectTransform bodyRect = _body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(44f, 120f);
            bodyRect.offsetMax = new Vector2(-44f, -140f);
            _body.lineSpacing = 1.4f;

            Button next = UIFactory.MakeButton(panel, "Next", "继续", Continue, Palette.Green, Palette.Paper);
            UIFactory.PlaceRightBottom(next.GetComponent<RectTransform>(), 44, 34, 220, 62);
        }

        private void Continue()
        {
            gameObject.SetActive(false);
            Action callback = _onClosed;
            _onClosed = null;
            if (callback != null)
            {
                callback();
            }
        }

        private static string PresentationLabel(StoryPresentation presentation)
        {
            switch (presentation)
            {
                case StoryPresentation.VisualNovel:
                    return "视觉小说";
                case StoryPresentation.Newspaper:
                    return "新闻报纸";
                case StoryPresentation.Comic:
                    return "漫画";
                case StoryPresentation.Cg:
                    return "CG";
                default:
                    return "情节";
            }
        }
    }
}
