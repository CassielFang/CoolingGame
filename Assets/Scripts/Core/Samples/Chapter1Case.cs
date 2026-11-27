using System.Collections.Generic;

namespace CoolingGame.Core.Samples
{
    /// <summary>
    /// 第一章「斯威克的连环杀手」的完整案件数据，按《第一章游戏元素设定集》转写。
    ///
    /// 这一版拍板的建模取舍（设定集里没写死的地方，先这么定，策划可以直接改数据）：
    ///
    /// 1. **问题页填「线索」而不是「卡牌」**。设定集里 Q01–Q05 的「答案的线索来源」全是
    ///    C 开头的线索 ID（C04、C23~C27…），所以槽位用 AcceptedClueIds；只有死者的实体
    ///    卡（P04/P05）用 AutoFilledCardId 自动填。
    /// 2. **Q03 是排除页**：仇杀/情杀 → C05、劫财 → C04、体貌特征 → C17，三格各独立判定，
    ///    填对后由表现层划掉（StrikeThroughWhenCorrect）。Q03 填满 = 第一阶段结束。
    /// 3. **Q01/Q02 是过程页**，不参与任何完成条件；玩家可以只填一部分，
    ///    第三阶段它们会被移除、换成 Q05 综合页（设定集：移除 Q01、Q02、Q04，添加 Q05）。
    /// 4. **Q04 是进度页**，按具体线索 ID（C23~C29 共 7 条）判定，不用「线索总数」，
    ///    因为笔记本跨阶段累积，按数量会在进教堂的瞬间直接满足。
    /// 5. **设定集的「两阶段」拆成三段**：警察局段 → 教堂段 → 回警局结案段。
    ///    设定集把「回警局」写在第二阶段后半，但中间有清牌和换场景，用阶段表达才干净。
    /// 6. **「前往」配方带主角卡**（MP01 莫里森 / MP02 雪莉）作为附加卡，
    ///    这就是双主角「谁去行动」的数据表达；场景切换用 OutputSceneId。
    /// 7. **Q05 的「证据」格设定集留空**，这里定为 C34（袖口白色颜料的比对结论），
    ///    因为它同时用到 I11 祭坛彩绘，是三段证据链的收口。
    /// </summary>
    public static class Chapter1Case
    {
        public const string Id = "chapter1";

        public const string Stage1 = "stage1";
        public const string Stage2 = "stage2";
        public const string Stage3 = "stage3";

        public const string PageQ01 = "Q01";
        public const string PageQ02 = "Q02";
        public const string PageQ03 = "Q03";
        public const string PageQ04 = "Q04";
        public const string PageQ05 = "Q05";

        public const string ScenePolice = "S00";
        public const string SceneStreet = "S01";
        public const string SceneClub = "S02";
        public const string SceneSalon = "S03";
        public const string SceneChurch = "S04";

        public const string Morrison = "MP01";
        public const string Shirley = "MP02";

        public static CaseDefinition Build()
        {
            var definition = new CaseDefinition
            {
                Id = Id,
                Title = "第一章 · 斯威克的连环杀手",
                Summary = "两起夜路命案、一间教堂、一双握刀的手。斯威克东区警长莫里森，和来收集小说的雪莉。",
                TimeBudget = -1,
                StartSceneId = ScenePolice,
                StartingDetectiveId = Morrison,
                FinalQuestionPageId = PageQ05,
                ClosingStoryNodeId = "story_case_closed",
                OpeningDialogueId = "VN01",
                ClosingDialogueId = "VN03"
            };

            AddScenes(definition);
            AddDetectives(definition);
            AddCards(definition);
            AddClues(definition);
            AddStoryNodes(definition);
            Chapter1Dialogue.AddDialogues(definition);
            Chapter1Newspapers.AddNewspapers(definition);
            AddRecipes(definition);
            AddQuestionPages(definition);
            AddStages(definition);

            return definition;
        }

        // ── 场景 ────────────────────────────────────────────────────────────

