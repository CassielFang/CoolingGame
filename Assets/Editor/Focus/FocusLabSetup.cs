using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// 一键重建：导入设置 → 卡牌数据 → 场景 → 自检。
    /// 命令行入口给批处理用，成功 exit 0，失败 exit 1。
    /// </summary>
    public static class FocusLabSetup
    {
        public const float ExpectedCameraSize = 5.4f;
        public const int ExpectedCardCount = 6;
        public static readonly Vector2 ExpectedCardSize = new Vector2(270f, 360f);

        /// <summary>案件板固定五槽：案件 / 凶手 / 凶器 / 物证 / 人证。</summary>
        public const int ExpectedQuestionCount = 5;

        /// <summary>演示用的线索条数，和 FocusLabAssetBuilder.ClueDefinitions 保持一致。</summary>
        public const int ExpectedClueCount = 15;

        private static readonly (FocusCardCategory Category, int Count)[] ExpectedCategories =
        {
            (FocusCardCategory.Character, 2),
            (FocusCardCategory.Location, 1),
            (FocusCardCategory.Item, 3),
        };

        [MenuItem("Tools/Focus Lab/重建测试场景", false, 0)]
        public static void RebuildFromMenu()
        {
            try
            {
                Rebuild();
                EditorUtility.DisplayDialog("Focus Lab", "测试场景已重建并通过自检。", "好");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Focus Lab", "重建失败：" + exception.Message, "好");
            }
        }

        [MenuItem("Tools/Focus Lab/自检当前场景", false, 1)]
        public static void ValidateFromMenu()
        {
            try
            {
                Validate();
                EditorUtility.DisplayDialog("Focus Lab", "自检通过。", "好");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Focus Lab", exception.Message, "好");
            }
        }

        /// <summary>-executeMethod 入口（批处理）。</summary>
        public static void RebuildBatch()
        {
            int exitCode = 0;
            try
            {
                Rebuild();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[FocusLab] RESULT=FAIL");
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>-executeMethod 入口，只跑自检。</summary>
        public static void ValidateBatch()
        {
            int exitCode = 0;
            try
            {
                Validate();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[FocusLab] RESULT=FAIL");
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Rebuild()
        {
            Debug.Log("[FocusLab] rebuild: art import");
            FocusLabAssetBuilder.ConfigureImports();

            Debug.Log("[FocusLab] rebuild: card data");
            List<FocusCardData> cards = FocusLabAssetBuilder.CreateCards();

            Debug.Log("[FocusLab] rebuild: notebook data");
            FocusLabAssetBuilder.NotebookDefinitions notebook = FocusLabAssetBuilder.CreateNotebook(cards);
            Debug.Log("[FocusLab] rebuild: clues=" + notebook.Clues.Count + " questions=" + notebook.Questions.Count);

            Debug.Log("[FocusLab] rebuild: art index");
            int sprites = FocusArtIndexer.Refresh();
            Debug.Log("[FocusLab] rebuild: art index sprites=" + sprites);

            Debug.Log("[FocusLab] rebuild: scene");
            FocusLabSceneBuilder.Build(cards, notebook);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("[FocusLab] rebuild: validate");
            Validate();
        }

        /// <summary>检查磁盘上的场景是否满足测试场的最低要求，不满足就抛异常。</summary>
        public static void Validate()
        {
            var problems = new List<string>();

            Scene scene = EditorSceneManager.OpenScene(FocusLabSceneBuilder.ScenePath, OpenSceneMode.Single);

            Camera camera = Camera.main;
            Check(problems, camera != null, "场景里没有 tag 为 MainCamera 的相机");
            if (camera != null)
            {
                Check(problems, camera.orthographic, "主相机不是正交相机");
                Check(problems, Mathf.Abs(camera.orthographicSize - ExpectedCameraSize) < 0.001f,
                    "正交尺寸不是 " + ExpectedCameraSize + "，实际 " + camera.orthographicSize);
            }

            EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            Check(problems, eventSystem != null, "场景里没有 EventSystem");
            if (eventSystem != null)
            {
                Check(problems, eventSystem.GetComponent<InputSystemUIInputModule>() != null,
                    "EventSystem 上没有 InputSystemUIInputModule（工程只开了新输入系统）");
            }

            // 立绘层各自带 Canvas，所以这里要挑主画布（ScreenSpaceCamera + CanvasScaler），不能随便取一个。
            Canvas canvas = null;
            foreach (Canvas candidate in UnityEngine.Object.FindObjectsByType<Canvas>(UnityEngine.FindObjectsInactive.Exclude))
            {
                if (candidate.renderMode == RenderMode.ScreenSpaceCamera && candidate.GetComponent<UnityEngine.UI.CanvasScaler>() != null)
                {
                    canvas = candidate;
                    break;
                }
            }
            Check(problems, canvas != null, "场景里没有 Canvas");
            if (canvas != null)
            {
                Check(problems, canvas.renderMode == RenderMode.ScreenSpaceCamera, "Canvas 不是 ScreenSpaceCamera 模式");
                Check(problems, canvas.worldCamera != null, "Canvas 没有指定 worldCamera");
                Check(problems, canvas.GetComponent<GraphicRaycaster>() != null, "Canvas 上没有 GraphicRaycaster");
            }

            Image background = FindImage("Background");
            Check(problems, background != null && background.sprite != null, "背景图缺失或没挂 sprite");

            FocusCardView[] cards = UnityEngine.Object.FindObjectsByType<FocusCardView>(UnityEngine.FindObjectsInactive.Exclude);
            Check(problems, cards.Length == ExpectedCardCount, "卡牌数量应为 " + ExpectedCardCount + "，实际 " + cards.Length);

            foreach (FocusCardView card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                string label = "卡牌 " + card.name;

                Check(problems, card.Data != null, label + " 没有绑定 FocusCardData");
                if (card.Data != null)
                {
                    Check(problems, card.Data.FrontSprite != null, label + " 的正面图缺失");
                    Check(problems, card.Data.BackSprite != null, label + " 的背面图缺失");
                    Check(problems, !string.IsNullOrEmpty(card.Data.DisplayName), label + " 的显示名为空");
                }

                var rect = (RectTransform)card.transform;
                Check(problems, Mathf.Abs(rect.rect.width - ExpectedCardSize.x) < 0.5f
                                 && Mathf.Abs(rect.rect.height - ExpectedCardSize.y) < 0.5f,
                    label + " 尺寸应为 " + ExpectedCardSize.x + "x" + ExpectedCardSize.y + "，实际 " + rect.rect.size);

                Check(problems, rect.anchoredPosition.y > -1000f, label + " 的位置像没摆好");

                var hitArea = card.GetComponent<Image>();
                Check(problems, hitArea != null && hitArea.raycastTarget, label + " 缺少接收鼠标事件的 Image");

                Image face = card.transform.Find("Flip") != null ? card.transform.Find("Flip").GetComponent<Image>() : null;
                Check(problems, face != null && face.sprite != null, label + " 的 Flip 子节点没有卡面图");

                Text nameLabel = card.GetComponentInChildren<Text>(true);
                Check(problems, nameLabel != null, label + " 没有名字文字");

                // 开局必须全是背面：否则「只有背面能点开」这条规则测不出来。
                Check(problems, !card.IsFaceUp, label + " 开局不是背面朝上");

                // 选中描边要预置好、并且默认不显示。
                Transform selectionTransform = card.transform.Find("Selection");
                Image selectionRing = selectionTransform != null ? selectionTransform.GetComponent<Image>() : null;
                Check(problems, selectionRing != null && selectionRing.sprite != null, label + " 没有选中描边图");
                Check(problems, selectionRing == null || !selectionRing.enabled, label + " 的选中描边开局就亮着");
                Check(problems, !card.IsSelected, label + " 开局就是选中状态");
            }

            var categoryCounts = new Dictionary<FocusCardCategory, int>();
            foreach (FocusCardView card in cards)
            {
                if (card == null || card.Data == null)
                {
                    continue;
                }

                categoryCounts.TryGetValue(card.Category, out int count);
                categoryCounts[card.Category] = count + 1;
            }

            foreach ((FocusCardCategory category, int count) in ExpectedCategories)
            {
                categoryCounts.TryGetValue(category, out int actual);
                Check(problems, actual == count,
                    "「" + FocusCardCategories.ToChinese(category) + "」类卡牌应为 " + count + " 张，实际 " + actual + " 张");
            }

            foreach (FocusCardView card in cards)
            {
                if (card == null || card.Data == null)
                {
                    continue;
                }

                // 人物卡要有立绘，非人物卡不该有。
                Check(problems, card.IsCharacter == card.Data.HasPortrait,
                    "卡牌 " + card.name + " 的立绘配置与分类不匹配（人物卡必须有立绘，非人物卡不该有）");
            }

            FocusPortraitPanel portrait = UnityEngine.Object.FindAnyObjectByType<FocusPortraitPanel>();
            Check(problems, portrait != null, "场景里没有人物立绘面板 FocusPortraitPanel");
            if (portrait != null)
            {
                Check(problems, !portrait.IsVisible, "立绘面板进场时应该是隐藏的");
            }

            Button confirmButton = FindButton("Btn_Confirm");
            Check(problems, confirmButton != null, "场景里没有确认键 Btn_Confirm");
            if (confirmButton != null)
            {
                Check(problems, confirmButton.onClick.GetPersistentEventCount() > 0, "确认键没有绑定点击事件");
                var confirmGroup = confirmButton.GetComponent<CanvasGroup>();
                Check(problems, confirmGroup != null && confirmGroup.alpha < 0.01f, "确认键进场时应该是隐藏的");
            }

            FocusSubtitlePanel subtitle = UnityEngine.Object.FindAnyObjectByType<FocusSubtitlePanel>();
            Check(problems, subtitle != null, "场景里没有字幕面板 FocusSubtitlePanel");
            if (subtitle != null)
            {
                Check(problems, !subtitle.IsVisible, "字幕面板进场时应该是隐藏的");
                Check(problems, Mathf.Abs(subtitle.HintDuration - FocusSubtitlePanel.DefaultHintDuration) < 0.01f,
                    "提示字幕时长应为 " + FocusSubtitlePanel.DefaultHintDuration + " 秒，实际 " + subtitle.HintDuration);
            }

            Check(problems, CountPersistentListeners("Btn_VisualNovel") > 0, "「视觉小说」调试按钮没有绑定点击事件");
            Check(problems, CountPersistentListeners("Btn_CloseNovel") > 0, "「收起」调试按钮没有绑定点击事件");

            FocusVisualNovelPlayer vnPlayer = UnityEngine.Object.FindAnyObjectByType<FocusVisualNovelPlayer>();
            Check(problems, vnPlayer != null, "场景里没有视觉小说播放器 FocusVisualNovelPlayer");
            if (vnPlayer != null)
            {
                Check(problems, !vnPlayer.IsPlaying, "进场时不该在播视觉小说");
                Check(problems, vnPlayer.Dialogue != null, "视觉小说播放器没有绑定字幕面板");
                Check(problems, vnPlayer.Library != null, "视觉小说播放器没有绑定目录资产 VisualNovelLibrary");
            }

            foreach (FocusVnPortrait portraitSide in UnityEngine.Object.FindObjectsByType<FocusVnPortrait>(UnityEngine.FindObjectsInactive.Exclude))
            {
                Check(problems, !portraitSide.IsVisible, portraitSide.name + " 进场时应该是空着的");
                Check(problems, portraitSide.GetComponent<Canvas>() != null, portraitSide.name + " 缺少自己的 Canvas（独立图层）");
            }

            FocusCardTooltip tooltip = UnityEngine.Object.FindAnyObjectByType<FocusCardTooltip>();
            Check(problems, tooltip != null, "场景里没有名字浮层 FocusCardTooltip");
            if (tooltip != null)
            {
                Check(problems, tooltip.HasContent, "浮层的标题/副标题文字没接上");
            }

            FocusLabController controller = UnityEngine.Object.FindAnyObjectByType<FocusLabController>();
            Check(problems, controller != null, "场景里没有 FocusLabController");
            if (controller != null)
            {
                Check(problems, controller.Cards != null && controller.Cards.Length == ExpectedCardCount,
                    "Controller 的卡牌列表数量不对");
                Check(problems, controller.Tooltip != null, "Controller 没有绑定浮层");
                Check(problems, controller.Subtitle != null, "Controller 没有绑定字幕面板");
                Check(problems, controller.Portrait != null, "Controller 没有绑定立绘面板");
                Check(problems, controller.ConfirmButton != null, "Controller 没有绑定确认键");
                Check(problems, !controller.HasPendingOperation, "Controller 进场时不该有待确认的操作");
                Check(problems, controller.SelectedCharacter == null && controller.SelectedTarget == null,
                    "Controller 进场时不该有选中的卡牌");
            }

            ValidateNotebook(problems);

            if (problems.Count > 0)
            {
                var builder = new StringBuilder();
                builder.AppendLine("[FocusLab] 自检失败 " + problems.Count + " 项：");
                foreach (string problem in problems)
                {
                    builder.AppendLine("  - " + problem);
                }

                throw new InvalidOperationException(builder.ToString());
            }

            Debug.Log("[FocusLab] RESULT=PASS scene=" + scene.path + " cards=" + cards.Length);
        }

        /// <summary>笔记本层的最低要求：入口道具、五槽案件板、线索表与问题表都接上了。</summary>
        private static void ValidateNotebook(List<string> problems)
        {
            FocusNotebookPanel notebook = UnityEngine.Object.FindAnyObjectByType<FocusNotebookPanel>();
            Check(problems, notebook != null, "场景里没有笔记本面板 FocusNotebookPanel");
            if (notebook == null)
            {
                return;
            }

            Check(problems, !notebook.IsOpen, "笔记本进场时应该是合上的");

            FocusNotebookSource source = UnityEngine.Object.FindAnyObjectByType<FocusNotebookSource>();
            Check(problems, source != null, "场景里没有笔记本入口 FocusNotebookSource");
            if (source != null)
            {
                Check(problems, source.Notebook == notebook, "笔记本入口没有绑定面板");
            }

            Check(problems, CountPersistentListeners(FocusLabSceneBuilder.NotebookButtonName) > 0,
                "右上角的笔记本道具没有绑定点击事件");

            FocusNotebookSlot[] slots = UnityEngine.Object.FindObjectsByType<FocusNotebookSlot>(UnityEngine.FindObjectsInactive.Include);
            Check(problems, slots.Length == ExpectedQuestionCount,
                "案件板槽位数应为 " + ExpectedQuestionCount + "，实际 " + slots.Length);
            foreach (FocusNotebookSlot slot in slots)
            {
                Check(problems, slot.Question != null, slot.name + " 没有绑定 FocusQuestionData");
                Check(problems, slot.Clue == null, slot.name + " 进场时就该是空的");
            }

            FocusNotebookCaseBoard board = UnityEngine.Object.FindAnyObjectByType<FocusNotebookCaseBoard>(UnityEngine.FindObjectsInactive.Include);
            Check(problems, board != null, "场景里没有案件板 FocusNotebookCaseBoard");
            if (board != null)
            {
                Check(problems, board.Slots.Length == ExpectedQuestionCount, "案件板上的槽位数不对");
                Check(problems, board.FilledCount == 0, "案件板进场时应该一个都没填");
            }

            // 线索表与问题表的实例要落在 Assets/Data 下。新建资产后 FindAssets 有延迟，
            // 所以以目录里真实存在的 .asset 文件为准，再用加载结果核对数量。
            int clueAssets = CountAssetsOnDisk(FocusLabAssetBuilder.ClueRoot);
            int questionAssets = CountAssetsOnDisk(FocusLabAssetBuilder.QuestionRoot);
            Check(problems, clueAssets == ExpectedClueCount,
                FocusLabAssetBuilder.ClueRoot + " 下的线索数应为 " + ExpectedClueCount + "，实际 " + clueAssets);
            Check(problems, questionAssets == ExpectedQuestionCount,
                FocusLabAssetBuilder.QuestionRoot + " 下的问题数应为 " + ExpectedQuestionCount + "，实际 " + questionAssets);

            int loadedQuestions = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:FocusQuestionData", new[] { FocusLabAssetBuilder.QuestionRoot }))
            {
                if (AssetDatabase.LoadAssetAtPath<FocusQuestionData>(AssetDatabase.GUIDToAssetPath(guid)) != null)
                {
                    loadedQuestions++;
                }
            }

            Check(problems, loadedQuestions == ExpectedQuestionCount,
                "问题实例读取失败，应为 " + ExpectedQuestionCount + "，实际 " + loadedQuestions);

            Check(problems, notebook.ClueCount == 0, "笔记本开局应该是空的，实际 " + notebook.ClueCount + " 条");
        }

        private static int CountAssetsOnDisk(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return 0;
            }

            string absolute = Path.Combine(Directory.GetCurrentDirectory(), folder);
            return Directory.Exists(absolute) ? Directory.GetFiles(absolute, "*.asset").Length : 0;
        }

        private static int CountPersistentListeners(string buttonName)
        {
            foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(UnityEngine.FindObjectsInactive.Exclude))
            {
                if (button != null && button.name == buttonName)
                {
                    return button.onClick.GetPersistentEventCount();
                }
            }

            return 0;
        }

        private static Button FindButton(string buttonName)
        {
            foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(UnityEngine.FindObjectsInactive.Exclude))
            {
                if (button != null && button.name == buttonName)
                {
                    return button;
                }
            }

            return null;
        }

        private static Image FindImage(string objectName)

        {
            foreach (Image image in UnityEngine.Object.FindObjectsByType<Image>(UnityEngine.FindObjectsInactive.Exclude))
            {
                if (image != null && image.name == objectName)
                {
                    return image;
                }
            }

            return null;
        }

        private static void Check(List<string> problems, bool condition, string message)
        {
            if (!condition)
            {
                problems.Add(message);
            }
        }
    }
}
