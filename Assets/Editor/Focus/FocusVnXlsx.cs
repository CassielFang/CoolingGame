using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 极简 xlsx 读取：只取第一张工作表，只取文本/数值。
    /// 不引第三方库（Unity 自带 System.IO.Compression 与 System.Xml.Linq 就够了）。
    /// </summary>
    public static class FocusVnXlsx
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        /// <summary>读出第一张工作表，返回「行 → 按列号排好的单元格文本」，空行保留为空数组。</summary>
        public static List<string[]> ReadFirstSheet(string xlsxPath)
        {
            if (string.IsNullOrEmpty(xlsxPath) || !File.Exists(xlsxPath))
            {
                throw new FileNotFoundException("找不到 xlsx：" + xlsxPath);
            }

            using (ZipArchive zip = ZipFile.OpenRead(xlsxPath))
            {
                List<string> shared = ReadSharedStrings(zip);
                ZipArchiveEntry sheet = FindFirstSheet(zip);

                using (Stream stream = sheet.Open())
                using (var reader = new StreamReader(stream))
                {
                    XDocument doc = XDocument.Load(reader);
                    return ReadRows(doc, shared);
                }
            }
        }

        private static ZipArchiveEntry FindFirstSheet(ZipArchive zip)
        {
            ZipArchiveEntry[] sheets = zip.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                            && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (sheets.Length == 0)
            {
                throw new InvalidDataException("xlsx 里没有工作表");
            }

            return sheets[0];
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var result = new List<string>();
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return result;
            }

            using (Stream stream = entry.Open())
            using (var reader = new StreamReader(stream))
            {
                XDocument doc = XDocument.Load(reader);
                foreach (XElement item in doc.Descendants(Main + "si"))
                {
                    // 富文本会被拆成多个 <t>，拼起来就是可见文本。
                    string text = string.Concat(item.Descendants(Main + "t").Select(t => t.Value));
                    result.Add(text);
                }
            }

            return result;
        }

        private static List<string[]> ReadRows(XDocument doc, List<string> shared)
        {
            var rows = new List<string[]>();

            foreach (XElement row in doc.Descendants(Main + "row"))
            {
                var cells = new SortedDictionary<int, string>();

                foreach (XElement cell in row.Elements(Main + "c"))
                {
                    string reference = (string)cell.Attribute("r") ?? "A1";
                    int column = ColumnIndex(reference);
                    string type = (string)cell.Attribute("t") ?? "n";
                    string value;

                    if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
                    {
                        XElement v = cell.Element(Main + "v");
                        int index;
                        value = v != null && int.TryParse(v.Value, out index) && index >= 0 && index < shared.Count
                            ? shared[index]
                            : string.Empty;
                    }
                    else if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
                    {
                        value = string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
                    }
                    else if (string.Equals(type, "str", StringComparison.OrdinalIgnoreCase))
                    {
                        XElement v = cell.Element(Main + "v");
                        value = v != null ? v.Value : string.Empty;
                    }
                    else
                    {
                        XElement v = cell.Element(Main + "v");
                        value = v != null ? v.Value : string.Empty;
                    }

                    cells[column] = value ?? string.Empty;
                }

                if (cells.Count == 0)
                {
                    rows.Add(Array.Empty<string>());
                    continue;
                }

                int last = cells.Keys.Max();
                var line = new string[last + 1];
                for (int i = 0; i < line.Length; i++)
                {
                    line[i] = string.Empty;
                }

                foreach (KeyValuePair<int, string> pair in cells)
                {
                    line[pair.Key] = pair.Value;
                }

                rows.Add(line);
            }

            return rows;
        }

        /// <summary>"AB12" → 27（0 基列号）。</summary>
        private static int ColumnIndex(string reference)
        {
            int column = 0;
            for (int i = 0; i < reference.Length; i++)
            {
                char c = reference[i];
                if (c < 'A' || c > 'Z')
                {
                    break;
                }

                column = (column * 26) + (c - 'A' + 1);
            }

            return Math.Max(0, column - 1);
        }

        /// <summary>把单元格文本当整数读，读不出返回 fallback。</summary>
        public static int ReadInt(string text, int fallback)
        {
            if (string.IsNullOrEmpty(text))
            {
                return fallback;
            }

            int value;
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            double parsed;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return (int)Math.Round(parsed);
            }

            return fallback;
        }
    }
}
