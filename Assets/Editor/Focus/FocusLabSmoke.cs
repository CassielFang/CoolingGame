using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 快速冒烟：进播放模式等两帧、把画面渲到 RenderTexture 存图、检查运行期状态，然后立刻退出。
    /// 只做「能不能跑起来 + 笔记本主链路通不通」，完整交互（真鼠标拖拽的手感）由用户亲自试。
    ///
    /// 批处理入口：-executeMethod FocusTest.EditorTools.FocusLabSmoke.RunBatch
    /// </summary>
    [InitializeOnLoad]
    public static class FocusLabSmoke
    {
        private const string RunKey = "FocusLab.Smoke.Run";
        private const string StepKey = "FocusLab.Smoke.Step";
        private const string DeadlineKey = "FocusLab.Smoke.Deadline";
        private const string FailKey = "FocusLab.Smoke.Failures";
        private const string ShotDir = "Logs/FocusLabShots";
        private const int ShotWidth = 1920;
        private const int ShotHeight = 1080;

        private static readonly List<string> Failures = new List<string>();
        private static RenderTexture _target;
        private static int _cleanFrames;
        private static int _siblingBefore = -1;
        private static string _droppedClueText;

        static FocusLabSmoke()
        {
            EditorApplication.update += Tick;
        }

        /// <summary>-executeMethod 入口。</summary>
        public static void RunBatch()
        {
            Failures.Clear();
            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(StepKey, 0);
            SessionState.SetFloat(DeadlineKey, (float)(EditorApplication.timeSinceStartup + 240d));
            SessionState.SetString(FailKey, string.Empty);

            EditorSceneManager.OpenScene(FocusLabSceneBuilder.ScenePath, OpenSceneMode.Single);
            Application.logMessageReceived += OnLog;
            EditorApplication.EnterPlaymode();
            Debug.Log("[FocusLab] SMOKE start");
        }

        /// <summary>播放模式里任何异常/报错都记成失败，避免「看起来没崩」的假通过。</summary>
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(RunKey, false))
            {
                return;
            }

            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            {
                return;
            }

            if (condition.StartsWith("[FocusLab]", StringComparison.Ordinal))
            {
                return;
            }

            AddFailure("运行期日志报错：" + condition);
        }

        private static int Step
        {
            get => SessionState.GetInt(StepKey, 0);
            set => SessionState.SetInt(StepKey, value);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunKey, false))
            {
                return;
            }

            double deadline = SessionState.GetFloat(DeadlineKey, 0f);
            if (deadline > 0d && EditorApplication.timeSinceStartup > deadline)
            {
                AddFailure("冒烟超时（没能在限定时间内走完）");
                Finish();
                return;
            }

            try
            {
                switch (Step)
                {
                    case 0:
                        if (Application.isPlaying)
                        {
                            Step = 1;
                        }

                        break;

                    case 1:
                        if (!Application.isPlaying)
                        {
                            break;
                        }

                        // 等两帧：一帧脚本 Awake/Start，一帧 UI 布局落定。
                        if (Time.frameCount < 2)
                        {
                            break;
                        }

                        StartCapture();
                        Step = 2;
                        break;

                    case 2:
                        if (!WaitFrames(2))
                        {
                            break;
                        }

                        CaptureImage("00_layout");
                        CheckRuntimeState();
                        SelectForPreview();
                        Step = 3;
                        break;

                    case 3:
                        if (!WaitFrames(2))
                        {
                            break;
                        }

                        CaptureImage("00_selected");
                        CheckSelectionState();
                        UnityEngine.Object.FindAnyObjectByType<FocusLabController>()?.ClearSelection();
                        StartNovel();
                        Step = 4;
                        break;

                    case 4:
                        if (!WaitFrames(2))
                        {
                            break;
                        }

                        CaptureImage("00_vn");
                        CheckNovelState();

                        // 再挑一帧「左右立绘同时在」的，验证右侧是另一块独立图层。
                        FocusVisualNovelPlayer vnPlayer = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
                        if (vnPlayer != null && !TryPlayFrame(vnPlayer, needLeft: true, needRight: true))
                        {
                            AddFailure("目录里没有「左右立绘同时在」的帧，挑不出截图样本");
                        }

                        Step = 5;
                        break;

                    case 5:
                        if (!WaitFrames(2))
                        {
                            break;
                        }

                        CaptureImage("00_vn_two");
                        CheckBothSides();
                        StopNovel();
                        OpenNotebook();
                        Step = 6;
                        break;

                    case 6:
                        if (!WaitFrames(3))
                        {
                            break;
                        }

                        // 开局线索页本来就该是空的（线索要随剧情记进来），先把这一条断言过掉，
                        // 再把演示用的线索全部记上，后面的分组/拖拽才有东西可看。
                        CheckNotebookEmpty();
                        CollectAllClues();
                        Step = 7;
                        break;

                    case 7:
                        if (!WaitFrames(3))
                        {
                            break;
                        }

                        CaptureImage("01_notebook");
                        CheckNotebookOpen();
                        Step = 8;
                        break;

                    case 8:
                        if (!WaitFrames(2))
                        {
                            break;
                        }

                        CaptureImage("02_notebook_case");
                        CheckClueGrouping();
                        DragIntoSlot();
                        Step = 9;
                        break;

                    case 9:
                        if (!WaitFrames(3))
                        {
                            break;
                        }

                        CaptureImage("03_drop");
                        CheckDropResult();
                        CloseNotebook();
                        StopCapture();
                        Step = 10;
                        break;

                    case 10:
                        if (Application.isPlaying)
                        {
                            EditorApplication.ExitPlaymode();
                            break;
                        }

                        Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AddFailure("冒烟过程中抛异常：" + exception.Message);
                Finish();
            }
        }

        /// <summary>攒够 n 帧再往下走；返回 false 表示还在等（或已退出播放）。</summary>
        private static bool WaitFrames(int count)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            _cleanFrames++;
            if (_cleanFrames < count)
            {
                return false;
            }

            _cleanFrames = 0;
            return true;
        }

        // ---- 笔记本相关 -------------------------------------------------

        private static FocusNotebookPanel FindNotebook()
        {
            return UnityEngine.Object.FindAnyObjectByType<FocusNotebookPanel>(UnityEngine.FindObjectsInactive.Include);
        }

        /// <summary>走场景里真实的入口道具：点右键上的笔记本按钮。</summary>
        private static void OpenNotebook()
        {
            FocusNotebookSource source = UnityEngine.Object.FindAnyObjectByType<FocusNotebookSource>(UnityEngine.FindObjectsInactive.Include);
            if (source == null)
            {
                AddFailure("找不到右上角的笔记本入口 FocusNotebookSource");
                return;
            }

            source.Open();
        }

        private static void CloseNotebook()
        {
            FindNotebook()?.Close();
        }

        /// <summary>刚开本时线索页应该是空的：线索要随剧情一条条记进来。</summary>
        private static void CheckNotebookEmpty()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null)
            {
                return;
            }

            if (notebook.ClueCount != 0)
            {
                AddFailure("刚打开笔记本时线索页应该是空的，实际 " + notebook.ClueCount + " 条");
            }
        }

        /// <summary>把演示用的线索全部记进来（正式流程里由剧情/翻牌触发）。</summary>
        private static void CollectAllClues()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null)
            {
                AddFailure("找不到笔记本面板，无法记录线索");
                return;
            }

            int guard = 0;
            while (notebook.CollectNextClue() && guard < 64)
            {
                guard++;
            }

            notebook.RebuildCluePage();
        }

        /// <summary>开本之后：面板要打开、要摊开书、每条演示线索都在列表里。</summary>
        private static void CheckNotebookOpen()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null)
            {
                AddFailure("找不到笔记本面板");
                return;
            }

            if (!notebook.IsOpen)
            {
                AddFailure("点右上角的笔记本道具后，面板没有打开");
                return;
            }

            if (notebook.Alpha < 0.5f)
            {
                AddFailure("笔记本打开了但没淡入（alpha=" + notebook.Alpha.ToString("0.00") + "）");
            }

            if (notebook.ClueCount != FocusLabSetup.ExpectedClueCount)
            {
                AddFailure("线索页应记下 " + FocusLabSetup.ExpectedClueCount + " 条线索，实际 " + notebook.ClueCount);
            }

            if (notebook.Entries.Count != notebook.ClueCount)
            {
                AddFailure("线索条目数（" + notebook.Entries.Count + "）和线索数（" + notebook.ClueCount + "）不一致");
            }

            if (notebook.Board == null || notebook.Board.Slots.Length != FocusLabSetup.ExpectedQuestionCount)
            {
                AddFailure("案件板槽位不是 " + FocusLabSetup.ExpectedQuestionCount + " 个");
            }

            // 每条线索都得挂在自己的来源卡下面，并且按来源卡分组挨着排。
            var seenKeys = new List<string>();
            foreach (FocusClueEntry entry in notebook.Entries)
            {
                if (entry == null || entry.Clue == null)
                {
                    AddFailure("线索页里出现了空条目");
                    continue;
                }

                if (entry.Clue.SourceCard == null)
                {
                    AddFailure("线索「" + entry.Clue.Text + "」没有来源卡");
                }

                if (string.IsNullOrEmpty(entry.DisplayText))
                {
                    AddFailure("线索「" + entry.Clue.ClueId + "」的条目没有显示短句");
                }
            }

            // 用兄弟序号验一下同来源相邻：同一来源的条目序号必须是连续的一段。
            var order = new List<string>();
            var bySibling = new List<FocusClueEntry>(notebook.Entries);
            bySibling.Sort((a, b) => notebook.SiblingIndexFor(a).CompareTo(notebook.SiblingIndexFor(b)));
            string lastKey = null;
            foreach (FocusClueEntry entry in bySibling)
            {
                string key = entry.Clue != null && entry.Clue.SourceCard != null ? entry.Clue.SourceCard.CardId : "(none)";
                if (key != lastKey)
                {
                    if (seenKeys.Contains(key))
                    {
                        AddFailure("同一来源「" + key + "」的线索被拆成了不连续的两段");
                    }

                    seenKeys.Add(key);
                    lastKey = key;
                }

                order.Add(entry.Clue != null ? entry.Clue.Text : "?");
            }

            Debug.Log("[FocusLab] SMOKE clues=" + string.Join(" / ", order));
            DumpCluePage();
        }

        /// <summary>临时诊断：把线索页上文字/条目的实际状态打出来（排查左页空白）。</summary>
        private static void DumpCluePage()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null)
            {
                return;
            }

            Transform content = notebook.ClueContent;
            Debug.Log("[FocusLab] SMOKE diag content=" +
                      (content == null ? "null" : content.name + " children=" + content.childCount +
                       " worldRect=" + RectToString(content) + " active=" + content.gameObject.activeInHierarchy));

            if (content != null)
            {
                var contentRect = (RectTransform)content;
                for (int i = 0; i < Mathf.Min(content.childCount, 6); i++)
                {
                    RectTransform child = (RectTransform)content.GetChild(i);
                    Debug.Log("[FocusLab] SMOKE diag @" + i + " " + child.name + " " + RectToString(child) +
                              " layoutH=" + LayoutUtility.GetPreferredHeight(child));
                }

                CanvasGroup scrollGroup = content.parent != null ? content.parent.parent.GetComponent<CanvasGroup>() : null;
                if (scrollGroup != null)
                {
                    Debug.Log("[FocusLab] SMOKE diag scrollGroup alpha=" + scrollGroup.alpha);
                }

                // 从 Content 往上逐层打出每个 RectTransform 的 worldRect / 尺寸 / 锚点，定位哪一层塌了。
                for (Transform t = content; t != null && t != notebook.transform.parent; t = t.parent)
                {
                    var rt = (RectTransform)t;
                    Debug.Log("[FocusLab] SMOKE diag chain " + t.name + " rect=" + RectToString(t) +
                              " size=" + rt.rect.size + " anchor=" + rt.anchorMin + ".." + rt.anchorMax +
                              " pivot=" + rt.pivot + " scale=" + rt.localScale + " active=" + t.gameObject.activeSelf);
                }
            }

            Text[] labels = notebook.GetComponentsInChildren<Text>(true);
            int shown = 0;
            foreach (Text label in labels)
            {
                if (!label.gameObject.activeInHierarchy || label.canvasRenderer.cull)
                {
                    continue;
                }

                if (shown++ >= 8)
                {
                    break;
                }

                Debug.Log("[FocusLab] SMOKE diag text '" + label.name + "' = '" + label.text + "' font=" +
                          (label.font != null ? label.font.name : "null") + " size=" + label.fontSize +
                          " color=" + label.color + " rect=" + RectToString(label.rectTransform) +
                          " cull=" + label.canvasRenderer.cull + " alpha=" + label.canvasRenderer.GetAlpha());
            }

            Debug.Log("[FocusLab] SMOKE diag visibleTexts=" + shown + " / " + labels.Length);
        }

        private static string RectToString(Transform rect)
        {
            if (rect == null)
            {
                return "null";
            }

            Vector3[] corners = new Vector3[4];
            ((RectTransform)rect).GetWorldCorners(corners);
            return "x[" + corners[0].x.ToString("0") + ".." + corners[2].x.ToString("0") + "] y[" +
                   corners[0].y.ToString("0") + ".." + corners[2].y.ToString("0") + "]";
        }

        /// <summary>切到案件页并确认五槽都是空的；把一条线索拖进第一个槽。</summary>
        private static void CheckClueGrouping()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null || !notebook.IsOpen)
            {
                AddFailure("案件页检查时笔记本没开着");
                return;
            }

            if (notebook.Board == null)
            {
                AddFailure("没有案件板");
                return;
            }

            // 右侧案件板要先翻到「案件」页才会亮起来，槽位也才有屏幕坐标可言。
            notebook.ShowCasePage();

            if (!notebook.Board.gameObject.activeInHierarchy)
            {
                AddFailure("翻到案件页后案件板仍然是隐藏的");
            }

            if (notebook.Board.FilledCount != 0)
            {
                AddFailure("还没拖线索，案件板就已经填了 " + notebook.Board.FilledCount + " 个槽");
            }

            foreach (FocusNotebookSlot slot in notebook.Board.Slots)
            {
                if (slot == null || slot.Question == null)
                {
                    AddFailure("案件板上有没绑问题的槽");
                }
            }
        }

        /// <summary>
        /// 不模拟真鼠标（那属于用户手测），直接走面板的拖拽 API：
        /// BeginClueDrag → MoveDraggedClue 打到第一个槽 → EndClueDrag 松手。
        /// </summary>
        private static void DragIntoSlot()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null || notebook.Board == null)
            {
                return;
            }

            FocusClueEntry entry = null;
            foreach (FocusClueEntry candidate in notebook.Entries)
            {
                if (candidate != null && candidate.Clue != null && !candidate.IsDetached)
                {
                    entry = candidate;
                    break;
                }
            }

            if (entry == null)
            {
                AddFailure("线索页里没有可以拖的条目");
                return;
            }

            FocusNotebookSlot slot = notebook.Board.Slots[0];
            if (slot == null)
            {
                AddFailure("第一个槽是空的引用");
                return;
            }

            _siblingBefore = notebook.SiblingIndexFor(entry);
            _droppedClueText = entry.Clue.Text;

            // 用屏幕坐标打到槽上，跟真拖拽走同一条路径（含吸附判定）。
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(notebook.UiCamera, slot.transform.position);
            if (!notebook.BeginClueDrag(entry, screen))
            {
                AddFailure("拖拽没能开始");
                return;
            }

            if (!entry.IsDragging)
            {
                AddFailure("开始拖拽后条目没有进入跟随状态");
            }

            notebook.MoveDraggedClue(entry, screen);

            if (notebook.DraggedEntry != entry)
            {
                AddFailure("拖拽中的条目不是被拖的那条");
            }

            if (!notebook.EndClueDrag(entry, screen, released: true))
            {
                AddFailure("松手在空槽上，却没有吸附进去");
            }
        }

        /// <summary>吸附之后：槽里有线索、线索页原位那条变成虚线空位、兄弟序号没变。</summary>
        private static void CheckDropResult()
        {
            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null || notebook.Board == null)
            {
                AddFailure("检查投放结果时找不到笔记本");
                return;
            }

            FocusNotebookSlot slot = notebook.Board.Slots[0];
            if (slot == null || slot.Clue == null)
            {
                AddFailure("拖进去之后第一个槽还是空的");
                return;
            }

            if (slot.Clue.Text != _droppedClueText)
            {
                AddFailure("槽里的线索不是被拖进去的那条");
            }

            if (notebook.Board.FilledCount != 1)
            {
                AddFailure("案件板应填了 1 个槽，实际 " + notebook.Board.FilledCount);
            }

            FocusClueEntry kept = notebook.FindEntry(slot.Clue);
            if (kept == null)
            {
                AddFailure("线索被拖走后，原位置没有留下条目（要求原位刷新）");
                return;
            }

            if (!kept.IsDetached)
            {
                AddFailure("原位置的条目没有变成「已放上案件板」的空位");
            }

            if (notebook.SiblingIndexFor(kept) != _siblingBefore)
            {
                AddFailure("原位置的条目换了位置（拖走前 " + _siblingBefore + "，拖走后 " + notebook.SiblingIndexFor(kept) + "）");
            }

            if (notebook.Entries.Count != notebook.ClueCount)
            {
                AddFailure("投放后线索条目数变了");
            }

            // 取下来要能恢复。
            notebook.ClearSlot(slot);
            if (slot.Clue != null)
            {
                AddFailure("ClearSlot 之后槽里还有线索");
            }

            if (notebook.FindEntry(kept.Clue) != kept || kept.IsDetached)
            {
                AddFailure("把线索从槽里取下来后，原位置的条目没有恢复");
            }

            // 再放一次，截图看得到吸附结果。
            notebook.ApplyDrop(kept, slot);
        }

        // ---- 原有各段的检查 ---------------------------------------------

        private static void StartCapture()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                AddFailure("场景里没有主相机，没法截图");
                Step = 3;
                EditorApplication.ExitPlaymode();
                return;
            }

            _target = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
            };
            _target.Create();
            camera.targetTexture = _target;
            _cleanFrames = 0;
        }

        private static void CaptureImage(string fileName)
        {
            if (_target == null)
            {
                return;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;

            var texture = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, ShotWidth, ShotHeight), 0, 0);
            texture.Apply(false, false);

            RenderTexture.active = previous;

            Directory.CreateDirectory(ShotDir);
            string path = ShotDir + "/" + fileName + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);

            Debug.Log("[FocusLab] SMOKE shot=" + path);
        }

        /// <summary>
        /// 冒烟不模拟鼠标事件，直接让控制器走一遍选中流程：
        /// 人物卡 + 非人物卡各翻开一张并选中，用来确认立绘、描边、确认键真的亮起来。
        /// </summary>
        private static void SelectForPreview()
        {
            FocusLabController controller = UnityEngine.Object.FindAnyObjectByType<FocusLabController>();
            FocusCardView[] cards = UnityEngine.Object.FindObjectsByType<FocusCardView>(UnityEngine.FindObjectsInactive.Exclude);
            if (controller == null || cards.Length == 0)
            {
                return;
            }

            FocusCardView character = null;
            FocusCardView target = null;
            foreach (FocusCardView card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                if (card.IsCharacter && character == null)
                {
                    character = card;
                }
                else if (!card.IsCharacter && target == null)
                {
                    target = card;
                }
            }

            if (character == null || target == null)
            {
                AddFailure("找不到人物卡或非人物卡，没法验证选中流程");
                return;
            }

            character.SetFaceUp(true);
            target.SetFaceUp(true);
            controller.NotifyClicked(character);
            controller.NotifyClicked(target);
        }

        private static void CheckSelectionState()
        {
            FocusLabController controller = UnityEngine.Object.FindAnyObjectByType<FocusLabController>();
            if (controller == null)
            {
                AddFailure("选中后找不到 FocusLabController");
                return;
            }

            if (!controller.HasPendingOperation)
            {
                AddFailure("人物 + 非人物都选中了，却没有待确认的操作");
            }

            if (controller.SelectedCharacter == null || controller.SelectedTarget == null)
            {
                AddFailure("选中后两侧卡牌没有都记下来");
            }

            if (!controller.IsConfirmVisible)
            {
                AddFailure("双选之后确认键没有亮起来");
            }

            FocusPortraitPanel portrait = UnityEngine.Object.FindAnyObjectByType<FocusPortraitPanel>();
            if (portrait == null || !portrait.IsVisible)
            {
                AddFailure("选中人物卡后立绘面板没有出现");
            }
            else if (portrait.PortraitSprite == null)
            {
                AddFailure("立绘面板里没有立绘贴图");
            }
        }

        private static void StartNovel()
        {
            FocusVisualNovelPlayer player = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
            if (player == null)
            {
                AddFailure("场景里没有视觉小说播放器");
                return;
            }

            if (player.Novels == null || player.Novels.Count == 0)
            {
                AddFailure("视觉小说目录是空的，先导入 xlsx");
                return;
            }

            // 挑一组「只有左侧立绘」的帧截图——右侧空着，能看出左右是分开的。
            if (!TryPlayFrame(player, needLeft: true, needRight: false))
            {
                AddFailure("目录里没有「只有左侧立绘」的帧，挑不出截图样本");
            }
        }

        /// <summary>按「这一帧有哪些立绘」挑一段剧本并推进到那一帧。</summary>
        private static bool TryPlayFrame(FocusVisualNovelPlayer player, bool needLeft, bool needRight)
        {
            IReadOnlyList<FocusVisualNovel> novels = player.Novels;
            for (int n = 0; n < novels.Count; n++)
            {
                FocusVisualNovel novel = novels[n];
                if (novel == null)
                {
                    continue;
                }

                IReadOnlyList<FocusVnFrame> frames = novel.Frames;
                for (int f = 0; f < frames.Count; f++)
                {
                    FocusVnFrame frame = frames[f];
                    bool hasLeft = IsDeclared(frame.LeftPortrait);
                    bool hasRight = IsDeclared(frame.RightPortrait);
                    if (hasLeft != needLeft || hasRight != needRight)
                    {
                        continue;
                    }

                    if (!FocusVnDirector.Play(novel.NovelId))
                    {
                        AddFailure("按 id 触发视觉小说失败：" + novel.NovelId);
                        return false;
                    }

                    // Play 会先显示第 0 帧，再调 f 次刚好落到目标帧。
                    for (int step = 0; step < f; step++)
                    {
                        FocusVnDirector.Advance();
                    }

                    return true;
                }
            }

            return false;
        }

        private static void CheckNovelState()
        {
            FocusVisualNovelPlayer player = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
            if (player == null)
            {
                return;
            }

            if (!player.IsPlaying)
            {
                AddFailure("视觉小说没有进入播放状态");
            }

            FocusVnFrame frame = player.CurrentFrame;
            if (frame == null)
            {
                AddFailure("视觉小说没有当前帧");
            }
            else if (string.IsNullOrEmpty(frame.Line))
            {
                AddFailure("视觉小说第一帧没有台词");
            }

            if (player.Dialogue == null || !player.Dialogue.IsVisible)
            {
                AddFailure("视觉小说台词没有走到字幕面板");
            }

            FocusVnPortrait[] portraits = UnityEngine.Object.FindObjectsByType<FocusVnPortrait>(UnityEngine.FindObjectsInactive.Exclude);
            if (portraits.Length != 2)
            {
                AddFailure("视觉小说应有左右两个立绘层，实际 " + portraits.Length);
            }

            foreach (FocusVnPortrait portrait in portraits)
            {
                if (portrait.LayerCanvas == null)
                {
                    AddFailure(portrait.name + " 缺少独立 Canvas");
                }
            }

            // 这一帧声明了左侧立绘：左边要亮。
            if (frame != null && IsDeclared(frame.LeftPortrait))
            {
                FocusVnPortrait left = FindSide(portraits, true);
                if (left == null || !left.IsVisible || left.Sprite == null)
                {
                    AddFailure("这一帧有左侧立绘，但左侧没有显示出来");
                }
            }

            // 这一帧明确写了「清空右侧」：右边不能亮。
            if (frame != null && FocusArtLibrary.IsClearToken(frame.RightPortrait))
            {
                FocusVnPortrait right = FindSide(portraits, false);
                if (right != null && right.IsVisible)
                {
                    AddFailure("这一帧把右侧立绘清空了，右侧却还亮着");
                }
            }
        }

        /// <summary>这一帧有没有显式声明这一侧的立绘（留空=沿用，不算声明）。</summary>
        private static bool IsDeclared(string text)
        {
            return !string.IsNullOrEmpty(text) && !FocusArtLibrary.IsClearToken(text);
        }

        /// <summary>左右立绘同时在的一帧：两边各用各的图层，都得亮。</summary>
        private static void CheckBothSides()
        {
            FocusVnPortrait[] portraits = UnityEngine.Object.FindObjectsByType<FocusVnPortrait>(UnityEngine.FindObjectsInactive.Exclude);

            FocusVnPortrait left = FindSide(portraits, true);
            FocusVnPortrait right = FindSide(portraits, false);

            if (left == null || right == null)
            {
                AddFailure("找不到左右立绘层");
                return;
            }

            if (!left.IsVisible || left.Sprite == null)
            {
                AddFailure("左右同时在的那帧，左侧立绘没显示");
            }

            if (!right.IsVisible || right.Sprite == null)
            {
                AddFailure("左右同时在的那帧，右侧立绘没显示");
            }

            Canvas leftCanvas = left.LayerCanvas;
            Canvas rightCanvas = right.LayerCanvas;
            if (leftCanvas == null || rightCanvas == null)
            {
                AddFailure("立绘层缺少独立 Canvas");
            }
            else if (leftCanvas == rightCanvas)
            {
                AddFailure("左右立绘共用了同一个 Canvas，光效没法分开做");
            }

            FocusVisualNovelPlayer player = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
            if (player != null && player.Dialogue != null && !player.Dialogue.IsVisible)
            {
                AddFailure("视觉小说在播，字幕面板却是收着的");
            }
        }

        private static FocusVnPortrait FindSide(FocusVnPortrait[] portraits, bool left)
        {
            foreach (FocusVnPortrait portrait in portraits)
            {
                if (portrait == null)
                {
                    continue;
                }

                Image image = portrait.GetComponentInChildren<Image>(true);
                if (image == null)
                {
                    continue;
                }

                // 左侧立绘的图片节点 anchor 靠左，右侧靠右——用这个区分，不依赖命名。
                bool isLeft = image.rectTransform.anchorMin.x < 0.5f;
                if (isLeft == left)
                {
                    return portrait;
                }
            }

            return null;
        }

        private static void StopNovel()
        {
            FocusVnDirector.Stop();
        }

        private static void StopCapture()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.Destroy(_target);
                _target = null;
            }
        }

        private static void CheckRuntimeState()
        {
            FocusCardView[] cards = UnityEngine.Object.FindObjectsByType<FocusCardView>(UnityEngine.FindObjectsInactive.Exclude);
            if (cards.Length != FocusLabSetup.ExpectedCardCount)
            {
                AddFailure("运行期卡牌数应为 " + FocusLabSetup.ExpectedCardCount + "，实际 " + cards.Length);
            }

            foreach (FocusCardView card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                if (card.IsFaceUp)
                {
                    AddFailure("卡牌 " + card.name + " 进场就是正面");
                }

                if (card.IsSelected)
                {
                    AddFailure("卡牌 " + card.name + " 进场就是选中状态");
                }
            }

            FocusLabController controller = UnityEngine.Object.FindAnyObjectByType<FocusLabController>();
            if (controller == null)
            {
                AddFailure("运行期找不到 FocusLabController");
                return;
            }

            if (controller.HasPendingOperation)
            {
                AddFailure("进场就有待确认的操作");
            }

            FocusPortraitPanel portrait = UnityEngine.Object.FindAnyObjectByType<FocusPortraitPanel>();
            if (portrait != null && portrait.IsVisible)
            {
                AddFailure("立绘面板进场就亮着");
            }

            FocusSubtitlePanel subtitle = UnityEngine.Object.FindAnyObjectByType<FocusSubtitlePanel>();
            if (subtitle != null && subtitle.IsVisible)
            {
                AddFailure("字幕面板进场就亮着");
            }

            if (controller.ConfirmButton != null && controller.IsConfirmVisible)
            {
                AddFailure("确认键进场就亮着");
            }

            FocusVisualNovelPlayer vnPlayer = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
            if (vnPlayer == null)
            {
                AddFailure("运行期找不到视觉小说播放器");
            }
            else
            {
                if (vnPlayer.IsPlaying)
                {
                    AddFailure("进场时视觉小说不该在播");
                }

                if (vnPlayer.Library == null)
                {
                    AddFailure("视觉小说播放器没有绑定目录资产");
                }
            }

            FocusVnPortrait[] vnPortraits = UnityEngine.Object.FindObjectsByType<FocusVnPortrait>(UnityEngine.FindObjectsInactive.Exclude);
            foreach (FocusVnPortrait vnPortrait in vnPortraits)
            {
                if (vnPortrait != null && vnPortrait.IsVisible)
                {
                    AddFailure(vnPortrait.name + " 进场就亮着");
                }
            }

            FocusNotebookPanel notebook = FindNotebook();
            if (notebook == null)
            {
                AddFailure("运行期找不到笔记本面板");
            }
            else if (notebook.IsOpen)
            {
                AddFailure("笔记本进场就是打开的");
            }

            // 截图能存下来，说明相机与 Canvas 至少在渲染。
            if (!File.Exists(ShotDir + "/00_layout.png"))
            {
                AddFailure("没有生成截图 " + ShotDir + "/00_layout.png");
            }
        }

        private static void AddFailure(string message)
        {
            Failures.Add(message);
            Debug.LogWarning("[FocusLab] SMOKE FAIL " + message);
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(RunKey, false);

            if (Application.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }

            StopCapture();

            if (Failures.Count > 0)
            {
                Debug.LogError("[FocusLab] SMOKE RESULT=FAIL 共 " + Failures.Count + " 项：\n  - " + string.Join("\n  - ", Failures));
                SessionState.SetString(FailKey, string.Join("；", Failures));
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[FocusLab] SMOKE RESULT=PASS shot=" + ShotDir + "/00_layout.png");
            EditorApplication.Exit(0);
        }
    }
}
