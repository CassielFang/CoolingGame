using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// xlsx → 视觉小说资产。
    /// 列固定为：组ID | 序号 | 背景 | 左立绘 | 右立绘 | 说话人 | 台词。
    /// 每个组生成一个 Assets/Data/VisualNovels/&lt;组ID&gt;.asset，并刷新 Assets/Data/VisualNovelLibrary.asset。
    /// 中断事件不写进表里（写剧本的人预料不到），导入后在资产上手动加。
    /// </summary>
    public static class FocusVnImporter
    {
        public const string NovelRoot = "Assets/Data/VisualNovels";
        public const string LibraryPath = "Assets/Data/VisualNovelLibrary.asset";

        public const string ColumnGroup = "组ID";
        public const string ColumnIndex = "序号";
        public const string ColumnBackground = "背景";
        public const string ColumnLeft = "左立绘";
        public const string ColumnRight = "右立绘";
        public const string ColumnSpeaker = "说话人";
        public const string ColumnLine = "台词";

        [MenuItem("Tools/视觉小说/导入 xlsx…", false, 20)]
        public static void ImportFromDialog()
        {
            string path = EditorUtility.OpenFilePanel("选择视觉小说表格", string.Empty, "xlsx");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ImportResult result = Import(path);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("视觉小说导入", "导入失败：" + result.Error, "好");
                return;
            }

            var text = new StringBuilder();
            text.Append("导入完成：").Append(result.GroupCount).Append(" 组、").Append(result.FrameCount).Append(" 句。\n\n");

            if (result.Updated.Count > 0)
            {
                text.Append("与已有脚本冲突，已整组替换（").Append(result.Updated.Count).Append(" 组）：")
                    .Append(string.Join("、", result.Updated)).Append("\n\n");
            }

            if (result.Added.Count > 0)
            {
                text.Append("新增（").Append(result.Added.Count).Append(" 组）：")
                    .Append(string.Join("、", result.Added)).Append("\n\n");
            }

            text.Append("表格里没有的脚本保持原样：").Append(result.Kept.Count).Append(" 组。\n\n").Append(result.GroupList);

            EditorUtility.DisplayDialog("视觉小说导入", text.ToString(), "好");
            EditorUtility.RevealInFinder(LibraryPath);
        }

        /// <summary>批处理入口：命令行追加 `-vnXlsx "D:\path\book.xlsx"`。</summary>
        public static void ImportFromCommandLine()
        {
            string path = ReadCommandLineValue("-vnXlsx");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[FocusLab] VN IMPORT RESULT=FAIL 缺少 -vnXlsx 参数");
                EditorApplication.Exit(1);
                return;
            }

            ImportResult result;
            try
            {
                result = Import(path);
            }
            catch (Exception exception)
            {
                Debug.LogError("[FocusLab] VN IMPORT RESULT=FAIL " + exception.Message);
                EditorApplication.Exit(1);
                return;
            }

            if (!result.Success)
            {
                Debug.LogError("[FocusLab] VN IMPORT RESULT=FAIL " + result.Error);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[FocusLab] VN IMPORT RESULT=PASS groups=" + result.GroupCount + " frames=" + result.FrameCount
                      + " updated=" + result.Updated.Count + " added=" + result.Added.Count
                      + " kept=" + result.Kept.Count
                      + (result.Updated.Count > 0 ? " updatedIds=" + string.Join("|", result.Updated) : string.Empty)
                      + (result.Added.Count > 0 ? " addedIds=" + string.Join("|", result.Added) : string.Empty));
            EditorApplication.Exit(0);
        }

        public sealed class ImportResult
        {
            public bool Success;
            public string Error = string.Empty;
            public int GroupCount;
            public int FrameCount;
            public string GroupList = string.Empty;

            /// <summary>与已有脚本 id 冲突、被整组替换的组。</summary>
            public readonly List<string> Updated = new List<string>();

            /// <summary>这次新导入的组。</summary>
            public readonly List<string> Added = new List<string>();

            /// <summary>表格里没出现、保持原样的组。</summary>
            public readonly List<string> Kept = new List<string>();
        }

        public static ImportResult Import(string xlsxPath)
        {
            var result = new ImportResult();

            List<string[]> rows;
            try
            {
                rows = FocusVnXlsx.ReadFirstSheet(xlsxPath);
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                return result;
            }

            if (rows.Count < 2)
            {
                result.Error = "表格里只有表头，没有对话行。";
                return result;
            }

            Dictionary<string, int> columns = MapHeader(rows[0], out string missing);
            if (columns == null)
            {
                result.Error = "缺少必要的列：" + missing;
                return result;
            }

            EnsureFolder(NovelRoot);

            // 组按出现顺序、组内按「序号」排；序号空就保持表内顺序。
            var order = new List<string>();
            var byGroup = new Dictionary<string, List<(int Seq, int Row, string[] Cells)>>(StringComparer.Ordinal);

            for (int i = 1; i < rows.Count; i++)
            {
                string[] cells = rows[i];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }

                string group = Cell(cells, Col(columns, ColumnGroup));
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                int seq = FocusVnXlsx.ReadInt(Cell(cells, Col(columns, ColumnIndex)), i);

                if (!byGroup.TryGetValue(group, out List<(int Seq, int Row, string[] Cells)> list))
                {
                    list = new List<(int, int, string[])>();
                    byGroup.Add(group, list);
                    order.Add(group);
                }

                list.Add((seq, i, cells));
            }

            if (order.Count == 0)
            {
                result.Error = "没有读到任何带组 ID 的行。";
                return result;
            }

            var novels = new List<FocusVisualNovel>(order.Count);
            var summary = new StringBuilder();
            var updated = new HashSet<string>(StringComparer.Ordinal);
            var added = new HashSet<string>(StringComparer.Ordinal);

            // 目录里已有的脚本（含表格里没提到的），先按原顺序全部保留。
            FocusVisualNovelLibrary library = AssetDatabase.LoadAssetAtPath<FocusVisualNovelLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FocusVisualNovelLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            // 目录里可能有被删掉资产留下的空引用，先清一遍再谈合并。
            var cleaned = new List<FocusVisualNovel>(library.Count);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < library.Count; i++)
            {
                FocusVisualNovel entry = library.Novels[i];
                if (entry != null && seenIds.Add(entry.NovelId))
                {
                    cleaned.Add(entry);
                }
            }

            int pruned = library.Count - cleaned.Count;
            if (pruned > 0)
            {
                library.SetNovels(cleaned);
                Debug.Log("[FocusLab] 目录里有 " + pruned + " 条失效/重复条目，已清理。");
            }

            var existingById = new Dictionary<string, FocusVisualNovel>(StringComparer.Ordinal);
            for (int i = 0; i < cleaned.Count; i++)
            {
                FocusVisualNovel existing = cleaned[i];
                existingById.Add(existing.NovelId, existing);
                novels.Add(existing);
            }

            for (int g = 0; g < order.Count; g++)
            {
                string group = order[g];
                List<(int Seq, int Row, string[] Cells)> lines = byGroup[group];
                lines.Sort((a, b) =>
                {
                    int bySeq = a.Seq.CompareTo(b.Seq);
                    return bySeq != 0 ? bySeq : a.Row.CompareTo(b.Row);
                });

                var frames = new List<FocusVnFrame>(lines.Count);
                for (int i = 0; i < lines.Count; i++)
                {
                    string[] cells = lines[i].Cells;

                    // 只存文本：图片在播放时按名字去美术表里找，换图不用重新导入。
                    frames.Add(new FocusVnFrame
                    {
                        Background = Cell(cells, Col(columns, ColumnBackground)),
                        LeftPortrait = Cell(cells, Col(columns, ColumnLeft)),
                        RightPortrait = Cell(cells, Col(columns, ColumnRight)),
                        Speaker = Cell(cells, Col(columns, ColumnSpeaker)),
                        Line = Cell(cells, Col(columns, ColumnLine)),
                    });
                }

                FocusVisualNovel novel;
                if (existingById.TryGetValue(group, out FocusVisualNovel hit))
                {
                    // 冲突：整组按新的来（中断事件由 Setup 保留并修剪悬空的）。
                    novel = hit;
                    updated.Add(group);
                }
                else
                {
                    string assetPath = NovelRoot + "/" + SafeFileName(group) + ".asset";
                    novel = AssetDatabase.LoadAssetAtPath<FocusVisualNovel>(assetPath);
                    if (novel == null)
                    {
                        novel = ScriptableObject.CreateInstance<FocusVisualNovel>();
                        AssetDatabase.CreateAsset(novel, assetPath);
                    }

                    novels.Add(novel);
                    existingById.Add(group, novel);
                    added.Add(group);
                }

                novel.Setup(group, frames);
                EditorUtility.SetDirty(novel);

                result.FrameCount += frames.Count;
                summary.Append(group).Append("（").Append(frames.Count).Append(" 句）");
                if (g < order.Count - 1)
                {
                    summary.Append("、");
                }
            }

            for (int i = 0; i < novels.Count; i++)
            {
                string id = novels[i].NovelId;
                if (!updated.Contains(id) && !added.Contains(id))
                {
                    result.Kept.Add(id);
                }
            }

            library.SetNovels(novels);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.Success = true;
            result.GroupCount = order.Count;
            result.GroupList = summary.ToString();
            result.Updated.AddRange(updated);
            result.Added.AddRange(added);
            return result;
        }

        /// <summary>表头键统一过 Normalize，取值时也得走同一条路，否则「组ID」查不到「组id」。</summary>
        private static int Col(Dictionary<string, int> columns, string header)
        {
            return columns[Normalize(header)];
        }

        private static Dictionary<string, int> MapHeader(string[] header, out string missing)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] required = { ColumnGroup, ColumnIndex, ColumnBackground, ColumnLeft, ColumnRight, ColumnSpeaker, ColumnLine };

            for (int i = 0; i < header.Length; i++)
            {
                string name = Normalize(header[i]);
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                {
                    map.Add(name, i);
                }
            }

            var absent = new List<string>();
            for (int i = 0; i < required.Length; i++)
            {
                if (!map.ContainsKey(Normalize(required[i])))
                {
                    absent.Add(required[i]);
                }
            }

            missing = string.Join("、", absent);
            return absent.Count == 0 ? map : null;
        }

        /// <summary>去掉空格、全角空格、大小写差异，容忍「组 ID」「组id」这类写法。</summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == '\u3000')
                {
                    continue;
                }

                builder.Append(char.ToLowerInvariant(c));
            }

            // 统一大小写后，"组id" 能对上 "组ID"。
            return builder.ToString();
        }

        private static string Cell(string[] cells, int index)
        {
            if (cells == null || index < 0 || index >= cells.Length)
            {
                return string.Empty;
            }

            return (cells[index] ?? string.Empty).Trim();
        }

        private static Sprite LoadSpriteOrNull(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string path = FocusLabAssetBuilder.ArtRoot + "/" + fileName;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Debug.LogWarning("[FocusLab] 视觉小说引用了找不到的图片：" + path);
            return null;
        }

        private static string SafeFileName(string id)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(id.Length);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                builder.Append(Array.IndexOf(invalid, c) >= 0 || c == '/' || c == '\\' ? '_' : c);
            }

            string name = builder.ToString().Trim();
            return string.IsNullOrEmpty(name) ? "novel" : name;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string ReadCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }
    }
}
