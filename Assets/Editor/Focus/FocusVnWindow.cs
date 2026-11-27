using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 视觉小说主功能窗：左边剧本目录，右边按序号排的时间轴；另一个页签维护「立绘名 → 图片」。
    /// 这里只做「看看、筛筛、删删、绑图」这类事，逐帧的断点事件（UnityEvent）在 Inspector 里编辑。
    /// </summary>
    public sealed class FocusVnWindow : EditorWindow
    {
        private const float ListWidth = 268f;
        private const float ThumbWidth = 62f;

        private int _tab;
        private string _filter = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _portraitScroll;
        private bool _showSprites;

        private FocusVisualNovelLibrary _library;
        private FocusArtLibrary _art;
        private readonly List<FocusVisualNovel> _visible = new List<FocusVisualNovel>();
        private FocusVisualNovel _selected;

        private static class Styles
        {
            public static readonly GUIStyle Badge = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 0.78f, 0.85f) }
            };

            public static readonly GUIStyle Speaker = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            public static readonly GUIStyle Line = new GUIStyle(EditorStyles.label) { wordWrap = true };
            public static readonly GUIStyle Chip = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false };
            public static readonly GUIStyle Hint = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.6f, 0.62f, 0.66f) }
            };

            public static readonly GUIStyle Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        }

        [MenuItem("Tools/视觉小说/视觉小说主功能窗", false, 22)]
        public static void Open()
        {
            var window = GetWindow<FocusVnWindow>("视觉小说主功能窗");
            window.minSize = new Vector2(760f, 420f);
            window.Reload();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnFocus()
        {
            Reload();
        }

        private void Reload()
        {
            _library = AssetDatabase.LoadAssetAtPath<FocusVisualNovelLibrary>(FocusVnImporter.LibraryPath);
            _art = AssetDatabase.LoadAssetAtPath<FocusArtLibrary>(FocusArtIndexer.LibraryPath);

            if (_selected == null && _library != null && _library.Count > 0)
            {
                _selected = _library.Novels[0];
            }

            ApplyFilter();
            Repaint();
        }

        private void ApplyFilter()
        {
            _visible.Clear();
            if (_library == null)
            {
                _selected = null;
                return;
            }

            string filter = _filter == null ? string.Empty : _filter.Trim();
            for (int i = 0; i < _library.Count; i++)
            {
                FocusVisualNovel novel = _library.Novels[i];
                if (novel == null)
                {
                    continue;
                }

                if (filter.Length == 0 || novel.NovelId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _visible.Add(novel);
                }
            }

            if (_selected != null && !_visible.Contains(_selected))
            {
                _selected = _visible.Count > 0 ? _visible[0] : null;
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_library == null)
            {
                EditorGUILayout.HelpBox(
                    "还没有视觉小说目录。先 Tools/视觉小说/导入 xlsx… 导入一份表格，或先用「生成导入模板…」。",
                    MessageType.Info);
                return;
            }

            if (_tab == 0)
            {
                DrawScriptTab();
            }
            else
            {
                DrawPortraitTab();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Toggle(_tab == 0, "剧本", EditorStyles.toolbarButton, GUILayout.Width(56)) != (_tab == 0))
                {
                    _tab = 0;
                }

                if (GUILayout.Toggle(_tab == 1, "立绘名", EditorStyles.toolbarButton, GUILayout.Width(64)) != (_tab == 1))
                {
                    _tab = 1;
                }

                GUILayout.Space(8f);

                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48)))
                {
                    Reload();
                }

                if (GUILayout.Button("导入 xlsx…", EditorStyles.toolbarButton, GUILayout.Width(88)))
                {
                    FocusVnImporter.ImportFromDialog();
                    Reload();
                }

                if (GUILayout.Button("生成模板…", EditorStyles.toolbarButton, GUILayout.Width(88)))
                {
                    FocusVnTemplate.GenerateFromDialog();
                }

                if (GUILayout.Button("刷新美术索引", EditorStyles.toolbarButton, GUILayout.Width(96)))
                {
                    FocusArtIndexer.Refresh();
                    Reload();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("共 " + (_library != null ? _library.Count : 0) + " 组", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("id 筛选", EditorStyles.miniLabel, GUILayout.Width(48));
                string next = GUILayout.TextField(_filter, EditorStyles.toolbarTextField, GUILayout.Width(220));
                if (!string.Equals(next, _filter, StringComparison.Ordinal))
                {
                    _filter = next;
                    ApplyFilter();
                }

                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    _filter = string.Empty;
                    ApplyFilter();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("当前显示 " + _visible.Count + " 组", EditorStyles.miniLabel);

                if (!Application.isPlaying)
                {
                    GUILayout.Label("（进 Play 模式后这里才能直接播放）", Styles.Hint);
                }
            }
        }

        private void DrawScriptTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGroupList();
                DrawGroupDetail();
            }
        }

        private void DrawGroupList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                for (int i = 0; i < _visible.Count; i++)
                {
                    DrawGroupRow(_visible[i], i);
                }

                if (_visible.Count == 0)
                {
                    EditorGUILayout.LabelField("没有匹配的剧本组", Styles.Hint);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawGroupRow(FocusVisualNovel novel, int index)
        {
            bool selected = novel == _selected;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label((index + 1).ToString("00"), Styles.Badge, GUILayout.Width(24));

                    if (GUILayout.Button(novel.NovelId, selected ? EditorStyles.boldLabel : EditorStyles.label))
                    {
                        _selected = novel;
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        DeleteGroup(novel);
                        return;
                    }
                }

                GUILayout.Label(novel.FrameCount + " 帧 · " + novel.InterruptCount + " 处中断", Styles.Hint);
            }
        }

        private void DrawGroupDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("左边选一组剧本。", MessageType.None);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(_selected.NovelId, Styles.Title);
                    GUILayout.Label("· " + _selected.FrameCount + " 帧 · " + _selected.InterruptCount + " 处中断", Styles.Hint);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button("播放", EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            FocusVnDirector.Play(_selected.NovelId);
                        }
                    }

                    if (GUILayout.Button("Inspector", EditorStyles.miniButton, GUILayout.Width(72)))
                    {
                        Selection.activeObject = _selected;
                        EditorGUIUtility.PingObject(_selected);
                    }
                }

                EditorGUILayout.LabelField("逐帧事件（BeforeLine / AfterLine）要加的话，点 Inspector 展开对应帧；这里只显示已挂的调用名。",
                    Styles.Hint);
                EditorGUILayout.Space(4f);

                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                for (int i = 0; i < _selected.FrameCount; i++)
                {
                    DrawFrameCard(_selected, i);
                }

                DrawInterrupts(_selected);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFrameCard(FocusVisualNovel novel, int index)
        {
            FocusVnFrame frame = novel.Frames[index];
            if (frame == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label((index + 1).ToString("00"), Styles.Badge, GUILayout.Width(26));
                    GUILayout.Label(string.IsNullOrEmpty(frame.Speaker) ? "（旁白）" : frame.Speaker, Styles.Speaker,
                        GUILayout.Width(96));
                    GUILayout.Label(frame.Line, Styles.Line, GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        Undo.RecordObject(novel, "删除视觉小说的一帧");
                        novel.RemoveFrameAt(index);
                        EditorUtility.SetDirty(novel);
                        AssetDatabase.SaveAssets();
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawArtwork("背景", frame.Background, false);
                    DrawArtwork("左侧立绘", frame.LeftPortrait, true);
                    DrawArtwork("右侧立绘", frame.RightPortrait, true);
                }

                DrawFrameEvents(frame);
            }
        }

        private void DrawArtwork(string label, string text, bool portrait)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ThumbWidth + 96f)))
            {
                Sprite sprite = ResolveSprite(text, portrait);
                DrawThumbnail(sprite, portrait ? 34f : 62f, portrait ? 48f : 36f);

                string shown;
                if (string.IsNullOrEmpty(text))
                {
                    shown = "（沿用）";
                }
                else if (FocusArtLibrary.IsClearToken(text))
                {
                    shown = "（清空）";
                }
                else if (sprite == null)
                {
                    shown = "⚠ " + text;
                }
                else
                {
                    shown = text;
                }

                GUILayout.Label(label + "：" + shown, Styles.Chip);
            }
        }

        private void DrawThumbnail(Sprite sprite, float width, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.13f, 0.16f, 1f));

            if (sprite == null || sprite.texture == null)
            {
                EditorGUI.LabelField(rect, "?", Styles.Badge);
                return;
            }

            Texture texture = sprite.texture;
            float scale = Mathf.Min(width / texture.width, height / texture.height);
            var fitted = new Rect(
                rect.x + (width - (texture.width * scale)) * 0.5f,
                rect.y + (height - (texture.height * scale)) * 0.5f,
                texture.width * scale,
                texture.height * scale);

            GUI.DrawTexture(fitted, texture, ScaleMode.ScaleToFit, true);
        }

        private Sprite ResolveSprite(string text, bool portrait)
        {
            if (_art == null || string.IsNullOrEmpty(text) || FocusArtLibrary.IsClearToken(text))
            {
                return null;
            }

            if (portrait)
            {
                return _art.TryResolvePortrait(text, out Sprite sprite, out _) ? sprite : null;
            }

            return _art.TryResolveBackground(text, out Sprite background, out _) ? background : null;
        }

        private void DrawFrameEvents(FocusVnFrame frame)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("断点事件", Styles.Hint, GUILayout.Width(56));
                GUILayout.Label("前：" + DescribeEvent(frame.BeforeLine), Styles.Chip);
                GUILayout.Space(8f);
                GUILayout.Label("后：" + DescribeEvent(frame.AfterLine), Styles.Chip);
            }
        }

        private static string DescribeEvent(UnityEngine.Events.UnityEvent unityEvent)
        {
            if (unityEvent == null)
            {
                return "无";
            }

            int count = unityEvent.GetPersistentEventCount();
            if (count == 0)
            {
                return "无";
            }

            var names = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object target = unityEvent.GetPersistentTarget(i);
                string method = unityEvent.GetPersistentMethodName(i);
                names.Add((target != null ? target.name : "（空目标）") + "." + method + "()");
            }

            return count + " 个：" + string.Join("、", names);
        }

        private void DrawInterrupts(FocusVisualNovel novel)
        {
            if (novel.InterruptCount == 0)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("组级中断（播到断点会暂停，等外部 Resume）", EditorStyles.boldLabel);

            for (int i = 0; i < novel.Interrupts.Count; i++)
            {
                FocusVnInterrupt interrupt = novel.Interrupts[i];
                if (interrupt == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string where = interrupt.AfterFrame < 0 ? "开场前" : "第 " + (interrupt.AfterFrame + 1) + " 帧后";
                    GUILayout.Label(where, Styles.Chip, GUILayout.Width(80));
                    GUILayout.Label(interrupt.EventKey, EditorStyles.boldLabel, GUILayout.Width(140));
                    GUILayout.Label(interrupt.Note, Styles.Hint);

                    if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        Undo.RecordObject(novel, "删除中断事件");
                        novel.RemoveInterruptAt(i);
                        EditorUtility.SetDirty(novel);
                        AssetDatabase.SaveAssets();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DeleteGroup(FocusVisualNovel novel)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "删除视觉小说组",
                "要从目录里移除「" + novel.NovelId + "」吗？\n\n移出目录：资产文件保留在 Assets/Data/VisualNovels 下，以后还能手动加回来。\n"
                + "同时删除资产文件：连 .asset 一起删掉。",
                "移出目录",
                "取消",
                "同时删除资产文件");

            if (choice == 1)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(novel);
            Undo.RecordObject(_library, "从目录移除剧本");
            _library.Remove(novel);
            EditorUtility.SetDirty(_library);

            if (choice == 2 && !string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _selected = null;
            Reload();
        }

        private void DrawPortraitTab()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("从剧本补齐立绘名", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    int added = FocusArtIndexer.RegisterUnboundNames(_art, _library);
                    Reload();
                    EditorUtility.DisplayDialog("立绘名", added > 0 ? "补了 " + added + " 条，接着给它们选图片。" : "没有需要补的名字。", "好");
                }

                if (GUILayout.Button("从美术目录补条目", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    int added = AddMissingFileEntries();
                    Reload();
                    EditorUtility.DisplayDialog("立绘名", "按文件名补了 " + added + " 条（图片已绑好）。", "好");
                }

                if (GUILayout.Button("新增一条", EditorStyles.toolbarButton, GUILayout.Width(72)))
                {
                    Undo.RecordObject(_art, "新建立绘名");
                    _art.AddPortrait(string.Empty);
                    EditorUtility.SetDirty(_art);
                    AssetDatabase.SaveAssets();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(_art != null ? "已登记 " + _art.Portraits.Count + " 条" : "没有美术表", Styles.Hint);
            }

            if (_art == null)
            {
                EditorGUILayout.HelpBox("没有 Assets/Data/FocusArtLibrary.asset，点工具栏「刷新美术索引」会建一个。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                "编剧在表格里写「实例」或「实例（暗）」都会用这里登记的「实例」；括号里的说明目前忽略，留给以后的附加事件。",
                Styles.Hint);

            _portraitScroll = EditorGUILayout.BeginScrollView(_portraitScroll);
            DrawPortraitRows();
            DrawUnboundHints();
            DrawSpriteIndex();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPortraitRows()
        {
            EditorGUILayout.LabelField("立绘名 → 图片", EditorStyles.boldLabel);

            for (int i = 0; i < _art.Portraits.Count; i++)
            {
                FocusPortraitEntry entry = _art.Portraits[i];
                if (entry == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string name = EditorGUILayout.TextField(entry.Name, GUILayout.Width(180));
                    Sprite sprite = (Sprite)EditorGUILayout.ObjectField(entry.Sprite, typeof(Sprite), false, GUILayout.Width(180));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_art, "修改立绘名");
                        entry.Name = name;
                        entry.Sprite = sprite;
                        EditorUtility.SetDirty(_art);
                        AssetDatabase.SaveAssets();
                    }

                    DrawThumbnail(entry.Sprite, 26f, 36f);

                    if (entry.Sprite == null)
                    {
                        GUILayout.Label("未绑图", Styles.Hint, GUILayout.Width(48));
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        Undo.RecordObject(_art, "删除立绘名");
                        _art.RemovePortraitAt(i);
                        EditorUtility.SetDirty(_art);
                        AssetDatabase.SaveAssets();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DrawUnboundHints()
        {
            var used = new List<string>();
            if (_library != null)
            {
                for (int i = 0; i < _library.Count; i++)
                {
                    if (_library.Novels[i] != null)
                    {
                        _library.Novels[i].CollectPortraitNames(used);
                    }
                }
            }

            var missing = new List<string>();
            for (int i = 0; i < used.Count; i++)
            {
                string name = FocusArtLibrary.SplitNote(used[i], out _);
                if (!string.IsNullOrEmpty(name) && !FocusArtLibrary.IsClearToken(name) && !_art.HasPortraitEntry(name))
                {
                    missing.Add(name);
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "剧本里用到、但还没登记的名字：" + string.Join("、", missing) + "\n播放时会打 warning 并跳过这一侧的图。",
                MessageType.Warning);
        }

        private void DrawSpriteIndex()
        {
            EditorGUILayout.Space(6f);
            _showSprites = EditorGUILayout.Foldout(_showSprites,
                "背景索引（自动扫描 " + FocusArtIndexer.ArtRoot + "，共 " + _art.Sprites.Count + " 张）", true);

            if (!_showSprites)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < _art.Sprites.Count; i++)
                {
                    FocusSpriteEntry entry = _art.Sprites[i];
                    if (entry == null || string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(entry.Name, GUILayout.Width(200));
                        DrawThumbnail(entry.Sprite, 26f, 26f);
                        GUILayout.Label(entry.Sprite != null ? "" : "缺图", Styles.Hint);
                    }
                }
            }
        }

        /// <summary>美术目录里每张图补一条同名立绘名，省得手输。</summary>
        private int AddMissingFileEntries()
        {
            if (_art == null)
            {
                return 0;
            }

            int added = 0;
            for (int i = 0; i < _art.Sprites.Count; i++)
            {
                FocusSpriteEntry sprite = _art.Sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.Name) || _art.HasPortraitEntry(sprite.Name))
                {
                    continue;
                }

                FocusPortraitEntry entry = _art.AddPortrait(sprite.Name);
                entry.Sprite = sprite.Sprite;
                added++;
            }

            EditorUtility.SetDirty(_art);
            AssetDatabase.SaveAssets();
            return added;
        }
    }
}