        private static void AddScenes(CaseDefinition definition)
        {
            definition.Scenes.AddRange(new[]
            {
                new SceneDefinition
                {
                    Id = ScenePolice,
                    DisplayName = "东区警察局",
                    Description = "推理的主场所：案件档案、白板、报纸。"
                },
                new SceneDefinition
                {
                    Id = SceneStreet,
                    DisplayName = "砖瓦巷街道",
                    Description = "两起命案的现场：破旧、街边小楼挡月光、电气灯。"
                },
                new SceneDefinition
                {
                    Id = SceneClub,
                    DisplayName = "私人俱乐部",
                    Description = "上流人士聚会场所，第一名受害者生前在此饮酒。"
                },
                new SceneDefinition
                {
                    Id = SceneSalon,
                    DisplayName = "青年读书沙龙",
                    Description = "年轻人的读书会，第二名受害者的日常去处。"
                },
                new SceneDefinition
                {
                    Id = SceneChurch,
                    DisplayName = "东区教堂",
                    Description = "哈罗德神父的教堂，画师塞德里克在这里画祭坛彩绘。"
                }
            });
        }

        // ── 办案人（主角，不是卡牌） ──────────────────────────────────────

        private static void AddDetectives(CaseDefinition definition)
        {
            definition.Detectives.AddRange(new[]
            {
                new DetectiveDefinition
                {
                    Id = Morrison,
                    DisplayName = "莫里森警长",
                    PortraitArtKey = "char/mp01_default",
                    PortraitSmileKey = "char/mp01_smile",
                    PortraitQuestionKey = "char/mp01_question",
                    Description = "东区警局警长。强硬、务实、有点急躁；擅长现场和审讯。"
                },
                new DetectiveDefinition
                {
                    Id = Shirley,
                    DisplayName = "雪莉",
                    // 暂用 SP03 女路人剪影顶替，正式立绘到位后换掉这一行
                    PortraitArtKey = "char/mp02_default",
                    PortraitSmileKey = "char/mp02_smile",
                    PortraitQuestionKey = "char/mp02_question",
                    Description = "写小说的外行人。观察细、擅长比对文字与证词；老警长推荐她来协助。"
                }
            });
        }

        // ── 卡牌 ────────────────────────────────────────────────────────────

