# 《迷城双探》（暂定名）— 卡牌解谜侦探游戏

一款以**卡牌**为主要表达的解谜推理游戏：场景、道具、人物都是卡牌，线索、推理、情境信息记在笔记本里。
玩家点卡 → 选行动 → 附加卡牌 → 命中配方 → 产出新卡 / 线索 / 情节 → 在问题页五格结案。

目前仓库里是**核心机制切片**（一条完整可玩的推理线），内容与美术是占位的。

---

## 环境要求

| 项 | 版本 / 说明 |
|---|---|
| Unity | **6000.6.5f1**（Unity 6，URP） |
| 平台 | PC |
| Git LFS | **必须安装**，仓库里的图片/PDF/音视频都走 LFS |

> 没装 LFS 直接 clone 会只拿到指针文件（几行文本），图片和 PDF 打不开。
> 安装后执行 `git lfs install`，再 `git clone`；已经 clone 过的补一句 `git lfs pull`。

```bash
git lfs install
git clone https://github.com/CassielFang/CoolingGame.git
```

---

## 第一次跑起来

1. 用 Unity Hub 打开仓库根目录（引擎版本对齐 6000.6.5f1）。
2. 首次打开会导入资源，等它跑完。
3. 打开场景 **`Assets/Scenes/DeskDemo.unity`**。
4. Game 视图设为 **16:9（1920×1080）**，点 Play。

演示怎么点、每步该看到什么，全写在 [documents/演示脚本.md](documents/演示脚本.md) 里，照着点即可复现整条推理线。

---

## 目录结构

```
Assets/
  Scripts/
    Core/                  纯 C# 逻辑层，不依赖 UnityEngine，可单独跑测试
      CaseSession.cs       案件会话：行动、配方判定、卡牌产出、结案
      NotebookModel.cs     笔记本：线索去重 / 置顶 / 搜索 / 未读
      QuestionPage.cs      问题页五卡槽与判定
      CaseDefinition.cs    「关卡表」根结构
      CardDefinition.cs / ClueDefinition.cs / RecipeDefinition.cs / StoryNodeDefinition.cs
      ActionKind.cs        三种行动：调查 / 交谈 / 前往
      Samples/             代码里构建的示例案件（生成资产用）
    Runtime/               表现层
      CaseDefinitionAsset.cs  案件数据的 ScriptableObject 载体
      Presentation/
        DeskDemoController.cs 桌面交互主控（点卡、行动面板、配方反馈）
        NotebookPanel.cs      笔记本界面（线索页 / 问题页）
        StoryOverlay.cs       情节演出（视觉小说 / 报纸）
        CardView.cs / UIFactory.cs  卡牌视图与 UI 构建
  Resources/Cases/
    SampleManorCase.asset  >>> 策划配内容就在这个文件里 <<<
  Scenes/DeskDemo.unity    演示场景
  Tests/EditMode/          26 个 EditMode 测试
documents/                 演示脚本、策划草案、MDA、参考截图
```

---

## 策划：怎么配内容（不用改代码）

**入口：`Assets/Resources/Cases/SampleManorCase.asset`**（Inspector 里直接编辑）

一个案件 = 一张「关卡表」，包含六张子表：

### 1. Cards（卡牌）

| 字段 | 说明 |
|---|---|
| `Id` | 唯一 ID，**其它表全靠它关联**，建议 `loc_/item_/per_` 前缀 |
| `DisplayName` | 牌面显示名 |
| `Category` | Location / Item / Person / Clue / Action / Other，决定边框样式 |
| `ArtKey` | 美术资源键，可空（美术接入前留空） |
| `Description` | 查看卡时的说明，可空 |
| `ClueId` | **翻开这张卡时自动入档的线索**——"线索卡"就是靠它 |
| `Tags` | 配方按标签匹配时用，如 `person`、`locked` |

### 2. Clues（线索）

| 字段 | 说明 |
|---|---|
| `Id` | 唯一 ID |
| `Text` | 玩家真正读到的正文 |
| `Keywords` | 检索关键词（和 `Text` 一起参与笔记本搜索） |
| `SourceCardId` / `SourceCardName` | 来源卡，留空则运行时用实际产出卡补齐 |

### 3. Recipes（配方）—— 玩法的全部内容都在这

