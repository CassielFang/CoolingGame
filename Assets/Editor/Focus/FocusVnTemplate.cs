using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 生成一份标准格式的视觉小说导入模板（.xlsx），交给编剧直接填。
    /// 工程里没有引入 Excel 库，这里按 OOXML 最小结构直接写 zip。
    /// </summary>
    public static class FocusVnTemplate
    {
        public const string FileName = "视觉小说导入模板.xlsx";

        private const string ContentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private const string RootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string Workbook =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets>" +
            "<sheet name=\"对话\" sheetId=\"1\" r:id=\"rId1\"/>" +
            "<sheet name=\"填写说明\" sheetId=\"2\" r:id=\"rId2\"/>" +
            "</sheets>" +
            "</workbook>";

        private const string WorkbookRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        private const string Styles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\">" +
            "<font><sz val=\"11\"/><name val=\"Microsoft YaHei\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Microsoft YaHei\"/></font>" +
            "</fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"2\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "</cellXfs>" +
            "</styleSheet>";

        private static readonly string[] Headers =
        {
            FocusVnImporter.ColumnGroup,
            FocusVnImporter.ColumnIndex,
            FocusVnImporter.ColumnBackground,
            FocusVnImporter.ColumnLeft,
            FocusVnImporter.ColumnRight,
            FocusVnImporter.ColumnSpeaker,
            FocusVnImporter.ColumnLine,
        };

        private static readonly int[] ColumnWidths = { 14, 8, 20, 20, 20, 14, 60 };

        [MenuItem("Tools/视觉小说/生成导入模板…", false, 21)]
        public static void GenerateFromDialog()
        {
            string path = EditorUtility.SaveFilePanel("保存视觉小说导入模板", string.Empty, FileName, "xlsx");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                Generate(path);
                EditorUtility.DisplayDialog("视觉小说模板", "模板已生成：\n" + path, "好");
            }
            catch (Exception exception)
            {
                Debug.LogError("[FocusLab] 模板生成失败：" + exception);
                EditorUtility.DisplayDialog("视觉小说模板", "生成失败：\n" + exception.Message, "好");
            }
        }

        /// <summary>批处理入口：-executeMethod ...GenerateFromCommandLine -vnTemplate &lt;路径&gt;。</summary>
        public static void GenerateFromCommandLine()
        {
            string path = ReadCommandLineValue("-vnTemplate");
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", FileName);
            }

            try
            {
                Generate(path);
                Debug.Log("[FocusLab] VN TEMPLATE RESULT=PASS path=" + path);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[FocusLab] VN TEMPLATE RESULT=FAIL " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>批处理自检：生成一份模板再读回来，确认结构能被导入器解析。</summary>
        public static void ValidateFromCommandLine()
        {
            string path = ReadCommandLineValue("-vnTemplate");
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", FileName);
            }

            try
            {
                Generate(path);

                List<string[]> rows = FocusVnXlsx.ReadFirstSheet(path);
                if (rows.Count < 2)
                {
                    throw new InvalidOperationException("读回来的模板只有 " + rows.Count + " 行");
                }

                string header = string.Join("|", rows[0]);
                for (int i = 0; i < Headers.Length; i++)
                {
                    if (header.IndexOf(Headers[i], StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidOperationException("表头缺少「" + Headers[i] + "」，实际：" + header);
                    }
                }

                if (rows[1].Length < Headers.Length)
                {
                    throw new InvalidOperationException("第一行示例数据的列数不足：" + rows[1].Length);
                }

                Debug.Log("[FocusLab] VN TEMPLATE RESULT=PASS path=" + path + " rows=" + rows.Count + " header=" + header);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[FocusLab] VN TEMPLATE RESULT=FAIL " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        public static void Generate(string path)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            List<string[]> lines = BuildSampleRows();
            List<string[]> help = BuildHelpRows();

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddText(zip, "[Content_Types].xml", ContentTypes);
                AddText(zip, "_rels/.rels", RootRels);
                AddText(zip, "xl/workbook.xml", Workbook);
                AddText(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
                AddText(zip, "xl/styles.xml", Styles);
                AddText(zip, "xl/worksheets/sheet1.xml", BuildSheet(lines, true, ColumnWidths, 16.5));
                AddText(zip, "xl/worksheets/sheet2.xml", BuildSheet(help, true, new[] { 22, 90 }, 15));
            }

            Debug.Log("[FocusLab] 视觉小说模板已写入 " + path);
        }

        private static List<string[]> BuildSampleRows()
        {
            return new List<string[]>
            {
                Headers,
                new[] { "prologue_01", "1", "", "", "", "旁白", "钟楼的灯灭了。" },
                new[] { "prologue_01", "2", "bg_lab.png", "阿岚", "", "阿岚", "更夫说，熄灯之后别往顶层去。" },
                new[] { "prologue_01", "3", "", "", "银瞳", "银瞳", "可你已经在往上了。" },
                new[] { "prologue_01", "4", "", "阿岚（暗）", "银瞳", "阿岚", "那就上去——括号里是给以后的事件用的说明，不影响找图。" },
                new[] { "clue_01", "1", "", "-", "-", "旁白", "石室的地面上有一圈被磨亮的划痕。" },
                new[] { "clue_01", "2", "", "银瞳", "", "银瞳", "有人在这里反复走过同一条路。" },
            };
        }

        private static List<string[]> BuildHelpRows()
        {
            return new List<string[]>
            {
                new[] { "列名", "怎么填" },
                new[] { FocusVnImporter.ColumnGroup, "同一组的行会按「序号」串成一段连续剧情；不同组互不影响。组 ID 会变成资产名，建议用英文加下划线。" },
                new[] { "id 填 -1", "约定：组 ID 写 -1 的那组是测试文本，场景右上角的「视觉小说」按钮会优先播它；没有 -1 才播目录里的第一组。" },
                new[] { FocusVnImporter.ColumnIndex, "组内播放顺序，从 1 开始。留空则按表格行顺序。" },
                new[] { FocusVnImporter.ColumnBackground, "写 Assets/Art/Test 下的文件名（bg_lab.png 或 bg_lab 都行）。留空 = 沿用上一句；写 - 表示这一句换成纯色底。" },
                new[] { FocusVnImporter.ColumnLeft, "写「立绘名」，不是文件名。比如登记了「阿岚 → 某张图」，这里就写 阿岚。可以加括号说明：阿岚（暗），找图时只看 阿岚，括号内容暂不生效。留空 = 沿用上一句，写 - = 这一侧消失。" },
                new[] { FocusVnImporter.ColumnRight, "右侧立绘名，规则同左立绘。" },
                new[] { "立绘名在哪登记", "Tools / 视觉小说 / 视觉小说主功能窗 → 「立绘名」页签：一行一个名字，选中对应的图片。播放时找不到会打 warning 并跳过那一侧，不会中断剧情。" },
                new[] { FocusVnImporter.ColumnSpeaker, "显示在台词左上方；留空表示只有旁白台词。" },
                new[] { FocusVnImporter.ColumnLine, "这一句的台词正文。" },
                new[] { "逐帧事件", "每句台词前/后的 UnityEvent（BeforeLine / AfterLine）也能从表格以外加：导入后在剧本资产的 Inspector 里展开对应帧挂调用，主功能窗会显示已挂的调用名。" },
                new[] { "中断事件", "组级断点：播到某帧后暂停、抛 eventKey 给外部，等 Resume。不要写进表格，导入后在资产 Inspector 的 Interrupts 里按帧号添加。" },
                new[] { "导入方式", "Unity 顶端菜单 Tools / 视觉小说 / 导入 xlsx…，选中填好的表格即可。导入后可在 Tools / 视觉小说 / 视觉小说主功能窗 里查看。" },
                new[] { "重复导入", "表格里出现的组：已存在就整组替换（旧台词不残留），不存在就新增；表格里没出现的组保持原样，不会被删。导入结束会报告「替换了哪几组、新增了哪几组」。" },
            };
        }

        private static string BuildSheet(List<string[]> rows, bool boldHeader, int[] widths, double defaultRowHeight)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
            builder.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

            builder.Append("<sheetViews><sheetView workbookViewId=\"0\">");
            builder.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
            builder.Append("</sheetView></sheetViews>");

            builder.Append("<sheetFormatPr defaultRowHeight=\"").Append(defaultRowHeight.ToString("0.##")).Append("\"/>");

            if (widths != null && widths.Length > 0)
            {
                builder.Append("<cols>");
                for (int i = 0; i < widths.Length; i++)
                {
                    builder.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1)
                        .Append("\" width=\"").Append(widths[i]).Append("\" customWidth=\"1\"/>");
                }

                builder.Append("</cols>");
            }

            builder.Append("<sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                bool header = boldHeader && r == 0;
                builder.Append("<row r=\"").Append(r + 1).Append("\">");

                string[] cells = rows[r];
                for (int c = 0; c < cells.Length; c++)
                {
                    string text = cells[c] ?? string.Empty;
                    if (text.Length == 0)
                    {
                        if (!header)
                        {
                            continue;
                        }
                    }

                    builder.Append("<c r=\"").Append(ColumnName(c)).Append(r + 1).Append('"');
                    if (header)
                    {
                        builder.Append(" s=\"1\"");
                    }

                    builder.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                        .Append(Escape(text))
                        .Append("</t></is></c>");
                }

                builder.Append("</row>");
            }

            builder.Append("</sheetData></worksheet>");
            return builder.ToString();
        }

        private static string ColumnName(int index)
        {
            var builder = new StringBuilder();
            int value = index;
            while (true)
            {
                builder.Insert(0, (char)('A' + value % 26));
                value = value / 26 - 1;
                if (value < 0)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private static string Escape(string text)
        {
            var builder = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '"':
                        builder.Append("&quot;");
                        break;
                    case '\'':
                        builder.Append("&apos;");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        private static void AddText(ZipArchive zip, string entryName, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static string ReadCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