        private static void AddCards(CaseDefinition definition)
        {
            definition.Cards.AddRange(new[]
            {
                // 地点卡（可「前往」的地方）
                new CardDefinition(ScenePolice, "警察局", CardCategory.Location, "location", "scene") { Description = "斯威克东区警局。莫里森的办公室，窗外的砖瓦巷就在两条街外。" },
                new CardDefinition(SceneStreet, "砖瓦巷街道", CardCategory.Location, "location", "scene") { Description = "砖瓦巷街道。两具尸体被发现的地方，夜里几乎没人经过。" },
                new CardDefinition(SceneClub, "私人俱乐部", CardCategory.Location, "location", "scene") { Description = "私人俱乐部。上流人士的夜间去处，音乐、酒，和不愿意多说的舞女。" },
                new CardDefinition(SceneSalon, "青年读书沙龙", CardCategory.Location, "location", "scene") { Description = "青年读书沙龙。读书、分享、争论，约翰·卡林顿在这里被叫作“讲书人”。" },
                new CardDefinition(SceneChurch, "东区教堂", CardCategory.Location, "location", "scene") { Description = "东区教堂。哈罗德神父主持仪式，祭坛的彩绘由一个年轻画师完成。" },

                // 物品/文件
                new CardDefinition("I00", "案件档案", CardCategory.Item, "item", "file") { Description = "两起命案的卷宗。东区警局一个月来的全部材料，被莫里森翻得起了毛边。" },
                new CardDefinition("I01", "死者文件一", CardCategory.Item, "item", "file") { Description = "第一名死者：珠宝店店主。口袋里有俱乐部的酒单，衣物完好。" },
                new CardDefinition("I02", "死者文件二", CardCategory.Item, "item", "file") { Description = "第二名死者：议员卡林顿之子。死状与第一起高度相似。" },
                new CardDefinition("I03", "现场记录一", CardCategory.Item, "item", "file") { Description = "现场记录：砖瓦巷。尸体、血迹、被雨水冲淡的车辙。" },
                new CardDefinition("I04", "问话记录一", CardCategory.Item, "item", "file") { Description = "问话记录：俱乐部。舞女记得他喝了不少，坚持要走路回去醒酒。" },
                new CardDefinition("I05", "尸检报告一", CardCategory.Item, "item", "file") { Description = "尸检报告：第一例。胸腹锐器创八处，头部还有钝器伤。" },
                new CardDefinition("I06", "现场记录二", CardCategory.Item, "item", "file") { Description = "现场记录：砖瓦巷（第二起）。凶手这次留下了一个残缺的脚印。" },
                new CardDefinition("I07", "问话记录二", CardCategory.Item, "item", "file") { Description = "问话记录：沙龙。他读完了书，和大家聊了几句，然后一个人走了。" },
                new CardDefinition("I08", "尸检报告二", CardCategory.Item, "item", "file") { Description = "尸检报告：第二例。创口形态与第一例相符，可能是同一把刀。" },
                new CardDefinition("I09", "血脚印拓印", CardCategory.Item, "item", "evidence") { ArtKey = "item/footprint", Description = "血脚印拓印。中等身高、体重偏轻，鞋底花纹普通。" },
                new CardDefinition("I10", "染血的衣服？", CardCategory.Item, "item", "doubt") { Description = "染血的衣服？雪莉注意到塞德里克只有一套衣服，却始终干净。" },
                new CardDefinition("I11", "祭坛彩绘", CardCategory.Item, "item", "evidence") { Description = "祭坛彩绘。画的是圣像，颜料是教会专属的白色。" },
                new CardDefinition("I12", "画师的短刀", CardCategory.Item, "item", "weapon") { ArtKey = "item/cut_cloth", Description = "画师的短刀。细长、适合裁布，也适合捅进人的胸口。" },
                new CardDefinition("I13", "染血的衣服", CardCategory.Item, "item", "evidence") { Description = "染血的衣服。从阁楼的包袱里翻出来的，血洗过，但没洗净。" },

                // 人物
                new CardDefinition("P01", "塞德里克", CardCategory.Person, "person", "suspect") { ArtKey = "char/p01_cedric", Description = "塞德里克。教堂的画师，昼夜颠倒，沉默寡言，手很稳。" },
                new CardDefinition("P02", "哈罗德神父", CardCategory.Person, "person") { Description = "哈罗德神父。莫里森受洗时的神父，给了他一个工作，也给了他一套说辞。" },
                new CardDefinition("P03", "老警长亨利", CardCategory.Person, "person") { Description = "老警长亨利。把位置交给莫里森之后就去度假了，信是从游轮上寄来的。" },
                new CardDefinition("P04", "珠宝店店主", CardCategory.Person, "person", "victim") { Description = "珠宝店店主。第一名死者，为人和善，从不与人结仇。" },
                new CardDefinition("P05", "约翰·卡林顿", CardCategory.Person, "person", "victim") { Description = "约翰·卡林顿。议员之子，与父亲不和，住在公寓里，没人知道他的身份。" },
                new CardDefinition("P06", "舞女", CardCategory.Person, "person", "witness") { ArtKey = "char/p06_dancer", Description = "舞女。俱乐部里最后一个见过第一名死者的人。" },
                new CardDefinition("P07", "沙龙女主人", CardCategory.Person, "person", "witness") { ArtKey = "char/p07_salon", Description = "沙龙女主人。读书沙龙的主人，约翰常来这里讲书。" },
                new CardDefinition("P08", "监牢中的塞德里克", CardCategory.Person, "person", "suspect") { ArtKey = "char/p08_officer", Description = "监牢中的塞德里克。审讯室里只有他和莫里森，还有一扇关着的门。" }
            });
        }

