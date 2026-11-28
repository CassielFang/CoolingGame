using CoolingGame.Presentation.ArtTuning;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools.ArtTuning
{
    /// <summary>
    /// Tools/美术调试：所有美术调参页面的容器。
    ///
    /// 结构：
    /// - 最上面一行是各调试对象的子选项卡（每加一个对象就在 CreatePages 里注册一行）；
    /// - 第二行是「已保存 / 正在调」状态 + 保存 / 读取 / 丢弃 —— 每个页面共用这套，
    ///   页面只管改「正在调」的那些数，什么时候落盘由你点按钮决定；
    /// - 下面整块给页面自己排版（全宽，不分左右栏）。
    ///
    /// 保存 / 读取 只在点了按钮时才动资产文件；窗口关掉、退出 Play 都不会偷偷写盘。
    /// </summary>
    public sealed class ArtTuningWindow : EditorWindow
    {
        private IArtTuningPage[] _pages;
        private int _pageIndex;
        private bool[] _opened;
        private Vector2 _scroll;
        private ArtTuningSettings _working;
        private readonly ArtTuningPageContext _context = new ArtTuningPageContext();

        /// <summary>新增调试对象：在这里加一行 <c>() =&gt; new 你的页面()</c> 即可。</summary>
        private static IArtTuningPage[] CreatePages()
        {
            return new IArtTuningPage[]
            {
                new VnStandingPage(),
            };
        }

        [MenuItem("Tools/美术调试")]
        public static void Open()
        {
            var window = GetWindow<ArtTuningWindow>("美术调试");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("美术调试");
        }

        private void Update()
        {
            ArtTuningAssets.TickSave();
        }

        private void OnGUI()
        {
            // 游戏每帧都读工作副本，窗口这边也保证每帧重画，数值一直是最新的
            Repaint();

            EnsureStore();
            EnsurePages();

            DrawToolbar();
            DrawStorageBar();

            if (_pageIndex >= _pages.Length)
            {
                DrawHelpPage();
                return;
            }

            IArtTuningPage page = _pages[_pageIndex];
            if (!_opened[_pageIndex])
            {
                _opened[_pageIndex] = true;
                page.OnOpened(_working);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUIUtility.labelWidth = ArtTuningGui.LabelWidth;
            page.OnDraw(_working, _context);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(6f);
        }

        // ------------------------------------------------------------------ 顶部

        private void EnsurePages()
        {
            if (_pages != null)
            {
                return;
            }

            _pages = CreatePages();
            _opened = new bool[_pages.Length + 1];
        }

        /// <summary>拿到「正在调」的工作副本；第一次打开窗口时用已保存的那份初始化。</summary>
        private void EnsureStore()
        {
            if (_working != null)
            {
                _context.Working = _working;
                return;
            }

            ArtTuningAssets.LoadAssetToWorking();
            _working = ArtTuningStore.Working;
            _context.Working = _working;
        }

        private void DrawToolbar()
        {
            var titles = new string[_pages.Length + 1];
            for (int i = 0; i < _pages.Length; i++)
            {
                titles[i] = _pages[i].Title;
            }

            titles[_pages.Length] = "说明";

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            int picked = GUILayout.Toolbar(_pageIndex, titles, EditorStyles.toolbarButton, GUILayout.Height(20f));
            if (picked != _pageIndex)
            {
                _pageIndex = picked;
                _scroll = Vector2.zero;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private void DrawStorageBar()
        {
            VnStandingSettings working = _working != null ? _working.VnStanding : null;
            VnStandingSettings saved = ArtTuningStore.Saved != null ? ArtTuningStore.Saved.VnStanding : null;

            bool dirty = IsDirty(working, saved);

            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(new GUIContent("保存", "把窗口里所有参数写进已保存的那份（编辑器里还会落盘）"),
                    EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                SaveWorking();
            }

            if (GUILayout.Button(new GUIContent("读取", "把已保存的那份重新读进窗口；窗口里没保存的改动会丢掉"),
                    EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                LoadSaved();
            }

            if (GUILayout.Button(new GUIContent("丢弃", "放弃窗口里没保存的改动，回到已保存的那份"),
                    EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                LoadSaved();
            }

            GUILayout.Space(10f);

            if (GUILayout.Button(new GUIContent("选中参数资产", "在 Project 里定位 ArtTuningSettings.asset"),
                    EditorStyles.toolbarButton, GUILayout.Width(96f)))
            {
                ArtTuningSettings asset = AssetDatabase.LoadAssetAtPath<ArtTuningSettings>(ArtTuningAssets.SettingsAssetPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }

            GUILayout.FlexibleSpace();

            string state = dirty ? "正在调（有改动没保存）" : "已保存";
            if (working != null && working.apply)
            {
                state += " · 接管中";
            }

            if (Application.isPlaying)
            {
                state += " · Play 中";
            }

            GUILayout.Label(state, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            if (dirty)
            {
                ArtTuningGui.HelpBox("改的是「正在调」的那份，游戏每帧读它（Play 中当场生效）。点「保存」才写进资产，关窗口 / 退出 Play 不会自动存。",
                    MessageType.Info);
            }

            GUILayout.Space(4f);
        }

        private static bool IsDirty(VnStandingSettings working, VnStandingSettings saved)
        {
            if (working == null || saved == null)
            {
                return false;
            }

            return !SameSide(working.left, saved.left)
                   || !SameSide(working.right, saved.right)
                   || working.apply != saved.apply
                   || working.applyDim != saved.applyDim
                   || !Mathf.Approximately(working.dimStrength, saved.dimStrength)
                   || working.applyArt != saved.applyArt
                   || working.applyBackground != saved.applyBackground
                   || !Mathf.Approximately(working.unifiedHeight, saved.unifiedHeight)
                   || !Mathf.Approximately(working.unifiedAlignY, saved.unifiedAlignY)
                   || working.backgroundTop != saved.backgroundTop
                   || working.backgroundBottom != saved.backgroundBottom;
        }

        private static bool SameSide(VnStandingSide a, VnStandingSide b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a.apply == b.apply
                   && a.anchor == b.anchor
                   && Mathf.Approximately(a.gapLeft, b.gapLeft)
                   && Mathf.Approximately(a.gapRight, b.gapRight)
                   && Mathf.Approximately(a.gapBottom, b.gapBottom)
                   && Mathf.Approximately(a.scale, b.scale)
                   && a.baseSize == b.baseSize
                   && Mathf.Approximately(a.dim, b.dim)
                   && a.rawMode == b.rawMode
                   && Mathf.Approximately(a.artScale, b.artScale)
                   && a.artOffset == b.artOffset
                   && a.flipHorizontal == b.flipHorizontal;
        }

        // ------------------------------------------------------------------ 三个按钮

        private void SaveWorking()
        {
            if (_working == null)
            {
                return;
            }

            _context.Undo("美术调试：保存参数");
            ArtTuningAssets.SaveWorkingToAsset();
            Debug.Log("[美术调试] 已保存到 " + ArtTuningAssets.SettingsAssetPath);
        }

        private void LoadSaved()
        {
            if (_working != null)
            {
                _working.EnsureDefaults();
                _working.VnStanding.CopyFrom(ArtTuningStore.Saved.VnStanding);
            }

            GUI.FocusControl(null);
            Repaint();
        }

        // ------------------------------------------------------------------ 说明页

        private void DrawHelpPage()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            ArtTuningGui.Section("这个窗口怎么用");
            EditorGUILayout.LabelField(
                "1. 最上面一行是各个调试对象的子选项卡。\n" +
                "2. 第二行三个按钮：「保存」= 把正在调的值写进已保存的那份；「读取」= 把已保存的那份重新读进窗口；「丢弃」= 放弃没保存的改动。\n" +
                "3. 关掉窗口、退出 Play 都不会自动保存；试坏了按「读取」就回去。\n" +
                "4. 想看效果就进 Play：Play 中改这些数，画面下一帧就跟着变；编辑器里改也能 Ctrl+Z 撤销。",
                EditorStyles.wordWrappedLabel);

            ArtTuningGui.Section("参数存在哪");
            EditorGUILayout.LabelField(ArtTuningAssets.SettingsAssetPath, EditorStyles.wordWrappedLabel);
            ArtTuningGui.SmallLabel("这是个 ScriptableObject。已保存的那份就是它；正在调的那份是运行时内存里的副本。");

            ArtTuningGui.Section("以后加新的调试对象");
            EditorGUILayout.LabelField(
                "1. 在 Assets/Scripts/Runtime/Presentation/ArtTuning/ 下加一组设置类（照抄 VnStandingSettings 的写法：有一个 apply 总开关、有 Sanitize）。\n" +
                "2. 在 ArtTuningSettings 里加一个字段。\n" +
                "3. 在 Assets/Editor/ArtTuning/ 下写一个实现 IArtTuningPage 的页面（排版随对象自己定）。\n" +
                "4. 在 ArtTuningWindow.CreatePages() 里加一行。",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(10f);
            GUILayout.EndScrollView();
        }
    }
}
