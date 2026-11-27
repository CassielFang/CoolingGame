# 《迷城双探》（暂定名）— 桌面卡牌推理

一款**把侦探的桌子做成界面**的推理游戏：场景、物品、人物都是桌上的卡牌，
玩家把资料**拖到台灯下**查看、把牌**拖到人身上**问话、把牌**拖到地点上**前往，
线索自动记进笔记本，最后在问题页上结案。

不是"点卡→弹出行动菜单"：**牌是可以拿起来、放下、叠在一起的实物**，行动靠把牌拖到它该去的地方。

---

## 现在能玩到什么

第一章《斯威克的连环杀手》**完整可通关**：

- 29 张卡、34 条线索、30 条配方、3 个阶段、5 个场景、5 张问题页
- 25 段剧情对话（253 句，VN01–VN25）+ 3 篇报纸
- 两个办案人（莫里森 / 雪莉）可切换，**同一张卡换个人问，结果不一样**
- 最短通关 16 次行动、22 条线索；`FullPlaythrough_SolvesTheCase` 测试从开局跑到结案

---

## 环境要求

| 项 | 版本 / 说明 |
|---|---|
| Unity | **6000.6.5f1**（Unity 6，URP，2D Renderer） |
| 平台 | PC（1920×1080，16:9） |
| Git LFS | **必须安装**，图片/PDF 走 LFS |

```bash
git lfs install
git clone https://github.com/CassielFang/CoolingGame.git
```

> 没装 LFS 只会拿到指针文件。已 clone 过的执行 `git lfs pull`。

---

## 第一次跑起来

1. Unity Hub 打开仓库根目录（引擎版本对齐 6000.6.5f1）。
2. 等首次资源导入跑完。
3. 打开场景 **`Assets/Scenes/DeskDemo.unity`**。
4. Game 视图设 16:9（1920×1080），点 Play。

开局会自动播放第一章开场剧情（VN01）。**点画面继续**，播完落回桌面。

### 桌面操作

| 你想做什么 | 怎么做 |
|---|---|
| 看一张背面朝上的牌 | 点它，翻牌 |
| 看一张牌的说明 | 点它，对话框里念出说明 |
| 调查一张资料 | 把它**拖到台灯下的光斑里**，光斑上方浮出「调查「XXX」」，点它 |
| 比对几份资料 | 把 2–3 张都拖进光斑，浮出「比对 N 张」 |
| 拿着证据去问人 | 把证据**拖到人物卡上** |
| 去某个地方 | 把任意一张牌**拖到地点卡上** |
| 换办案人 | 点左下角的**人物立绘**（只认人物像素，点旁边的空气不算）或「切换办案人」 |
| 看线索/答题/结案 | 右上角「笔记本」 |
| 随便摆放 | 拖到空白处松手，牌就停在那儿；越晚放下的越在上面 |

---

## 目录结构

```
Assets/
  Scripts/
    Core/                        纯 C# 逻辑层，不引用 UnityEngine
      CaseSession.cs             案件会话：行动、配方判定、产出、阶段/场景/结案
      NotebookModel.cs           笔记本：线索去重 / 置顶 / 搜索 / 未读
      QuestionPage.cs            多问题页与卡槽
      DialogueScript.cs          逐句剧情（台词 + 左右立绘 + 暗态）
      NewspaperDefinition.cs     报纸
      DetectiveDefinition.cs     办案人（主角，不是卡牌）
      CaseDefinition.cs          「关卡表」根结构
      CardDefinition / ClueDefinition / RecipeDefinition / StoryNodeDefinition / StageDefinition / SceneDefinition
      Samples/
        Chapter1Case.cs          第一章全部数据（卡/线索/配方/阶段/问题页）
        Chapter1Dialogue.cs      剧情台词（由 xlsx 生成）
        Chapter1Newspapers.cs    报纸正文（由 xlsx 生成）
    Runtime/
      CaseDefinitionAsset.cs     案件数据的 ScriptableObject 载体
      Presentation/
        DeskDemoController.cs    桌面主控（拖拽、灯下、办案人、HUD、剧情接线）
        CardView.cs              卡牌视图（翻牌/抬起/厚度/拖拽）
        NotebookPanel.cs         笔记本（线索页 / 问题页）
        DialogueOverlay.cs       逐句剧情播放（立绘 + 打字机 + 对话框）
        NewspaperOverlay.cs      报纸（报头 + 标题 + 正文）
        StoryOverlay.cs          旧版情节弹窗
        CinematicDirector.cs     黑场/闪光/暗角/颗粒
        ArtLibrary.cs / UIFactory.cs  美术取图与 UI 构建
  Resources/
    Cases/Chapter1Case.asset     >>> 第一章数据资产（由代码生成，不要手改）<<<
    Art/                         接入后的美术（desk / ui / char / item）
  Scenes/DeskDemo.unity
  Tests/EditMode/                45 个 EditMode 测试
documents/                       设定集、文案 xlsx、演示脚本、MDA
GameArt/                         美术原始交付（不进 Assets）
```