        // ── 线索 ────────────────────────────────────────────────────────────

        private static void AddClues(CaseDefinition definition)
        {
            definition.Clues.AddRange(new[]
            {
                Clue("C01", "尸体仰面倒在血泊里", "尸体", "血泊"),
                Clue("C02", "尸体上有一股酒气", "酒气", "醉酒"),
                Clue("C03", "尸体上没有反抗痕迹", "反抗", "偷袭"),
                Clue("C04", "死者是珠宝店店主，并无财物丢失", "珠宝店", "财物", "劫财"),
                Clue("C05", "死者虔诚而和善，没有与人结仇", "虔诚", "结仇", "仇杀"),
                Clue("C06", "死者当晚去了私人俱乐部", "俱乐部", "当晚"),
                Clue("C07", "尸体头上有重击痕迹", "头部", "重击"),
                Clue("C08", "胸前和腹部中了八刀，排列混乱，胸口刀伤致命", "八刀", "致命", "刀"),
                Clue("C09", "尸体以扭曲的姿势倒在血泊里", "姿势", "纠缠"),
                Clue("C10", "衣服上混乱的血迹表明死者与凶手发生了纠缠", "血迹", "纠缠"),
                Clue("C11", "尸体附近有几个残缺的血脚印", "血脚印", "脚印"),
                Clue("C12", "死者是卡林顿议员的长子，并无财物丢失", "议员", "卡林顿", "财物"),
                Clue("C13", "死者生活低调，朋友基本不知道他的身份", "低调", "身份"),
                Clue("C14", "死者常前往教堂、济贫院和一家读书沙龙", "教堂", "济贫院", "沙龙"),
                Clue("C15", "死者头上有重击痕迹，手上也有被刀割伤的痕迹", "重击", "割伤"),
                Clue("C16", "胸前中了四刀为致命伤，应与第一死者是同一把刀", "四刀", "同一把刀"),
                Clue("C17", "脚印状态和分布推测凶手身高中等、体重偏轻", "身高", "体重", "体貌"),
                Clue("C18", "死者当晚喝了不少酒", "喝酒", "当晚"),
                Clue("C19", "死者欣赏了舞蹈", "舞蹈", "舞女"),
                Clue("C20", "沙龙女主人对警方有些敌意", "敌意", "沙龙女主人"),
                Clue("C21", "死者只是俱乐部的普通成员", "普通成员", "俱乐部"),
                Clue("C22", "沙龙主要阅读一些文学作品", "文学", "沙龙"),
                Clue("C23", "塞德里克是东区教堂的画师", "塞德里克", "画师", "教堂"),
                Clue("C24", "塞德里克自称案发当夜在教堂绘制彩绘", "当夜", "彩绘", "不在场"),
                Clue("C25", "塞德里克声称自己只有身上的衣服", "只有一套衣服", "衣服"),
                Clue("C26", "塞德里克是哈罗德神父亲自招募的", "神父", "招募"),
                Clue("C27", "塞德里克很有绘画天赋，教堂工作帮他维持生计", "天赋", "生计"),
                Clue("C28", "塞德里克的东西不多，都是绘画用的：笔刷、铲刀、短刀", "短刀", "铲刀", "工具"),
                Clue("C29", "哈罗德证明塞德里克没有多余的备用衣服", "备用衣服", "血衣"),
                Clue("C30", "教堂的彩绘用的是教堂专属的颜料", "颜料", "专属"),
                Clue("C31", "塞德里克拒绝承认自己是凶手", "拒绝", "否认"),
                Clue("C32", "莫里森发现尸体袖子上蹭着一片白色痕迹，像舞女的化妆品", "白色痕迹", "化妆品"),
                Clue("C33", "雪莉觉得袖子上那片白色痕迹的颜色有些眼熟", "眼熟", "白色痕迹"),
                Clue("C34", "雪莉比照祭坛彩绘：袖子上的白色就是教堂专属颜料", "颜料", "白色", "祭坛彩绘")
            });
        }

