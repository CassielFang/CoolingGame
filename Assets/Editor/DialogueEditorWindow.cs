using System.Collections.Generic;
using System.IO;
using CoolingGame.Core;
using CoolingGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 剧本编辑器：不用碰 xlsx 和 C#，直接在编辑器里改台词、看预览、按 Ctrl+S 保存。
    ///
    /// 数据落在 Assets/Resources/Cases/Chapter1Dialogues.json（运行时优先读它）。
    /// 左边选剧本，中间逐句编辑，右边实时预览这一句的立绘 + 对话框。
    /// xlsx 仍然是策划的原始表；两边都改时以最后保存的一方为准（不要并行改同一句）。
    /// </summary>
    public sealed class DialogueEditorWindow : EditorWindow
    {
        private const string JsonPath = "Assets/Resources/Cases/Chapter1Dialogues.json";

        private static readonly string[] PortraitOptions =
        {
            "",
            "char/mp01_default", "char/mp01_smile", "char/mp01_question",
            "char/mp02_default", "char/mp02_smile", "char/mp02_question",
            "char/p01_cedric", "char/p06_dancer", "char/p07_salon", "char/p08_officer"
        };

        private DialogueLibrary _library;
        private int _scriptIndex;
        private int _lineIndex;
        private Vector2 _scriptScroll;
        private Vector2 _lineScroll;
        private bool _dirty;

        [MenuItem("CoolingGame/剧本编辑器")]
        public static void Open()
        {
            var window = GetWindow<DialogueEditorWindow>("剧本编辑器");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            Load();
        }

        private void Load()
        {
            if (!File.Exists(JsonPath))
            {
                _library = new DialogueLibrary();
                return;
            }

            string json = File.ReadAllText(JsonPath, System.Text.Encoding.UTF8);
            _library = JsonUtility.FromJson<DialogueLibrary>(json) ?? new DialogueLibrary();
            if (_library.scripts == null)
            {
                _library.scripts = new List<DialogueScript>();
            }

            _scriptIndex = Mathf.Clamp(_scriptIndex, 0, Mathf.Max(0, _library.scripts.Count - 1));
            _lineIndex = 0;
            _dirty = false;
        }

        private void Save()
        {
            if (_library == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(_library, true);
            File.WriteAllText(JsonPath, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(JsonPath, ImportAssetOptions.ForceUpdate);
            _dirty = false;
            Debug.Log("[剧本] 已保存 " + _library.scripts.Count + " 段到 " + JsonPath);
        }

        private void OnGUI()
        {
            if (_library == null)
            {
                Load();
            }

            DrawToolbar();

            if (_library.scripts.Count == 0)
            {
                EditorGUILayout.HelpBox("没有剧本数据。点「从 xlsx 重新导入」或手动放一份 JSON 到 " + JsonPath, MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            DrawScriptList();
            DrawLineEditor();
            DrawPreview();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("保存 (Ctrl+S)", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                Save();
            }

            if (GUILayout.Button("重新读取", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                if (_dirty && !EditorUtility.DisplayDialog("放弃未保存的修改？", "磁盘上的文件会覆盖当前编辑内容。", "放弃", "取消"))
                {
                    return;
                }

                Load();
            }

            GUILayout.Space(8f);

            int totalLines = 0;
            for (int i = 0; i < _library.scripts.Count; i++)
            {
                totalLines += _library.scripts[i].Lines != null ? _library.scripts[i].Lines.Count : 0;
            }

            GUILayout.Label(string.Format("{0} 段 · {1} 句{2}", _library.scripts.Count, totalLines,
                _dirty ? " · 有未保存修改" : string.Empty), EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            GUILayout.Label(JsonPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawScriptList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190f));
            EditorGUILayout.LabelField("剧本", EditorStyles.boldLabel);

            _scriptScroll = EditorGUILayout.BeginScrollView(_scriptScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _library.scripts.Count; i++)
            {
                DialogueScript script = _library.scripts[i];
                string label = script.Id + (string.IsNullOrEmpty(script.Title) ? "" : "  " + script.Title);
                bool selected = i == _scriptIndex;
                if (GUILayout.Toggle(selected, label, "Button") && !selected)
                {
                    _scriptIndex = i;
                    _lineIndex = 0;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawLineEditor()
        {
            DialogueScript script = _library.scripts[_scriptIndex];
            if (script.Lines == null)
            {
                script.Lines = new List<DialogueLine>();
            }

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            script.Id = EditorGUILayout.TextField("剧本 ID", script.Id);
            script.Title = EditorGUILayout.TextField("标题", script.Title);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 加一句", GUILayout.Width(90f)))
            {
                script.Lines.Insert(Mathf.Min(script.Lines.Count, _lineIndex + 1), new DialogueLine
                {
                    Speaker = string.Empty,
                    Text = string.Empty
                });
                _lineIndex = Mathf.Min(script.Lines.Count - 1, _lineIndex + 1);
                _dirty = true;
            }

            if (GUILayout.Button("删除这句", GUILayout.Width(90f)) && script.Lines.Count > 0)
            {
                script.Lines.RemoveAt(Mathf.Clamp(_lineIndex, 0, script.Lines.Count - 1));
                _lineIndex = Mathf.Clamp(_lineIndex, 0, Mathf.Max(0, script.Lines.Count - 1));
                _dirty = true;
            }

            if (GUILayout.Button("↑", GUILayout.Width(28f)) && _lineIndex > 0)
            {
                Swap(script.Lines, _lineIndex, _lineIndex - 1);
                _lineIndex--;
                _dirty = true;
            }

            if (GUILayout.Button("↓", GUILayout.Width(28f)) && _lineIndex < script.Lines.Count - 1)
            {
                Swap(script.Lines, _lineIndex, _lineIndex + 1);
                _lineIndex++;
                _dirty = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format("{0} / {1}", _lineIndex + 1, script.Lines.Count), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            _lineScroll = EditorGUILayout.BeginScrollView(_lineScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < script.Lines.Count; i++)
            {
                DialogueLine line = script.Lines[i];
                bool selected = i == _lineIndex;

                EditorGUILayout.BeginHorizontal(selected ? "SelectionRect" : GUIStyle.none);
                if (GUILayout.Button(selected ? "▶" : "·", EditorStyles.miniButton, GUILayout.Width(24f)))
                {
                    _lineIndex = i;
                }

                string preview = (line.Speaker ?? "") + "：" + (line.Text ?? "");
                if (preview.Length > 70)
                {
                    preview = preview.Substring(0, 70) + "…";
                }

                if (GUILayout.Button(preview, EditorStyles.label))
                {
                    _lineIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (script.Lines.Count > 0)
            {
                _lineIndex = Mathf.Clamp(_lineIndex, 0, script.Lines.Count - 1);
                DialogueLine line = script.Lines[_lineIndex];

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("第 " + (_lineIndex + 1) + " 句", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                line.Speaker = EditorGUILayout.TextField("说话人", line.Speaker);
                line.LeftDimmed = EditorGUILayout.ToggleLeft("左暗", line.LeftDimmed, GUILayout.Width(60f));
                line.RightDimmed = EditorGUILayout.ToggleLeft("右暗", line.RightDimmed, GUILayout.Width(60f));
                EditorGUILayout.EndHorizontal();

                line.LeftArtKey = DrawPortraitField("左侧立绘", line.LeftArtKey);
                line.RightArtKey = DrawPortraitField("右侧立绘", line.RightArtKey);

                EditorGUILayout.LabelField("台词");
                line.Text = EditorGUILayout.TextArea(line.Text ?? string.Empty, GUILayout.MinHeight(78f));

                if (GUI.changed)
                {
                    _dirty = true;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static string DrawPortraitField(string label, string current)
        {
            int index = System.Array.IndexOf(PortraitOptions, current ?? string.Empty);
            if (index < 0)
            {
                index = 0;
            }

            int picked = EditorGUILayout.Popup(label, index, PortraitOptions);
            return PortraitOptions[picked];
        }

        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330f));
            EditorGUILayout.LabelField("预览（当前句）", EditorStyles.boldLabel);

            DialogueScript script = _library.scripts[_scriptIndex];
            if (script.Lines == null || script.Lines.Count == 0)
            {
                EditorGUILayout.HelpBox("这一段还没有台词。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            DialogueLine line = script.Lines[Mathf.Clamp(_lineIndex, 0, script.Lines.Count - 1)];

            Rect area = GUILayoutUtility.GetRect(320f, 300f, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, new Color(0.06f, 0.05f, 0.04f));

            DrawPortrait(area, line.LeftArtKey, new Rect(0f, 0f, 0.48f, 1f), line.LeftDimmed);
            DrawPortrait(area, line.RightArtKey, new Rect(0.52f, 0f, 0.48f, 1f), line.RightDimmed);

            Rect box = new Rect(area.x + 10f, area.yMax - 96f, area.width - 20f, 86f);
            EditorGUI.DrawRect(box, new Color(0.86f, 0.83f, 0.75f));

            var speakerStyle = new GUIStyle(EditorStyles.boldLabel);
            speakerStyle.normal.textColor = new Color(0.16f, 0.13f, 0.1f);
            speakerStyle.fontSize = 12;
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, box.width - 20f, 18f), line.Speaker ?? "", speakerStyle);

            var textStyle = new GUIStyle(EditorStyles.label);
            textStyle.normal.textColor = new Color(0.16f, 0.13f, 0.1f);
            textStyle.wordWrap = true;
            GUI.Label(new Rect(box.x + 10f, box.y + 24f, box.width - 20f, 56f), line.Text ?? "", textStyle);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("提示", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "保存后直接进 Play 就能看到新台词（运行时优先读 JSON）。\n" +
                "xlsx 是策划的原始表；如果策划改了表，重新生成会覆盖这里的改动，两边不要同时改。",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private static void DrawPortrait(Rect area, string artKey, Rect slot, bool dimmed)
        {
            if (string.IsNullOrEmpty(artKey))
            {
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/" + artKey + ".png");
            if (sprite == null)
            {
                return;
            }

            Rect target = new Rect(
                area.x + area.width * slot.x + 6f,
                area.y + 12f,
                area.width * slot.width - 12f,
                area.height - 100f);

            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            float height = target.width / Mathf.Max(0.05f, aspect);
            target = new Rect(target.x, target.yMax - height, target.width, height);

            Color previous = GUI.color;
            GUI.color = dimmed ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            GUI.DrawTexture(target, sprite.texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        private static void Swap(List<DialogueLine> list, int a, int b)
        {
            DialogueLine temp = list[a];
            list[a] = list[b];
            list[b] = temp;
        }
    }
}
