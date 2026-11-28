using CoolingGame.Presentation;
using CoolingGame.Presentation.ArtTuning;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CoolingGame.EditorTools.ArtTuning
{
    /// <summary>
    /// 视觉小说 · 立绘页。
    ///
    /// 只给美术看这几项（全部是画面语言，不出现 Anchor / Pivot / SizeDelta 这类程序词）：
    /// - 贴在屏幕哪个位置（六选一）；
    /// - 等比例缩放（就是 Transform 的 Local Scale）；
    /// - 离屏幕左边 / 右边 / 底边多远；
    /// - 变暗强度（两侧共用一条）。
    ///
    /// 另外每侧一个「在场景里定位」按钮，点一下直接在 Inspector 里选中那个立绘物体。
    /// </summary>
    internal sealed class VnStandingPage : IArtTuningPage
    {
        private static readonly PortraitAnchor[] AnchorOrder = PortraitAnchorMath.All();
        private static readonly string[] AnchorNames = BuildAnchorNames();

        private bool _advanced;

        public string Title
        {
            get { return "视觉小说 · 立绘"; }
        }

        public void OnOpened(ArtTuningSettings settings)
        {
            _advanced = false;
        }

        public void OnDraw(ArtTuningSettings working, ArtTuningPageContext context)
        {
            if (working == null || working.VnStanding == null)
            {
                ArtTuningGui.HelpBox("没有读到参数。点工具栏的「读取」重来一次。", MessageType.Warning);
                return;
            }

            VnStandingSettings tuning = working.VnStanding;
            tuning.Sanitize();

            DrawHeader(tuning, context);
            DrawSide(tuning, tuning.left, true, context);
            DrawSide(tuning, tuning.right, false, context);
            DrawDim(tuning, context);
            DrawLocate(context);
            DrawAdvanced(tuning, context);
        }

        // ------------------------------------------------------------------ 开关

        private void DrawHeader(VnStandingSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("立绘",
                "改这里的数，进 Play 后画面当场跟着变。调好点顶上的「保存」。");

            EditorGUI.BeginChangeCheck();
            bool apply = EditorGUILayout.ToggleLeft(
                new GUIContent("接管立绘", "关着 = 完全按代码里原来的样子（左侧贴左下角、右侧贴右下角、离边 70 / 40、框 700×900）"),
                tuning.apply);
            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：接管立绘");
                tuning.apply = apply;
                context.Commit();
            }

            if (!tuning.apply)
            {
                ArtTuningGui.HelpBox(
                    "现在没有接管，游戏按代码原值显示：左右两侧都贴底、离屏幕边 70（左/右）和 40（下），框 700×900。\n" +
                    "下面的参数照常能存，勾上「接管立绘」就会生效。",
                    MessageType.Info);
            }
        }

        // ------------------------------------------------------------------ 每一侧

        private void DrawSide(VnStandingSettings tuning, VnStandingSide side, bool isLeft, ArtTuningPageContext context)
        {
            ArtTuningGui.Section(isLeft ? "左侧立绘" : "右侧立绘",
                "屏幕上的位置、缩放、离边距离");

            if (side == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            bool apply = EditorGUILayout.ToggleLeft(
                new GUIContent("这一侧接管", "关着 = 这一侧还是用代码原值"),
                side.apply);
            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：立绘接管");
                side.apply = apply;
                context.Commit();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("左下角", GUILayout.Width(64f)))
                {
                    ApplyPreset(tuning, side, PortraitAnchor.BottomLeft, context);
                }

                if (GUILayout.Button("右下角", GUILayout.Width(64f)))
                {
                    ApplyPreset(tuning, side, PortraitAnchor.BottomRight, context);
                }

                if (GUILayout.Button("底边中点", GUILayout.Width(72f)))
                {
                    ApplyPreset(tuning, side, PortraitAnchor.BottomCenter, context);
                }

                if (GUILayout.Button("出厂值", GUILayout.Width(64f)))
                {
                    context.Undo("美术调试：立绘出厂值");
                    side.CopyFrom(VnStandingSide.CreateDefault(!isLeft));
                    side.apply = true;
                    context.Commit();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUI.BeginChangeCheck();

            // 锚点：六选一
            int index = IndexOf(side.anchor);
            int picked = EditorGUILayout.Popup(
                new GUIContent("放在屏幕的", "立绘贴在屏幕的哪个位置"),
                index,
                AnchorNames,
                GUILayout.Width(220f));
            if (picked != index && picked >= 0 && picked < AnchorOrder.Length)
            {
                side.anchor = AnchorOrder[picked];
            }

            // 缩放：就是 localScale
            float percent = side.scale * 100f;
            float newPercent = EditorGUILayout.FloatField(
                new GUIContent("缩放（%）", "等比例缩放，就是 Transform 的 Local Scale。100% = 原始大小"),
                percent,
                GUILayout.Width(220f));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ArtTuningGui.LabelWidth);
                newPercent = GUILayout.HorizontalSlider(newPercent, 10f, 300f, GUILayout.Width(220f));
                if (GUILayout.Button("100%", GUILayout.Width(56f)))
                {
                    newPercent = 100f;
                }
            }

            side.scale = Mathf.Max(1f, newPercent) / 100f;

            // 离边距离：按锚点决定哪几个有意义
            bool usesLeft = PortraitAnchorMath.IsLeftSide(side.anchor);
            bool usesRight = PortraitAnchorMath.IsRightSide(side.anchor);
            bool usesBottom = PortraitAnchorMath.IsBottomSide(side.anchor);

            using (new EditorGUI.DisabledScope(!usesLeft))
            {
                side.gapLeft = EditorGUILayout.FloatField(
                    new GUIContent("离屏幕左边", "立绘框左边缘到屏幕左边缘的距离（像素）。只有靠左的位置用得到"),
                    side.gapLeft,
                    GUILayout.Width(220f));
            }

            using (new EditorGUI.DisabledScope(!usesRight))
            {
                side.gapRight = EditorGUILayout.FloatField(
                    new GUIContent("离屏幕右边", "立绘框右边缘到屏幕右边缘的距离（像素）。只有靠右的位置用得到"),
                    side.gapRight,
                    GUILayout.Width(220f));
            }

            using (new EditorGUI.DisabledScope(!usesBottom))
            {
                side.gapBottom = EditorGUILayout.FloatField(
                    new GUIContent("离屏幕下边", "立绘框下边缘到屏幕下边缘的距离（像素）。只有贴底的位置用得到"),
                    side.gapBottom,
                    GUILayout.Width(220f));
            }

            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：立绘位置");
                side.Sanitize();
                context.Commit();
            }

            ArtTuningGui.SmallLabel(Describe(tuning, side, isLeft));
            ArtTuningGui.SmallLabel(side.IsFlipped ? "镜像：开" : "镜像：关（「高级」里可以打开）");
            GUILayout.Space(4f);
        }

        // ------------------------------------------------------------------ 变暗

        private void DrawDim(VnStandingSettings tuning, ArtTuningPageContext context)
        {
            ArtTuningGui.Section("变暗", "一个人说话时，另一侧的立绘会变暗");

            EditorGUI.BeginChangeCheck();
            bool apply = EditorGUILayout.ToggleLeft(
                new GUIContent("接管变暗", "关着 = 用代码原值 0.42"),
                tuning.applyDim);
            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：接管变暗");
                tuning.applyDim = apply;
                context.Commit();
            }

            EditorGUI.BeginChangeCheck();
            float strength = EditorGUILayout.FloatField(
                new GUIContent("变暗强度", "0 = 不变暗，1 = 全黑。原来的值是 0.42"),
                tuning.dimStrength,
                GUILayout.Width(220f));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ArtTuningGui.LabelWidth);
                strength = GUILayout.HorizontalSlider(strength, 0f, 1f, GUILayout.Width(220f));
                if (GUILayout.Button("0.42", GUILayout.Width(56f)))
                {
                    strength = VnStandingSettings.DefaultDim;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                context.Undo("美术调试：变暗强度");
                tuning.dimStrength = Mathf.Clamp01(strength);
                context.Commit();
            }

            GUILayout.Space(4f);
        }

        // ------------------------------------------------------------------ 定位

        private void DrawLocate(ArtTuningPageContext context)
        {
            ArtTuningGui.Section("在场景里找到立绘", "点一下会在 Hierarchy 里选中那个物体，Inspector 直接显示它");

            DialogueOverlay overlay = FindOverlay();
            if (overlay == null)
            {
                ArtTuningGui.HelpBox(
                    Application.isPlaying
                        ? "还没找到视觉小说界面：等剧情里出现对话就找到了。"
                        : "进 Play 之后才有这个物体。先在编辑器里把数调好，再进 Play 看效果。",
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("定位左侧立绘", "在 Hierarchy 里选中左侧立绘物体"),
                        GUILayout.Width(120f)))
                {
                    Select(overlay.LeftPortrait);
                }

                using (new EditorGUI.DisabledScope(overlay.LeftPortrait == null || !overlay.LeftPortrait.enabled))
                {
                    GUILayout.Label(overlay.LeftPortrait == null ? "（没有）" : "已找到", EditorStyles.miniLabel,
                        GUILayout.Width(48f));
                }

                if (GUILayout.Button(new GUIContent("定位右侧立绘", "在 Hierarchy 里选中右侧立绘物体"),
                        GUILayout.Width(120f)))
                {
                    Select(overlay.RightPortrait);
                }

                using (new EditorGUI.DisabledScope(overlay.RightPortrait == null || !overlay.RightPortrait.enabled))
                {
                    GUILayout.Label(overlay.RightPortrait == null ? "（没有）" : "已找到", EditorStyles.miniLabel,
                        GUILayout.Width(48f));
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4f);
        }

        // ------------------------------------------------------------------ 高级（默认收起）

        private void DrawAdvanced(VnStandingSettings tuning, ArtTuningPageContext context)
        {
            _advanced = EditorGUILayout.Foldout(_advanced, "高级（一般不碰）", true);
            if (!_advanced)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "下面这些是「图怎么塞进框」，和位置无关。框比例和图比例不一致时才有影响。",
                    EditorStyles.wordWrappedLabel);

                EditorGUI.BeginChangeCheck();
                bool applyArt = EditorGUILayout.ToggleLeft(
                    new GUIContent("管图的摆放", "关着 = 原行为（图等比缩到框内）"),
                    tuning.applyArt);
                if (EditorGUI.EndChangeCheck())
                {
                    context.Undo("美术调试：图的摆放");
                    tuning.applyArt = applyArt;
                    context.Commit();
                }

                DrawArt(tuning, tuning.left, true, context);
                DrawArt(tuning, tuning.right, false, context);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("镜像左侧", "左右翻转"), GUILayout.Width(100f)))
                    {
                        context.Undo("美术调试：镜像左侧");
                        tuning.left.flipHorizontal = !tuning.left.flipHorizontal;
                        context.Commit();
                    }

                    if (GUILayout.Button(new GUIContent("镜像右侧", "左右翻转"), GUILayout.Width(100f)))
                    {
                        context.Undo("美术调试：镜像右侧");
                        tuning.right.flipHorizontal = !tuning.right.flipHorizontal;
                        context.Commit();
                    }

                    GUILayout.FlexibleSpace();
                }

                GUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("全部接管"), GUILayout.Width(90f)))
                    {
                        context.Undo("美术调试：全部接管");
                        tuning.apply = true;
                        tuning.left.apply = true;
                        tuning.right.apply = true;
                        context.Commit();
                    }

                    if (GUILayout.Button(new GUIContent("全部放开"), GUILayout.Width(90f)))
                    {
                        context.Undo("美术调试：全部放开");
                        tuning.apply = false;
                        tuning.left.apply = false;
                        tuning.right.apply = false;
                        context.Commit();
                    }

                    if (GUILayout.Button(new GUIContent("从游戏抄回", "把 Play 中两侧的实际位置抄成现在的参数"),
                            GUILayout.Width(90f)))
                    {
                        CopyFromGame(tuning, context);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawArt(VnStandingSettings tuning, VnStandingSide side, bool isLeft, ArtTuningPageContext context)
        {
            if (side == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(isLeft ? "左侧" : "右侧", EditorStyles.boldLabel, GUILayout.Width(40f));

                using (new EditorGUI.DisabledScope(!tuning.applyArt))
                {
                    int mode = side.rawMode == ArtRawMode.FitInside ? 0 : 1;
                    mode = EditorGUILayout.Popup(new GUIContent("图的摆放"), mode,
                        new[] { "塞进框（老行为）", "原始尺寸（按框裁）" }, GUILayout.Width(230f));
                    side.rawMode = mode == 0 ? ArtRawMode.FitInside : ArtRawMode.RawSize;

                    if (side.rawMode == ArtRawMode.RawSize)
                    {
                        side.artScale = EditorGUILayout.FloatField(new GUIContent("图缩放"), side.artScale,
                            GUILayout.Width(200f));
                        side.artOffset = EditorGUILayout.Vector2Field(new GUIContent("图偏移"), side.artOffset,
                            GUILayout.Width(240f));
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        // ------------------------------------------------------------------ 工具

        /// <summary>「实际框 x..y / 离边多少」这类说明，让美术能核对。</summary>
        private static string Describe(VnStandingSettings tuning, VnStandingSide side, bool isLeft)
        {
            Vector2 canvas = new Vector2(1920f, 1080f);
            Vector2 size = side.ScaledSize();
            Vector2 anchorPos = PortraitAnchorMath.ToAnchorPosition(side.anchor);
            Vector2 pos = PortraitAnchorMath.ToAnchoredPosition(side.anchor, size, side.gapLeft, side.gapRight,
                side.gapBottom);

            // 与 RectTransform 的算法保持一致：锚点处 + 偏移 - 枢轴 × 尺寸
            Vector2 pivot = PortraitAnchorMath.ToPivot(side.anchor);
            float left = anchorPos.x * canvas.x + pos.x - pivot.x * size.x;
            float bottom = anchorPos.y * canvas.y + pos.y - pivot.y * size.y;

            return string.Format("{0}：框 {1:0}×{2:0}，屏幕 x {3:0}~{4:0}，y {5:0}~{6:0}",
                PortraitAnchorMath.DisplayName(side.anchor), size.x, size.y,
                left, left + size.x, bottom, bottom + size.y);
        }

        private static void ApplyPreset(VnStandingSettings tuning, VnStandingSide side, PortraitAnchor anchor,
                                        ArtTuningPageContext context)
        {
            context.Undo("美术调试：立绘预置位置");
            side.anchor = anchor;
            side.Sanitize();
            context.Commit();
        }

        private static void Select(Image image)
        {
            if (image == null)
            {
                return;
            }

            Selection.activeObject = image.gameObject;
            EditorGUIUtility.PingObject(image.gameObject);
        }

        private void CopyFromGame(VnStandingSettings tuning, ArtTuningPageContext context)
        {
            DialogueOverlay overlay = FindOverlay();
            if (overlay == null)
            {
                Debug.LogWarning("[美术调试] 进 Play 并且剧情里出现对话之后，才能从游戏里抄回。");
                return;
            }

            bool any = false;

            if (overlay.LeftPortrait != null && tuning.left != null)
            {
                context.Undo("美术调试：抄回左侧");
                tuning.left.ReadFrom(overlay.LeftPortrait.rectTransform);
                any = true;
            }

            if (overlay.RightPortrait != null && tuning.right != null)
            {
                context.Undo("美术调试：抄回右侧");
                tuning.right.ReadFrom(overlay.RightPortrait.rectTransform);
                any = true;
            }

            if (!any)
            {
                Debug.LogWarning("[美术调试] 没有找到立绘，没有可抄的值。");
                return;
            }

            context.Commit();
            Debug.Log("[美术调试] 已从游戏里抄回左右两侧的实际值。");
        }

        private static DialogueOverlay FindOverlay()
        {
            return Application.isPlaying ? Object.FindAnyObjectByType<DialogueOverlay>() : null;
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