        // ── 情节节点 ────────────────────────────────────────────────────────

        private static void AddStoryNodes(CaseDefinition definition)
        {
            definition.StoryNodes.AddRange(new[]
            {
                new StoryNodeDefinition
                {
                    Id = "story_stage2_start",
                    Title = "第一阶段结束：排查对象",
                    Body = "「随即杀人？疯子？无政府主义者？」\n\n排查对象：中等身高，体重稍轻；外国人；从事体力劳动，恶毒凶狠。\n\n「发现立即抓捕！」",
                    Presentation = StoryPresentation.Comic
                },
                new StoryNodeDefinition
                {
                    Id = "story_stage3_start",
                    Title = "塞德里克被带走",
                    Body = "莫里森：塞德里克，跟我们去警局吧。\n塞德里克：什，什么？为什么！\n哈罗德神父：警长，我能问问为什么吗？\n莫里森：他这把刀，和死者身上的伤口一致。\n雪莉：神父，他只有这一套衣服吗？\n神父：是的，是的。\n雪莉：那怎么会这么干净呢？",
                    Presentation = StoryPresentation.VisualNovel
                },
                new StoryNodeDefinition
                {
                    Id = "story_case_closed",
                    Title = "案件了结",
                    Body = "血衣上的颜料、教堂专属的白、和那件永远只有一件的外套。\n塞德里克在监牢里不再说话。\n\n斯威克东区的两桩夜路命案，结了。",
                    Presentation = StoryPresentation.VisualNovel
                }
            });
        }

        // ── 配方（设定集「合成表」30 条） ──────────────────────────────────

