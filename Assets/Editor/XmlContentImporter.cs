using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using CoolingGame.Core;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 读「剧本模板 XML」（documents/剧本模板.xml 定义了格式）。
    ///
    /// 相比 xlsx，XML 的好处是：能备注、能 diff、能分支合并、角色用 id 引用。
    /// 这个类只做一件事：把 XML 解析成 DialogueScript / NewspaperDefinition，
    /// 写 JSON、运行时加载都交给上层（ContentImporter / DialogueLibrary）。
    /// </summary>
    public static class XmlContentImporter
    {
        private sealed class CharacterInfo
        {
            public string Name;
            public string Portrait;
            public string Smile;
            public string Question;
        }

        public static void Read(string xmlPath, out List<DialogueScript> scripts, out List<NewspaperDefinition> newspapers)
        {
            if (!File.Exists(xmlPath))
            {
                throw new FileNotFoundException("找不到 XML：" + xmlPath, xmlPath);
            }

            var characters = new Dictionary<string, CharacterInfo>();
            scripts = new List<DialogueScript>();
            newspapers = new List<NewspaperDefinition>();

            var document = new XmlDocument();
            document.Load(xmlPath);

            XmlNode chapter = document.SelectSingleNode("/chapter");
            if (chapter == null)
            {
                throw new InvalidDataException("XML 根节点必须是 <chapter>");
            }

            XmlNode characterRoot = chapter.SelectSingleNode("characters");
            if (characterRoot != null)
            {
                foreach (XmlNode node in characterRoot.SelectNodes("character"))
                {
                    string id = Attr(node, "id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    characters[id] = new CharacterInfo
                    {
                        Name = Attr(node, "name"),
                        Portrait = Attr(node, "portrait"),
                        Smile = Attr(node, "smile"),
                        Question = Attr(node, "question")
                    };
                }
            }

            XmlNode dialogueRoot = chapter.SelectSingleNode("dialogues");
            if (dialogueRoot != null)
            {
                foreach (XmlNode node in dialogueRoot.SelectNodes("dialogue"))
                {
                    string id = Attr(node, "id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var script = new DialogueScript { Id = id, Title = Attr(node, "title") };

                    foreach (XmlNode lineNode in node.SelectNodes("line"))
                    {
                        script.Lines.Add(ReadLine(lineNode, characters));
                    }

                    scripts.Add(script);
                }
            }

            XmlNode paperRoot = chapter.SelectSingleNode("newspapers");
            if (paperRoot != null)
            {
                foreach (XmlNode node in paperRoot.SelectNodes("newspaper"))
                {
                    string id = Attr(node, "id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var lines = new List<string>();
                    foreach (XmlNode lineNode in node.SelectNodes("line"))
                    {
                        lines.Add(lineNode.InnerText.Trim());
                    }

                    newspapers.Add(new NewspaperDefinition
                    {
                        Id = id,
                        Title = Attr(node, "title"),
                        Body = string.Join("\n", lines)
                    });
                }
            }
        }

        private static DialogueLine ReadLine(XmlNode node, Dictionary<string, CharacterInfo> characters)
        {
            string leftId = Attr(node, "left");
            string rightId = Attr(node, "right");
            string expression = Attr(node, "expression");

            var line = new DialogueLine
            {
                LeftArtKey = PortraitOf(characters, leftId, expression),
                RightArtKey = PortraitOf(characters, rightId, expression),
                LeftDimmed = IsTrue(Attr(node, "left-dim")),
                RightDimmed = IsTrue(Attr(node, "right-dim")),
                Speaker = Attr(node, "speaker"),
                Text = node.InnerText.Trim()
            };

            if (string.IsNullOrEmpty(line.Speaker))
            {
                line.Speaker = NameOf(characters, leftId);
            }

            return line;
        }

        private static string PortraitOf(Dictionary<string, CharacterInfo> characters, string id, string expression)
        {
            if (string.IsNullOrEmpty(id) || !characters.TryGetValue(id, out CharacterInfo info))
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(expression))
            {
                if (expression == "smile" && !string.IsNullOrEmpty(info.Smile))
                {
                    return info.Smile;
                }

                if (expression == "question" && !string.IsNullOrEmpty(info.Question))
                {
                    return info.Question;
                }
            }

            return info.Portrait ?? string.Empty;
        }

        private static string NameOf(Dictionary<string, CharacterInfo> characters, string id)
        {
            if (!string.IsNullOrEmpty(id) && characters.TryGetValue(id, out CharacterInfo info))
            {
                return info.Name;
            }

            return string.Empty;
        }

        private static string Attr(XmlNode node, string name)
        {
            XmlAttribute attribute = node.Attributes?[name];
            return attribute != null ? attribute.Value : string.Empty;
        }

        private static bool IsTrue(string value)
        {
            return value == "true" || value == "1";
        }
    }
}
