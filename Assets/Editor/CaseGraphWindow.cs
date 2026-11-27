using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoolingGame.Core;
using CoolingGame.Core.Samples;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 案件流程图：把「卡牌 → 配方 → 产出卡/线索 → 问题页」画成一张有向图。
    ///
    /// 用途不是好看，是找问题：
    ///   - 哪张卡没有任何配方能产出（拿不到）
    ///   - 哪条线索没有流进任何问题页（白送）
    ///   - 哪个问题页的答案靠什么路径到达
    ///
    /// 布局按「类别分列、出现顺序分行」，稳定、不用手动摆；
    /// 点一个节点会高亮它直接相连的边。
    /// </summary>
    public sealed class CaseGraphWindow : EditorWindow
    {
        private sealed class Node
        {
            public string Id;
            public string Label;
            public Rect Rect;
            public Color Color;
            public string Group;
        }

        private sealed class Edge
        {
            public string From;
            public string To;
            public string Label;
            public bool Dashed;
        }

        private const float ColumnWidth = 240f;
        private const float RowHeight = 34f;
        private const float NodeWidth = 200f;
        private const float NodeHeight = 24f;
        private const float Margin = 40f;

        private CaseDefinition _case;
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<Edge> _edges = new List<Edge>();
        private readonly Dictionary<string, Node> _nodeById = new Dictionary<string, Node>();
        private Vector2 _scroll;
        private string _selectedId;
        private bool _showRecipes = true;
        private bool _showPages = true;
        private Vector2 _canvasSize;

        [MenuItem("CoolingGame/案件流程图")]
        public static void Open()
        {
            var window = GetWindow<CaseGraphWindow>("案件流程图");
            window.minSize = new Vector2(900f, 600f);
            window.Rebuild();
            window.Show();
        }

        private void OnEnable()
        {
            if (_case == null)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            _case = Chapter1Case.Build();
            _nodes.Clear();
            _edges.Clear();
            _nodeById.Clear();

            BuildNodes();
            BuildCardEdges();
            BuildPageEdges();

            _canvasSize = new Vector2(ColumnWidth * 5f + Margin * 2f,
                Mathf.Max(600f, (_nodes.Count / 5f + 1f) * RowHeight + Margin * 2f));
        }

        private void BuildNodes()
        {
            var counters = new Dictionary<string, int>();

            foreach (CardDefinition card in _case.Cards)
            {
                string group = CardGroup(card.Category);
                AddNode(card.Id, card.DisplayName, group, CardColor(card.Category), counters);
            }

            foreach (ClueDefinition clue in _case.Clues)
            {
                AddNode(clue.Id, Shorten(clue.Text, 14), "clue", new Color(0.55f, 0.62f, 0.42f), counters);
            }

            foreach (QuestionPageDefinition page in _case.QuestionPages)
            {
                AddNode(page.Id, string.IsNullOrEmpty(page.Title) ? page.Id : page.Title,
                    "page", new Color(0.55f, 0.45f, 0.65f), counters);
            }
        }

        private void AddNode(string id, string label, string group, Color color, Dictionary<string, int> counters)
        {
            counters.TryGetValue(group, out int index);
            counters[group] = index + 1;

            int column = ColumnOf(group);
            var node = new Node
            {
                Id = id,
                Label = label,
                Group = group,
                Color = color,
                Rect = new Rect(
                    Margin + column * ColumnWidth,
                    Margin + index * RowHeight,
                    NodeWidth,
                    NodeHeight)
            };

            _nodes.Add(node);
            _nodeById[id] = node;
        }

        private static int ColumnOf(string group)
        {
            switch (group)
            {
                case "location": return 0;
                case "item": return 1;
                case "person": return 2;
                case "clue": return 3;
                default: return 4;
            }
        }

        private static string CardGroup(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Location: return "location";
                case CardCategory.Person: return "person";
                default: return "item";
            }
        }

        private static Color CardColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Location: return new Color(0.42f, 0.52f, 0.62f);
                case CardCategory.Person: return new Color(0.62f, 0.48f, 0.40f);
                default: return new Color(0.52f, 0.50f, 0.44f);
            }
        }

        private void BuildCardEdges()
        {
            foreach (RecipeDefinition recipe in _case.Recipes)
            {
                if (string.IsNullOrEmpty(recipe.PrimaryCardId))
                {
                    continue;
                }

                for (int i = 0; i < recipe.OutputCardIds.Count; i++)
                {
                    _edges.Add(new Edge
                    {
                        From = recipe.PrimaryCardId,
                        To = recipe.OutputCardIds[i],
                        Label = recipe.Action.ToString()
                    });
                }

                for (int i = 0; i < recipe.OutputClueIds.Count; i++)
                {
                    _edges.Add(new Edge
                    {
                        From = recipe.PrimaryCardId,
                        To = recipe.OutputClueIds[i],
                        Label = recipe.Action.ToString()
                    });
                }

                // 场景切换也画一条，能看出「去某地」的路线
                if (!string.IsNullOrEmpty(recipe.OutputSceneId))
                {
                    _edges.Add(new Edge
                    {
                        From = recipe.PrimaryCardId,
                        To = recipe.OutputSceneId,
                        Label = "Go",
                        Dashed = true
                    });
                }
            }
        }

        private void BuildPageEdges()
        {
            foreach (QuestionPageDefinition page in _case.QuestionPages)
            {
                foreach (QuestionSlotDefinition slot in page.Slots)
                {
                    for (int i = 0; i < slot.AcceptedCardIds.Count; i++)
                    {
                        _edges.Add(new Edge { From = slot.AcceptedCardIds[i], To = page.Id, Label = slot.Label, Dashed = true });
                    }

                    for (int i = 0; i < slot.AcceptedClueIds.Count; i++)
                    {
                        _edges.Add(new Edge { From = slot.AcceptedClueIds[i], To = page.Id, Label = slot.Label, Dashed = true });
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (_case == null)
            {
                Rebuild();
            }

            DrawToolbar();

            if (_nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("没有可画的内容。", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Rect canvas = GUILayoutUtility.GetRect(_canvasSize.x, _canvasSize.y);

            DrawEdges(canvas);
            DrawNodes();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            {
                Rebuild();
            }

            _showRecipes = GUILayout.Toggle(_showRecipes, "配方连线", EditorStyles.toolbarButton, GUILayout.Width(80f));
            _showPages = GUILayout.Toggle(_showPages, "问题页连线", EditorStyles.toolbarButton, GUILayout.Width(90f));

            GUILayout.Space(10f);
            GUILayout.Label(string.Format("{0} 个节点 · {1} 条连线", _nodes.Count, _edges.Count), EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("导出 Mermaid", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                ExportMermaid();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEdges(Rect canvas)
        {
            Handles.BeginGUI();

            for (int i = 0; i < _edges.Count; i++)
            {
                Edge edge = _edges[i];
                if (edge.Dashed && !_showPages)
                {
                    continue;
                }

                if (!edge.Dashed && !_showRecipes)
                {
                    continue;
                }

                if (!_nodeById.TryGetValue(edge.From, out Node from) || !_nodeById.TryGetValue(edge.To, out Node to))
                {
                    continue;
                }

                bool highlighted = string.IsNullOrEmpty(_selectedId)
                                   || edge.From == _selectedId
                                   || edge.To == _selectedId;

                Color color = highlighted
                    ? (edge.Dashed ? new Color(0.75f, 0.62f, 0.85f, 0.85f) : new Color(0.85f, 0.78f, 0.55f, 0.85f))
                    : new Color(0.6f, 0.6f, 0.6f, 0.12f);

                Vector2 start = new Vector2(from.Rect.xMax, from.Rect.center.y);
                Vector2 end = new Vector2(to.Rect.xMin, to.Rect.center.y);

                Handles.DrawBezier(start, end, start + Vector2.right * 40f, end + Vector2.left * 40f,
                    color, null, highlighted ? 2f : 1f);
            }

            Handles.EndGUI();
        }

        private void DrawNodes()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                Node node = _nodes[i];
                bool selected = node.Id == _selectedId;

                Color previous = GUI.color;
                GUI.color = selected ? Color.white : node.Color;
                GUI.Box(node.Rect, GUIContent.none);
                GUI.color = previous;

                if (selected)
                {
                    Handles.BeginGUI();
                    Handles.DrawSolidRectangleWithOutline(node.Rect, Color.clear, new Color(1f, 0.9f, 0.55f));
                    Handles.EndGUI();
                }

                var style = new GUIStyle(EditorStyles.miniLabel);
                style.alignment = TextAnchor.MiddleLeft;
                style.normal.textColor = new Color(0.95f, 0.94f, 0.9f);
                GUI.Label(new Rect(node.Rect.x + 6f, node.Rect.y, node.Rect.width - 10f, node.Rect.height),
                    node.Id + "  " + node.Label, style);

                if (Event.current.type == EventType.MouseDown && node.Rect.Contains(Event.current.mousePosition))
                {
                    _selectedId = _selectedId == node.Id ? null : node.Id;
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        private void ExportMermaid()
        {
            var builder = new StringBuilder();
            builder.AppendLine("flowchart LR");

            foreach (CardDefinition card in _case.Cards)
            {
                builder.AppendLine(string.Format("    {0}[\"{1} {2}\"]", Safe(card.Id), card.Id, card.DisplayName));
            }

            foreach (ClueDefinition clue in _case.Clues)
            {
                builder.AppendLine(string.Format("    {0}(\"{1} {2}\")", Safe(clue.Id), clue.Id, Shorten(clue.Text, 12)));
            }

            foreach (QuestionPageDefinition page in _case.QuestionPages)
            {
                builder.AppendLine(string.Format("    {0}{{{{{1}}}}}", Safe(page.Id),
                    string.IsNullOrEmpty(page.Title) ? page.Id : page.Title));
            }

            foreach (RecipeDefinition recipe in _case.Recipes)
            {
                if (string.IsNullOrEmpty(recipe.PrimaryCardId))
                {
                    continue;
                }

                for (int i = 0; i < recipe.OutputCardIds.Count; i++)
                {
                    builder.AppendLine(string.Format("    {0} -->|{1}| {2}",
                        Safe(recipe.PrimaryCardId), recipe.Action, Safe(recipe.OutputCardIds[i])));
                }

                for (int i = 0; i < recipe.OutputClueIds.Count; i++)
                {
                    builder.AppendLine(string.Format("    {0} -->|{1}| {2}",
                        Safe(recipe.PrimaryCardId), recipe.Action, Safe(recipe.OutputClueIds[i])));
                }
            }

            foreach (QuestionPageDefinition page in _case.QuestionPages)
            {
                foreach (QuestionSlotDefinition slot in page.Slots)
                {
                    for (int i = 0; i < slot.AcceptedCardIds.Count; i++)
                    {
                        builder.AppendLine(string.Format("    {0} -.->|{1}| {2}",
                            Safe(slot.AcceptedCardIds[i]), slot.Label, Safe(page.Id)));
                    }

                    for (int i = 0; i < slot.AcceptedClueIds.Count; i++)
                    {
                        builder.AppendLine(string.Format("    {0} -.->|{1}| {2}",
                            Safe(slot.AcceptedClueIds[i]), slot.Label, Safe(page.Id)));
                    }
                }
            }

            string path = EditorUtility.SaveFilePanel("导出流程图", "documents", "第一章流程图.md", "md");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            Debug.Log("[流程图] 已导出 Mermaid 到 " + path + "（可粘到 mermaid.live 直接看）");
            EditorUtility.RevealInFinder(path);
        }

        private static string Safe(string id)
        {
            return string.IsNullOrEmpty(id) ? "unknown" : id.Replace("-", "_");
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
