using CoolingGame.Core;

namespace CoolingGame.Core.Samples
{
    /// <summary>
    /// 第一章剧情台词（由 documents/第一章文案.xlsx 生成，改文案请改表再重新生成）。
    /// </summary>
    public static class Chapter1Dialogue
    {
        public static void AddDialogues(CaseDefinition definition)
        {
            definition.Dialogues.Add(new DialogueScript(
                "VN01",
                "",
                L("char/mp01_default", false, "", false, "莫里森", "都一个月了，怎么还找不到凶手！"),
                L("char/mp01_default", true, "char/p08_officer", false, "警员", "……"),
                L("char/mp01_default", false, "char/p08_officer", true, "莫里森", "上级又在警告我了，再抓不到犯人，全警队都要倒霉！"),
                L("", false, "", false, "旁白", "在莫里森警长的训斥下，警局立刻又忙碌了起来，以至于一个陌生的女人，径直走到了莫里森桌前才被人发现。"),
                L("char/p08_officer", false, "char/mp02_default", true, "警员", "女士，这里是CID，如果要报案请去……"),
                L("char/p08_officer", false, "char/mp02_default", false, "雪莉", "我找莫里森警长。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "我就是，你有什么事？"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "我是亨利先生的朋友，这是他的信。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "老警长？他在把警长的位置交给我之后，就去度假了，怎么会有信给我。"),
                L("char/mp01_default", true, "", false, "旁白", "莫里森检查了信封，狐疑的打开了老警长的信件"),
                L("char/mp01_default", true, "", false, "信件文本", "莫里森，我知道你看到这封信的时候一定摸不着头脑，警长这事情没那么好干，对吧？"),
                L("char/mp01_default", true, "", false, "信件文本", "我在游轮上认识了一个人。你别撇嘴，我知道你讨厌大海，但你老上司我这辈子就这点出息，退休金不花在赌桌上就得花在甲板上。"),
                L("char/mp01_default", true, "", false, "信件文本", "总之，船上有个女人叫雪莉，写小说的，她是个十分敏锐的人，一定能帮助到你。"),
                L("char/mp01_default", true, "", false, "信件文本", "虽然说，她没摸过枪，没见过尸体，更不能像你一样和追捕罪犯，和罪犯搏斗，但是她，嘿嘿，我先保个密，她一定会给你惊喜的。"),
                L("char/mp01_default", true, "", false, "信件文本", "雪莉打算来斯威克收集资料，我已经跟上面打过招呼了，让雪莉做你的副手，你可一定不能拒绝啊。"),
                L("char/mp01_default", true, "", false, "信件文本", "——老亨利"),
                L("char/mp01_default", false, "", false, "莫里森", "这老头子，搞什么名堂？"),
                L("char/mp01_default", true, "char/mp02_default", true, "旁白", "在莫里森读信时，雪莉已经读起了一旁黑板上的案件信息。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "嘿！小姐。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "雪莉。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "雪莉小姐，办案不是写小说，我会和上级报告，你不适合在这里工作。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "先生，我认为我比您更适合担任这份工作。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "你也太无礼了，小姐！"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "无政府主义者们是有政治诉求的人，他们更愿意自己的行径被人发现，不是吗?他们不会杀掉一个无关紧要的店主，也不会费尽心思隐瞒行踪。您的推测并不符合现实。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "您这位养尊处优的小姐，怎么能知道这些事。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "我从奥地利来，那边的无政府主义者多的能再建一个这样的警局了。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "你是在说我们也是乌合之众吗！"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "凶手用刀反复捅向受害人，这是一种发泄，他一定有一种被压抑的心理，仇恨与受害者相似的人。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "凶手很可能作息昼夜颠倒，而且外表不会是凶恶之人，甚至可能有些文弱。生活条件一般甚至贫困，但能与富人接触，你们应该从城里人找起，尤其是夜班工人这样的职业，而不是外国人。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "另外，还要排查济贫院、教堂这样的场所。"),
                L("char/mp01_default", true, "char/mp02_default", true, "旁白", "雪莉流畅的推理使莫里森警长感到惊讶，他想要反驳什么，但想到自己一个月来的一无所获，微张着的嘴巴，就发不出任何声音。"),
                L("char/mp01_default", true, "char/mp02_default", true, "旁白", "尴尬的莫里森憋了半天，只能看向警局里的其他人。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "看在老警长的份上，你……你们还在看什么！去按她说的找吧！别愣着！"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "既然您接受了我的推理，我们现在可以平等的合作了吗？莫里森警长？")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN02",
                "",
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "你有把握吗？"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "嗯，基本可以确定了。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "塞德里克，跟我们去警局吧。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "什，什么？为什么！"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "警长，我能问问为什么吗？"),
                L("char/mp01_default", false, "", true, "莫里森", "他这把刀，和死者身上的伤口一致。"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "警长，我想您是误会了，这把刀是我买的，而且这样的短刀到处都是。"),
                L("char/mp01_default", false, "", true, "莫里森", "这……"),
                L("char/mp02_default", false, "", true, "雪莉", "神父，他只有这一套衣服吗？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "是的，是的。"),
                L("char/mp02_default", false, "", true, "雪莉", "那怎么会这么干净呢？")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN03",
                "",
                L("char/mp02_default", true, "", true, "旁白", "神父一时语塞，雪莉便趁这个空挡示意了一下莫里森，一起将塞德里克带走了。"),
                L("char/mp01_default", false, "", true, "莫里森", "您一定是在开玩笑，神父。"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "人是我杀的，那孩子是无辜的，你们把他放了吧。"),
                L("char/mp02_default", false, "", true, "雪莉", "您的动机是什么？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "魔鬼上了我的身，每次到夜里，我都会出来杀人。"),
                L("char/mp01_default", false, "", true, "莫里森", "哈罗德神父，我在您的教导下长大，您怎么会这样说。"),
                L("char/mp01_default", false, "", true, "莫里森", "您是不是遇到了什么事，神父，您可以告诉我。"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "不要问了孩子，把我关起来吧。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "你看不出吗，他是想用自己交换塞德里克。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "你以为我很愚蠢吗？"),
                L("char/mp01_default", true, "char/mp02_default", true, "旁白", "莫里森沉默的和雪莉对视，眼中闪着泪光，雪莉这才意识到，神父在他的心中扮演着多么重要的角色。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "你如果想知道神父这么做的原因，只有一个办法。"),
                L("char/mp01_default", false, "char/mp02_default", true, "旁白", "莫里森听懂了雪莉的意思，叫来了其他警员，让他们照顾哈罗德神父，站起身来和雪莉出了门。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "你们怎么问也没用，我没有杀人。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "你最好老实点，这里没有别人会来。"),
                L("", false, "", false, "旁白", "才刚坐好的莫里森，又起身站了起来。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "你要干什么！"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "让他认罪！"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "不要这么冲动，就算逼他认罪他也可在法庭上翻供！"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "……塞德里克，你不要以为嘴硬就有用，我们已经找到了证据。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我没有杀人，你怎么栽赃我都行。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "旁白", "莫里森情绪又要被塞德里克激了起来，雪莉立刻伸手，拦在了他的手臂上。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "我们是不会诬陷一个好人的，如果你真的无辜，就一定有被放走的那一天，对吧。"),
                L("char/mp02_default", true, "char/p01_cedric", true, "旁白", "塞德里克低垂着眼，没有任何反应，仿佛什么都没听见。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "但我们放了你，没有新的凶手，媒体也会认为你是凶手吧。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "那些记者，仿佛无孔不入，他们一定会不停的问你，无论你怎么拒绝，那些目光也不会从你的身上移开。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "很快，他们就会发现，这个隐藏在深夜巷道里的杀人凶手，还是一个艺术家。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "你在教堂中的画会被报道，你曾经画出的作品会被发现，那些艺术评论家将从你的画中解读出更多不一样的东西。"),
                L("char/mp02_default", true, "char/mp01_default", false, "莫里森", "你和他说这个干什么？"),
                L("char/mp02_default", false, "char/mp01_default", true, "旁白", "雪莉没有理会莫里森的问题。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "卷进杀人案，然后名声鹊起，是不是？"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "我没有。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "没有什么？没有杀人，还是没有想过这样的好事。"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "我，我不会用这样的手段。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "没关系，现在也只能想想，这样的事已经不会发生了。"),
                L("char/mp02_default", true, "char/p01_cedric", true, "旁白", "雪莉有意停顿了一下，看着塞德里克的反应，但塞德里克在刚刚的反驳后，就又恢复了先前的沉默。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "刚刚有一个人来自首了。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "他拿出了杀死约翰·卡林顿时的衣物，现在所有的注意力都会集中到他的身上。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "来自首的人，就是收留你的哈罗德神父。"),
                L("char/mp02_default", true, "char/p01_cedric", false, "旁白", "塞德里克依然凝视着自己被铐住的双手，但听到这里，不自觉的轻轻的抽了一下鼻子。"),
                L("char/mp02_default", true, "char/p01_cedric", true, "旁白", "这个动作没有逃过雪莉的眼睛，莫里森也隐约意识到，雪莉的语言策略可能比自己的威胁更有用。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "你的画我们在教堂看过，不得不说，以你的年龄，你的技艺足称精深。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "如果有评论家给出高评价，或者有人用大价钱买下，便可以出人头地，甚至有机会名留艺术史。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "但是没有。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "没有人赏识你，没有人认可你的画，你一次次碰壁，一年年蹉跎岁月。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "在耗尽财富和耐心之后，你开始反思，也许开始时，你想的是自己的画技尚不成熟，自己的风格还不够有特色。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "时间长了，你开始憎恨那些富人，那些不识货的家伙，那些不明白你作品价值，高高在上的家伙，尤其是，他们还来教堂祈祷，还去做慈善，真是虚伪啊。"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "……"),
                L("char/mp02_default", true, "char/p01_cedric", true, "旁白", "塞德里克把头偏到了一边。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "可是你遇到了哈罗德神父。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "他认可了你的画，给了你能维持生计的工作。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "你怎么能辜负神父的善意，你怎么能在上帝的注视下犯下这样的罪！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我就是要在上帝眼前杀人！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "上帝让你们过着完美的生活，可是上帝为什么不来帮我！"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "哈罗德神父已经在帮助你了！他甚至愿意顶替你的罪名！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我用不着！！！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "他只是控制着我！白天看着那些虚伪的家伙来教堂祷告，晚上画那些过时的宗教画，那些画两百年前就不流行了！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我在他的控制下，一辈子都没有成功的机会！！"),
                L("char/mp01_default", false, "char/p01_cedric", false, "旁白", "莫里森愤怒的拍打着桌子，站起来抓住了塞德里克的领子，但塞德里克直直的瞪着他，毫无悔意与恐惧。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "你们没有证据，如果你打了我，记者们会知道的。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "我们已经掌握了证据。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "旁白", "雪莉拿出了从祭坛上采集的颜料。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "你把祭坛上的白色颜料，沾到了受害者的身上，恐怕你自己都没有注意到。"),
                L("char/mp02_default", true, "char/p01_cedric", false, "旁白", "塞德里克难以置信的看着雪莉，激动的呼吸逐渐平缓。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "跟我上去，签认罪书吧，你还要向神父忏悔。"),
                L("", false, "char/p01_cedric", false, "旁白", "塞德里克仿佛失去了力气，仿佛被莫里森提在手上，无力的摇着头。"),
                L("char/mp01_default", true, "", false, "旁白", "莫里森在办公室拿起了最新的报纸，曾经攻击自己无能的地方上，现在是凶手被判处绞刑的头条，莫里森如释重负的叹了口气，然后听到了敲门声。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "进来。"),
                L("char/mp01_default", true, "char/mp02_default", false, "旁白", "雪莉走了进来，依然像第一天到警局时那样干练。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "这次多谢你了。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "我知道我在断案上几乎一窍不通，今后还要你多多协助。"),
                L("char/mp01_default", true, "char/mp02_default", false, "旁白", "雪莉听了这话，第一次露出了笑意。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "好啊，我本来就是来当警长先生的副手的。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "不必叫我警长了，这么久了还没有正式介绍，我是斯威克东区警队的警长，我叫马丁·莫里森"),
                L("char/mp01_default", false, "char/mp02_default", true, "旁白", "莫里森站了起来，伸出了右手，邀请雪莉，雪莉也欣然握住了他的手。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "雪莉，雪莉·勃朗特。"),
                L("char/mp01_default", false, "char/mp02_default", false, "旁白", "案件一·暗夜下的杀人魔 结束")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN04",
                "",
                L("", false, "", false, "旁白", "一个月前，警局接到报案，一名珠宝店店主被杀死在了斯威克城东的砖瓦巷街道。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN05",
                "",
                L("", false, "", false, "旁白", "两周前，在斯威克城东的砖瓦巷边发生了杀人案，麻烦的是，死者是郡议员卡林顿爵士的长子。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN06",
                "",
                L("char/mp01_default", false, "", false, "莫里森", "尸体被发现时，还是凌晨，我们赶到时，周围的人不多，没有造成恐慌，现场也没有被破坏。"),
                L("char/mp01_default", false, "", false, "莫里森", "死者仰面躺在血泊中，没有什么反抗的迹象，周围的地面上也没有任何凶手留下的线索，这可真为难人了。"),
                L("char/mp01_default", false, "", false, "莫里森", "对了，当时死者身上有股酒气，应该是死前喝过酒。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN07",
                "",
                L("char/mp01_default", false, "", false, "莫里森", "死者是一家珠宝店的店主，穿戴的衣物都很昂贵。"),
                L("char/mp01_default", false, "", false, "莫里森", "不过这些昂贵衣物，除了伤口处都没什么损坏，我们也向死者家属确认过，身上的其他财物也没有丢失。"),
                L("char/mp01_default", false, "", false, "莫里森", "值得一提的是，死者口碑不错，不仅为人和善，从不与人结仇，还经常去教堂聆听圣训。"),
                L("char/mp01_default", false, "", false, "莫里森", "死者死前的夜间，据他的妻子所说是去了一家私人俱乐部，那个可怜的女人，还以为丈夫只是遇到了朋友，在外过夜，谁想到……")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN08",
                "",
                L("", false, "", false, "尸检报告", "胸腹部检见锐器创8处，分布散在、排列不规则；头部检见钝器打击所致损伤痕迹。"),
                L("", false, "", false, "", "肺脏检见锐器刺创，伴胸腔大量积血。"),
                L("", false, "", false, "", "推测死因：肺脏锐器刺创致大出血死亡。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN09",
                "",
                L("char/mp01_default", false, "", false, "莫里森", "那天夜里，巡夜的警员最先发现了尸体。"),
                L("char/mp01_default", false, "", false, "莫里森", "死者以一个扭曲的姿势倒在了血泊里，衣服上的血迹很混乱，明显是和凶手发生过纠缠。"),
                L("char/mp01_default", false, "", false, "莫里森", "尸体附近还有几个残缺的血脚印，我让手下将脚印拓了下来。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN10",
                "",
                L("char/mp01_default", false, "", false, "莫里森", "这场问话就是一场灾难，我们当时并不知道，死者是郡议员卡林顿爵士的长子，而卡林顿爵士的态度很差，我们只能向卡林顿夫人询问死者的情况。"),
                L("char/mp01_default", false, "", false, "莫里森", "卡林顿爵士的长子叫约翰，他和卡林顿爵士向来不和，生活作风也相去甚远。"),
                L("char/mp01_default", false, "", false, "莫里森", "约翰生活十分低调，与父亲闹掰后，便独自住在了公寓中，房东、身边的朋友，在这件事之前都不知道他的身份。"),
                L("char/mp01_default", false, "", false, "莫里森", "卡林顿爵士在议会以强硬著称，而约翰在生活中则完全看不到他父亲的影子，他经常前往济贫院、教堂做慈善，还经常参与一家读书沙龙的活动。"),
                L("char/mp01_default", false, "", false, "莫里森", "当日他的具体情况，还要亲自去这家沙龙确认。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN11",
                "",
                L("", false, "", false, "尸检报告", "头部检见钝器打击所致损伤痕迹；双手检见锐器割创；胸前区检见锐器创4处，构成致命伤。"),
                L("", false, "", false, "", "胸前区创口形态特征与第一例死者相应创口相符，不排除为同一把刀所致")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN12",
                "",
                L("", false, "", false, "旁白", "这家私人俱乐部乱糟糟的，在莫里森的要求下，店主将当日与珠宝店店主有接触的那名舞女叫了过来。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN13",
                "",
                L("char/mp01_default", false, "", false, "莫里森", "你与死者很熟吗？"),
                L("char/mp01_default", true, "char/p06_dancer", true, "旁白", "舞女似乎对警察完全不在乎，对莫里森警长轻轻的点了个头，手上点了根烟，抽了一口。"),
                L("char/mp01_default", true, "char/p06_dancer", false, "舞女", "对，他经常来看我跳舞，可惜了，少了一个熟客。"),
                L("char/mp01_default", false, "char/p06_dancer", true, "莫里森", "和我说说当天夜里的情况。"),
                L("char/mp01_default", true, "char/p06_dancer", false, "舞女", "没什么特殊的，无非是跳舞，喝酒，哪天晚上不是这样。"),
                L("char/mp01_default", false, "char/p06_dancer", true, "莫里森", "然后他就回去了？"),
                L("char/mp01_default", true, "char/p06_dancer", false, "舞女", "对，他走的时候，我看他醉意朦胧的，我本来想把他掺上车，但是他说夜风醒酒，要走回去。"),
                L("char/mp01_default", true, "char/p06_dancer", false, "旁白", "舞女说到这里，终于露出了一点不忍的神色。"),
                L("char/mp01_default", false, "char/p06_dancer", true, "舞女", "也许那时我阻止了他，他就不会死了。"),
                L("char/mp01_default", true, "char/p06_dancer", false, "莫里森", "咳，你最近不要外出，以备有新情况还要找你问话。"),
                L("char/mp01_default", false, "char/p06_dancer", true, "舞女", "你真看得起我，警长。我要是随便就能去其他城市，也不用做这个工作了。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN14",
                "",
                L("", false, "", false, "旁白", "警员拓下了尸体旁的脚印，脚印的主人没有注意到自己沾上了血，踩下了一串脚印，迷蒙的夜色既做了凶手的帮凶，又留下了恶行的线索。"),
                L("char/mp01_default", false, "", false, "莫里森", "根据这个脚印并不难拼凑出凶手的体貌特征，根据经验，凶手应该是中等身高，体重偏轻。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN15",
                "",
                L("", false, "", false, "旁白", "这家沙龙在一栋干净的公寓里，莫里森进门时，一个穿着中性的女人正坐在中间在讲书，莫里森扫了一眼，听书的人穿什么的都有，这里好像不是有钱人来的体面地方地方。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN16",
                "",
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "先生，这里不是你们来的地方，请出去。"),
                L("char/mp01_default", false, "char/p07_salon", true, "旁白", "这一下子，所有人的目光都集中在了莫里森身上，气氛变得紧张。"),
                L("char/mp01_default", false, "char/p07_salon", true, "莫里森", "嘿！我们不是来找麻烦的，你认识约翰·卡林顿吗？我有关于他的事要问。"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "……"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "来这边吧。"),
                L("char/mp01_default", true, "char/p07_salon", false, "旁白", "女人起身，带莫里森和其他警察到了门外。"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "你要问什么？"),
                L("char/mp01_default", false, "char/p07_salon", true, "莫里森", "约翰被人杀死了，你知不知道。"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "当然知道，这段时间的报纸一直在骂你们无能。"),
                L("char/mp01_default", false, "char/p07_salon", true, "莫里森", "咳咳，约翰在你们这里做什么？"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "就是读书、分享，我们这里读一些文学作品，经典的、时兴的，他很擅长讲书，最普通的故事在他嘴里读出来也变得引人入胜。"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "他出事那天，也没有什么特别的，他在这里读完书，和大家交流了感想，就自己离开了，像每个普通的夜晚。"),
                L("char/mp01_default", false, "char/p07_salon", true, "莫里森", "没了？"),
                L("char/mp01_default", true, "char/p07_salon", false, "沙龙女主人", "让你失望了，没了。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN17",
                "",
                L("", false, "", false, "旁白", "东区的这座教堂规模不算大，历史也不是很长，但在本地有不小的影响力，许多的居民都来这里进行礼拜，连莫里森警长出生时都是在这里受洗的。"),
                L("", false, "", false, "旁白", "莫里森和雪莉刚到教堂里面，哈罗德神父就走了过来，老神父穿着一件极为整洁的黑色罗马领衬衫，似乎是正在祭坛处准备仪式，在祭坛的旁边，两个警员正在看管着一位穿着干净的年轻人。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN18",
                "",
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "你叫什么？"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "塞德里克"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "做什么工作的？"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我在这里画彩绘。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "九月四日和九月十七日的夜晚，你在哪里？"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我一直在负责完善、翻新教堂的彩绘，这两个月都在教堂里。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "谁能证明？"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "哈罗德神父，白天他在教堂主持仪式，晚上我就继续工作。"),
                L("char/mp01_default", true, "char/p01_cedric", true, "旁白", "莫里森点点头，拿出来两名死者的尸体照片。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "你认识这两个人吗？"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "不认识。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN19",
                "",
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "你在这里工作？"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "对，这里墙壁、祭坛上的彩绘，都是我画的。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "九月四日和九月十七日的夜晚，你在哪里？"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "在教堂，白天教堂要举行各种仪式，我只能在晚上画。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "谁能证明？"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "哈罗德神父。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "旁白", "雪莉盯着塞德里克长期昼夜颠倒形成的黑眼圈和胡茬，然后将目光放在了他的衣服上。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "你……你只有这一套衣服吗？"),
                L("char/mp02_default", true, "char/p01_cedric", false, "塞德里克", "是的，我没有太多钱买其他衣服。"),
                L("char/mp02_default", false, "char/p01_cedric", true, "雪莉", "那就怪了。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN20",
                "",
                L("char/mp01_default", true, "", false, "哈罗德神父", "警长，那孩子遇到什么麻烦了吗？"),
                L("char/mp01_default", false, "", true, "莫里森", "哈罗德神父，您不用担心，我们只是来寻找线索。"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "好，我会配合的。"),
                L("char/mp01_default", false, "", true, "莫里森", "神父，这位年轻人，是什么时候来教堂工作的呢？"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "有一段时间了，大概半年前，我看到塞德里克在出售自己的画，但是没什么人关注。"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "后来，在济贫院发放食物时，我又看到了这孩子，我就邀请他来为教堂作画。"),
                L("char/mp01_default", false, "", true, "莫里森", "原来是这样。"),
                L("char/mp01_default", true, "", false, "哈罗德神父", "这孩子很有绘画天赋，还按时参与早晚祷，最近的报纸上的案子，我也看过，我觉得不可能是他做的。"),
                L("char/mp01_default", true, "", false, "莫里森", "神父，寻找证据是我们警察的责任，您不需要担心，我们不会冤枉好人的。"),
                L("char/mp01_default", false, "", true, "莫里森", "塞德里克的住处在哪里，我们能查看一下吗？"),
                L("char/mp01_default", false, "", true, "旁白", "哈罗德神父将莫里森带到阁楼，阁楼堆放的杂物中，临近窗口的地方，被清理出了一片区域，放着一张床，一个矮桌，矮桌下面，放着一个包袱、一桶工具。莫里森回头，向阁楼下的其他警员示意过来。"),
                L("char/mp01_default", false, "", true, "莫里森", "嗯，来人，把这里的东西记录一下。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN21",
                "",
                L("char/mp02_default", false, "", true, "雪莉", "神父，我是莫里森的副手，能问您一些问题吗？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "嗯，请您问吧。"),
                L("char/mp02_default", false, "", true, "雪莉", "这位塞德里克先生是您招募的，还是教区主教指派的？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "嗯，是我见他需要帮助，教堂也需要一位绘制彩绘的画师，我便招募了他。"),
                L("char/mp02_default", false, "", true, "雪莉", "原来如此。"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "塞德里克之前的画我也看过，他画得很不错，也需要一份工作维生，他绝不会做杀人的事的。"),
                L("char/mp02_default", false, "", true, "雪莉", "死者和凶手发生过搏斗，凶手的衣服恐怕会沾血，您有见过塞德里克穿过其他的衣物吗？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "这，没有，没有。"),
                L("char/mp02_default", false, "", true, "雪莉", "您确定吗？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "确定。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN22",
                "",
                L("char/mp02_default", false, "", true, "雪莉", "神父，这里也是塞德里克在画的吗？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "嗯，是的，他这几天夜里，都在画这幅祭坛彩绘，这也是所有画中的重点，除了绘画，还要雕刻和镶嵌出圣像。"),
                L("char/mp01_default", false, "", true, "莫里森", "看来他除了绘画，还很擅长雕塑啊。"),
                L("char/mp02_default", false, "", true, "雪莉", "这里这么多的画，用到的的颜料都是塞德里克自己买的吗？得花不少钱吧？"),
                L("char/mp02_default", true, "", false, "哈罗德神父", "这倒不是，教堂画用到的颜料是教会专属的。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN23",
                "",
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "你主动交代吧，九月四日，九月十七日，你是怎么犯下两起杀人案的。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我没有杀人……"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "那你那两天在哪里！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "我在教堂工作。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "可你工作的时候只有你一个人，神父不可能一直盯着你。"),
                L("char/mp01_default", false, "char/p01_cedric", true, "莫里森", "我再问你一遍！九月四日，九月十七日，你干了什么！你把染血的衣服藏到哪里了！还有那把裁布的刀，是不是凶器！"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "那把刀很容易买到的，我没有杀人。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "如果你们找不到凶手，就把罪名安在我这样无依无靠的人身上，我也什么办法都没有。"),
                L("char/mp01_default", true, "char/p01_cedric", false, "塞德里克", "上帝啊，求你在不仁不义的百姓中，为我伸冤，拯救我脱离奸诈邪恶的人……"),
                L("char/mp01_default", true, "char/p01_cedric", false, "旁白", "塞德里克几乎快哭了出来，双手合十，开始念起祷文，莫里森无可奈何，气愤的起身出了门，将塞德里克一人留在审讯室。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "看来不管用。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "我们缺少直接的证据，你的推理只能证明他有杀人条件。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "肯定有什么我们遗漏的东西，我们最好重新检查一下案件档案。")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN24",
                "",
                L("", false, "", false, "旁白", "两人将案件档案的全部文件放在桌上，又推来了贴满线索和笔记的案件板。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "塞德里克的身形和我根据脚印的推测基本吻合，鞋底也符合。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "但是相同的鞋底太多了，法庭上没法作为有力的指控。凶器呢？"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "从死者的创口看，凶器确实是一把细长的刀，但塞德里克的刀上沾了各种颜料。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "对，就算是刀上沾了血，他也完全来得及清洗掉。"),
                L("char/mp01_default", true, "char/mp02_default", false, "莫里森", "诶？尸体照片手上这是什么？"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "这个啊，这应该是私人俱乐部舞女脸上的化妆品。"),
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "这个白色，好像有点眼熟，和化妆品的白色不太一样。"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "白色就是白色嘛，能有什么区别？")
            ));
            definition.Dialogues.Add(new DialogueScript(
                "VN25",
                "",
                L("char/mp01_default", true, "char/mp02_default", false, "雪莉", "就是这个！这是教堂专用的白色颜料！"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "如果死者身上沾了教堂的颜料，他就绝对跑不了了！"),
                L("char/mp01_default", false, "char/mp02_default", true, "莫里森", "我现在去教堂采集证物，你去和入殓师说，让他采集死者手上的颜料。"),
                L("", false, "char/mp02_default", false, "旁白", "画未说完，莫里森便冲出了办公室，雪莉暗自感叹警长的雷厉风行，拨电话给了殓尸房。"),
                L("", false, "", false, "", "不久之后，莫里森和手下的警员便拿着证物回到了警局，但跟着一起回来的，还有哈罗德神父。")
            ));
        }

        private static DialogueLine L(string left, bool leftDimmed, string right, bool rightDimmed, string speaker, string text)
        {
            return new DialogueLine
            {
                LeftArtKey = left,
                LeftDimmed = leftDimmed,
                RightArtKey = right,
                RightDimmed = rightDimmed,
                Speaker = speaker,
                Text = text
            };
        }
    }
}