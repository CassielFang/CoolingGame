using System;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 报纸：一张摊开的报纸盖住桌面。玩家读完点一下收起来。
    /// 排版故意做得像新闻纸——大标题、细分割线、小字正文。
    /// </summary>
    public sealed class NewspaperOverlay : MonoBehaviour
    {
        private Text _headline;
        private Text _dateline;
        private Text _body;
        private Action _onClosed;

        public bool Visible
        {
            get { return gameObject.activeSelf; }
        }

        public static NewspaperOverlay Create(Transform parent)
        {
            RectTransform root = UIFactory.MakePanel(parent, "NewspaperOverlay", new Color(0.02f, 0.02f, 0.02f, 0.88f), false);
            UIFactory.Stretch(root);

            var overlay = root.gameObject.AddComponent<NewspaperOverlay>();
            overlay.Build(root);
            root.gameObject.SetActive(false);
            return overlay;
        }

        public void Show(NewspaperDefinition paper, Action onClosed)
        {
            if (paper == null)
            {
                if (onClosed != null)
                {
                    onClosed();
                }

                return;
            }

            string[] lines = (paper.Body ?? string.Empty).Split('\n');
            string dateline = lines.Length > 0 ? lines[0].Trim() : string.Empty;
            string rest = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1).Trim() : string.Empty;

            _headline.text = paper.Title;
            _dateline.text = dateline;
            _body.text = rest;
            _onClosed = onClosed;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Build(RectTransform root)
        {
            var closeButton = root.gameObject.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(Close);

            RectTransform page = UIFactory.MakePanel(root, "Page", new Color(0.90f, 0.87f, 0.79f, 1f));
            page.anchorMin = new Vector2(0.5f, 0.5f);
            page.anchorMax = new Vector2(0.5f, 0.5f);
            page.pivot = new Vector2(0.5f, 0.5f);
            page.anchoredPosition = Vector2.zero;
            page.sizeDelta = new Vector2(880f, 1000f);

            Text dateline = UIFactory.MakeText(page, "Dateline", string.Empty, 18,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.62f), TextAnchor.MiddleCenter);
            UIFactory.PlaceLeftTop(dateline.rectTransform, 40f, 26f, 800f, 26f);
            _dateline = dateline;

            // 报头下的一道细线
            RectTransform rule = UIFactory.MakePanel(page, "Rule", new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.35f));
            rule.anchorMin = new Vector2(0f, 1f);
            rule.anchorMax = new Vector2(1f, 1f);
            rule.pivot = new Vector2(0.5f, 1f);
            rule.anchoredPosition = new Vector2(0f, -62f);
            rule.sizeDelta = new Vector2(-80f, 2f);

            _headline = UIFactory.MakeText(page, "Headline", string.Empty, 40, Palette.Ink,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.PlaceLeftTop(_headline.rectTransform, 40f, 78f, 800f, 112f);

            _body = UIFactory.MakeText(page, "Body", string.Empty, 20, Palette.Ink, TextAnchor.UpperLeft);
            RectTransform bodyRect = _body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(52f, 56f);
            bodyRect.offsetMax = new Vector2(-52f, -206f);
            _body.lineSpacing = 1.7f;

            Text hint = UIFactory.MakeText(page, "Hint", "点击收起报纸", 15,
                new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.42f), TextAnchor.LowerRight);
            UIFactory.PlaceRightBottom(hint.rectTransform, 48f, 22f, 260f, 24f);
        }

        private void Close()
        {
            gameObject.SetActive(false);

            Action callback = _onClosed;
            _onClosed = null;
            if (callback != null)
            {
                callback();
            }
        }
    }
}
