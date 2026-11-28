using System.IO;
using CoolingGame.Presentation.ArtTuning;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools.ArtTuning
{
    /// <summary>
    /// 参数资产与立绘素材的编辑器侧帮手：找到/创建 Resources/ArtTuningSettings.asset，
    /// 保存「已保存」那份（攒一会再写盘，拖控件时不至于每帧写磁盘）。
    /// </summary>
    internal static class ArtTuningAssets
    {
        public const string SettingsFolder = "Assets/Resources";
        public const string SettingsAssetPath = "Assets/Resources/ArtTuningSettings.asset";

        /// <summary>窗口里点过一次「保存」之后，才在窗口关闭时顺手写盘。</summary>
        private const double SaveDelay = 0.5;

        private static bool _savePending;
        private static double _saveAt;

        /// <summary>参数资产（就是「已保存」的那份）；没有就建一个（并选中）。</summary>
        public static ArtTuningSettings LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ArtTuningSettings>(SettingsAssetPath);
            if (asset != null)
            {
                asset.EnsureDefaults();
                return asset;
            }

            // 资产路径上可能有 .asset 但类型对不上（残骸）：先删掉再建
            if (File.Exists(SettingsAssetPath) || AssetDatabase.LoadMainAssetAtPath(SettingsAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(SettingsAssetPath);
            }

            return CreateAsset();
        }

        public static ArtTuningSettings CreateAsset()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var asset = ScriptableObject.CreateInstance<ArtTuningSettings>();
            asset.EnsureDefaults();
            AssetDatabase.CreateAsset(asset, SettingsAssetPath);
            AssetDatabase.SaveAssets();

            // 运行时那份缓存要丢掉，否则还在用「找不到资产」时建的临时实例
            ArtTuningSettings.Forget();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[美术调试] 已创建参数资产：" + SettingsAssetPath);
            return asset;
        }

        /// <summary>「保存」：把工作副本写进资产并落盘。</summary>
        public static void SaveWorkingToAsset()
        {
            ArtTuningStore.SaveWorking();
            ArtTuningSettings asset = LoadOrCreate();
            EditorUtility.SetDirty(asset);
            _savePending = true;
            _saveAt = EditorApplication.timeSinceStartup + SaveDelay;
            AssetDatabase.SaveAssets();
        }

        /// <summary>「读取」：把资产抄回工作副本，丢掉未保存的改动。</summary>
        public static void LoadAssetToWorking()
        {
            LoadOrCreate();
            ArtTuningStore.LoadSaved();
        }

        public static void TickSave()
        {
            if (!_savePending || EditorApplication.timeSinceStartup < _saveAt)
            {
                return;
            }

            _savePending = false;
            AssetDatabase.SaveAssets();
        }

        public static void SaveNow()
        {
            ArtTuningSettings asset = AssetDatabase.LoadAssetAtPath<ArtTuningSettings>(SettingsAssetPath);
            if (asset != null && EditorUtility.IsDirty(asset))
            {
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>根据立绘 key（例如 "char/mp01_default"）取图。</summary>
        public static Sprite LoadPortrait(string artKey)
        {
            return string.IsNullOrEmpty(artKey) ? null : CoolingGame.Presentation.ArtLibrary.LoadCardArt(artKey);
        }

        /// <summary>可选的立绘 key：扫 Art/char 下所有贴图，按路径排序；第 0 项是「无立绘」。</summary>
        public static string[] PortraitKeys()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art/char" });
            var keys = new string[guids.Length + 1];
            keys[0] = string.Empty;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string key = path.Replace("Assets/Resources/Art/", string.Empty);
                key = Path.ChangeExtension(key, null);
                keys[i + 1] = key;
            }

            if (keys.Length > 1)
            {
                System.Array.Sort(keys, 1, keys.Length - 1, System.StringComparer.Ordinal);
            }

            return keys;
        }
    }
}
