using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 极简 xlsx 读取器：只取单元格文本，够把策划的表格读进编辑器用。
    ///
    /// xlsx = zip + XML。这里只需要共享字符串表和各 sheet 的单元格，
    /// 用 BCL 的 ZipArchive + XmlDocument 就够，不依赖 Excel 或第三方库。
    /// </summary>
    public static class XlsxReader
    {
        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>一张表：行号 → (列字母 → 文本)。行号从 1 开始。</summary>
        public sealed class Sheet
        {
            public string Name;
            public readonly SortedDictionary<int, Dictionary<string, string>> Rows =
                new SortedDictionary<int, Dictionary<string, string>>();

            public string Get(int row, string column)
            {
                if (Rows.TryGetValue(row, out Dictionary<string, string> cells)
                    && cells.TryGetValue(column, out string value))
                {
                    return value;
                }

                return string.Empty;
            }

            public int MaxRow
            {
                get
                {
                    int max = 0;
                    foreach (int key in Rows.Keys)
                    {
                        max = key;
                    }

                    return max;
                }
            }
        }

        /// <summary>读整个工作簿，返回按出现顺序排列的表。</summary>
        public static List<Sheet> Read(string xlsxPath)
        {
            if (!File.Exists(xlsxPath))
            {
                throw new FileNotFoundException("找不到 xlsx：" + xlsxPath, xlsxPath);
            }

            using ZipArchive zip = ZipFile.OpenRead(xlsxPath);

            List<string> shared = ReadSharedStrings(zip);
            List<KeyValuePair<string, string>> sheets = ReadSheetMap(zip);

            var result = new List<Sheet>();
            for (int i = 0; i < sheets.Count; i++)
            {
                var sheet = new Sheet { Name = sheets[i].Key };
                ReadSheet(zip, sheets[i].Value, shared, sheet);
                result.Add(sheet);
            }

            return result;
        }

        private static XmlNamespaceManager MainNsManager(XmlDocument document)
        {
            var manager = new XmlNamespaceManager(document.NameTable);
            manager.AddNamespace("m", MainNs);
            return manager;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return list;
            }

            var document = new XmlDocument();
            using (Stream stream = entry.Open())
            {
                document.Load(stream);
            }

            XmlNamespaceManager ns = MainNsManager(document);
            foreach (XmlNode si in document.SelectNodes("//m:si", ns))
            {
                var builder = new StringBuilder();
                foreach (XmlNode t in si.SelectNodes(".//m:t", ns))
                {
                    builder.Append(t.InnerText);
                }

                list.Add(builder.ToString());
            }

            return list;
        }

        private static List<KeyValuePair<string, string>> ReadSheetMap(ZipArchive zip)
        {
            var result = new List<KeyValuePair<string, string>>();

            ZipArchiveEntry workbookEntry = zip.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relsEntry == null)
            {
                throw new InvalidDataException("不是有效的 xlsx：缺 workbook.xml 或 rels");
            }

            var workbook = new XmlDocument();
            using (Stream stream = workbookEntry.Open())
            {
                workbook.Load(stream);
            }

            XmlNamespaceManager ns = MainNsManager(workbook);

            var idByName = new List<KeyValuePair<string, string>>();
            foreach (XmlNode sheet in workbook.SelectNodes("//m:sheet", ns))
            {
                string name = sheet.Attributes?["name"]?.Value ?? "sheet";
                string id = sheet.Attributes?["id", RelNs]?.Value;
                if (!string.IsNullOrEmpty(id))
                {
                    idByName.Add(new KeyValuePair<string, string>(id, name));
                }
            }

            var rels = new XmlDocument();
            using (Stream stream = relsEntry.Open())
            {
                rels.Load(stream);
            }

            var targetById = new Dictionary<string, string>();
            foreach (XmlNode rel in rels.SelectNodes("//*[local-name()='Relationship']"))
            {
                string id = rel.Attributes?["Id"]?.Value;
                string target = rel.Attributes?["Target"]?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                {
                    targetById[id] = target;
                }
            }

            for (int i = 0; i < idByName.Count; i++)
            {
                if (targetById.TryGetValue(idByName[i].Key, out string target))
                {
                    string path = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                    result.Add(new KeyValuePair<string, string>(idByName[i].Value, path));
                }
            }

            return result;
        }

        private static void ReadSheet(ZipArchive zip, string path, List<string> shared, Sheet sheet)
        {
            ZipArchiveEntry entry = zip.GetEntry(path);
            if (entry == null)
            {
                return;
            }

            var document = new XmlDocument();
            using (Stream stream = entry.Open())
            {
                document.Load(stream);
            }

            XmlNamespaceManager ns = MainNsManager(document);

            foreach (XmlNode row in document.SelectNodes("//m:sheetData/m:row", ns))
            {
                string rowAttr = row.Attributes?["r"]?.Value;
                if (!int.TryParse(rowAttr, out int rowIndex))
                {
                    continue;
                }

                foreach (XmlNode cell in row.SelectNodes("m:c", ns))
                {
                    string reference = cell.Attributes?["r"]?.Value ?? string.Empty;
                    string column = ReadColumn(reference);
                    if (string.IsNullOrEmpty(column))
                    {
                        continue;
                    }

                    string type = cell.Attributes?["t"]?.Value;
                    XmlNode valueNode = cell.SelectSingleNode("m:v", ns);
                    string text = valueNode?.InnerText ?? string.Empty;

                    if (type == "s" && int.TryParse(text, out int index)
                        && index >= 0 && index < shared.Count)
                    {
                        text = shared[index];
                    }
                    else if (type == "inlineStr")
                    {
                        XmlNode inline = cell.SelectSingleNode("m:is", ns);
                        if (inline != null)
                        {
                            var builder = new StringBuilder();
                            foreach (XmlNode t in inline.SelectNodes(".//m:t", ns))
                            {
                                builder.Append(t.InnerText);
                            }

                            text = builder.ToString();
                        }
                    }

                    Put(sheet, rowIndex, column, text);
                }
            }
        }

        private static string ReadColumn(string reference)
        {
            var letters = new StringBuilder();
            for (int i = 0; i < reference.Length; i++)
            {
                char c = reference[i];
                if (c >= 'A' && c <= 'Z')
                {
                    letters.Append(c);
                }
                else
                {
                    break;
                }
            }

            return letters.ToString();
        }

        private static void Put(Sheet sheet, int row, string column, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (!sheet.Rows.TryGetValue(row, out Dictionary<string, string> cells))
            {
                cells = new Dictionary<string, string>();
                sheet.Rows[row] = cells;
            }

            cells[column] = value;
        }
    }
}
