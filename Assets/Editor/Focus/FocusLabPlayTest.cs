using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 进播放模式跑一遍交互（悬停 / 翻面 / 拖拽 / 两种字幕），把每一步截成 PNG，并断言状态。
    /// 截图走主相机的 RenderTexture，所以不需要开 Game 视图。
    /// 步骤状态存在 SessionState 里，能跨「进入/退出播放模式」的程序集重载。
    /// </summary>
    [InitializeOnLoad]
    public static class FocusLabPlayTest
    {
        private const string RunKey = "FocusLab.PlayTest.Run";
        private const string StepKey = "FocusLab.PlayTest.Step";
        private const string DeadlineKey = "FocusLab.PlayTest.Deadline";
        private const string FailKey = "FocusLab.PlayTest.Failures";
        private const string OutputFolder = "Logs/FocusLabShots";

        private const int Width = 1920;
        private const int Height = 1080;
        private const int StopStep = 12;

        /// <summary>提示字幕在测试里只给 3 秒，省得等 30 秒。</summary>
        private const float ShortHintDuration = 3f;

        private static RenderTexture _target;
        private static string _scriptBodyBefore;

        static FocusLabPlayTest()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Focus Lab/跑交互测试并截图")]
        public static void RunFromMenu()
        {
            Start();
        }

        /// <summary>-executeMethod 入口。</summary>
        public static void RunBatch()
        {
            Start();
        }

        private static void Start()
        {
            EditorSceneManager.OpenScene(FocusLabSceneBuilder.ScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(OutputPath());

            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(StepKey, 1);
            SessionState.SetFloat(DeadlineKey, 0f);
            SessionState.SetString(FailKey, string.Empty);

            Debug.Log("[FocusLab] play test: enter play mode");
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunKey, false))
            {
                return;
            }

            int step = SessionState.GetInt(StepKey, 0);

            if (step >= StopStep)
            {
                if (!EditorApplication.isPlaying)
                {
                    Finish();
                }

                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(DeadlineKey, 0f))
            {
                return;
            }

            Run(step);
        }

        private static void Run(int step)
        {
            if (step >= 2 && !TryFindScene(out _, out _, out _))
            {
                GoTo(StopStep, 0f);
                return;
            }

            switch (step)
            {
                case 1:
                {
                    if (Time.time < 0.2f)
                    {
                        SetDeadline(EditorApplication.timeSinceStartup + 0.1f);
                        return;
                    }

                    Camera camera = Camera.main;
                    if (camera == null)
                    {
                        Fail("没有 MainCamera，无法截图");
                        GoTo(StopStep, 0f);
                        return;
                    }

                    EnsureTarget(camera);
                    GoTo(2, 0.6f);
                    return;
                }

                case 2:
                {
                    TryFindScene(out FocusCardView[] cards, out _, out FocusSubtitlePanel subtitle);

                    Capture("01_idle");

                    foreach (FocusCardView card in cards)
                    {
                        Check(!card.IsFaceUp, "开局卡牌应该都是背面：" + card.name);
                    }

                    Check(!subtitle.IsVisible, "进场时不该有字幕");

                    var categories = new System.Collections.Generic.HashSet<FocusCardCategory>();
                    foreach (FocusCardView card in cards)
                    {
                        categories.Add(card.Category);
                    }

                    Check(categories.Count == 3, "三张卡应该是三种不同分类，实际 " + categories.Count + " 种");

                    GoTo(3, 0.3f);
                    return;
                }

                case 3:
                {
                    TryFindScene(out FocusCardView[] cards, out FocusCardTooltip tooltip, out _);

                    // 悬停中间那张：浮层 + 一条提示字幕应该同时出现。
                    cards[1].OnPointerEnter(null);
                    tooltip.ShowAt(cards[1].TooltipTitle, cards[1].TooltipHint, TooltipAnchor(cards[1]));

                    GoTo(4, 0.6f);
                    return;
                }

                case 4:
                {
                    TryFindScene(out FocusCardView[] cards, out FocusCardTooltip tooltip, out FocusSubtitlePanel subtitle);

                    Capture("02_hover");

                    float scale = cards[1].transform.localScale.x;
                    Check(cards[1].IsHovered, "悬停状态没有生效");
                    Check(Mathf.Abs(scale - 1.1f) < 0.02f, "悬停缩放应为 1.1，实际 " + scale.ToString("0.###"));
                    Check(tooltip.IsVisible, "浮层没有显示");
                    Check(tooltip.TitleText == "未翻开", "背面卡牌的浮层标题应为「未翻开」，实际「" + tooltip.TitleText + "」");
                    Check(subtitle.IsHint, "悬停时应该出现提示字幕");
                    Check(subtitle.KindText == FocusSubtitlePanel.HintKindName,
                        "字幕类型应显示「提示」，实际「" + subtitle.KindText + "」");

                    cards[1].OnPointerExit(null);
                    tooltip.Hide();

                    ClickCard(cards[1]);
                    GoTo(5, 0.6f);
                    return;
                }

                case 5:
                {
                    TryFindScene(out FocusCardView[] cards, out FocusCardTooltip tooltip, out FocusSubtitlePanel subtitle);

                    Check(cards[1].IsFaceUp, "点击背面应该把牌翻开");
                    Check(!cards[1].IsFlipping, "翻面动画应该已经结束");
                    Check(subtitle.IsScript, "翻面时应该出现台本字幕");
                    Check(subtitle.KindText == FocusSubtitlePanel.ScriptKindName,
                        "字幕类型应显示「台本」，实际「" + subtitle.KindText + "」");
                    Check(subtitle.SpeakerText == cards[1].DisplayName,
                        "台本字幕的说话人应为卡名「" + cards[1].DisplayName + "」，实际「" + subtitle.SpeakerText + "」");
                    Check(!string.IsNullOrEmpty(subtitle.BodyText), "台本字幕正文为空");

                    Text nameLabel = cards[1].GetComponentInChildren<Text>(true);
                    Check(nameLabel != null && nameLabel.text == cards[1].DisplayName, "翻开后应该显示卡名");

                    _scriptBodyBefore = subtitle.BodyText;

                    // 截图用：翻面后鼠标还停在牌上，浮层会跟着鼠标跑到左下方压住字幕。
                    // 截图有一帧延迟，所以这里先收起来，等下一步开头再拍。
                    tooltip.HideImmediate();

                    GoTo(6, 1.4f);
                    return;
                }

                case 6:
                {
                    TryFindScene(out FocusCardView[] cards, out _, out FocusSubtitlePanel subtitle);

                    Capture("03_flip");

                    // 台本字幕没有倒计时：1.4 秒过去必须还在。
                    Check(subtitle.IsScript, "台本字幕不该自动消失");

                    // 正面朝上的牌再点一次不应该翻回去。
                    ClickCard(cards[1]);
                    Check(cards[1].IsFaceUp, "正面朝上的牌不应该翻回背面");

                    // 台本字幕不会被提示字幕顶掉。
                    cards[0].OnPointerEnter(null);
                    cards[0].OnPointerExit(null);
                    Check(subtitle.IsScript, "台本字幕被提示字幕顶掉了");
                    Check(subtitle.BodyText == _scriptBodyBefore, "台本字幕正文被改写了");

                    GoTo(7, 0.3f);
                    return;
                }

                case 7:
                {
                    TryFindScene(out _, out _, out FocusSubtitlePanel subtitle);

                    // 点一下字幕把它收掉。
                    subtitle.OnPointerClick(null);
                    Check(!subtitle.IsVisible, "点击台本字幕后应该收起");

                    // 再来一条 3 秒的提示字幕，测倒计时。
                    subtitle.ShowHint("提示：三张牌都翻开之后，钟楼的灯就会亮起来。", ShortHintDuration);
                    Check(subtitle.IsHint, "提示字幕没有显示出来");

                    GoTo(8, 0.9f);
                    return;
                }

                case 8:
                {
                    TryFindScene(out _, out _, out FocusSubtitlePanel subtitle);

                    Capture("04_hint");

                    float remaining = subtitle.RemainingSeconds;
                    Check(subtitle.IsVisible, "提示字幕应该还在");
                    Check(remaining > 0f && remaining < ShortHintDuration,
                        "提示字幕剩余时间应在 (0, " + ShortHintDuration + ") 之间，实际 " + remaining.ToString("0.##"));

                    GoTo(9, 2.6f);
                    return;
                }

                case 9:
                {
                    TryFindScene(out FocusCardView[] cards, out FocusCardTooltip tooltip, out FocusSubtitlePanel subtitle);

                    // 提示字幕到点自动消失，不需要点。
                    Check(!subtitle.IsVisible, "提示字幕到时间应该自动消失");

                    // 拖拽：故意给一个超出画布的位置，验证会被裁回容器内。
                    var canvasRect = (RectTransform)UnityEngine.Object.FindAnyObjectByType<Canvas>().transform;
                    cards[2].MoveTo(new Vector2(5000f, 5000f));

                    var cardRect = (RectTransform)cards[2].transform;
                    float maxX = canvasRect.rect.width * 0.5f - cardRect.rect.width * 0.5f;
                    float maxY = canvasRect.rect.height * 0.5f - cardRect.rect.height * 0.5f;

                    Check(cardRect.anchoredPosition.x <= maxX + 1f && cardRect.anchoredPosition.x >= -maxX - 1f,
                        "拖拽后 X 没有被裁进容器：" + cardRect.anchoredPosition.x.ToString("0.#") + " / max " + maxX.ToString("0.#"));
                    Check(cardRect.anchoredPosition.y <= maxY + 1f && cardRect.anchoredPosition.y >= -maxY - 1f,
                        "拖拽后 Y 没有被裁进容器：" + cardRect.anchoredPosition.y.ToString("0.#") + " / max " + maxY.ToString("0.#"));

                    // 截图用：裁边验证完了，挪到画面中间空一点的地方，别贴在角上。
                    cards[2].MoveTo(new Vector2(-430f, 170f));

                    // 悬停已经翻开的牌：浮层标题换成卡名。
                    FocusCardView faceUp = FindFaceUp(cards);
                    if (faceUp == null)
                    {
                        Fail("找不到已经翻开的卡牌");
                    }
                    else
                    {
                        faceUp.OnPointerEnter(null);
                        tooltip.ShowAt(faceUp.TooltipTitle, faceUp.TooltipHint, TooltipAnchor(faceUp));
                    }

                    GoTo(10, 0.5f);
                    return;
                }

                case 10:
                {
                    TryFindScene(out FocusCardView[] cards, out FocusCardTooltip tooltip, out _);

                    Capture("05_dragged");

                    FocusCardView faceUp = FindFaceUp(cards);
                    if (faceUp != null)
                    {
                        Check(tooltip.TitleText == faceUp.DisplayName,
                            "正面卡牌的浮层标题应为卡名「" + faceUp.DisplayName + "」，实际「" + tooltip.TitleText + "」");
                        Check(tooltip.HintText == faceUp.TooltipHint,
                            "正面卡牌的浮层副标题应为「" + faceUp.TooltipHint + "」，实际「" + tooltip.HintText + "」");
                    }

                    GoTo(11, 0.1f);
                    return;
                }

                case 11:
                {
                    ReleaseTarget();
                    Debug.Log("[FocusLab] play test: leave play mode");
                    EditorApplication.ExitPlaymode();
                    GoTo(StopStep, 0f);
                    return;
                }

                default:
                    GoTo(StopStep, 0f);
                    return;
            }
        }

        private static void Finish()
        {
            SessionState.SetBool(RunKey, false);

            string failures = SessionState.GetString(FailKey, string.Empty);
            if (string.IsNullOrEmpty(failures))
            {
                Debug.Log("[FocusLab] PLAYTEST RESULT=PASS");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("[FocusLab] PLAYTEST RESULT=FAIL\n" + failures);
            EditorApplication.Exit(1);
        }

        private static bool TryFindScene(
            out FocusCardView[] cards,
            out FocusCardTooltip tooltip,
            out FocusSubtitlePanel subtitle)
        {
            cards = UnityEngine.Object.FindObjectsByType<FocusCardView>(UnityEngine.FindObjectsInactive.Exclude);
            tooltip = UnityEngine.Object.FindAnyObjectByType<FocusCardTooltip>();
            subtitle = UnityEngine.Object.FindAnyObjectByType<FocusSubtitlePanel>();

            if (cards == null || cards.Length < 3)
            {
                Fail("场景里找不到 3 张卡牌");
                return false;
            }

            // 按屏幕上的横向顺序排，截图里的「第几张」跟数组下标才对得上。
            Array.Sort(cards, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            if (tooltip == null)
            {
                Fail("场景里找不到名字浮层");
                return false;
            }

            if (subtitle == null)
            {
                Fail("场景里找不到字幕面板");
                return false;
            }

            return true;
        }

        private static FocusCardView FindFaceUp(FocusCardView[] cards)
        {
            foreach (FocusCardView card in cards)
            {
                if (card != null && card.IsFaceUp)
                {
                    return card;
                }
            }

            return null;
        }

        private static void ClickCard(FocusCardView card)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = ScreenPointOf(card.transform),
            };

            card.OnPointerDown(eventData);
            card.OnPointerUp(eventData);
        }

        private static void EnsureTarget(Camera camera)
        {
            if (_target != null)
            {
                return;
            }

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "FocusLabCapture",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
            };
            _target.Create();

            camera.targetTexture = _target;
            Debug.Log("[FocusLab] capture target " + Width + "x" + Height);
        }

        private static void ReleaseTarget()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.DestroyImmediate(_target);
                _target = null;
            }
        }

        private static void Capture(string name)
        {
            if (_target == null)
            {
                Fail("截图失败：没有 RenderTexture");
                return;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;

            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            texture.Apply();

            RenderTexture.active = previous;

            string path = Path.Combine(OutputPath(), name + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            Debug.Log("[FocusLab] shot " + path + " (" + new FileInfo(path).Length + " bytes)");
        }

        private static Vector2 ScreenPointOf(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Vector2.zero;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, target.position);

            // 稍微往卡牌右上方偏一点，别把卡面挡住。
            return screen + new Vector2(120f, 120f);
        }

        /// <summary>浮层贴着卡牌右上角放，截图里不会压住隔壁的牌。</summary>
        private static Vector2 TooltipAnchor(FocusCardView card)
        {
            if (card == null)
            {
                return new Vector2(960f, 540f);
            }

            RectTransform rect = card.transform as RectTransform;
            Camera camera = Camera.main;

            if (rect == null || camera == null)
            {
                return ScreenPointOf(card.transform);
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            // corners[2] 是右上角。
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return topRight + new Vector2(16f, -8f);
        }

        private static string OutputPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, OutputFolder);
        }

        private static void GoTo(int step, float delay)
        {
            SessionState.SetInt(StepKey, step);
            SetDeadline(EditorApplication.timeSinceStartup + delay);
        }

        private static void SetDeadline(double deadline)
        {
            SessionState.SetFloat(DeadlineKey, (float)deadline);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                Fail(message);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[FocusLab] FAIL " + message);
            string existing = SessionState.GetString(FailKey, string.Empty);
            SessionState.SetString(FailKey, existing + "- " + message + "\n");
        }
    }
}
