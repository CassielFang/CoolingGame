using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 用代码搭出 Focus_Lab 测试场景：相机 + 画布 + 背景 + 三张可交互卡牌 + 名字浮层 + 视觉小说 + 笔记本。
    /// 全部用代码生成，场景文件可以随时删掉重来。
    /// </summary>
    public static class FocusLabSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Focus_Lab.unity";

        /// <summary>笔记本层里所有节点的前缀：遮罩层节点名，别和别的层撞。</summary>
        public const string NotebookRootName = "Notebook";
        public const string NotebookButtonName = "Btn_Notebook";

        private const float CameraSize = 5.4f;

        /// <summary>卡牌尺寸：450×600 的 3/5。</summary>
        private static readonly Vector2 CardSize = new Vector2(270f, 360f);

        /// <summary>6 张牌 3 列 2 行排在右侧，左侧留给人物立绘。</summary>
        private static readonly Vector2[] CardPositions =
        {
            new Vector2(-80f, 220f),
            new Vector2(260f, 220f),
            new Vector2(600f, 220f),
            new Vector2(-80f, -200f),
            new Vector2(260f, -200f),
            new Vector2(600f, -200f),
        };

        public static void Build(IReadOnlyList<FocusCardData> cards)
        {
            Build(cards, null);
        }

        /// <summary>建场景；notebook 为 null 时不建笔记本层。</summary>
        public static void Build(IReadOnlyList<FocusCardData> cards, FocusLabAssetBuilder.NotebookDefinitions? notebook)
        {
            if (cards == null || cards.Count == 0)
            {
                throw new InvalidOperationException("没有可用的卡牌数据，先建 Assets/Data 里的实例。");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            CreateEventSystem();

            RectTransform canvasRect = CreateCanvas(camera, out Canvas canvas);
            CreateBackground(canvasRect);
            // 背景图里已经烧了 FOCUS LAB 标题，场景里不再叠一层文字，避免重影。

            RectTransform cardsRoot = NewRect("Cards", canvasRect);
            Stretch(cardsRoot);

            var views = new FocusCardView[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                Vector2 position = i < CardPositions.Length
                    ? CardPositions[i]
                    : new Vector2(600f + ((i - 5) * 340f), -200f);
                views[i] = CreateCard(cardsRoot, cards[i], i, position);
            }

            FocusCardTooltip tooltip = CreateTooltip(canvasRect);
            FocusPortraitPanel portrait = CreatePortrait(canvasRect);

            var controllerGo = new GameObject("FocusLab");
            var controller = controllerGo.AddComponent<FocusLabController>();
            var vnPlayer = controllerGo.AddComponent<FocusVisualNovelPlayer>();

            // 图层顺序（后面的盖前面）：背景 → 卡牌 → 名字浮层 → 选中立绘 → 视觉小说 → 字幕 → 确认键 → 调试按钮
            //                                    → 笔记本道具 →（笔记本打开时）遮罩 + 摊开的书。
            VnLayer vn = CreateVisualNovel(canvasRect);
            FocusSubtitlePanel subtitle = CreateSubtitle(canvasRect);
            vnPlayer.EditorBind(
                vn.Root,
                vn.Group,
                vn.Background,
                vn.Left,
                vn.Right,
                subtitle,
                vn.Catcher,
                AssetDatabase.LoadAssetAtPath<FocusVisualNovelLibrary>(FocusVnImporter.LibraryPath),
                FocusArtIndexer.EnsureLibrary());
            vnPlayer.HideImmediate();

            Button confirm = CreateConfirmButton(canvasRect, controller, out CanvasGroup confirmGroup, out Text confirmLabel);
            controller.EditorBind(views, tooltip, subtitle, portrait, confirm, confirmGroup, confirmLabel);

            CreateDebugButtons(canvasRect, controller);

            if (notebook.HasValue && notebook.Value.Clues != null && notebook.Value.Questions != null)
            {
                CreateNotebook(canvasRect, notebook.Value.Clues, notebook.Value.Questions, cards, subtitle);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.035f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            go.AddComponent<AudioListener>();
            EnsureUniversalCameraData(go);

            return camera;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // 工程的输入只开了新输入系统，模块必须用 InputSystemUIInputModule，
            // 它在 OnEnable 里会自己补一套默认 UI actions。
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static RectTransform CreateCanvas(Camera camera, out Canvas canvas)
        {
            var go = new GameObject("UI Canvas", typeof(RectTransform));
            var rect = (RectTransform)go.transform;

            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            return rect;
        }

        private static void CreateBackground(RectTransform canvasRect)
        {
            RectTransform rect = NewRect("Background", canvasRect);
            Stretch(rect);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.BackgroundFile);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            // 要接点击：点空白处取消选中。
            image.raycastTarget = true;

            rect.gameObject.AddComponent<FocusBackground>();
        }

        private static FocusCardView CreateCard(RectTransform parent, FocusCardData data, int index, Vector2 position)
        {
            RectTransform root = NewRect("Card_" + (index + 1).ToString("00"), parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = CardSize;
            root.anchoredPosition = position;

            // 根节点上的透明 Image 只负责接收鼠标事件，面积不受翻面压扁影响。
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;

            RectTransform flip = NewRect("Flip", root);
            Stretch(flip);

            var face = flip.gameObject.AddComponent<Image>();
            face.sprite = data.FrontSprite;
            face.type = Image.Type.Simple;
            face.preserveAspect = true;
            face.raycastTarget = false;

            RectTransform nameRect = NewRect("Name", flip);
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            // 卡面底部预留的名片板按 450×600 画在 34..148px，缩到 0.6 倍后对应 20..89px。
            nameRect.sizeDelta = new Vector2(-38f, 60f);
            nameRect.anchoredPosition = new Vector2(0f, 25f);

            Text nameLabel = AddText(nameRect, "Label", data.DisplayName, 22, new Color(0.93f, 0.97f, 1f, 1f), TextAnchor.MiddleCenter);

            // 选中描边：一张白色圆角环，运行时按分类染色。
            RectTransform selectionRect = NewRect("Selection", root);
            Stretch(selectionRect);
            var selectionRing = selectionRect.gameObject.AddComponent<Image>();
            selectionRing.sprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.OutlineFile);
            selectionRing.type = Image.Type.Simple;
            selectionRing.preserveAspect = true;
            selectionRing.raycastTarget = false;
            selectionRing.color = FocusCardCategories.Accent(data.Category);
            selectionRing.enabled = false;

            var view = root.gameObject.AddComponent<FocusCardView>();
            view.EditorBind(data, flip, face, nameLabel, selectionRing);
            // 开局全部背面朝上：只有背面的牌才能翻开。
            view.SetFaceUp(false);

            return view;
        }

        /// <summary>左侧人物立绘：选中人物卡时滑出，占位图按人物染色区分。</summary>
        private static FocusPortraitPanel CreatePortrait(RectTransform canvasRect)
        {
            RectTransform rect = NewRect("Portrait", canvasRect);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(430f, 720f);
            rect.anchoredPosition = new Vector2(44f, 96f);

            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var frame = rect.gameObject.AddComponent<Image>();
            frame.color = new Color(0.05f, 0.07f, 0.11f, 0.92f);
            frame.raycastTarget = false;

            RectTransform imageRect = NewRect("PortraitImage", rect);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = new Vector2(8f, 8f);
            imageRect.offsetMax = new Vector2(-8f, -8f);
            var portraitImage = imageRect.gameObject.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            portraitImage.enabled = false;

            RectTransform tintRect = NewRect("Tint", rect);
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.offsetMin = new Vector2(8f, 8f);
            tintRect.offsetMax = new Vector2(-8f, -8f);
            var tintOverlay = tintRect.gameObject.AddComponent<Image>();
            tintOverlay.color = new Color(1f, 1f, 1f, 0f);
            tintOverlay.raycastTarget = false;

            // 底部名字条：压一条深色底，免得浅色立绘上看不清字。
            RectTransform nameBand = NewRect("NameBand", rect);
            nameBand.anchorMin = new Vector2(0f, 0f);
            nameBand.anchorMax = new Vector2(1f, 0f);
            nameBand.pivot = new Vector2(0.5f, 0f);
            nameBand.sizeDelta = new Vector2(0f, 74f);
            nameBand.anchoredPosition = Vector2.zero;
            var bandImage = nameBand.gameObject.AddComponent<Image>();
            bandImage.color = new Color(0.02f, 0.03f, 0.05f, 0.82f);
            bandImage.raycastTarget = false;

            RectTransform roleRect = NewRect("RoleChip", rect);
            roleRect.anchorMin = new Vector2(0f, 1f);
            roleRect.anchorMax = new Vector2(0f, 1f);
            roleRect.pivot = new Vector2(0f, 1f);
            roleRect.sizeDelta = new Vector2(74f, 30f);
            roleRect.anchoredPosition = new Vector2(16f, -14f);
            var roleChip = roleRect.gameObject.AddComponent<Image>();
            roleChip.color = new Color(0.14f, 0.2f, 0.3f, 0.95f);
            roleChip.raycastTarget = false;
            Text roleLabel = AddText(roleRect, "Label", "角色", 16, new Color(0.72f, 0.82f, 0.94f, 1f), TextAnchor.MiddleCenter);

            Text nameLabel = AddText(nameBand, "NameLabel", "人物", 28, new Color(0.93f, 0.97f, 1f, 1f), TextAnchor.MiddleLeft);
            nameLabel.rectTransform.offsetMin = new Vector2(20f, 10f);
            nameLabel.rectTransform.offsetMax = new Vector2(-20f, -10f);

            var panel = rect.gameObject.AddComponent<FocusPortraitPanel>();
            panel.EditorBind(rect, group, frame, portraitImage, tintOverlay, nameLabel, roleLabel);

            return panel;
        }

        /// <summary>确认操作按钮：人物 + 非人物同时选中时才亮，位置由控制器贴着目标牌摆。</summary>
        private static Button CreateConfirmButton(
            RectTransform canvasRect,
            FocusLabController controller,
            out CanvasGroup group,
            out Text label)
        {
            RectTransform rect = NewRect("Btn_Confirm", canvasRect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 46f);
            rect.anchoredPosition = Vector2.zero;

            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.45f, 0.4f, 0.96f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = AddText(rect, "Label", "执行操作", 24, new Color(0.95f, 0.99f, 1f, 1f), TextAnchor.MiddleCenter);

            // 持久化监听：点确认直接调控制器，不经过中间脚本，也不依赖播放模式里的订阅。
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, controller.ConfirmOperation);

            return button;
        }

        private static FocusCardTooltip CreateTooltip(RectTransform canvasRect)
        {
            RectTransform rect = NewRect("Tooltip", canvasRect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(380f, 116f);
            rect.anchoredPosition = new Vector2(-4000f, -4000f);

            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var panel = rect.gameObject.AddComponent<Image>();
            panel.color = new Color(0.045f, 0.065f, 0.1f, 0.94f);
            panel.raycastTarget = false;

            RectTransform titleRect = NewRect("Title", rect);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-36f, 44f);
            titleRect.anchoredPosition = new Vector2(0f, -14f);
            Text title = AddText(titleRect, "Label", "卡名", 30, new Color(0.93f, 0.97f, 1f, 1f), TextAnchor.MiddleLeft);

            RectTransform hintRect = NewRect("Hint", rect);
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.sizeDelta = new Vector2(-36f, 34f);
            hintRect.anchoredPosition = new Vector2(0f, -60f);
            Text hint = AddText(hintRect, "Label", "点击翻面 · 拖拽移动", 18, new Color(0.6f, 0.72f, 0.86f, 1f), TextAnchor.MiddleLeft);

            var tooltip = rect.gameObject.AddComponent<FocusCardTooltip>();
            tooltip.EditorBind(rect, group, title, hint);
            tooltip.HideImmediate();

            return tooltip;
        }

        private static FocusSubtitlePanel CreateSubtitle(RectTransform canvasRect)
        {
            RectTransform rect = NewRect("Subtitle", canvasRect);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1520f, 180f);
            rect.anchoredPosition = new Vector2(0f, 44f);

            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = true;

            // 面板本身就是点击区域：点一下就能收掉台本字幕。
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.08f, 0.92f);
            background.raycastTarget = true;

            RectTransform barRect = NewRect("AccentBar", rect);
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(6f, -36f);
            barRect.anchoredPosition = new Vector2(16f, 0f);
            var accentBar = barRect.gameObject.AddComponent<Image>();
            accentBar.raycastTarget = false;

            RectTransform chipRect = NewRect("KindChip", rect);
            chipRect.anchorMin = new Vector2(0f, 1f);
            chipRect.anchorMax = new Vector2(0f, 1f);
            chipRect.pivot = new Vector2(0f, 1f);
            chipRect.sizeDelta = new Vector2(80f, 34f);
            chipRect.anchoredPosition = new Vector2(40f, -20f);
            var kindChip = chipRect.gameObject.AddComponent<Image>();
            kindChip.raycastTarget = false;
            Text kindLabel = AddText(chipRect, "Label", FocusSubtitlePanel.ScriptKindName, 18, new Color(0.6f, 0.72f, 0.86f, 1f), TextAnchor.MiddleCenter);

            RectTransform speakerRect = NewRect("Speaker", rect);
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(0f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.sizeDelta = new Vector2(700f, 40f);
            speakerRect.anchoredPosition = new Vector2(136f, -16f);
            Text speaker = AddText(speakerRect, "Label", "旁白", 26, new Color(0.93f, 0.97f, 1f, 1f), TextAnchor.MiddleLeft);

            RectTransform bodyRect = NewRect("Body", rect);
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(40f, 48f);
            bodyRect.offsetMax = new Vector2(-40f, -66f);
            Text body = AddText(bodyRect, "Label", "字幕正文", 28, new Color(0.82f, 0.88f, 0.95f, 1f), TextAnchor.UpperLeft);

            RectTransform footerRect = NewRect("Footer", rect);
            footerRect.anchorMin = new Vector2(1f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(1f, 0f);
            footerRect.sizeDelta = new Vector2(720f, 30f);
            footerRect.anchoredPosition = new Vector2(-32f, 14f);
            Text footer = AddText(footerRect, "Label", "点击继续", 17, new Color(0.55f, 0.66f, 0.8f, 1f), TextAnchor.MiddleRight);

            // 倒计时条没有 sprite，用 anchorMax.x 伸缩宽度（Image 的 filled 模式需要 sprite）。
            RectTransform timerRect = NewRect("Timer", rect);
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = new Vector2(1f, 0f);
            timerRect.pivot = new Vector2(0f, 0f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = new Vector2(0f, 4f);
            var timerFill = timerRect.gameObject.AddComponent<Image>();
            timerFill.color = new Color(0.898f, 0.76f, 0.416f, 0.85f);
            timerFill.raycastTarget = false;

            var panel = rect.gameObject.AddComponent<FocusSubtitlePanel>();
            panel.EditorBind(rect, group, background, accentBar, kindChip, kindLabel, speaker, body, footer, timerFill);
            panel.HideImmediate();

            return panel;
        }

        private static readonly string[] DebugButtonLabels = { "视觉小说", "收起" };

        private static void CreateDebugButtons(RectTransform canvasRect, FocusLabController controller)
        {
            for (int i = 0; i < DebugButtonLabels.Length; i++)
            {
                RectTransform rect = NewRect(i == 0 ? "Btn_VisualNovel" : "Btn_CloseNovel", canvasRect);
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(148f, 46f);
                rect.anchoredPosition = new Vector2(-24f - (i * 160f), -22f);

                var image = rect.gameObject.AddComponent<Image>();
                image.color = new Color(0.09f, 0.13f, 0.2f, 0.92f);

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                AddText(rect, "Label", DebugButtonLabels[i], 20, new Color(0.72f, 0.82f, 0.94f, 1f), TextAnchor.MiddleCenter);

                // 持久化监听：运行中不经过任何中间脚本，点按钮直接调控制器上的方法。
                if (i == 0)
                {
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, controller.PlaySampleVisualNovel);
                }
                else
                {
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, controller.StopVisualNovel);
                }
            }
        }

        // ---- 笔记本 -----------------------------------------------------

        /// <summary>
        /// 笔记本的版面尺寸：一整本摊开的书，1820×920 居中，左页放线索、右页放案件板。
        /// 坐标都以书的左下角为原点（Book 的 pivot 在中心，子节点用 (0,0) 锚点）。
        /// </summary>
        private static class NotebookLayout
        {
            // 书（美术 notebook_book.png 按这个尺寸拉伸，1820×940 压到 920 高刚好盖住页签区域）
            public static readonly Vector2 BookSize = new Vector2(1820f, 920f);

            // 左页：纸页 + 线索列表（纸页图 860×800，右页用同一张图左右翻转当右侧纸）
            public const float LeafWidth = 860f;
            public const float LeafHeight = 800f;
            public const float LeafLeftX = 45f;
            public const float LeafY = 45f;

            // 右页：案件板（软木板压在右侧纸上；纸页右缘在 1775，板右缘 1755）
            public const float BoardWidth = 820f;
            public const float BoardHeight = 740f;
            public const float BoardRightX = 935f;
            public const float BoardY = 75f;

            // 页签
            public const float TabHeight = 48f;
            public const float TabWidth = 168f;
            public const float TabStartX = 168f;

            // 右上角的笔记本道具
            public static readonly Vector2 ItemSize = new Vector2(160f, 124f);
            public static readonly Vector2 ItemPosition = new Vector2(-96f, -80f);

            // 线索条目 / 来源卡
            public const float SourceCardWidth = 116f;
        }

        private static void CreateNotebook(
            RectTransform canvasRect,
            IReadOnlyList<FocusClueData> clues,
            IReadOnlyList<FocusQuestionData> questions,
            IReadOnlyList<FocusCardData> cards,
            FocusSubtitlePanel subtitle)
        {
            Sprite bookSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookBookFile);
            Sprite pagesSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookPagesFile);
            Sprite boardSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookBoardFile);
            Sprite ticketSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookTicketFile);
            Sprite slipSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookSlipFile);
            Sprite slipEmptySprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookSlipEmptyFile);
            Sprite pinSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookPinFile);
            Sprite tapeSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookTapeFile);
            Sprite itemSprite = FocusLabAssetBuilder.LoadSprite(FocusLabAssetBuilder.NotebookItemFile);

            // ---- 遮罩层 ----
            RectTransform root = NewRect(NotebookRootName, canvasRect);
            Stretch(root);

            var rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            // 点黑幕 = 收起笔记本。
            RectTransform dimRect = NewRect("Dim", root);
            Stretch(dimRect);
            var dim = dimRect.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);

            // ---- 摊开的书 ----
            RectTransform bookRect = NewRect("Book", root);
            bookRect.anchorMin = new Vector2(0.5f, 0.5f);
            bookRect.anchorMax = new Vector2(0.5f, 0.5f);
            bookRect.pivot = new Vector2(0.5f, 0.5f);
            bookRect.sizeDelta = NotebookLayout.BookSize;
            bookRect.anchoredPosition = Vector2.zero;

            var cover = bookRect.gameObject.AddComponent<Image>();
            cover.sprite = bookSprite;
            cover.type = Image.Type.Simple;
            cover.preserveAspect = false;

            // 左页纸：底图；线索列表在它上面。
            RectTransform leftPage = NewRect("LeftPage", bookRect);
            leftPage.anchorMin = new Vector2(0f, 0f);
            leftPage.anchorMax = new Vector2(0f, 0f);
            leftPage.pivot = new Vector2(0f, 0f);
            leftPage.sizeDelta = new Vector2(NotebookLayout.LeafWidth, NotebookLayout.LeafHeight);
            leftPage.anchoredPosition = new Vector2(NotebookLayout.LeafLeftX, NotebookLayout.LeafY);

            var pages = leftPage.gameObject.AddComponent<Image>();
            pages.sprite = pagesSprite;
            pages.type = Image.Type.Simple;
            pages.preserveAspect = false;
            pages.raycastTarget = false;

            // 右页纸：同一张纸页图左右翻转，装订侧的阴影就靠在书缝那边。
            RectTransform rightPage = NewRect("RightPage", bookRect);
            rightPage.anchorMin = new Vector2(0f, 0f);
            rightPage.anchorMax = new Vector2(0f, 0f);
            rightPage.pivot = new Vector2(0f, 0f);
            rightPage.sizeDelta = new Vector2(NotebookLayout.LeafWidth, NotebookLayout.LeafHeight);
            rightPage.anchoredPosition = new Vector2(NotebookLayout.BookSize.x - NotebookLayout.LeafLeftX - NotebookLayout.LeafWidth, NotebookLayout.LeafY);
            rightPage.localScale = new Vector3(-1f, 1f, 1f);

            var rightPages = rightPage.gameObject.AddComponent<Image>();
            rightPages.sprite = pagesSprite;
            rightPages.type = Image.Type.Simple;
            rightPages.preserveAspect = false;
            rightPages.raycastTarget = false;

            // 左页标题：固定在纸的顶端，不随线索列表滚动。
            RectTransform titleRect = NewRect("Title", leftPage);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-180f, 46f);
            titleRect.anchoredPosition = new Vector2(60f, -22f);
            Text titleLabel = AddText(titleRect, "Label", "线索", 34, new Color(0.26f, 0.17f, 0.09f, 1f), TextAnchor.MiddleLeft);

            RectTransform subtitleRect = NewRect("SubTitle", leftPage);
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.sizeDelta = new Vector2(-180f, 26f);
            subtitleRect.anchoredPosition = new Vector2(60f, -68f);
            Text subTitle = AddText(subtitleRect, "Label", "按来源卡分组 · 同一条来源下的线索按获得的先后排列", 17, new Color(0.46f, 0.36f, 0.24f, 1f), TextAnchor.MiddleLeft);

            // 纸的胶带角：纯装饰。
            RectTransform tapeRect = NewRect("Tape", leftPage);
            tapeRect.anchorMin = new Vector2(0f, 1f);
            tapeRect.anchorMax = new Vector2(0f, 1f);
            tapeRect.pivot = new Vector2(0.5f, 0.5f);
            tapeRect.sizeDelta = new Vector2(180f, 44f);
            tapeRect.anchoredPosition = new Vector2(0f, 2f);
            tapeRect.localRotation = Quaternion.Euler(0f, 0f, -9f);
            var tape = tapeRect.gameObject.AddComponent<Image>();
            tape.sprite = tapeSprite;
            tape.type = Image.Type.Sliced;
            tape.raycastTarget = false;

            // 线索列表：ScrollRect + VerticalLayout 的行里再套一层按来源分好的组。
            RectTransform scrollRect = NewRect("ClueScroll", leftPage);
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(76f, 56f);
            scrollRect.offsetMax = new Vector2(-76f, -104f);

            var scrollGroup = scrollRect.gameObject.AddComponent<CanvasGroup>();
            scrollGroup.alpha = 1f;
            scrollGroup.blocksRaycasts = false;
            scrollGroup.interactable = false;

            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var scrollImage = scrollRect.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0f);
            scrollImage.raycastTarget = true;

            RectTransform viewport = NewRect("Viewport", scrollRect);
            Stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 4f;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = true;

            // 线索一条都没有时的提示（有线索就藏起来）。
            RectTransform emptyRect = NewRect("EmptyHint", leftPage);
            emptyRect.anchorMin = new Vector2(0f, 0f);
            emptyRect.anchorMax = new Vector2(1f, 1f);
            emptyRect.offsetMin = new Vector2(76f, 56f);
            emptyRect.offsetMax = new Vector2(-76f, -104f);
            Text emptyHint = AddText(emptyRect, "Label", "这一页还是空的——\n翻开桌上的卡片，线索会自动记进来。", 22, new Color(0.48f, 0.38f, 0.26f, 1f), TextAnchor.MiddleCenter);

            // ---- 右页：案件板 ----
            RectTransform boardRect = NewRect("BoardView", bookRect);
            boardRect.anchorMin = new Vector2(0f, 0f);
            boardRect.anchorMax = new Vector2(0f, 0f);
            boardRect.pivot = new Vector2(0f, 0f);
            boardRect.sizeDelta = new Vector2(NotebookLayout.BoardWidth, NotebookLayout.BoardHeight);
            boardRect.anchoredPosition = new Vector2(NotebookLayout.BoardRightX, NotebookLayout.BoardY);

            var boardGroup = boardRect.gameObject.AddComponent<CanvasGroup>();
            boardGroup.alpha = 1f;
            boardGroup.blocksRaycasts = true;
            boardGroup.interactable = true;

            var boardImage = boardRect.gameObject.AddComponent<Image>();
            boardImage.sprite = boardSprite;
            boardImage.type = Image.Type.Sliced;
            boardImage.preserveAspect = false;

            RectTransform boardTitleRect = NewRect("BoardTitle", boardRect);
            boardTitleRect.anchorMin = new Vector2(0f, 1f);
            boardTitleRect.anchorMax = new Vector2(1f, 1f);
            boardTitleRect.pivot = new Vector2(0.5f, 1f);
            boardTitleRect.sizeDelta = new Vector2(-80f, 44f);
            boardTitleRect.anchoredPosition = new Vector2(0f, -12f);
            Text boardTitle = AddText(boardTitleRect, "Label", "案件板", 30, new Color(0.93f, 0.86f, 0.73f, 1f), TextAnchor.MiddleLeft);

            RectTransform boardSummaryRect = NewRect("BoardSummary", boardRect);
            boardSummaryRect.anchorMin = new Vector2(0f, 1f);
            boardSummaryRect.anchorMax = new Vector2(1f, 1f);
            boardSummaryRect.pivot = new Vector2(0.5f, 1f);
            boardSummaryRect.sizeDelta = new Vector2(-80f, 28f);
            boardSummaryRect.anchoredPosition = new Vector2(0f, -52f);
            Text boardSummary = AddText(boardSummaryRect, "Label", "把左边的线索拖到空槽里", 18, new Color(0.72f, 0.62f, 0.47f, 1f), TextAnchor.MiddleLeft);

            RectTransform slotRoot = NewRect("Slots", boardRect);
            slotRoot.anchorMin = new Vector2(0f, 0f);
            slotRoot.anchorMax = new Vector2(1f, 1f);
            slotRoot.offsetMin = new Vector2(34f, 24f);
            slotRoot.offsetMax = new Vector2(-34f, -86f);

            var slotLayout = slotRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            slotLayout.childControlWidth = true;
            slotLayout.childControlHeight = false;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = false;
            slotLayout.spacing = 12f;

            var slots = new List<FocusNotebookSlot>();
            for (int i = 0; i < questions.Count; i++)
            {
                slots.Add(CreateNotebookSlot(slotRoot, questions[i], i, slipSprite, slipEmptySprite, pinSprite, tapeSprite));
            }

            var board = boardRect.gameObject.AddComponent<FocusNotebookCaseBoard>();

            // ---- 页签：书的上沿 ----
            RectTransform tabsRect = NewRect("Tabs", bookRect);
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(0f, 1f);
            tabsRect.pivot = new Vector2(0f, 0f);
            tabsRect.sizeDelta = new Vector2(NotebookLayout.TabWidth * 2f + 8f, NotebookLayout.TabHeight);
            tabsRect.anchoredPosition = new Vector2(NotebookLayout.TabStartX, -6f);

            Button bookTab = CreateTab(tabsRect, "Tab_Clue", FocusNotebookPanel.TabClueName, 0, out Text clueTabLabel);
            Button boardTab = CreateTab(tabsRect, "Tab_Case", FocusNotebookPanel.TabCaseName, 1, out Text caseTabLabel);

            // 页签下划线：跟着当前页滑。
            RectTransform underlineRect = NewRect("Underline", tabsRect);
            underlineRect.anchorMin = new Vector2(0f, 0f);
            underlineRect.anchorMax = new Vector2(0f, 0f);
            underlineRect.pivot = new Vector2(0f, 0f);
            underlineRect.sizeDelta = new Vector2(NotebookLayout.TabWidth, 4f);
            underlineRect.anchoredPosition = new Vector2(0f, 0f);
            var underline = underlineRect.gameObject.AddComponent<Image>();
            underline.color = new Color(0.898f, 0.76f, 0.416f, 1f);
            underline.raycastTarget = false;

            // ---- 关闭键 & 返回键 ----
            Button closeButton = CreateSmallButton(bookRect, "Btn_NotebookClose", "合上", new Vector2(1f, 1f), new Vector2(-28f, -28f), new Color(0.78f, 0.24f, 0.20f, 0.95f));
            Button backButton = CreateSmallButton(bookRect, "Btn_NotebookBack", "翻回线索", new Vector2(0f, 0f), new Vector2(96f, -32f), new Color(0.24f, 0.32f, 0.42f, 0.95f));

            // ---- 页脚 ----
            RectTransform footerRect = NewRect("Footer", bookRect);
            footerRect.anchorMin = new Vector2(0.5f, 0f);
            footerRect.anchorMax = new Vector2(0.5f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.sizeDelta = new Vector2(1000f, 30f);
            footerRect.anchoredPosition = new Vector2(0f, 6f);
            Text footerLabel = AddText(footerRect, "Label", "左键拖条目到右边空槽 · 右键看详情", 17, new Color(0.86f, 0.79f, 0.66f, 1f), TextAnchor.MiddleCenter);

            // ---- 详情面板（右键线索时弹出）----
            RectTransform detailRect = NewRect("Detail", bookRect);
            detailRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailRect.pivot = new Vector2(0.5f, 0.5f);
            detailRect.sizeDelta = new Vector2(780f, 460f);
            detailRect.anchoredPosition = Vector2.zero;

            var detailGroup = detailRect.gameObject.AddComponent<CanvasGroup>();
            detailGroup.alpha = 0f;
            detailGroup.blocksRaycasts = false;
            detailGroup.interactable = false;

            var detailPanel = detailRect.gameObject.AddComponent<Image>();
            detailPanel.sprite = slipSprite;
            detailPanel.type = Image.Type.Sliced;

            RectTransform detailTextRect = NewRect("Text", detailRect);
            Stretch(detailTextRect);
            detailTextRect.offsetMin = new Vector2(30f, 30f);
            detailTextRect.offsetMax = new Vector2(-30f, -30f);
            Text detailText = AddText(detailTextRect, "Label", string.Empty, 21, new Color(0.24f, 0.16f, 0.09f, 1f), TextAnchor.UpperLeft);
            detailText.verticalOverflow = VerticalWrapMode.Truncate;

            // ---- 拖拽层：提起来的条目跟着鼠标，画在整本书之上 ----
            RectTransform dragRect = NewRect("DragLayer", root);
            Stretch(dragRect);

            var dragGroup = dragRect.gameObject.AddComponent<CanvasGroup>();
            dragGroup.alpha = 0f;
            dragGroup.blocksRaycasts = false;
            dragGroup.interactable = false;

            RectTransform ghostRect = NewRect("DragGhost", dragRect);
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = new Vector2(330f, 58f);
            var ghostImage = ghostRect.gameObject.AddComponent<Image>();
            ghostImage.sprite = slipSprite;
            ghostImage.type = Image.Type.Sliced;
            ghostImage.raycastTarget = false;

            RectTransform ghostIconRect = NewRect("Icon", ghostRect);
            ghostIconRect.anchorMin = new Vector2(0f, 0f);
            ghostIconRect.anchorMax = new Vector2(0f, 1f);
            ghostIconRect.pivot = new Vector2(0f, 0.5f);
            ghostIconRect.sizeDelta = new Vector2(7f, 0f);
            ghostIconRect.anchoredPosition = Vector2.zero;
            var ghostIcon = ghostIconRect.gameObject.AddComponent<Image>();
            ghostIcon.raycastTarget = false;

            RectTransform ghostLabelRect = NewRect("Label", ghostRect);
            Stretch(ghostLabelRect);
            ghostLabelRect.offsetMin = new Vector2(18f, 4f);
            ghostLabelRect.offsetMax = new Vector2(-14f, -4f);
            Text ghostLabel = AddText(ghostLabelRect, "Label", string.Empty, 21, new Color(0.24f, 0.16f, 0.09f, 1f), TextAnchor.MiddleLeft);
            ghostLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            ghostLabel.verticalOverflow = VerticalWrapMode.Truncate;

            // ---- 笔记本（面板本体）----
            var notebookGo = new GameObject("NotebookPanel", typeof(RectTransform));
            var notebookRect = (RectTransform)notebookGo.transform;
            notebookRect.SetParent(root, false);
            Stretch(notebookRect);

            var panel = notebookGo.AddComponent<FocusNotebookPanel>();

            var clueArray = new FocusClueData[clues.Count];
            for (int i = 0; i < clues.Count; i++)
            {
                clueArray[i] = clues[i];
            }

            var sourceCards = new FocusCardData[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                sourceCards[i] = cards[i];
            }

            panel.EditorBind(
                root,
                rootGroup,
                bookRect.gameObject,
                boardRect.gameObject,
                bookRect.gameObject,
                bookTab,
                boardTab,
                backButton,
                closeButton,
                underline,
                clueTabLabel,
                caseTabLabel,
                footerLabel,
                detailText,
                emptyHint,
                scroll,
                content,
                board,
                dragGroup,
                ghostRect,
                ghostIcon,
                ghostLabel,
                detailGroup,
                subtitle,
                clueArray,
                sourceCards,
                slipSprite);

            board.EditorBind(panel, slotRoot, slots.ToArray(), boardTitle, boardSummary);

            // 合上键、返回键、点黑幕：走持久化监听，不依赖播放模式里的订阅。
            UnityEditor.Events.UnityEventTools.AddPersistentListener(closeButton.onClick, panel.Close);

            // ---- 右上角的笔记本道具：入口按钮 ----
            RectTransform itemRootRect = NewRect(NotebookButtonName, canvasRect);
            itemRootRect.anchorMin = new Vector2(1f, 1f);
            itemRootRect.anchorMax = new Vector2(1f, 1f);
            itemRootRect.pivot = new Vector2(1f, 1f);
            itemRootRect.sizeDelta = NotebookLayout.ItemSize;
            itemRootRect.anchoredPosition = NotebookLayout.ItemPosition;

            var itemImage = itemRootRect.gameObject.AddComponent<Image>();
            itemImage.sprite = itemSprite;
            itemImage.type = Image.Type.Simple;
            itemImage.preserveAspect = true;

            var itemButton = itemRootRect.gameObject.AddComponent<Button>();
            itemButton.targetGraphic = itemImage;

            RectTransform itemLabelRect = NewRect("Name", itemRootRect);
            itemLabelRect.anchorMin = new Vector2(0.5f, 0f);
            itemLabelRect.anchorMax = new Vector2(0.5f, 0f);
            itemLabelRect.pivot = new Vector2(0.5f, 1f);
            itemLabelRect.sizeDelta = new Vector2(160f, 24f);
            itemLabelRect.anchoredPosition = new Vector2(0f, -4f);
            AddText(itemLabelRect, "Label", "笔记本", 18, new Color(0.86f, 0.79f, 0.66f, 1f), TextAnchor.MiddleCenter);

            var source = itemRootRect.gameObject.AddComponent<FocusNotebookSource>();
            source.EditorBind(panel);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(itemButton.onClick, source.Open);
        }

        private static FocusNotebookSlot CreateNotebookSlot(
            RectTransform parent,
            FocusQuestionData question,
            int index,
            Sprite slip,
            Sprite slipEmpty,
            Sprite pin,
            Sprite tape)
        {
            RectTransform root = NewRect("Slot_" + (question != null ? question.QuestionId : index.ToString()), parent);

            // 槽纸条：比原来高一点，让短语有两行的余地。
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 148f;
            element.minHeight = 148f;
            element.flexibleWidth = 1f;

            RectTransform body = NewRect("Body", root);
            Stretch(body);

            var bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.sprite = slip;
            bodyImage.type = Image.Type.Sliced;
            bodyImage.raycastTarget = false;

            // 空槽的虚线底：填上线索就藏起来。
            RectTransform ghostRect = NewRect("Ghost", body);
            Stretch(ghostRect);
            ghostRect.offsetMin = new Vector2(10f, 10f);
            ghostRect.offsetMax = new Vector2(-10f, -10f);
            var ghost = ghostRect.gameObject.AddComponent<Image>();
            ghost.sprite = slipEmpty;
            ghost.type = Image.Type.Sliced;
            ghost.raycastTarget = false;

            RectTransform frameRect = NewRect("Frame", body);
            Stretch(frameRect);
            var frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = slipEmpty;
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;

            RectTransform tapeRect = NewRect("Tape", body);
            tapeRect.anchorMin = new Vector2(0.5f, 1f);
            tapeRect.anchorMax = new Vector2(0.5f, 1f);
            tapeRect.pivot = new Vector2(0.5f, 0.5f);
            tapeRect.sizeDelta = new Vector2(150f, 38f);
            tapeRect.anchoredPosition = new Vector2(0f, 4f);
            tapeRect.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? -7f : 6f);
            var tapeImage = tapeRect.gameObject.AddComponent<Image>();
            tapeImage.sprite = tape;
            tapeImage.type = Image.Type.Sliced;
            tapeImage.raycastTarget = false;

            RectTransform pinRect = NewRect("Pin", body);
            pinRect.anchorMin = new Vector2(0f, 1f);
            pinRect.anchorMax = new Vector2(0f, 1f);
            pinRect.pivot = new Vector2(0.5f, 0.5f);
            pinRect.sizeDelta = new Vector2(28f, 28f);
            pinRect.anchoredPosition = new Vector2(26f, -18f);
            var pinImage = pinRect.gameObject.AddComponent<Image>();
            pinImage.sprite = pin;
            pinImage.preserveAspect = true;
            pinImage.raycastTarget = false;
            pinImage.enabled = false;

            RectTransform titleRect = NewRect("Title", body);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.sizeDelta = new Vector2(96f, 34f);
            titleRect.anchoredPosition = new Vector2(18f, -14f);
            Text titleLabel = AddText(titleRect, "Label", question != null ? question.Title : "槽", 24, new Color(0.30f, 0.20f, 0.11f, 1f), TextAnchor.MiddleLeft);

            RectTransform hintRect = NewRect("Hint", body);
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.offsetMin = new Vector2(22f, 14f);
            hintRect.offsetMax = new Vector2(-20f, -52f);
            Text hintLabel = AddText(hintRect, "Label", string.Empty, 17, new Color(0.47f, 0.37f, 0.25f, 1f), TextAnchor.UpperLeft);
            hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintLabel.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform clueRect = NewRect("Clue", body);
            clueRect.anchorMin = new Vector2(0f, 0f);
            clueRect.anchorMax = new Vector2(1f, 1f);
            clueRect.offsetMin = new Vector2(22f, 14f);
            clueRect.offsetMax = new Vector2(-20f, -52f);
            Text clueLabel = AddText(clueRect, "Label", string.Empty, 26, new Color(0.62f, 0.45f, 0.30f, 1f), TextAnchor.MiddleLeft);
            clueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            clueLabel.verticalOverflow = VerticalWrapMode.Truncate;
            clueLabel.gameObject.SetActive(false);

            var slot = root.gameObject.AddComponent<FocusNotebookSlot>();
            slot.EditorBind(question, body, frame, ghost, pinImage, titleLabel, hintLabel, clueLabel, body);
            return slot;
        }

        private static Button CreateTab(RectTransform parent, string objectName, string text, int index, out Text label)
        {
            RectTransform rect = NewRect(objectName, parent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(NotebookLayout.TabWidth, NotebookLayout.TabHeight);
            rect.anchoredPosition = new Vector2(index * (NotebookLayout.TabWidth + 8f), 0f);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.42f, 0.28f, 0.18f, 0.96f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = AddText(rect, "Label", text, 24, new Color(0.96f, 0.92f, 0.84f, 1f), TextAnchor.MiddleCenter);
            return button;
        }

        private static Button CreateSmallButton(
            RectTransform parent,
            string objectName,
            string text,
            Vector2 anchor,
            Vector2 position,
            Color color)
        {
            RectTransform rect = NewRect(objectName, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(132f, 44f);
            rect.anchoredPosition = position;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            AddText(rect, "Label", text, 20, new Color(0.97f, 0.95f, 0.92f, 1f), TextAnchor.MiddleCenter);
            return button;
        }

        private struct VnLayer
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Background;
            public FocusVnPortrait Left;
            public FocusVnPortrait Right;
            public Button Catcher;
        }

        /// <summary>
        /// 视觉小说层：全屏底图 + 左右两块独立画布上的立绘 + 一张全屏透明「推进」按钮。
        /// 左右各用一张自己的 Canvas，是为了以后能给某一侧单独加光效/后期而不影响另一边。
        /// </summary>
        private static VnLayer CreateVisualNovel(RectTransform canvasRect)
        {
            RectTransform root = NewRect("VisualNovel", canvasRect);
            Stretch(root);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform backgroundRect = NewRect("VnBackground", root);
            Stretch(backgroundRect);
            var background = backgroundRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);
            background.raycastTarget = false;
            background.enabled = false;

            FocusVnPortrait left = CreateVnPortrait(root, true);
            FocusVnPortrait right = CreateVnPortrait(root, false);

            // 全屏透明按钮：视觉小说播放期间点画面任意处推进下一句。
            RectTransform catcherRect = NewRect("VnCatcher", root);
            Stretch(catcherRect);
            var catcherImage = catcherRect.gameObject.AddComponent<Image>();
            catcherImage.color = new Color(0f, 0f, 0f, 0f);
            catcherImage.raycastTarget = true;

            var catcher = catcherRect.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.targetGraphic = catcherImage;
            catcherRect.gameObject.SetActive(false);

            return new VnLayer
            {
                Root = root,
                Group = group,
                Background = background,
                Left = left,
                Right = right,
                Catcher = catcher,
            };
        }

        private static FocusVnPortrait CreateVnPortrait(RectTransform vnRoot, bool leftSide)
        {
            RectTransform layer = NewRect(leftSide ? "VnLeft" : "VnRight", vnRoot);
            Stretch(layer);

            // 独立画布：以后给单侧加光效/描边时不用动另一侧。
            var canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = false;

            var group = layer.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform artRect = NewRect("Portrait", layer);
            artRect.anchorMin = new Vector2(leftSide ? 0f : 1f, 0f);
            artRect.anchorMax = new Vector2(leftSide ? 0f : 1f, 0f);
            artRect.pivot = new Vector2(leftSide ? 0f : 1f, 0f);
            artRect.sizeDelta = new Vector2(560f, 860f);
            artRect.anchoredPosition = new Vector2(leftSide ? 56f : -56f, 30f);

            var image = artRect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;

            var portrait = layer.gameObject.AddComponent<FocusVnPortrait>();
            portrait.EditorBind(artRect, group, image);
            portrait.HideImmediate();

            return portrait;
        }

        private static Text AddText(RectTransform rect, string name, string content, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var textRect = (RectTransform)go.transform;
            textRect.SetParent(rect, false);
            Stretch(textRect);

            var text = go.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = anchor;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;

            return text;
        }

        private static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>工程用的是 URP，给相机补上 URP 的附加数据（反射，免得编辑器脚本硬依赖 URP）。</summary>
        private static void EnsureUniversalCameraData(GameObject cameraGo)
        {
            Type type = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type != null && cameraGo.GetComponent(type) == null)
            {
                cameraGo.AddComponent(type);
            }
        }
    }
}
