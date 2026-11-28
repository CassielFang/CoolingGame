using CoolingGame.Presentation;
using CoolingGame.Presentation.ArtTuning;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoolingGame.EditorTools.ArtTuning
{
    /// <summary>
    /// 视觉小说 · 对话框页。
    ///
    /// 只给美术看这几项：
    /// - 贴在屏幕哪个位置（六选一）+ 离屏幕左 / 右 / 下多远；
    /// - 宽度、高度（或者宽度按屏幕百分比算）；
    /// - 整体比例（对话框和里面的字一起缩放）。
    ///
    /// 另外给一个「在场景里定位对话框」和「把游戏里的实测值抄回」。
    /// </summary>
    internal sealed class VnDialogPage : IArtTuningPage
    {
        private static readonly PortraitAnchor[] AnchorOrder = PortraitAnchorMath.All();
        private static readonly string[] AnchorNames = BuildAnchorNames();

        public string Title
        {
            get { return "视觉小说 · 对话框"; }
        }

        public void OnOpened(ArtTuningSettings settings)
        {
        }

        public void OnDraw(ArtTuningSettings working, ArtTuningPageContext context)
        {
            if (working == null || working.Dialog == null)
            {
                ArtTuningGui.HelpBox("没有读到参数。点工具栏的「读取」重来一次。", MessageType.Warning);
                return;
            }

            VnDialogSettings tuning = working.Dialog;
            tuning.Sanitize();

            DrawHeader(tuning, context);
            DrawPosition(tuning, context);
            DrawSize(tuning, context);
            DrawScale(tuning, context);
            DrawTools(tuning, context);
        }

        // ------------------------------------------------------------------ 开关

        private void DrawHeader(VnDialogSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("对话框",
                "改这里的数，进 Play 后对话框当场跟着变。调好点顶上的「保存」。");

            EditorGUI.BeginChangeCheck();
            bool apply = EditorGUILayout.ToggleLeft(
                new GUIContent("接管对话框",
                    "关着 = 完全按代码里原来的样子（1560×148，底部居中，离屏幕底边 36）"),
                tuning.apply);
            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：接管对话框");
                tuning.apply = apply;
                context.Commit();
            }

            if (!tuning.apply)
            {
                ArtTuningGui.HelpBox(
                    "现在没有接管，游戏按代码原值显示：宽 1560、高 148，底部居中，离屏幕底边 36。\n" +
                    "下面的参数照常能存，勾上「接管对话框」就会生效。",
                    MessageType.Info);
            }
        }

        // ------------------------------------------------------------------ 位置

        private void DrawPosition(VnDialogSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("位置", "对话框贴在屏幕的哪一块、离边多远");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("底部居中", GUILayout.Width(72f)))
                {
                    context.Undo("美术调试：对话框预设");
                    tuning.anchor = PortraitAnchor.BottomCenter;
                    tuning.offsetX = 0f;
                    tuning.offsetY = 0f;
                    context.Commit();
                }

                if (GUILayout.Button("左下角", GUILayout.Width(64f)))
                {
                    context.Undo("美术调试：对话框预设");
                    tuning.anchor = PortraitAnchor.BottomLeft;
                    tuning.offsetX = 0f;
                    tuning.offsetY = 0f;
                    context.Commit();
                }

                if (GUILayout.Button("右下角", GUILayout.Width(64f)))
                {
                    context.Undo("美术调试：对话框预设");
                    tuning.anchor = PortraitAnchor.BottomRight;
                    tuning.offsetX = 0f;
                    tuning.offsetY = 0f;
                    context.Commit();
                }

                if (GUILayout.Button("屏幕中央", GUILayout.Width(72f)))
                {
                    context.Undo("美术调试：对话框预设");
                    tuning.anchor = PortraitAnchor.Center;
                    tuning.offsetX = 0f;
                    tuning.offsetY = 0f;
                    context.Commit();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUI.BeginChangeCheck();

            int index = IndexOf(tuning.anchor);
            int picked = EditorGUILayout.Popup(
                new GUIContent("放在屏幕的", "对话框贴在屏幕的哪个位置"),
                index,
                AnchorNames,
                GUILayout.Width(220f));
            if (picked != index && picked >= 0 && picked < AnchorOrder.Length)
            {
                tuning.anchor = AnchorOrder[picked];
            }

            bool usesLeft = PortraitAnchorMath.IsLeftSide(tuning.anchor);
            bool usesRight = PortraitAnchorMath.IsRightSide(tuning.anchor);
            bool usesBottom = PortraitAnchorMath.IsBottomSide(tuning.anchor);

            using (new EditorGUI.DisabledScope(!usesLeft))
            {
                tuning.gapLeft = EditorGUILayout.FloatField(
                    new GUIContent("离屏幕左边", "对话框左边缘到屏幕左边缘的距离（像素）。只有靠左的位置用得到"),
                    tuning.gapLeft,
                    GUILayout.Width(220f));
            }

            using (new EditorGUI.DisabledScope(!usesRight))
            {
                tuning.gapRight = EditorGUILayout.FloatField(
                    new GUIContent("离屏幕右边", "对话框右边缘到屏幕右边缘的距离（像素）。只有靠右的位置用得到"),
                    tuning.gapRight,
                    GUILayout.Width(220f));
            }

            using (new EditorGUI.DisabledScope(!usesBottom))
            {
                tuning.gapBottom = EditorGUILayout.FloatField(
                    new GUIContent("离屏幕下边", "对话框下边缘到屏幕下边缘的距离（像素）。只有贴底的位置用得到"),
                    tuning.gapBottom,
                    GUILayout.Width(220f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                tuning.offsetX = EditorGUILayout.FloatField(
                    new GUIContent("位置微调 X", "在自动对齐的基础上再往右挪多少像素（负数往左）"),
                    tuning.offsetX,
                    GUILayout.Width(150f));
                tuning.offsetY = EditorGUILayout.FloatField(
                    new GUIContent("Y", "在自动对齐的基础上再往上挪多少像素（负数往下）"),
                    tuning.offsetY,
                    GUILayout.Width(96f));

                if (GUILayout.Button("归零", GUILayout.Width(56f)))
                {
                    tuning.offsetX = 0f;
                    tuning.offsetY = 0f;
                }

                GUILayout.FlexibleSpace();
            }

            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：对话框位置");
                tuning.Sanitize();
                context.Commit();
            }

            GUILayout.Space(2f);
        }

        // ------------------------------------------------------------------ 尺寸

        private void DrawSize(VnDialogSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("尺寸", "对话框有多大。宽度可以按屏幕宽度的百分比算，换分辨率也不会跑偏");

            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(tuning.widthByScreen))
            {
                tuning.width = EditorGUILayout.FloatField(
                    new GUIContent("宽度", "对话框宽度（像素）。勾了下面那条就改用百分比"),
                    tuning.width,
                    GUILayout.Width(220f));
            }

            tuning.height = EditorGUILayout.FloatField(
                new GUIContent("高度", "对话框高度（像素）。字多放不下就调高"),
                tuning.height,
                GUILayout.Width(220f));

            tuning.widthByScreen = EditorGUILayout.ToggleLeft(
                new GUIContent("宽度按屏幕百分比", "开：宽度 = 屏幕宽度 × 百分比；关：用上面那个像素值"),
                tuning.widthByScreen);

            using (new EditorGUI.DisabledScope(!tuning.widthByScreen))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    tuning.widthPercent = EditorGUILayout.FloatField(
                        new GUIContent("占屏宽（%）", "对话框宽度占屏幕宽度的百分比"),
                        tuning.widthPercent,
                        GUILayout.Width(180f));
                    tuning.widthPercent = GUILayout.HorizontalSlider(tuning.widthPercent, 20f, 100f,
                        GUILayout.Width(200f));
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：对话框尺寸");
                tuning.Sanitize();
                context.Commit();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ArtTuningGui.LabelWidth);

                if (GUILayout.Button("出厂 1560×148", GUILayout.Width(120f)))
                {
                    context.Undo("美术调试：对话框尺寸出厂值");
                    tuning.width = VnDialogSettings.DefaultWidth;
                    tuning.height = VnDialogSettings.DefaultHeight;
                    tuning.widthByScreen = false;
                    context.Commit();
                }

                if (GUILayout.Button("占屏宽 90%", GUILayout.Width(96f)))
                {
                    context.Undo("美术调试：对话框尺寸百分比");
                    tuning.widthByScreen = true;
                    tuning.widthPercent = 90f;
                    context.Commit();
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(2f);
        }

        // ------------------------------------------------------------------ 比例

        private void DrawScale(VnDialogSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("比例", "对话框和里面的字一起等比缩放（Transform 的 Local Scale）");

            EditorGUI.BeginChangeCheck();

            float percent = tuning.scale * 100f;
            float newPercent = EditorGUILayout.FloatField(
                new GUIContent("整体比例（%）", "100% = 原始大小。对话框边框和字一起缩放"),
                percent,
                GUILayout.Width(220f));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ArtTuningGui.LabelWidth);
                newPercent = GUILayout.HorizontalSlider(newPercent, 50f, 150f, GUILayout.Width(220f));
                if (GUILayout.Button("100%", GUILayout.Width(56f)))
                {
                    newPercent = 100f;
                }

                GUILayout.FlexibleSpace();
            }

            tuning.scale = Mathf.Max(5f, newPercent) / 100f;

            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：对话框比例");
                tuning.Sanitize();
                context.Commit();
            }

            GUILayout.Space(2f);
        }

        // ------------------------------------------------------------------ 工具

        private void DrawTools(VnDialogSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("核对 / 定位", "对着游戏里的实际数值确认一遍");

            ArtTuningGui.SmallLabel(Describe(tuning));

            using (new EditorGUILayout.HorizontalScope())
            {
                DialogueOverlay overlay = FindOverlay();
                using (new EditorGUI.DisabledScope(overlay == null || overlay.DialogueBox == null))
                {
                    if (GUILayout.Button(
                            new GUIContent("在场景里找到对话框", "点一下在 Inspector 里选中那个物体"),
                            GUILayout.Width(140f)))
                    {
                        RectTransform box = overlay != null ? overlay.DialogueBox : null;
                        if (box != null)
                        {
                            Selection.activeObject = box.gameObject;
                            EditorGUIUtility.PingObject(box.gameObject);
                        }
                    }

                    if (GUILayout.Button(
                            new GUIContent("把游戏里的实测值抄回", "进 Play 后点：把画面上对话框现在的位置尺寸读回这里"),
                            GUILayout.Width(170f)))
                    {
                        CopyFromGame(tuning, context);
                    }
                }

                GUILayout.FlexibleSpace();
            }

            if (FindOverlay() == null)
            {
                ArtTuningGui.HelpBox(
                    Application.isPlaying
                        ? "Scene 里没找到视觉小说界面，先进游戏再说。"
                        : "「定位 / 抄回」要在 Play 里用（编辑器没运行时画面上还没有这个界面）。",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        new GUIContent("恢复出厂值（并关闭接管）", "回到代码里原来的对话框"),
                        GUILayout.Width(200f)))
                {
                    context.Undo("美术调试：对话框出厂值");
                    tuning.ResetToDefault();
                    context.Commit();
                }

                if (GUILayout.Button(
                        new GUIContent("只恢复位置和尺寸", "保留接管开关，只把位置 / 尺寸 / 比例调回出厂"),
                        GUILayout.Width(180f)))
                {
                    context.Undo("美术调试：对话框位置尺寸出厂值");
                    bool keep = tuning.apply;
                    tuning.ResetToDefault();
                    tuning.apply = keep;
                    context.Commit();
                }

                GUILayout.FlexibleSpace();
            }
        }

        // ------------------------------------------------------------------ 细节

        private void CopyFromGame(VnDialogSettings tuning, ArtTuningPageContext context)
        {
            DialogueOverlay overlay = FindOverlay();
            if (overlay == null || overlay.DialogueBox == null)
            {
                Debug.LogWarning("[美术调试] 没找到对话框，先进 Play。");
                return;
            }

            context.Undo("美术调试：抄回对话框");
            tuning.ReadFrom(overlay.DialogueBox, CanvasSize());
            context.Commit();
            Debug.Log("[美术调试] 已从游戏的 RectTransform 抄回对话框的位置和尺寸。");
        }

        /// <summary>「实际框 x..y / 尺寸」这类说明，让美术能核对。</summary>
        private static string Describe(VnDialogSettings tuning)
        {
            Vector2 canvas = CanvasSize();
            Vector2 size = tuning.ResolveSize(canvas);
            Vector2 pos = tuning.ResolvePosition(size, canvas);
            Vector2 anchorPos = PortraitAnchorMath.ToAnchorPosition(tuning.anchor);
            Vector2 pivot = PortraitAnchorMath.ToPivot(tuning.anchor);

            // Unity 的算法：锚点处 + 偏移 - 枢轴 × 尺寸 × 缩放（localScale 是绕枢轴缩的）
            float s = Mathf.Max(0.01f, tuning.scale);
            float left = anchorPos.x * canvas.x + pos.x - pivot.x * size.x * s;
            float bottom = anchorPos.y * canvas.y + pos.y - pivot.y * size.y * s;

            return string.Format("{0}：{1:0}×{2:0}（比例 {3:0}%，画面上 {4:0}×{5:0}），屏幕 x {6:0}~{7:0}，y {8:0}~{9:0}",
                PortraitAnchorMath.DisplayName(tuning.anchor),
                size.x, size.y,
                s * 100f,
                size.x * s, size.y * s,
                left, left + size.x * s,
                bottom, bottom + size.y * s);
        }

        private static DialogueOverlay FindOverlay()
        {
            return Application.isPlaying ? Object.FindAnyObjectByType<DialogueOverlay>() : null;
        }

        private static Vector2 CanvasSize()
        {
            DialogueOverlay overlay = FindOverlay();
            if (overlay != null)
            {
                var root = overlay.transform.root;
                var canvas = root != null ? root.GetComponent<Canvas>() : null;
                if (canvas == null)
                {
                    canvas = overlay.GetComponentInParent<Canvas>();
                }

                var area = canvas != null ? canvas.GetComponent<RectTransform>() : null;
                if (area != null && area.rect.size.x > 1f)
                {
                    return area.rect.size;
                }
            }

            return new Vector2(1920f, 1080f);
        }

        private static int IndexOf(PortraitAnchor anchor)
        {
            for (int i = 0; i < AnchorOrder.Length; i++)
            {
                if (AnchorOrder[i] == anchor)
                {
                    return i;
                }
            }

            return 0;
        }

        private static string[] BuildAnchorNames()
        {
            var names = new string[AnchorOrder.Length];
            for (int i = 0; i < AnchorOrder.Length; i++)
            {
                names[i] = PortraitAnchorMath.DisplayName(AnchorOrder[i]);
            }

            return names;
        }
    }
}
