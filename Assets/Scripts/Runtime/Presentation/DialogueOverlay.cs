using System;
using System.Collections;
using CoolingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 逐句剧情演出：左右立绘 + 底部对话框 + 打字机。
    /// 数据来自 CaseDefinition.Dialogues（由策划的文案表生成），这里只负责播。
    /// 点画面任意处继续；正在打字时点击会先补全整句。
    /// </summary>
    public sealed class DialogueOverlay : MonoBehaviour
    {
        private Image _left;
        private Image _right;
        private Text _speaker;
        private Text _body;
        private Text _hint;

        private DialogueScript _script;
        private int _index;
        private Coroutine _typing;
        private bool _lineShown;
        private Action _onFinished;

        public bool Visible
        {
            get { return gameObject.activeSelf; }
        }

        public static DialogueOverlay Create(Transform parent)
        {
            RectTransform root = UIFactory.MakePanel(parent, "DialogueOverlay", new Color(0.02f, 0.02f, 0.02f, 1f), false);
            UIFactory.Stretch(root);

            var overlay = root.gameObject.AddComponent<DialogueOverlay>();
            overlay.Build(root);
            root.gameObject.SetActive(false);
            return overlay;
        }

        public void Play(DialogueScript script, Action onFinished)
        {
            if (script == null || script.Lines == null || script.Lines.Count == 0)
            {
                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            _script = script;
            _index = 0;
            _onFinished = onFinished;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowLine(0);
        }

        private void Build(RectTransform root)
        {
            // 点空白处继续
            var advance = root.gameObject.AddComponent<Button>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(Advance);

            _left = MakePortrait(root, "Left", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 40f));
            _right = MakePortrait(root, "Right", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 40f));

            // 对话框：美术的对话框九宫格
            RectTransform box = UIFactory.MakePanel(root, "DialogueBox", new Color(0.88f, 0.84f, 0.76f, 1f));
            box.anchorMin = new Vector2(0.5f, 0f);
            box.anchorMax = new Vector2(0.5f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.anchoredPosition = new Vector2(0f, 36f);
            box.sizeDelta = new Vector2(1560f, 148f);

            var boxImage = box.GetComponent<Image>();
            Sprite dialogArt = ArtLibrary.LoadCardArt("ui/dialog");
            if (dialogArt != null)
            {
                boxImage.sprite = dialogArt;
                boxImage.type = Image.Type.Sliced;
                boxImage.color = Color.white;
            }

            _speaker = UIFactory.MakeText(box, "Speaker", string.Empty, 26, Palette.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(_speaker.rectTransform, 64, 18, 460, 36);

            _body = UIFactory.MakeText(box, "Body", string.Empty, 26, Palette.Paper, TextAnchor.UpperLeft);
            RectTransform bodyRect = _body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(64f, 18f);
            bodyRect.offsetMax = new Vector2(-80f, -52f);
            _body.lineSpacing = 1.35f;

            _hint = UIFactory.MakeText(box, "Hint", "点击继续", 15,
                new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.45f), TextAnchor.LowerRight);
            UIFactory.PlaceRightBottom(_hint.rectTransform, 56, 16, 200, 24);
        }

        private Image MakePortrait(RectTransform root, string name, Vector2 anchor, Vector2 pivot, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(700f, 900f);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            return image;
        }

        private void ShowLine(int index)
        {
            if (_script == null || index < 0 || index >= _script.Lines.Count)
            {
                Finish();
                return;
            }

            _index = index;
            DialogueLine line = _script.Lines[index];
            _lineShown = false;

            ApplyPortrait(_left, line.LeftArtKey, line.LeftDimmed);
            ApplyPortrait(_right, line.RightArtKey, line.RightDimmed);

            _speaker.text = string.IsNullOrEmpty(line.Speaker) ? string.Empty : line.Speaker;

            if (_typing != null)
            {
                StopCoroutine(_typing);
            }

            _typing = StartCoroutine(TypeLine(line.Text));
        }

        private void ApplyPortrait(Image image, string artKey, bool dimmed)
        {
            if (image == null)
            {
                return;
            }

            Sprite art = string.IsNullOrEmpty(artKey) ? null : ArtLibrary.LoadCardArt(artKey);
            image.sprite = art;
            image.enabled = art != null;

            if (art == null)
            {
                return;
            }

            // 说话的一方亮，另一方压暗（策划表的「（暗）」）
            float value = dimmed ? 0.42f : 1f;
            image.color = new Color(value, value, value, 1f);
        }

        private IEnumerator TypeLine(string text)
        {
            _body.text = string.Empty;
            string full = text ?? string.Empty;

            // 每帧出几个字，中文读起来更顺
            const float charsPerSecond = 55f;
            float shown = 0f;

            while (shown < full.Length)
            {
                shown += Mathf.Max(1f, charsPerSecond * Time.unscaledDeltaTime);
                int count = Mathf.Min(full.Length, Mathf.FloorToInt(shown));
                _body.text = full.Substring(0, count);
                yield return null;
            }

            _body.text = full;
            _lineShown = true;
            _typing = null;
        }

        private void Advance()
        {
            if (_script == null)
            {
                return;
            }

            // 打字中：先补全整句
            if (!_lineShown || _typing != null)
            {
                if (_typing != null)
                {
                    StopCoroutine(_typing);
                    _typing = null;
                }

                _body.text = _script.Lines[_index].Text;
                _lineShown = true;
                return;
            }

            if (_index + 1 >= _script.Lines.Count)
            {
                Finish();
                return;
            }

            ShowLine(_index + 1);
        }

        private void Finish()
        {
            _script = null;
            gameObject.SetActive(false);

            Action callback = _onFinished;
            _onFinished = null;
            if (callback != null)
            {
                callback();
            }
        }
    }
}
