using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FocusTest.EditorTools
{
    /// <summary>
    /// 占位美术的导入设置 + ScriptableObject（卡牌 / 线索 / 问题）的创建与更新。
    /// 贴图按 1 像素 = 1 单位 / 100 PPU 导入；卡面是 450×600 的贴图，场景里按 270×360（3/5）显示。
    /// </summary>
    public static class FocusLabAssetBuilder
    {
        public const string ArtRoot = "Assets/Art/Test";
        public const string DataRoot = "Assets/Data";
        public const string ClueRoot = DataRoot + "/Clues";
        public const string QuestionRoot = DataRoot + "/Questions";

        public const string BackgroundFile = "bg_lab.png";
        public const string CardBackFile = "card_back.png";
        public const string OutlineFile = "card_outline.png";
        public const string PortraitFile = "测试立绘.jpg";

        // ---- 笔记本占位美术 ----
        public const string NotebookBookFile = "notebook_book.png";
        public const string NotebookPagesFile = "notebook_pages.png";
        public const string NotebookBoardFile = "notebook_board.png";
        public const string NotebookTicketFile = "notebook_ticket.png";
        public const string NotebookSlipFile = "notebook_slip.png";
        public const string NotebookSlipEmptyFile = "notebook_slip_empty.png";
        public const string NotebookItemFile = "notebook_item.png";
        public const string NotebookPinFile = "notebook_pin.png";
        public const string NotebookTapeFile = "notebook_tape.png";

        public static readonly string[] ArtFiles =
        {
            BackgroundFile,
            CardBackFile,
            OutlineFile,
            PortraitFile,
            "card_front_01.png",
            "card_front_02.png",
            "card_front_03.png",
            "card_front_04.png",
            "card_front_05.png",
            "card_front_06.png",
        };

        /// <summary>笔记本用图：按九宫格切边，用于拉伸的纸条/板/纸片。</summary>
        public static readonly string[] NotebookArtFiles =
        {
            NotebookBookFile,
            NotebookPagesFile,
            NotebookBoardFile,
            NotebookTicketFile,
            NotebookSlipFile,
            NotebookSlipEmptyFile,
            NotebookItemFile,
            NotebookPinFile,
            NotebookTapeFile,
        };

        /// <summary>卡牌定义：人物 2 张、地点 1 张、物品 3 张。</summary>
        private static readonly (string Id, string DisplayName, string Description, FocusCardCategory Category, string FrontFile, Color PortraitTint)[] Definitions =
        {
            (
                "focus_01",
                "青环卡",
                "更夫阿岚只在钟楼熄灯之后出现，青色环纹是他提灯的记号。",
                FocusCardCategory.Character,
                "card_front_01.png",
                new Color(0.55f, 0.86f, 0.82f, 1f)),
            (
                "focus_02",
                "赤菱卡",
                "钟楼底层的环形石室，地上刻着一枚赤色菱形，边角被脚步磨得发亮。",
                FocusCardCategory.Location,
                "card_front_02.png",
                Color.white),
            (
                "focus_03",
                "紫锥卡",
                "一枚尖端朝上的紫铜钥匙，齿槽磨成了三角形——能打开钟楼顶层的门。",
                FocusCardCategory.Item,
                "card_front_03.png",
                Color.white),
            (
                "focus_04",
                "澄弧卡",
                "半截弯月形的铜片，边缘有被火熏黑的痕迹，像是从某个框子上掰下来的。",
                FocusCardCategory.Item,
                "card_front_04.png",
                Color.white),
            (
                "focus_05",
                "方寸卡",
                "巴掌大的黄铜方牌，正面刻着棋盘格，背面是一串磨平的数字。",
                FocusCardCategory.Item,
                "card_front_05.png",
                Color.white),
            (
                "focus_06",
                "银瞳卡",
                "钟表匠的学徒，右眼是磨出来的银瞳，能看见钟摆停下的那一刻。",
                FocusCardCategory.Character,
                "card_front_06.png",
                new Color(0.68f, 0.78f, 0.92f, 1f)),
        };

        /// <summary>把占位 PNG 全部设成 UI 用的 Sprite。</summary>
        public static void ConfigureImports()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (string file in ArtFiles)
            {
                ConfigureImport(file, fullRect: true, compress: false);
            }

            // 笔记本的纸条/纸页是按九宫格拉伸的，要切 border；美术本身是干净的纯色块，压成 HQ 也不花。
            foreach (string file in NotebookArtFiles)
            {
                ConfigureImport(file, fullRect: false, compress: true);
            }
        }

        private static void ConfigureImport(string file, bool fullRect, bool compress)
        {
            string path = ArtRoot + "/" + file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("缺少占位美术，先跑生成脚本：" + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.textureCompression = compress ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);

            Vector4 border = BorderFor(file);
            if (border != Vector4.zero)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                float width = sprite != null ? sprite.rect.width : 256f;
                float height = sprite != null ? sprite.rect.height : 256f;

                importer.spritesheet = new[]
                {
                    new SpriteMetaData
                    {
                        name = Path.GetFileNameWithoutExtension(file),
                        rect = new Rect(0f, 0f, width, height),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        border = border,
                    },
                };
            }

            importer.SaveAndReimport();
        }

        /// <summary>九宫格边界（左、下、右、上），按占位图的圆角与描边给。</summary>
        private static Vector4 BorderFor(string file)
        {
            switch (file)
            {
                case NotebookSlipFile:
                    return new Vector4(26f, 26f, 26f, 26f);
                case NotebookSlipEmptyFile:
                    return new Vector4(20f, 20f, 20f, 20f);
                case NotebookTicketFile:
                    return new Vector4(24f, 24f, 24f, 24f);
                case NotebookBoardFile:
                    return new Vector4(30f, 30f, 30f, 30f);
                default:
                    return Vector4.zero;
            }
        }

        /// <summary>建/更新 Assets/Data 下的卡牌数据（人物卡同时灌入立绘与区分色）。</summary>
        public static List<FocusCardData> CreateCards()
        {
            EnsureFolder(DataRoot);

            Sprite back = LoadSprite(CardBackFile);
            Sprite portrait = LoadSprite(PortraitFile);
            var cards = new List<FocusCardData>();

            for (int i = 0; i < Definitions.Length; i++)
            {
                (string id, string displayName, string description, FocusCardCategory category, string frontFile, Color tint) = Definitions[i];
                string assetPath = DataRoot + "/focus_card_" + (i + 1).ToString("00") + ".asset";

                FocusCardData card = AssetDatabase.LoadAssetAtPath<FocusCardData>(assetPath);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<FocusCardData>();
                    AssetDatabase.CreateAsset(card, assetPath);
                }

                // 只有人物卡配立绘，其余留空。
                Sprite cardPortrait = FocusCardCategories.IsCharacter(category) ? portrait : null;
                card.Configure(id, displayName, description, category, LoadSprite(frontFile), back, cardPortrait, tint);
                EditorUtility.SetDirty(card);
                cards.Add(card);
            }

            AssetDatabase.SaveAssets();
            return cards;
        }

        /// <summary>线索表：15 条，按来源卡分组后就是「青环卡 / 赤菱卡 / 紫锥卡 / …」各一组。文本即条目上的短语。</summary>
        private static readonly (string Id, string Text, string Detail, int CardIndex, FocusClueKind Kind)[] ClueDefinitions =
        {
            ("clue_clock_lamp", "熄灯后提灯的人影", "钟楼熄灯之后，提灯的身影从石室口拐出去，灯罩上有一圈青色的环纹。", 0, FocusClueKind.Person),
            ("clue_clock_ring", "袖口的青色环纹", "阿岚的右袖口绣着一圈青环，收口处缝过两针——是被人剪过又缝回去的。", 0, FocusClueKind.Person),
            ("clue_clock_smell", "身上有烛油味", "靠近闻得到一股陈烛油味，像是在很多蜡烛之间待了很久。", 0, FocusClueKind.Person),
            ("clue_room_rune", "地刻赤菱纹", "石室正中刻着一枚赤色菱形，四角各有一道磨痕，像被反复踩过。", 1, FocusClueKind.Place),
            ("clue_room_blood", "菱形边上洗不掉的渍", "菱形左下角有一块深色渍，抹布擦过后仍透出一点褐色。", 1, FocusClueKind.Place),
            ("clue_room_draft", "石缝里的冷风", "靠墙第三块石砖的缝在漏风，风从地底来，比石室本身冷得多。", 1, FocusClueKind.Place),
            ("clue_key_teeth", "三角齿槽的紫铜钥匙", "齿槽磨成了三角形，斜着插进锁孔一次就开。", 2, FocusClueKind.Thing),
            ("clue_key_warm", "钥匙还是温的", "铜身带着体温，说明它离开人手不久。", 2, FocusClueKind.Thing),
            ("clue_key_mark", "钥匙柄上的刻痕", "柄上有一道新刻的竖痕，深浅不匀，像是用指甲抠出来的。", 2, FocusClueKind.Thing),
            ("clue_moon_burn", "熏黑的弯月铜片", "半截弯月形铜片，边缘有火熏的痕迹，弧度对得上钟摆的摇杆。", 3, FocusClueKind.Thing),
            ("clue_moon_half", "只剩半截的弧度", "断口是齐的，另一半应该还在别处——不像自然折断。", 3, FocusClueKind.Thing),
            ("clue_plate_grid", "棋盘格方牌", "巴掌大的黄铜方牌，正面刻着棋盘格，背面那串数字被磨平了。", 4, FocusClueKind.Thing),
            ("clue_plate_weight", "压在案卷上的方牌", "方牌的重量和钟楼那串钥匙相仿，搁在纸上会把纸压出一道印。", 4, FocusClueKind.Thing),
            ("clue_silver_eye", "磨出来的银瞳", "银瞳卡的右眼不是天生的，是磨出来的——看久了会反光。", 5, FocusClueKind.Person),
            ("clue_silver_witness", "「我看见钟摆停了」", "银瞳说自己看见钟摆停下的那一刻，可钟楼的钟当天本来就慢了半刻。", 5, FocusClueKind.Statement),
        };

        /// <summary>案件板的五个空槽（案件 / 凶手 / 凶器 / 物证 / 人证）。判定这期不做，Accepted 只做登记。</summary>
        private static readonly (string Id, string Title, string Hint, string Example, string[] Accepted)[] QuestionDefinitions =
        {
            ("case", "案件", "先写下这是什么案子。", "钟楼顶层的那起失窃", new[] { "clue_room_rune", "clue_key_teeth" }),
            ("culprit", "凶手", "谁动了手？", "熄灯后进钟楼的人", new[] { "clue_clock_lamp", "clue_silver_eye" }),
            ("weapon", "凶器", "用什么下的手？", "能打开顶层门的东西", new[] { "clue_key_teeth", "clue_key_warm" }),
            ("evidence", "物证", "手上有什么能拿出来的东西。", "从框子上掰下来的那半截", new[] { "clue_moon_burn", "clue_moon_half", "clue_plate_grid" }),
            ("testimony", "人证", "谁的说法能对上？", "看见钟摆停下的那个人", new[] { "clue_silver_witness", "clue_clock_ring" }),
        };

        public static Sprite LoadSprite(string fileName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + fileName);
            if (sprite == null)
            {
                throw new FileNotFoundException("美术没有导入成 Sprite：" + ArtRoot + "/" + fileName);
            }

            return sprite;
        }

        /// <summary>创建/更新 Assets/Data/Clues 与 Assets/Data/Questions 下的线索与问题；返回按源卡分组的线索表。</summary>
        public static NotebookDefinitions CreateNotebook(List<FocusCardData> cards)
        {
            EnsureFolder(DataRoot);
            EnsureFolder(ClueRoot);
            EnsureFolder(QuestionRoot);

            var clues = new List<FocusClueData>();
            for (int i = 0; i < ClueDefinitions.Length; i++)
            {
                (string id, string text, string detail, int cardIndex, FocusClueKind kind) = ClueDefinitions[i];
                FocusCardData source = cards != null && cardIndex < cards.Count ? cards[cardIndex] : null;
                string path = ClueRoot + "/" + id + ".asset";

                FocusClueData clue = AssetDatabase.LoadAssetAtPath<FocusClueData>(path);
                if (clue == null)
                {
                    clue = ScriptableObject.CreateInstance<FocusClueData>();
                    AssetDatabase.CreateAsset(clue, path);
                }

                clue.Configure(id, source, text, detail, kind);
                EditorUtility.SetDirty(clue);
                clues.Add(clue);
            }

            var questions = new List<FocusQuestionData>();
            for (int i = 0; i < QuestionDefinitions.Length; i++)
            {
                (string id, string title, string hint, string example, string[] clueIds) = QuestionDefinitions[i];
                string path = QuestionRoot + "/" + id + ".asset";

                FocusQuestionData question = AssetDatabase.LoadAssetAtPath<FocusQuestionData>(path);
                if (question == null)
                {
                    question = ScriptableObject.CreateInstance<FocusQuestionData>();
                    AssetDatabase.CreateAsset(question, path);
                }

                var accepted = new List<FocusClueData>();
                for (int k = 0; k < clueIds.Length; k++)
                {
                    FocusClueData clue = clues.Find(c => c.ClueId == clueIds[k]);
                    if (clue != null)
                    {
                        accepted.Add(clue);
                    }
                }

                question.Configure(id, title, hint, example, accepted);
                EditorUtility.SetDirty(question);
                questions.Add(question);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 新建的 ScriptableObject 在保存时会写 m_Script: {fileID: 0}，重新导入一次才会绑上脚本；
            // 否则读回来是 null、按类型也搜不到，场景里就会丢掉这些引用。
            for (int i = 0; i < clues.Count; i++)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(clues[i]), ImportAssetOptions.ForceSynchronousImport);
            }

            for (int i = 0; i < questions.Count; i++)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(questions[i]), ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.SaveAssets();
            return new NotebookDefinitions(clues, questions);
        }

        /// <summary>笔记本的线索表 + 问题表。</summary>
        public readonly struct NotebookDefinitions
        {
            public readonly List<FocusClueData> Clues;
            public readonly List<FocusQuestionData> Questions;

            public NotebookDefinitions(List<FocusClueData> clues, List<FocusQuestionData> questions)
            {
                Clues = clues;
                Questions = questions;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