---

## 配内容（不用改代码）

**第一章数据的唯一入口是代码：**
`Assets/Scripts/Core/Samples/Chapter1Case.cs` → 改完执行菜单
**`CoolingGame/Build Chapter1 Case Asset (overwrite)`** 重新生成
`Assets/Resources/Cases/Chapter1Case.asset`。

> 为什么不是直接改 asset：asset 是生成物，手改会在下次生成时被覆盖。
> 剧情和报纸的正文**不要手写**，改 `documents/第一章文案.xlsx` 后重新生成（见下）。

一个案件 = 一张「关卡表」，主要子表：

### Cards / Clues / Recipes（老四样）

字段含义与匹配规则见上一版 README 与 `CardDefinition.cs` 注释。要点：

- `Recipes` 是玩法的全部内容：`行动 + 主卡(+附加卡) → 产出卡/线索/flag/情节/场景/剧情/报纸`；
- 多个配方命中时**取最具体的**（精确卡 4 分 > 标签 2 分，附加卡与前置各 +1，上限 3）；
- 判定规则只写在 Core，表现层只读。

### Detectives（办案人，第一章）

`Id`（MP01 莫里森 / MP02 雪莉）、`DisplayName`、`PortraitArtKey` / `PortraitSmileKey` / `PortraitQuestionKey`、`Description`。

- 配方用 `RequiredDetectiveId` 指定「必须谁来办」——**主角不是卡牌，不占附加卡槽**；
- `CaseSession.SetDetective(id)` 切换，`DetectiveChanged` 事件通知表现层；
- 同一张卡换人问可能命中不同配方（雪莉问塞德里克会注意到"衣服太干净"）。

### Stages（阶段）

按顺序推进：`SceneId`、`ClearCardsOnEnter`、`StartingCardIds`、`AddQuestionPageIds`、`RemoveQuestionPageIds`、
`EnterDialogueId`（进入时播剧情）、`EnterNewspaperId`（进入时发报纸）、`CompletedByQuestionPageId`（填满即进入下一段）。

### QuestionPages（问题页）

每页若干 `Slots`：`Id`、`Label`、`AcceptedCardIds`、`AcceptedClueIds`、`AutoFilledCardId`、`StrikeThroughWhenCorrect`。
`FinalQuestionPageId` 指定结案页。

### Dialogues（逐句剧情）

一段 `DialogueScript` = 若干 `DialogueLine`：
`LeftArtKey` / `RightArtKey`（立绘键，空 = 这侧没人）、`LeftDimmed` / `RightDimmed`（非说话方压暗）、`Speaker`、`Text`。

挂在三处：
- `CaseDefinition.OpeningDialogueId`（开局）、`ClosingDialogueId`（结案）；
- `StageDefinition.EnterDialogueId`（进阶段）；
- `RecipeDefinition.DialogueId`（每次行动后念一段）。

### Newspapers（报纸）

`Id`、`Title`、`Body`（第一行做报头/日期，其余做正文）。
挂在 `RecipeDefinition.OutputNewspaperId` 或 `StageDefinition.EnterNewspaperId`，**同一张只发一次**。

---

## 文案与报纸：从 xlsx 到游戏

`documents/第一章文案.xlsx` 是文案的唯一来源（团队文档，改表不改代码）：

