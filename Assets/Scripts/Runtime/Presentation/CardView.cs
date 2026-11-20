using System;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>桌面上的一张牌。背面朝上时可点击翻开；正面可点击选中。</summary>
    public sealed class CardView : MonoBehaviour
    {
        private string _cardId;
        private Action<string> _onClick;
        private Image _edge;

        public string CardId
        {
            get { return _cardId; }
        }

        public static CardView Create(Transform parent, CardState card, bool selected, Action<string> onClick)
        {
            Color edgeColor = card.IsFaceUp
                ? Palette.CategoryColor(card.Definition.Category)
                : new Color(0.10f, 0.08f, 0.07f);

            RectTransform root = UIFactory.MakePanel(parent, "Card_" + card.Id, edgeColor, true, 12f);
            var view = root.gameObject.AddComponent<CardView>();
            view.Build(card, onClick);

            if (selected)
            {
                view.SetEdge(Palette.Gold);
                root.localScale = new Vector3(1.05f, 1.05f, 1f);
            }

            return view;
        }

        public void SetEdge(Color color)
        {
            if (_edge != null)
            {
                _edge.color = color;
            }
        }

        private void Build(CardState card, Action<string> onClick)
        {
            _cardId = card.Id;
            _onClick = onClick;
            _edge = GetComponent<Image>();

            RectTransform inner = UIFactory.MakePanel(
                transform,
                "Inner",
                card.IsFaceUp ? Palette.Paper : new Color(0.17f, 0.14f, 0.11f),
                true,
                9f);
            UIFactory.Stretch(inner, 5, 5, 5, 5);

            if (card.IsFaceUp)
            {
                Text name = UIFactory.MakeText(inner, "Name", card.Definition.DisplayName, 18, Palette.Ink,
                    TextAnchor.UpperCenter, FontStyle.Bold);
                RectTransform nameRect = name.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 1f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.pivot = new Vector2(0.5f, 1f);
                nameRect.anchoredPosition = new Vector2(0f, -8f);
                nameRect.sizeDelta = new Vector2(-14f, 46f);

                Text glyph = UIFactory.MakeText(inner, "Glyph", Palette.CategoryGlyph(card.Definition.Category), 54,
                    new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.32f), TextAnchor.MiddleCenter);
                RectTransform glyphRect = glyph.rectTransform;
                glyphRect.anchorMin = Vector2.zero;
                glyphRect.anchorMax = Vector2.one;
                glyphRect.offsetMin = new Vector2(0f, 34f);
                glyphRect.offsetMax = new Vector2(0f, -54f);

                string badge = !string.IsNullOrEmpty(card.Definition.ClueId) ? "◆ 线索" : string.Empty;
                if (card.IsNew)
                {
                    badge = string.IsNullOrEmpty(badge) ? "★ 新卡" : badge + "  ★";
                }

                if (!string.IsNullOrEmpty(badge))
                {
                    Text badgeText = UIFactory.MakeText(inner, "Badge", badge, 14,
                        new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.55f), TextAnchor.MiddleCenter);
                    RectTransform badgeRect = badgeText.rectTransform;
                    badgeRect.anchorMin = new Vector2(0f, 0f);
                    badgeRect.anchorMax = new Vector2(1f, 0f);
                    badgeRect.pivot = new Vector2(0.5f, 0f);
                    badgeRect.anchoredPosition = new Vector2(0f, 6f);
                    badgeRect.sizeDelta = new Vector2(-10f, 24f);
                }
            }
            else
            {
                Text question = UIFactory.MakeText(inner, "Question", "?", 76,
                    new Color(0.86f, 0.78f, 0.60f, 0.55f), TextAnchor.MiddleCenter);
                UIFactory.Stretch(question.rectTransform, 0, 0, 0, 24);

                Text hint = UIFactory.MakeText(inner, "Hint", "点击翻开", 15,
                    new Color(0.86f, 0.78f, 0.60f, 0.65f), TextAnchor.MiddleCenter);
                RectTransform hintRect = hint.rectTransform;
                hintRect.anchorMin = new Vector2(0f, 0f);
                hintRect.anchorMax = new Vector2(1f, 0f);
                hintRect.pivot = new Vector2(0.5f, 0f);
                hintRect.anchoredPosition = new Vector2(0f, 10f);
                hintRect.sizeDelta = new Vector2(-10f, 24f);
            }

            var button = gameObject.AddComponent<Button>();
            button.targetGraphic = _edge;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (_onClick != null)
                {
                    _onClick(_cardId);
                }
            });
        }
    }
}