        private static void AddRecipes(CaseDefinition definition)
        {
            // 第一阶段：警察局翻档案 → 两起命案现场
            // 打开案件档案之后：当天的报纸
            RecipeDefinition archive = Recipe("r_archive_investigate", ActionKind.Investigate, "I00",
                cards: new[] { "I01", "I02" });
            archive.OutputNewspaperId = "N01";
            definition.Recipes.Add(archive);

            definition.Recipes.Add(Recipe("r_doc1_investigate", ActionKind.Investigate, "I01",
                cards: new[] { "I03", "I04", "I05" }));

            definition.Recipes.Add(Recipe("r_doc2_investigate", ActionKind.Investigate, "I02",
                cards: new[] { "I06", "I07", "I08" }));

            definition.Recipes.Add(Recipe("r_scene1_investigate", ActionKind.Investigate, "I03",
                clues: new[] { "C01", "C02", "C03" }));

            definition.Recipes.Add(Recipe("r_interview1_investigate", ActionKind.Investigate, "I04",
                cards: new[] { SceneClub },
                clues: new[] { "C04", "C05", "C06" }));

            definition.Recipes.Add(Recipe("r_autopsy1_investigate", ActionKind.Investigate, "I05",
                detective: Morrison,
                clues: new[] { "C07", "C08" }));

            definition.Recipes.Add(Recipe("r_scene2_investigate", ActionKind.Investigate, "I06",
                cards: new[] { "I09" },
                clues: new[] { "C09", "C10", "C11" }));

            definition.Recipes.Add(Recipe("r_interview2_investigate", ActionKind.Investigate, "I07",
                cards: new[] { SceneSalon },
                clues: new[] { "C12", "C13", "C14" }));

            definition.Recipes.Add(Recipe("r_autopsy2_investigate", ActionKind.Investigate, "I08",
                detective: Morrison,
                clues: new[] { "C15", "C16" }));

            // 现场 → 俱乐部 → 沙龙 → 回警局
            definition.Recipes.Add(Recipe("r_go_club", ActionKind.Go, SceneClub,
                scene: ScenePolice,
                detective: Morrison,
                cards: new[] { ScenePolice, "P06" },
                outputScene: SceneClub));

            definition.Recipes.Add(Recipe("r_talk_dancer", ActionKind.Talk, "P06",
                detective: Morrison,
                clues: new[] { "C18", "C19" }));

            definition.Recipes.Add(Recipe("r_print_investigate", ActionKind.Investigate, "I09",
                detective: Morrison,
                clues: new[] { "C17" }));

            // 从别的场景回到警局：第二张报纸
            RecipeDefinition goPolice = Recipe("r_go_police", ActionKind.Go, ScenePolice,
                detective: Morrison,
                outputScene: ScenePolice);
            goPolice.OutputNewspaperId = "N02";
            definition.Recipes.Add(goPolice);

            definition.Recipes.Add(Recipe("r_go_salon", ActionKind.Go, SceneSalon,
                scene: ScenePolice,
                detective: Morrison,
                cards: new[] { ScenePolice, "P07" },
                outputScene: SceneSalon));

            definition.Recipes.Add(Recipe("r_talk_salon", ActionKind.Talk, "P07",
                detective: Morrison,
                clues: new[] { "C20", "C21", "C22" }));

            // 第二阶段：教堂，双主角各自的问话线
            definition.Recipes.Add(Recipe("r_church_enter_m", ActionKind.Go, SceneChurch,
                scene: SceneChurch,
                detective: Morrison,
                cards: new[] { "P01", "P02" }));

            definition.Recipes.Add(Recipe("r_church_enter_s", ActionKind.Go, SceneChurch,
                scene: SceneChurch,
                detective: Shirley,
                cards: new[] { "P01", "P02" }));

            definition.Recipes.Add(Recipe("r_talk_cedric_m", ActionKind.Talk, "P01",
                detective: Morrison,
                cards: new[] { "I11" },
                clues: new[] { "C23", "C24" }));

            definition.Recipes.Add(Recipe("r_talk_cedric_s", ActionKind.Talk, "P01",
                context: new[] { "I10" }, detective: Shirley,
                clues: new[] { "C23", "C24", "C25" }));

            definition.Recipes.Add(Recipe("r_talk_priest_m", ActionKind.Talk, "P02",
                detective: Morrison,
                clues: new[] { "C26", "C27", "C28" }));

            definition.Recipes.Add(Recipe("r_talk_priest_s", ActionKind.Talk, "P02",
                context: new[] { "I10" }, detective: Shirley,
                clues: new[] { "C26", "C27", "C29" }));

            definition.Recipes.Add(Recipe("r_talk_priest_paint", ActionKind.Talk, "P02",
                context: new[] { "I11" }, detective: Shirley,
                clues: new[] { "C26", "C27", "C30" }));

            // 第三阶段：回警局，监牢与档案
            definition.Recipes.Add(Recipe("r_talk_jail_m", ActionKind.Talk, "P08",
                detective: Morrison,
                cards: new[] { "I00" },
                clues: new[] { "C31" }));

            definition.Recipes.Add(Recipe("r_talk_jail_s", ActionKind.Talk, "P08",
                detective: Shirley,
                cards: new[] { "I00" },
                clues: new[] { "C31" }));

            definition.Recipes.Add(Recipe("r_archive_m", ActionKind.Investigate, "I00",
                detective: Morrison,
                clues: new[] { "C32" }));

            definition.Recipes.Add(Recipe("r_archive_s", ActionKind.Investigate, "I00",
                detective: Shirley,
                clues: new[] { "C33" }));

            definition.Recipes.Add(Recipe("r_archive_s_paint", ActionKind.Investigate, "I00",
                context: new[] { "I11" }, detective: Shirley,
                clues: new[] { "C34" }));

            definition.Recipes.Add(Recipe("r_priest_confess_m", ActionKind.Talk, "P02",
                detective: Morrison,
                cards: new[] { "I13" }));

            definition.Recipes.Add(Recipe("r_priest_confess_s", ActionKind.Talk, "P02",
                detective: Shirley,
                cards: new[] { "I13" }));

            definition.Recipes.Add(Recipe("r_bloody_investigate", ActionKind.Investigate, "I13",
                flags: new[] { "flag_bloody_cloth_examined" }));
        }

