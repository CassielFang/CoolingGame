using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CoolingGame.Core;
using CoolingGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 批量导入：把策划的文案 xlsx 一次转成游戏数据。
    ///
    /// 现在的产出：
    ///   - 剧本（两个对话 sheet）→ Resources/Cases/Chapter1Dialogues.json
    ///   - 报纸（报纸新闻 sheet）→ Resources/Cases/Chapter1Newspapers.json
    ///
    /// 设计上「表是唯一事实来源」：重新导入会覆盖 JSON，
    /// 所以在剧本编辑器里手改过的内容会被冲掉——要改内容请回到表里改。
    /// </summary>
    public static class ContentImporter
    {
        private const string DialogueJsonPath = "Assets/Resources/Cases/Chapter1Dialogues.json";
        private const string NewspaperJsonPath = "Assets/Resources/Cases/Chapter1Newspapers.json";
        private const string LastPathKey = "CoolingGame.ContentImporter.LastXlsx";

        /// <summary>文案表里出现的角色名 → 立绘键。新增角色在这里补一行即可。</summary>
        private static readonly Dictionary<string, string> PortraitMap = new Dictionary<string, string>
        {
            { "莫里森", "char/mp01_default" },
            { "雪莉", "char/mp02_default" },
            { "警员", "char/p08_officer" },
            { "塞德里克", "char/p01_cedric" },
            { "舞女", "char/p06_dancer" },
            { "沙龙女主人", "char/p07_salon" },
            { "哈罗德神父", "" }
        };

        [MenuItem("CoolingGame/批量导入/文案表 → 剧本与报纸")]
        public static void ImportWithDialog()
        {
            string start = Directory.Exists("documents") ? Path.GetFullPath("documents") : Application.dataPath;
            string path = EditorUtility.OpenFilePanel("选择第一章文案表", start, "xlsx");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            EditorPrefs.SetString(LastPathKey, path);
            Import(path);
        }

        [MenuItem("CoolingGame/批量导入/重新导入上次的文案表")]
        public static void ReimportLast()
        {
            string path = EditorPrefs.GetString(LastPathKey, string.Empty);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning("[导入] 还没有记录过文案表路径，先用「文案表 → 剧本与报纸」选一次。");
                return;
            }

            Import(path);
        }

        /// <summary>纯逻辑入口：给自动化/测试调用，不弹窗。</summary>
        public static void Import(string xlsxPath)
        {
            List<XlsxReader.Sheet> sheets = XlsxReader.Read(xlsxPath);
            if (sheets.Count == 0)
            {
                throw new InvalidDataException("表里没有可读的 sheet：" + xlsxPath);
            }

            List<DialogueScript> scripts = ReadDialogues(sheets);
            List<NewspaperDefinition> newspapers = ReadNewspapers(sheets);

            WriteJson(DialogueJsonPath, JsonUtility.ToJson(new DialogueLibrary { scripts = scripts }, true));
            WriteJson(NewspaperJsonPath, JsonUtility.ToJson(new NewspaperLibrary { newspapers = newspapers }, true));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int lineCount = 0;
            for (int i = 0; i < scripts.Count; i++)
            {
                lineCount += scripts[i].Lines.Count;
            }

            Debug.Log(string.Format("[导入] 完成：{0} 段剧本 / {1} 句台词 / {2} 篇报纸 → {3}",
                scripts.Count, lineCount, newspapers.Count, Path.GetFileName(xlsxPath)));
        }

        // ── 剧本 ────────────────────────────────────────────────────────────

        private static List<DialogueScript> ReadDialogues(List<XlsxReader.Sheet> sheets)
        {
            var scripts = new List<DialogueScript>();

            for (int s = 0; s < sheets.Count; s++)
            {
                XlsxReader.Sheet sheet = sheets[s];
                DialogueScript current = null;

                foreach (KeyValuePair<int, Dictionary<string, string>> row in sheet.Rows)
                {
                    string id = sheet.Get(row.Key, "A").Trim();
                    if (Regex.IsMatch(id, "^VN\\d+$"))
                    {
                        current = new DialogueScript { Id = id, Title = string.Empty };
                        scripts.Add(current);
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    string text = sheet.Get(row.Key, "E").Trim();
                    if (string.IsNullOrEmpty(text) || text == "台词")
                    {
                        continue;
                    }

                    string speakerRaw = sheet.Get(row.Key, "D").Trim();
                    if (speakerRaw == "说话人名字")
                    {
                        continue;
                    }

                    string left = sheet.Get(row.Key, "B");
                    string right = sheet.Get(row.Key, "C");
                    current.Lines.Add(new DialogueLine
                    {
                        LeftArtKey = PortraitKey(left, out bool leftDim),
                        LeftDimmed = leftDim,
                        RightArtKey = PortraitKey(right, out bool rightDim),
                        RightDimmed = rightDim,
                        Speaker = SpeakerName(speakerRaw) is { Length: > 0 } name ? name : RoleName(left),
                        Text = text
                    });
                }
            }

            return scripts;
        }

        private static string PortraitKey(string raw, out bool dimmed)
        {
            string role = RoleName(raw, out dimmed);
            return PortraitMap.TryGetValue(role, out string key) ? key : string.Empty;
        }

        private static string RoleName(string raw)
        {
            return RoleName(raw, out _);
        }

        private static string RoleName(string raw, out bool dimmed)
        {
            dimmed = false;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string text = raw.Trim();
            if (text.Contains("（暗）") || text.Contains("(暗)"))
            {
                dimmed = true;
                text = text.Replace("（暗）", string.Empty).Replace("(暗)", string.Empty);
            }

            return text.Trim();
        }

        private static string SpeakerName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // 「莫里森Morrison」→「莫里森」
            return Regex.Replace(raw.Trim(), "[A-Za-z·\\s]+$", string.Empty).Trim();
        }

        // ── 报纸 ────────────────────────────────────────────────────────────

        private static List<NewspaperDefinition> ReadNewspapers(List<XlsxReader.Sheet> sheets)
        {
            var list = new List<NewspaperDefinition>();

            for (int s = 0; s < sheets.Count; s++)
            {
                XlsxReader.Sheet sheet = sheets[s];
                if (!sheet.Name.Contains("报纸"))
                {
                    continue;
                }

                foreach (KeyValuePair<int, Dictionary<string, string>> row in sheet.Rows)
                {
                    string title = sheet.Get(row.Key, "A").Trim();
                    if (string.IsNullOrEmpty(title) || title == "题目")
                    {
                        continue;
                    }

                    string body = sheet.Get(row.Key, "C").Replace("\r", string.Empty).Trim();
                    list.Add(new NewspaperDefinition
                    {
                        Id = "N" + (list.Count + 1).ToString("00"),
                        Title = title,
                        Body = body
                    });
                }
            }

            return list;
        }

        private static void WriteJson(string assetPath, string json)
        {
            string full = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? "Assets");
            File.WriteAllText(full, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
