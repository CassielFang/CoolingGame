using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 把 Assets/Art/Test 里的图片扫进美术表，剧本播放时就能按名字找图。
    /// 立绘名表（Portraits）是人工维护的绑定，这里只在它空着时补两条示例。
    /// </summary>
    public static class FocusArtIndexer
    {
        public const string LibraryPath = "Assets/Data/FocusArtLibrary.asset";
        public const string ArtRoot = "Assets/Art/Test";
        public const string SamplePortraitFile = "测试立绘.jpg";

        /// <summary>空表时写入的示例绑定：立绘名 → 图片文件名。</summary>
        private static readonly (string Name, string File)[] SamplePortraits =
        {
            ("阿岚", "测试立绘.jpg"),
            ("银瞳", "测试立绘.jpg"),
        };

        [MenuItem("Tools/视觉小说/刷新美术索引", false, 23)]
        public static void RefreshFromMenu()
        {
            int count = Refresh();
            EditorUtility.DisplayDialog(
                "美术索引",
                "已刷新 " + ArtRoot + "：收录 " + count + " 张图。\n\n"
                + "立绘名表在「视觉小说主功能窗 → 立绘名」里维护。",
                "好");
        }

        /// <summary>新建或更新 Assets/Data/FocusArtLibrary.asset，返回它。</summary>
        public static FocusArtLibrary EnsureLibrary()
        {
            FocusArtLibrary library = AssetDatabase.LoadAssetAtPath<FocusArtLibrary>(LibraryPath);
            if (library != null)
            {
                return library;
            }

            EnsureFolder("Assets/Data");
            library = ScriptableObject.CreateInstance<FocusArtLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>扫描美术目录，重建 Sprites 表；返回收录数量。</summary>
        public static int Refresh()
        {
            FocusArtLibrary library = EnsureLibrary();

            var entries = new List<FocusSpriteEntry>();
            if (AssetDatabase.IsValidFolder(ArtRoot))
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
                var sorted = new List<string>(guids);
                sorted.Sort((a, b) =>
                    string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), StringComparison.Ordinal));

                for (int i = 0; i < sorted.Count; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(sorted[i]);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        continue;
                    }

                    entries.Add(new FocusSpriteEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        Sprite = sprite,
                    });
                }
            }

            library.SetSprites(entries);
            SeedSamplePortraits(library);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return entries.Count;
        }

        /// <summary>立绘名表空着时补两条示例绑定，让人一打开就能看到立绘。</summary>
        public static void SeedSamplePortraits(FocusArtLibrary library)
        {
            if (library == null || library.Portraits.Count > 0)
            {
                return;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + SamplePortraitFile);
            if (sprite == null)
            {
                return;
            }

            for (int i = 0; i < SamplePortraits.Length; i++)
            {
                FocusPortraitEntry entry = library.AddPortrait(SamplePortraits[i].Name);
                entry.Sprite = sprite;
            }
        }

        /// <summary>把剧本里用到、但立绘名表里还没有的名字补成空条目，方便逐个绑图。</summary>
        public static int RegisterUnboundNames(FocusArtLibrary library, FocusVisualNovelLibrary novels)
        {
            if (library == null || novels == null)
            {
                return 0;
            }

            var names = new List<string>();
            for (int i = 0; i < novels.Count; i++)
            {
                FocusVisualNovel novel = novels.Novels[i];
                if (novel != null)
                {
                    novel.CollectPortraitNames(names);
                }
            }

            int added = 0;
            for (int i = 0; i < names.Count; i++)
            {
                string name = FocusArtLibrary.SplitNote(names[i], out _);
                if (string.IsNullOrEmpty(name) || FocusArtLibrary.IsClearToken(name) || library.HasPortraitEntry(name))
                {
                    continue;
                }

                library.AddPortrait(name);
                added++;
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            return added;
        }

        public static void RefreshFromCommandLine()
        {
            int count = Refresh();
            FocusArtLibrary library = EnsureLibrary();
            Debug.Log("[FocusLab] VN ART RESULT=PASS sprites=" + count + " portraits=" + library.Portraits.Count);
            EditorApplication.Exit(0);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