一条配方 = 一次「卡牌(+附加卡) + 行动」能产出什么。

| 字段 | 说明 |
|---|---|
| `Action` | Investigate 调查 / Talk 交谈 / Go 前往 |
| `PrimaryCardId` | 行动对象卡 ID（精确匹配） |
| `PrimaryCardTag` | 按标签匹配（如任意 `person`）；`PrimaryCardId` 留空时生效 |
| `RequiredContextCardIds` | 需要放进附加卡槽的卡，**数量决定 UI 有几个卡槽**，必须完全一致 |
| `RequiredFlags` | 前置情节 flag，没满足时按钮是灰的 |
| `OutputCardIds` | 产出的新卡（以**背面朝上**出现，玩家自己翻） |
| `OutputClueIds` | 直接写入笔记本的线索 |
| `SetFlags` | 命中后置位的情节 flag |
| `StoryNodeId` | 命中后播放的情节节点，可空 |
| `TimeCost` | 时间消耗（时间系统默认关闭） |
| `ConsumeContextCards` | 命中后是否吃掉附加卡 |

**多个配方同时命中时取最具体的那个**，精度：精确卡牌 4 分 > 标签 2 分，附加卡与前置每个 +1（各上限 3）。

### 4. StartingCardIds（开局手牌）
开局正面朝上的卡，按 ID 列。

### 5. QuestionSlots（问题页五格）

每格：`Kind`（Case/凶手/凶器/物证/人证）、`Label`、`Hint`、`AcceptedCardIds`（**正确答案**）、`AcceptsAnyCard`。
五格填满 = 完成；**全部填对才算破案**，填错会给"推理有误"。

### 6. StoryNodes（情节节点）

`Id`、`Title`、`Body`（可空行分段）、`Presentation`（VisualNovel / Newspaper / Comic / Cg）、`SetFlagsOnPlay`。

### 新增一条推理线的标准流程

1. `Cards` 里加卡（含 `ClueId` 如果翻牌就该出线索）；
2. `Clues` 里写线索正文和关键词；
3. `Recipes` 里加配方，把「卡 + 行动 + 附加卡」连到「新卡 / 线索 / flag / 情节」；
4. 需要前置就先在某条配方里 `SetFlags`，再在后续配方 `RequiredFlags` 里引用；
5. 答案卡放进对应 `QuestionSlots.AcceptedCardIds`；
6. 进 Play 跑一遍。Content 有问题时 `SampleCase_HasNoDataWarnings` 测试会报数据缺失（引用了不存在的 ID 等）。

---

## 美术：现状与入口

- 目前 **UI 全部由代码构建**（`UIFactory.cs`），卡面还没有正式美术资源，`ArtKey` 字段已预留。
- 美术需求统一走腾讯文档的**游戏美术提需表**，不要在聊天里口头提。

---

## 文档

| 内容 | 链接 |
|---|---|
| 开发进度表（分工 / 进度追踪 / 会议安排） | https://docs.qq.com/sheet/DTk90RGtvc1JVaUJ2 |
| 游戏策划草案（玩法与机制） | https://docs.qq.com/sheet/DTkpmbGV6dnNsVWFU?tab=000002 |
| 游戏美术提需表 | https://docs.qq.com/sheet/DTnpOV0JZekdTQnFB?tab=000001 |
| 游戏程序提需表（含 shader 需求） | https://docs.qq.com/sheet/DTkhTdkhoQkpOZmN6?tab=000001 |
| 《迷城双探》台本文档（故事与设定） | https://docs.qq.com/doc/DTmVnUXhkUmtXREdz |

---

## 测试

Core 层有 26 个 EditMode 测试（卡牌/配方/时间预算/结案、笔记本/问题页）：

- 编辑器内：`Window > General > Test Runner` → EditMode → Run All
- 命令行：先**关闭编辑器**（同一项目不能同时被两个编辑器打开），再

```bash
unity test "D:\Desktop\Unity Projects\CoolingGame" --mode EditMode --output Temp\test-results.xml
```

---

## 代码约定

- `Assets/Scripts/Core` **不允许引用 UnityEngine**（纯 C#，保证可测、可换表现层）。
- 规则判定只写在 Core；表现层只读数据、只发事件。
- 新增可配置内容一律走 `CaseDefinition` 的结构，**不要为单个案件写特判代码**。
