using System.Collections.Generic;
using CoolingGame.Core;
using CoolingGame.Core.Samples;
using CoolingGame.Presentation;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 把代码里的第一章数据（<see cref="Chapter1Case"/>）烘成策划可编辑的资产。
    /// 菜单：CoolingGame/Build Chapter1 Case Asset。
    ///
    /// 生成之后策划可以直接在 Inspector 里改这张表；改完如果发现要回到代码版本，
    /// 再点一次菜单即可覆盖（会提示确认）。
    /// </summary>
    public static class Chapter1CaseAssetBuilder
    {
        private const string FolderPath = "Assets/Resources/Cases";
        private const string AssetPath = FolderPath + "/Chapter1Case.asset";
        private const string MenuPath = "CoolingGame/Build Chapter1 Case Asset";

        [MenuItem(MenuPath)]
        public static void BuildAsset()
        {
            Build(false);
        }

        /// <summary>不做覆盖确认的版本，给自动化/批处理用。</summary>
        [MenuItem(MenuPath + " (overwrite)", false, 1)]
        public static void BuildAssetNoPrompt()
        {
            Build(true);
        }

        private static void Build(bool overwriteWithoutPrompt)
        {
            EnsureFolder();

            CaseDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CaseDefinitionAsset>(AssetPath);
            bool created = asset == null;

            if (created)
            {
                asset = ScriptableObject.CreateInstance<CaseDefinitionAsset>();
                asset.name = "Chapter1Case";
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            else if (!overwriteWithoutPrompt && !EditorUtility.DisplayDialog(
                "覆盖第一章资产？",
                "Assets/Resources/Cases/Chapter1Case.asset 已存在，会用代码里的数据覆盖它。\n\n想保留 Inspector 里的手改，请先复制一份资产再执行。",
                "覆盖", "取消"))
            {
                return;
            }

            CaseDefinition definition = Chapter1Case.Build();
            asset.CopyFrom(definition);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log(string.Format(
                "[Chapter1] 资产已{0}：{1}\n卡牌 {2} · 线索 {3} · 配方 {4} · 问题页 {5} · 阶段 {6} · 场景 {7}",
                created ? "创建" : "刷新",
                AssetPath,
                definition.Cards.Count,
                definition.Clues.Count,
                definition.Recipes.Count,
                definition.QuestionPages.Count,
                definition.Stages.Count,
                definition.Scenes.Count));
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(FolderPath))
            {
                return;
            }

            var parts = new List<string>(FolderPath.Split('/'));
            string current = parts[0];
            for (int i = 1; i < parts.Count; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}

