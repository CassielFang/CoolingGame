using System;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>桌面上的一张牌。背面朝上时可点击翻开；正面可以拖去调查/问人/分析。</summary>
    public sealed class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private string _cardId;
        private Action<string> _onClick;
        private Image _edge;
        private RectTransform _shadow;
        private RectTransform _contact;
        private string _displayName;
        private Color _baseEdgeColor;
        private Vector2 _dragOffset;
        private Vector2 _homePosition;
        private float _lift;
        private Coroutine _liftRoutine;
        private Coroutine _flipRoutine;
        private Action<string> _dragStart;
        private Action<string, Vector2> _onMoved;
        private Action _onDragging;
        private Action<string, Vector2, Vector2> _onDrop;
        private Action _dragEnd;

        /// <summary>卡片在桌面上固定的尺寸（与 RefreshCards 的布局常量一致）。</summary>
        public const float CardWidth = 160f;
        public const float CardHeight = 224f;

        /// <summary>台灯在「桌面归一化坐标」里的位置：偏右上，和光池一致。</summary>
        public static readonly Vector2 LightPosition = new Vector2(0.42f, 0.46f);

        public string CardId
        {
            get { return _cardId; }
        }

        public static CardView Create(Transform parent, Transform shadowParent, CardState card, bool selected,
            Action<string> onClick)
        {
            Color edgeColor = card.IsFaceUp
                ? Palette.CategoryColor(card.Definition.Category)
                : new Color(0.10f, 0.08f, 0.07f);

            RectTransform root = UIFactory.MakePanel(parent, "Card_" + card.Id, edgeColor, true, 12f);

            // 卡纸厚度：比卡面矮一点、往下错开的一条暗边
            RectTransform thickness = UIFactory.MakePanel(root, "Thickness",
                new Color(0.04f, 0.03f, 0.02f, 0.55f), true, 12f);
            thickness.anchorMin = Vector2.zero;
            thickness.anchorMax = Vector2.one;
            thickness.offsetMin = new Vector2(7f, -6f);
            thickness.offsetMax = new Vector2(-7f, -10f);
            thickness.SetAsFirstSibling();
            var view = root.gameObject.AddComponent<CardView>();
            view._baseEdgeColor = edgeColor;

            // 影子放在独立的阴影层里：影子绝不能参与卡片之间的层叠，
            // 否则后画的卡片会把自己的影子盖到前一张卡片上。
            Transform shadowHolder = shadowParent != null ? shadowParent : parent;
            view._contact = MakeSoftShadow(shadowHolder, "Contact_" + card.Id, new Color(0f, 0f, 0f, 0.4f));
            view._shadow = MakeSoftShadow(shadowHolder, "Cast_" + card.Id, new Color(0f, 0f, 0f, 0.26f));

            view.Build(card, onClick);

            if (selected)
            {
                view.SetEdge(Palette.Gold);
                root.localScale = new Vector3(1.05f, 1.05f, 1f);
            }

            return view;
        }

        /// <summary>
        /// 按卡片在桌面上的相对位置（-1..1）摆影子。
        /// 投掷影只做偏移、不旋转——旋转会让卡片看起来是"歪的"，而不是"被光照出影子"。
        /// </summary>
        /// <summary>拖起一张牌时，能接收它的落点（人物/地点）亮一下。</summary>
        public void SetEdgeHighlight(bool highlighted)
        {
            if (_edge == null)
            {
                return;
            }

            _edge.color = highlighted ? Palette.Gold : _baseEdgeColor;
        }

        public void SetShadowLayerVisible(bool visible)
        {
            if (_shadow != null)
            {
                _shadow.gameObject.SetActive(visible);
            }

            if (_contact != null)
            {
                _contact.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 翻牌：牌面收窄到侧面 → 换面（重建）→ 展开。
        /// 先不管厚度，只做横向压扁，效果已经像把牌翻过来。
        /// </summary>
        public void PlayReveal(Action onMidpoint)
        {
            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
            }

            _flipRoutine = StartCoroutine(RevealRoutine(onMidpoint));
        }

        private System.Collections.IEnumerator RevealRoutine(Action onMidpoint)
        {
            const float half = 0.09f;

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                transform.localScale = new Vector3(Mathf.Lerp(1f, 0.04f, k), 1f, 1f);
                yield return null;
            }

            transform.localScale = new Vector3(0.04f, 1f, 1f);

            if (onMidpoint != null)
            {
                onMidpoint();
            }

            // 换面后可能已经重建了这个对象；对象还在才继续展开
            t = 0f;
            while (t < half && this != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                transform.localScale = new Vector3(Mathf.Lerp(0.04f, 1f, k), 1f, 1f);
                yield return null;
            }

            if (this != null)
            {
                transform.localScale = Vector3.one;
            }

            _flipRoutine = null;
        }

        /// <summary>接上拖拽：拖起、拖动中、松手、落位四段回调。</summary>
        public void SetDragHandlers(Action<string> onDragStart, Action<string, Vector2> onMoved,
            Action<string, Vector2, Vector2> onDrop, Action onDragEnd)
        {
            _dragStart = onDragStart;
            _onMoved = onMoved;
            _onDrop = onDrop;
            _dragEnd = onDragEnd;
        }

        /// <summary>拖动过程中每帧通知（用来实时浮现「在灯下能做什麼」）。</summary>
        public void SetDraggingHandler(Action onDragging)
        {
            _onDragging = onDragging;
        }

        /// <summary>
        /// 拿起 / 放下：卡片离开桌面——放大、轻微倾斜、影子出现；放回时落桌有一下回弹。
        /// 用缓动而不是硬切，手感才像「把纸片拎起来」。
        /// </summary>
        public void SetElevated(bool elevated)
        {
            if (_liftRoutine != null)
            {
                StopCoroutine(_liftRoutine);
            }

            _liftRoutine = StartCoroutine(LiftRoutine(elevated));
        }

        /// <summary>直接贴回桌面，不播动画（重建卡片时用）。</summary>
        public void SnapToTable()
        {
            if (_liftRoutine != null)
            {
                StopCoroutine(_liftRoutine);
                _liftRoutine = null;
            }

            ApplyLift(0f);
        }

        private System.Collections.IEnumerator LiftRoutine(bool elevated)
        {
            float from = _lift;
            float to = elevated ? 1f : 0f;
            float duration = elevated ? 0.13f : 0.16f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                ApplyLift(Mathf.Lerp(from, to, k));
                yield return null;
            }

            ApplyLift(to);

            if (!elevated)
            {
                // 落桌回弹：轻微压扁再弹回，给「啪」一下的落感
                float t = 0f;
                while (t < 0.14f)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / 0.14f);
                    float dip = Mathf.Sin(k * Mathf.PI);
                    transform.localScale = new Vector3(1f + 0.04f * dip, 1f - 0.05f * dip, 1f);
                    yield return null;
                }

                transform.localScale = Vector3.one;
            }

            _liftRoutine = null;
        }

        private void ApplyLift(float lift)
        {
            _lift = lift;

            transform.localScale = new Vector3(1f + 0.12f * lift, 1f + 0.12f * lift, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -2.5f * lift);

            bool visible = lift > 0.02f;
            if (_shadow != null)
            {
                _shadow.gameObject.SetActive(visible);
            }

            if (_contact != null)
            {
                _contact.gameObject.SetActive(visible);
            }

            if (visible)
            {
                ApplyElevatedShadow(lift);
            }
        }

        /// <summary>拿起来的影子：偏得更远、更实；抬起越高越明显。</summary>
        private void ApplyElevatedShadow(float lift)
        {
            if (_shadow == null)
            {
                return;
            }

            Vector2 cardPosition = ((RectTransform)transform).anchoredPosition;

            _shadow.anchoredPosition = cardPosition + new Vector2(30f, -38f) * lift;
            _shadow.sizeDelta = new Vector2(CardWidth * (1f + 0.04f * lift), CardHeight * (1f + 0.04f * lift));
            SetShadowAlpha(_shadow, 0.48f * lift);

            if (_contact != null)
            {
                _contact.anchoredPosition = cardPosition + new Vector2(12f * lift, -CardHeight * 0.5f - 8f * lift);
                _contact.sizeDelta = new Vector2(CardWidth * 0.9f, 24f);
                SetShadowAlpha(_contact, 0.34f * lift);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_onDrop == null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var self = (RectTransform)transform;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform.parent, eventData.position, camera, out local))
            {
                _dragOffset = self.anchoredPosition - local;
            }

            SetElevated(true);
            _homePosition = self.anchoredPosition;
            transform.SetAsLastSibling();

            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.enabled = false;
            }

            if (_dragStart != null)
            {
                _dragStart(_cardId);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform.parent, eventData.position, camera, out local))
            {
                ((RectTransform)transform).anchoredPosition = local + _dragOffset;
                ApplyElevatedShadow(_lift);

                if (_onDragging != null)
                {
                    _onDragging();
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.enabled = true;
            }

            SetElevated(false);

            if (_dragEnd != null)
            {
                _dragEnd();
            }

            // 先把落位交出去（玩家放在哪就是哪），再由 controller 判断落点是否触发规则
            if (_onMoved != null)
            {
                _onMoved(_cardId, ((RectTransform)transform).anchoredPosition);
            }

            if (_onDrop != null)
            {
                _onDrop(_cardId, eventData.position, ((RectTransform)transform).anchoredPosition);
            }
        }

        /// <summary>把牌放回拿起来之前的位置（放进分析台、或触发行动后用）。</summary>
        public void RestoreHome()
        {
            ((RectTransform)transform).anchoredPosition = _homePosition;
        }

        public void ApplyShadow(Vector2 gridPosition)
        {
            if (_shadow == null)
            {
                return;
            }

            Vector2 direction = gridPosition - LightPosition;
            float distance = direction.magnitude;
            if (distance < 0.0001f)
            {
                _shadow.gameObject.SetActive(false);
                if (_contact != null)
                {
                    _contact.gameObject.SetActive(false);
                }

                return;
            }

            _shadow.gameObject.SetActive(true);
            direction /= distance;

            float t = Mathf.Clamp01(distance);

            // 影子在独立层里，位置要和卡片对齐：卡片锚点 + 偏移
            var self = (RectTransform)transform;
            Vector2 cardPosition = self.anchoredPosition;

            _shadow.anchoredPosition = cardPosition + direction * (14f + 44f * t);
            _shadow.sizeDelta = new Vector2(CardWidth * 1.02f, CardHeight * 1.02f);
            SetShadowAlpha(_shadow, Mathf.Lerp(0.52f, 0.30f, t));

            if (_contact != null)
            {
                _contact.gameObject.SetActive(true);
                _contact.anchoredPosition = cardPosition + new Vector2(0f, -CardHeight * 0.5f + 5f);
                _contact.sizeDelta = new Vector2(CardWidth * 0.86f, 24f);
                SetShadowAlpha(_contact, Mathf.Lerp(0.55f, 0.34f, t));
            }
        }

        private static RectTransform MakeSoftShadow(Transform parent, string name, Color color)
        {
            RectTransform rect = UIFactory.MakePanel(parent, name, color, false);
            var image = rect.GetComponent<Image>();
            image.sprite = UIFactory.SoftShadow;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void SetShadowAlpha(RectTransform rect, float alpha)
        {
            var image = rect.GetComponent<Image>();
            var color = image.color;
            color.a = alpha;
            image.color = color;
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
            _displayName = card.Definition.DisplayName;
            _onClick = onClick;
            _edge = GetComponent<Image>();

            RectTransform inner = UIFactory.MakePanel(
                transform,
                "Inner",
                card.IsFaceUp ? Palette.Paper : new Color(0.17f, 0.14f, 0.11f),
                true,
                9f);
            UIFactory.Stretch(inner, 5, 5, 5, 5);

            Sprite backArt = !card.IsFaceUp ? ArtLibrary.CardBack : null;
            if (backArt != null)
            {
                var innerImage = inner.GetComponent<Image>();
                innerImage.sprite = backArt;
                innerImage.type = Image.Type.Simple;
                innerImage.color = Color.white;
            }

            // 受光渐变：灯在右上，卡片右上亮、左下暗，和影子方向一致
            var lightGo = new GameObject("Light", typeof(RectTransform), typeof(Image));
            lightGo.transform.SetParent(inner, false);
            lightGo.transform.SetAsFirstSibling();
            var lightRect = (RectTransform)lightGo.transform;
            UIFactory.Stretch(lightRect, 1, 1, 1, 1);
            var lightImage = lightGo.GetComponent<Image>();
            lightImage.sprite = UIFactory.LightGradient;
            lightImage.color = new Color(1f, 0.96f, 0.88f, 0.30f);
            lightImage.raycastTarget = false;

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

                Sprite art = ArtLibrary.LoadCardArt(card.Definition.ArtKey);
                if (art != null)
                {
                    // 立绘按「铺满卡面、顶部对齐、溢出裁掉」摆放：竖版半身像不会被压扁
                    RectTransform holder = UIFactory.MakePanel(inner, "ArtHolder",
                        new Color(0f, 0f, 0f, 0f), false);
                    UIFactory.Stretch(holder, 6, 6, 52, 30);
                    holder.gameObject.AddComponent<RectMask2D>();

                    var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artGo.transform.SetParent(holder, false);
                    var artRect = (RectTransform)artGo.transform;
                    artRect.anchorMin = new Vector2(0f, 1f);
                    artRect.anchorMax = new Vector2(1f, 1f);
                    artRect.pivot = new Vector2(0.5f, 1f);
                    artRect.anchoredPosition = Vector2.zero;

                    float areaWidth = 150f;
                    float aspect = art.rect.width / Mathf.Max(1f, art.rect.height);
                    artRect.sizeDelta = new Vector2(0f, areaWidth / Mathf.Max(0.05f, aspect));

                    var artImage = artGo.GetComponent<Image>();
                    artImage.sprite = art;
                    artImage.color = Color.white;
                    artImage.preserveAspect = true;
                    artImage.raycastTarget = false;
                }
                else
                {
                    Text glyph = UIFactory.MakeText(inner, "Glyph", Palette.CategoryGlyph(card.Definition.Category), 54,
                        new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.32f), TextAnchor.MiddleCenter);
                    RectTransform glyphRect = glyph.rectTransform;
                    glyphRect.anchorMin = Vector2.zero;
                    glyphRect.anchorMax = Vector2.one;
                    glyphRect.offsetMin = new Vector2(0f, 34f);
                    glyphRect.offsetMax = new Vector2(0f, -54f);
                }

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
                Text hint = UIFactory.MakeText(inner, "Hint", "点击翻开", 15,
                    new Color(0.86f, 0.78f, 0.60f, backArt != null ? 0.85f : 0.65f), TextAnchor.MiddleCenter);
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