| 表 | 内容 | 生成的 C# |
|---|---|---|
| 关卡流程对话 | VN01–VN03（开场/进教堂后/结案） | `Chapter1Dialogue.cs` |
| 配方合成完成对话 | VN04–VN25（每次调查/问话后） | 同上 |
| 报纸新闻 | 3 篇 | `Chapter1Newspapers.cs` |

重新生成需要跑仓库里的解析脚本（Python + openpyxl 读不出这个文件，走的是裸 XML 解析）：

```powershell
python <脚本>   # 解析 xlsx → 写 Chapter1Dialogue.cs / Chapter1Newspapers.cs
```

生成后执行 `Build Chapter1 Case Asset (overwrite)` 把数据灌进 asset。

> 表里的「配图」列（信封 / 信件 / 染血的衣服 / 祭坛彩绘）**还没接**，剧情只用了立绘。

---

## 美术接入

- UI 骨架仍由代码构建（`UIFactory.cs`），正式素材从 **`Assets/Resources/Art/`** 取，
  取不到就回落到程序化占位——素材可以一张张接，不会图没到就白屏；
- 接新图两步：放进 `Resources/Art/` → 执行菜单 **`CoolingGame/Apply Art Import Settings`**
  （设 Sprite、写九宫格 border、立绘开 Read/Write 给 alpha 点击判定用）；
- 新图要在 `Assets/Editor/ArtImportSettings.cs` 的清单里登记一行。

| 路径 | 用途 |
|---|---|
| `Art/desk` | 桌面背景 |
| `Art/ui/card_back` | 卡背 |
| `Art/ui/action_panel` | 灯下浮现的操作框（九宫格） |
| `Art/ui/dialog` | 对话框 / 桌面提示（裁到可见区，九宫格） |
| `Art/ui/dialog_full` | 对话框原图（备份，带大透明边） |
| `Art/char/mp01_*` `mp02_*` | 两个主角的默认/微笑/质问立绘（带 alpha，开 Read/Write） |
| `Art/char/p01 / p06 / p07 / p08` | 人物卡面 |
| `Art/item/*` | 道具插画（只给语义对得上的卡用，不要硬塞） |

美术需求统一走腾讯文档的**游戏美术提需表**，不在聊天里口头提。

---

## 测试

45 个 EditMode 测试：

```bash
# 编辑器内：Window > General > Test Runner → EditMode → Run All
# 命令行（先关编辑器）
unity test "D:\Desktop\Unity Projects\CoolingGame" --mode EditMode --output Temp\test-results.xml
```

覆盖：核心机制（配方/上下文/flag/时间）、笔记本、问题页、阶段与场景、第一章数据完整性
（引用必须能解析、每条线索必须有产出、主角不能出现在卡牌表里、按最短路径能通关）。

---

## 代码约定

- `Assets/Scripts/Core` **不允许引用 UnityEngine**（纯 C#，保证可测、可换表现层）。
- 规则判定只写在 Core；表现层只读数据、只发事件。
- 新增可配置内容一律走 `CaseDefinition` 的结构，**不要为单个案件写特判代码**。
- 剧情/报纸正文由 xlsx 生成，**不要手写进 C#**。

---

## 已知缺口

| 缺什么 | 影响 | 谁补 |
|---|---|---|
| 卡牌边框素材 | 卡面还是程序画的圆角 | 美术（提需表） |
| 场景背景图（5 个场景） | 切场景只换顶部标签，桌面永远是同一张木纹 | 美术 |
| 卡牌插画（29 张里 9 张有图） | 其余是"物/人/地"文字卡 | 美术 |
| 哈罗德神父立绘 | 他的台词只有文字没有立绘 | 美术 |
| 问题页槽位素材 | 槽位还是程序画的圆角矩形 | 美术 |
| 存档 / 主菜单 | 关掉就重来，没有标题界面 | 程序（下一步） |
| 剧情「配图」列 | 信封、染血衣服、祭坛彩绘等插图未接 | 程序+美术 |
| 表情差分 | 微笑/质问的 key 已接，但还没按台词切换 | 程序 |
