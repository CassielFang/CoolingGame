using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 把 Assets/Resources/Art 下的交付图批量设成 Sprite，并给九宫格部件写 border。
    /// 菜单：CoolingGame/Apply Art Import Settings。
    ///
    /// 为什么需要这一步：美术交付的是普通 PNG，Unity 默认可能按 Default Texture 导入，
    /// UGUI 的 Image 拿不到 Sprite；而对话框/操作框这类带装饰边的长条图，不设九宫格
    /// border 就会被拉变形。
    /// </summary>
    public static class ArtImportSettings
    {
        private sealed class Entry
        {
            public string Path;
            public Vector4 Border;   // left, bottom, right, top
            public bool Uncompressed;

            /// <summary>是否允许 CPU 读像素（立绘的 alpha 点击判定要用）。</summary>
            public bool Readable;
        }

        [MenuItem("CoolingGame/Apply Art Import Settings")]
        public static void Apply()
        {
            var entries = new List<Entry>
            {
                // 桌面背景：铺满 1920x1080，不做九宫格
                new Entry { Path = "Assets/Resources/Art/desk.jpg", Border = Vector4.zero, Uncompressed = true },

                // 卡背：整图，不拉伸
                new Entry { Path = "Assets/Resources/Art/ui/card_back.png", Border = Vector4.zero, Uncompressed = true },

                // 对话框：裁掉透明边后 2131x555，两端有铆钉，中间可拉伸
                new Entry { Path = "Assets/Resources/Art/ui/dialog.png", Border = new Vector4(26f, 10f, 26f, 10f) },

                // 操作框：按实心像素裁到 2050x521，左上角红胶带，左边距要包住胶带
                new Entry { Path = "Assets/Resources/Art/ui/action_panel.png", Border = new Vector4(240f, 40f, 90f, 40f) },

                // 角色立绘：450x480，按原比例用
                new Entry { Path = "Assets/Resources/Art/char/p06_dancer.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/char/p07_salon.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/char/p01_cedric.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/char/p08_officer.png", Border = Vector4.zero, Uncompressed = true },

                // 道具卡面
                new Entry { Path = "Assets/Resources/Art/item/notebook.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/item/letter.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/item/newspaper.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/item/footprint.png", Border = Vector4.zero, Uncompressed = true },
                new Entry { Path = "Assets/Resources/Art/item/cut_cloth.png", Border = Vector4.zero, Uncompressed = true },

                // 主角莫里森：已去白底，612x1069
                new Entry { Path = "Assets/Resources/Art/char/mp01_morrison.png", Border = Vector4.zero, Uncompressed = true },

                // 雪莉：先用 SP03 女路人剪影顶替，等主美出正式立绘再替换
                new Entry { Path = "Assets/Resources/Art/char/mp02_shirley.png", Border = Vector4.zero, Uncompressed = true },

                // 正式立绘：2357x3536 带透明通道，默认/微笑/质问 三种表情
                new Entry { Path = "Assets/Resources/Art/char/mp01_default.png", Border = Vector4.zero, Uncompressed = true, Readable = true },
                new Entry { Path = "Assets/Resources/Art/char/mp01_smile.png", Border = Vector4.zero, Uncompressed = true, Readable = true },
                new Entry { Path = "Assets/Resources/Art/char/mp01_question.png", Border = Vector4.zero, Uncompressed = true, Readable = true },
                new Entry { Path = "Assets/Resources/Art/char/mp02_default.png", Border = Vector4.zero, Uncompressed = true, Readable = true },
                new Entry { Path = "Assets/Resources/Art/char/mp02_smile.png", Border = Vector4.zero, Uncompressed = true, Readable = true },
                new Entry { Path = "Assets/Resources/Art/char/mp02_question.png", Border = Vector4.zero, Uncompressed = true, Readable = true }
            };

            int applied = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                var importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[Art] 找不到导入设置，跳过：" + entry.Path);
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 4096;
                importer.textureCompression = entry.Uncompressed
                    ? TextureImporterCompression.Uncompressed
                    : TextureImporterCompression.CompressedHQ;
                importer.spriteBorder = entry.Border;
                importer.isReadable = entry.Readable;
                importer.SaveAndReimport();
                applied++;
            }

            AssetDatabase.Refresh();
            Debug.Log(string.Format("[Art] 已设置 {0} 张图的导入参数。", applied));
        }
    }
}