        // ── 问题页 ──────────────────────────────────────────────────────────

        private static void AddQuestionPages(CaseDefinition definition)
        {
            // Q04：线索进度页，放在最前（设定集：这一页在最前）
            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageQ04,
                Title = "寻找嫌疑人",
                Description = "教堂标志居中，下方计数「线索 0/7」。收齐七条教堂线索后进入下一步。",
                RequiredClueIds = new List<string> { "C23", "C24", "C25", "C26", "C27", "C28", "C29" }
            });

            // Q01：珠宝店店主被杀页（过程页，不参与完成条件）
            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageQ01,
                Title = "珠宝店店主被杀页",
                Slots = new List<QuestionSlotDefinition>
                {
                    AutoSlot("victim", "死者", "P04"),
                    ClueSlot("culprit", "凶手", "C23", "C24", "C25", "C26", "C27"),
                    ClueSlot("weapon", "凶器", "C28"),
                    ClueSlot("evidence", "证据", "C34")
                }
            });

            // Q02：议员之子被杀页（过程页）
            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageQ02,
                Title = "议员之子被杀页",
                Slots = new List<QuestionSlotDefinition>
                {
                    AutoSlot("victim", "死者", "P05"),
                    ClueSlot("culprit", "凶手", "C23", "C24", "C25", "C26", "C27"),
                    ClueSlot("weapon", "凶器", "C28"),
                    ClueSlot("evidence", "证据", "C34")
                }
            });

            // Q03：凶手页（排除项，填满 = 第一阶段结束）
            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageQ03,
                Title = "凶手页",
                Description = "三个排除项：填对后划掉，最后得到一份「排查对象」名单。",
                Slots = new List<QuestionSlotDefinition>
                {
                    new QuestionSlotDefinition
                    {
                        Id = "motive_revenge",
                        Label = "仇杀？情杀？",
                        AcceptedClueIds = new List<string> { "C05" },
                        StrikeThroughWhenCorrect = true
                    },
                    new QuestionSlotDefinition
                    {
                        Id = "motive_robbery",
                        Label = "劫财？",
                        AcceptedClueIds = new List<string> { "C04" },
                        StrikeThroughWhenCorrect = true
                    },
                    new QuestionSlotDefinition
                    {
                        Id = "profile",
                        Label = "体貌特征",
                        AcceptedClueIds = new List<string> { "C17" },
                        StrikeThroughWhenCorrect = true
                    }
                }
            });

            // Q05：杀人案综合页（结案页）
            definition.QuestionPages.Add(new QuestionPageDefinition
            {
                Id = PageQ05,
                Title = "杀人案综合页",
                Slots = new List<QuestionSlotDefinition>
                {
                    AutoSlot("victim1", "死者", "P04"),
                    AutoSlot("victim2", "死者", "P05"),
                    ClueSlot("culprit", "凶手", "C23", "C24", "C25", "C26", "C27"),
                    ClueSlot("weapon", "凶器", "C28"),
                    ClueSlot("evidence", "证据", "C34")
                }
            });
        }

        // ── 阶段 ────────────────────────────────────────────────────────────

        private static void AddStages(CaseDefinition definition)
        {
            definition.Stages.Add(new StageDefinition
            {
                Id = Stage1,
                DisplayName = "第一阶段：两起夜路命案",
                SceneId = ScenePolice,
                ClearCardsOnEnter = false,
                StartingCardIds = new List<string> { "I00" },
                AddQuestionPageIds = new List<string> { PageQ04, PageQ01, PageQ02, PageQ03 },
                CompletedByQuestionPageId = PageQ03
            });

            definition.Stages.Add(new StageDefinition
            {
                Id = Stage2,
                DisplayName = "第二阶段：教堂的画师",
                SceneId = SceneChurch,
                ClearCardsOnEnter = true,
                StartingCardIds = new List<string> { "I10", SceneChurch },
                StoryNodeId = "story_stage2_start",
                CompletedByQuestionPageId = PageQ04
            });

            definition.Stages.Add(new StageDefinition
            {
                Id = Stage3,
                DisplayName = "第三阶段：警局结案",
                SceneId = ScenePolice,
                ClearCardsOnEnter = true,
                StartingCardIds = new List<string> { "P08", "I00", "I11", "P02" },
                RemoveQuestionPageIds = new List<string> { PageQ01, PageQ02, PageQ03, PageQ04 },
                AddQuestionPageIds = new List<string> { PageQ05 },
                StoryNodeId = "story_stage3_start",
                // 文案表：VN02「教堂问讯之后」在进警局结案时播
                EnterDialogueId = "VN02",
                // 教堂的八条线索收齐后，报纸登出「连环杀手落网」
                EnterNewspaperId = "N03"
            });
        }

        // ── 小工具 ──────────────────────────────────────────────────────────

        private static ClueDefinition Clue(string id, string text, params string[] keywords)
        {
            return new ClueDefinition(id, text, keywords);
        }

        private static QuestionSlotDefinition AutoSlot(string id, string label, string cardId)
        {
            return new QuestionSlotDefinition
            {
                Id = id,
                Label = label,
                AutoFilledCardId = cardId,
                AcceptedCardIds = new List<string> { cardId }
            };
        }

        private static QuestionSlotDefinition ClueSlot(string id, string label, params string[] clueIds)
        {
            return new QuestionSlotDefinition
            {
                Id = id,
                Label = label,
                AcceptedClueIds = new List<string>(clueIds)
            };
        }

        /// <summary>
        /// 每条行动配方配一段剧情（策划「配方合成完成对话」表 VN04–VN25）。
        /// 挂在这里而不是散在各处，改文案只动这张表。
        /// </summary>
        private static string DialogueFor(string recipeId)
        {
            switch (recipeId)
            {
                case "r_archive_investigate": return "VN04";
                case "r_doc2_investigate": return "VN05";
                case "r_autopsy1_investigate": return "VN06";
                case "r_doc1_investigate": return "VN07";
                case "r_scene1_investigate": return "VN09";
                case "r_interview1_investigate": return "VN10";
                case "r_autopsy2_investigate": return "VN11";
                case "r_go_club": return "VN12";
                case "r_talk_dancer": return "VN13";
                case "r_print_investigate": return "VN14";
                case "r_go_salon": return "VN15";
                case "r_talk_salon": return "VN16";
                case "r_church_enter_m":
                case "r_church_enter_s": return "VN17";
                case "r_talk_cedric_m": return "VN18";
                case "r_talk_cedric_s": return "VN19";
                case "r_talk_priest_m": return "VN20";
                case "r_talk_priest_s": return "VN21";
                case "r_talk_priest_paint": return "VN22";
                case "r_talk_jail_m": return "VN23";
                case "r_archive_m": return "VN24";
                case "r_archive_s": return "VN25";
                default: return null;
            }
        }

        private static RecipeDefinition Recipe(
            string id,
            ActionKind action,
            string primaryCardId,
            string scene = null,
            string[] context = null,
            string[] cards = null,
            string[] clues = null,
            string[] flags = null,
            string outputScene = null,
            string detective = null)
        {
            return new RecipeDefinition
            {
                Id = id,
                Action = action,
                PrimaryCardId = primaryCardId,
                RequiredSceneId = scene,
                RequiredDetectiveId = detective,
                DialogueId = DialogueFor(id),
                RequiredContextCardIds = new List<string>(context ?? new string[0]),
                OutputCardIds = new List<string>(cards ?? new string[0]),
                OutputClueIds = new List<string>(clues ?? new string[0]),
                SetFlags = new List<string>(flags ?? new string[0]),
                OutputSceneId = outputScene
            };
        }
    }
}
