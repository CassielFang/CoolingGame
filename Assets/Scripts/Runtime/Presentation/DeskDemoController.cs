using System;
using System.Collections;
using System.Collections.Generic;
using CoolingGame.Core;
using CoolingGame.Runtime;
using CoolingGame.Core.Samples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CoolingGame.Presentation
{
    /// <summary>
    /// 核心机制 demo 的主控制器：桌面 + 卡牌 + 行动面板（点卡→行动按钮→卡槽→确认）
    /// + 新卡翻面 + 笔记本（线索页/问题页）。
    /// 所有规则都委托给 <see cref="CaseSession"/>，这里只做表现与输入。
    /// </summary>
    public sealed class DeskDemoController : MonoBehaviour
    {
        [SerializeField] private CaseDefinitionAsset caseAsset;

        private CaseDefinition _definition;
        private CaseSession _session;

        private RectTransform _cardGrid;
        private RectTransform _cardLayer;
        private RectTransform _shadowLayer;
        private RectTransform _detectivePanel;
        private Image _detectivePortrait;
        private Image _detectiveShadow;
        private Text _detectiveName;
        private RectTransform _lampZone;
        private Button _lampAction;
        private string _draggingCardId;
        private readonly List<string> _lampCards = new List<string>();
        private readonly Dictionary<string, Vector2> _cardPositions = new Dictionary<string, Vector2>();

        // 叠放顺序：越晚放下的越靠上。列表末尾 = 最上层。
        private readonly List<string> _cardOrder = new List<string>();
        private RectTransform _actionPanel;
        private Button _notebookButton;
        private Text _timeLabel;
        private Text _stageLabel;
        private Text _toastLabel;
        private CanvasGroup _toastGroup;
        private NotebookPanel _notebook;
        private StoryOverlay _story;
        private DialogueOverlay _dialogue;
        private NewspaperOverlay _newspaper;
        private CinematicDirector _cinematic;
        private Coroutine _toastRoutine;

        private string _selectedCardId;
        private ActionKind _selectedAction;
        private bool _actionSelected;
        private bool _panelOpen;
        private bool _actionPanelHasArt;
        private readonly List<string> _context = new List<string>();

        public CaseSession Session
        {
            get { return _session; }
        }

        private void Start()
        {
            _definition = ResolveDefinition();
            ApplyDialogueOverride(_definition);
            StartNewSession();
            BuildUi();
            _notebook.Bind(_session);
            RefreshAll();

            DialogueScript opening = _session.Case.FindDialogue(_session.Case.OpeningDialogueId);
            if (opening != null && _dialogue != null)
            {
                // 开局先放剧情，放完再落回桌面
                _dialogue.Play(opening, () => ShowToast("把资料拖到灯下，就能开始查案。", true));
            }
            else
            {
                ShowToast("把资料拖到灯下，就能开始查案。", true);
            }
        }

        /// <summary>
        /// 剧本优先读 Resources/Cases/Chapter1Dialogues.json：
        /// 这样改台词只用改 JSON（剧本编辑器直接写），不必重新生成 C# 再编译。
        /// </summary>
        private static void ApplyDialogueOverride(CaseDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            TextAsset json = Resources.Load<TextAsset>("Cases/Chapter1Dialogues");
            if (json == null || string.IsNullOrEmpty(json.text))
            {
                return;
            }

            DialogueLibrary library = JsonUtility.FromJson<DialogueLibrary>(json.text);
            if (library == null || library.scripts == null || library.scripts.Count == 0)
            {
                return;
            }

            definition.Dialogues = library.scripts;
        }

        private CaseDefinition ResolveDefinition()
        {
            if (caseAsset != null)
            {
                return caseAsset.ToDefinition();
            }

            CaseDefinitionAsset resource = Resources.Load<CaseDefinitionAsset>("Cases/SampleManorCase");
            if (resource != null)
            {
                return resource.ToDefinition();
            }

            return SampleManorCase.Build();
        }

        private void StartNewSession()
        {
            if (_session != null)
            {
                _session.StageEntered -= OnStageEntered;
                _session.SceneChanged -= OnSceneChanged;
                _session.CaseFilled -= OnCaseFilled;
                _session.DetectiveChanged -= OnDetectiveChanged;
                _session.NewspaperTriggered -= OnNewspaperTriggered;
            }

            _session = new CaseSession(_definition);
            _session.StageEntered += OnStageEntered;
            _session.SceneChanged += OnSceneChanged;
            _session.CaseFilled += OnCaseFilled;
            _session.DetectiveChanged += OnDetectiveChanged;
            _session.NewspaperTriggered += OnNewspaperTriggered;

            _selectedCardId = null;
            _context.Clear();
            _actionSelected = false;
            _panelOpen = false;
        }

        /// <summary>结案（或推理错误）时的一次性高光。</summary>
        private void OnCaseFilled(bool correct)
        {
            if (_cinematic == null)
            {
                return;
            }

            _cinematic.Flash(
                correct ? new Color(1f, 0.97f, 0.9f) : new Color(0.85f, 0.25f, 0.2f),
                correct ? 0.5f : 0.34f,
                0.7f);
        }

        /// <summary>玩家在结案页点下「结案」且全对：播结局演出。</summary>
        private void ShowClosingStory()
        {
            // 先播结案剧情（VN03），放完再看有没有旧的情节节点
            if (_dialogue != null)
            {
                DialogueScript closing = _session.Case.FindDialogue(_session.Case.ClosingDialogueId);
                if (closing != null)
                {
                    _dialogue.Play(closing, ShowClosingStoryNode);
                    return;
                }
            }

            ShowClosingStoryNode();
        }

        private void ShowClosingStoryNode()
        {
            if (_story == null)
            {
                return;
            }

            string closingId = _session.Case.ClosingStoryNodeId;
            if (string.IsNullOrEmpty(closingId))
            {
                return;
            }

            StoryNodeDefinition node = _session.GetStoryNode(closingId);
            if (node != null)
            {
                _cinematic.Transition(() => _story.Show(node, RefreshAll), 0.3f, 0.2f, 0.6f);
            }
        }

        /// <summary>阶段切换：清牌/换场景/换问题页都由 Core 做完，这里只重画并播演出。</summary>
        private void OnStageEntered(StageDefinition stage)
        {
            if (stage == null)
            {
                RefreshAll();
                return;
            }

            Action apply = () =>
            {
                if (_notebook != null && _notebook.Visible)
                {
                    _notebook.Hide();
                }

                RefreshAll();

                // 阶段开场剧情优先于旧的情节弹窗
                if (!string.IsNullOrEmpty(stage.EnterDialogueId) && _dialogue != null)
                {
                    DialogueScript enter = _session.Case.FindDialogue(stage.EnterDialogueId);
                    if (enter != null)
                    {
                        _dialogue.Play(enter, null);
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(stage.StoryNodeId) && _story != null)
                {
                    StoryNodeDefinition node = _session.GetStoryNode(stage.StoryNodeId);
                    if (node != null)
                    {
                        _story.Show(node, RefreshAll);
                        return;
                    }
                }

                ShowToast(string.Format("{0} · {1}", stage.DisplayName, "开始调查"), true);
            };

            if (_cinematic != null)
            {
                _cinematic.Transition(apply);
                _cinematic.PulseVignette(0.16f, 0.9f);
            }
            else
            {
                apply();
            }
        }

        private void OnSceneChanged(string sceneId)
        {
            RefreshHud();

            SceneDefinition scene = _session.CurrentScene;
            if (scene != null)
            {
                ShowToast(string.Format("来到「{0}」。", scene.DisplayName), true);
            }
        }

        // ── 搭 UI ───────────────────────────────────────────────────────────

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;

            RectTransform background = UIFactory.MakePanel(root, "Desk", Color.white, false);
            UIFactory.Stretch(background);
            var backgroundImage = background.GetComponent<Image>();
            Sprite deskArt = ArtLibrary.Desk;
            if (deskArt != null)
            {
                backgroundImage.sprite = deskArt;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.color = Color.white;
            }
            else
            {
                backgroundImage.sprite = UIFactory.Wood;
                backgroundImage.type = Image.Type.Tiled;
            }

            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;
            backgroundButton.onClick.AddListener(() =>
            {
                if (_panelOpen)
                {
                    CloseActionPanel();
                }
            });

            RectTransform vignette = UIFactory.MakePanel(root, "Vignette", Color.white, false);
            UIFactory.Stretch(vignette);
            var vignetteImage = vignette.GetComponent<Image>();
            vignetteImage.sprite = UIFactory.Vignette;
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.raycastTarget = false;

            BuildHeader(root);
            // 灯下区域是桌面的一部分，卡片要压在它上面
            BuildLampZone(root);
            BuildCardGrid(root);
            // 灯下浮现的操作要在最上层，否则会被卡片挡住
            _lampAction.transform.SetAsLastSibling();
            BuildDetectivePanel(root);
            BuildActionPanel(root);
            BuildToast(root);

            _notebook = NotebookPanel.Create(root, ShowToast, ShowClosingStory);
            _story = StoryOverlay.Create(root);
            _dialogue = DialogueOverlay.Create(root);
            _newspaper = NewspaperOverlay.Create(root);

            // 最上层：颗粒 / 暗角 / 闪光 / 黑场转场
            _cinematic = CinematicDirector.Create(root);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private void BuildHeader(Transform root)
        {
            // 桌上没有横条：左上角压一张卷宗标签，写着案子名和所在地
            RectTransform label = UIFactory.MakePanel(root, "CaseLabel", new Color(0.09f, 0.07f, 0.055f, 0.70f));
            label.anchorMin = new Vector2(0f, 1f);
            label.anchorMax = new Vector2(0f, 1f);
            label.pivot = new Vector2(0f, 1f);
            label.anchoredPosition = new Vector2(26f, -22f);
            label.sizeDelta = new Vector2(420f, 82f);

            Text title = UIFactory.MakeText(label, "Title", _definition.Title, 21, Palette.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(title.rectTransform, 16, 6, 388, 32);

            _stageLabel = UIFactory.MakeText(label, "Stage", string.Empty, 16, Palette.Gold,
                TextAnchor.MiddleLeft);
            UIFactory.PlaceLeftTop(_stageLabel.rectTransform, 16, 42, 388, 26);

            _timeLabel = UIFactory.MakeText(label, "Time", string.Empty, 15,
                new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.62f), TextAnchor.MiddleRight);
            UIFactory.PlaceRightTop(_timeLabel.rectTransform, 16, 8, 180, 24);

            // 右上角两个小物件，没有底条
            _notebookButton = UIFactory.MakeButton(root, "NotebookButton", "笔记本", () => OpenNotebook(0),
                Palette.Paper, Palette.Ink, 18);
            RectTransform notebookRect = _notebookButton.GetComponent<RectTransform>();
            notebookRect.anchorMin = new Vector2(1f, 1f);
            notebookRect.anchorMax = new Vector2(1f, 1f);
            notebookRect.pivot = new Vector2(1f, 1f);
            notebookRect.anchoredPosition = new Vector2(-196f, -24f);
            notebookRect.sizeDelta = new Vector2(156f, 48f);

            Button restart = UIFactory.MakeButton(root, "Restart", "重新开始", Restart, Palette.Red,
                Palette.Paper, 18);
            RectTransform restartRect = restart.GetComponent<RectTransform>();
            restartRect.anchorMin = new Vector2(1f, 1f);
            restartRect.anchorMax = new Vector2(1f, 1f);
            restartRect.pivot = new Vector2(1f, 1f);
            restartRect.anchoredPosition = new Vector2(-26f, -24f);
            restartRect.sizeDelta = new Vector2(156f, 48f);
        }

        private void BuildCardGrid(Transform root)
        {
            // 整张桌面就是牌桌：不用 ScrollRect，否则牌出了可视区会被裁掉，
            // 看起来就是「只有中间那块能放」。
            _cardGrid = UIFactory.MakePanel(root, "DeskLayer", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(_cardGrid);
            _cardGrid.GetComponent<Image>().raycastTarget = false;

            // 两层容器：影子永远在所有卡片下面，卡片之间不会互相盖影子
            _shadowLayer = UIFactory.MakePanel(_cardGrid, "Shadows", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(_shadowLayer);
            _cardLayer = UIFactory.MakePanel(_cardGrid, "Cards", new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(_cardLayer);
        }

        private void BuildActionPanel(Transform root)
        {
            _actionPanel = UIFactory.MakePanel(root, "ActionPanel", new Color(0.11f, 0.085f, 0.065f, 1f));
            _actionPanel.anchorMin = new Vector2(0.5f, 0f);
            _actionPanel.anchorMax = new Vector2(0.5f, 0f);
            _actionPanel.pivot = new Vector2(0.5f, 0f);
            _actionPanel.anchoredPosition = new Vector2(0f, 26f);
            _actionPanel.sizeDelta = new Vector2(1200f, 330f);
            _actionPanel.gameObject.SetActive(false);

            Sprite panelArt = ArtLibrary.ActionPanel;
            if (panelArt != null)
            {
                var image = _actionPanel.GetComponent<Image>();
                image.sprite = panelArt;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                _actionPanelHasArt = true;
            }
        }

        /// <summary>办案人：站在桌边的人，不是桌上的卡。点立绘或按钮切换。</summary>
        private void BuildDetectivePanel(Transform root)
        {
            _detectivePanel = UIFactory.MakePanel(root, "Detective", new Color(0f, 0f, 0f, 0f), false);
            _detectivePanel.anchorMin = new Vector2(0f, 0f);
            _detectivePanel.anchorMax = new Vector2(0f, 0f);
            _detectivePanel.pivot = new Vector2(0f, 0f);
            _detectivePanel.anchoredPosition = new Vector2(16f, 14f);
            _detectivePanel.sizeDelta = new Vector2(640f, 940f);
            // 面板不接收点击：点击交给立绘的 alpha 判定，空白处要能点到底下的卡
            _detectivePanel.GetComponent<Image>().raycastTarget = false;

            // 立体感：立绘背后投一层暗影（2.5D 的「人站在桌边」）
            var shadowGo = new GameObject("PortraitShadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(_detectivePanel, false);
            var shadowRect = (RectTransform)shadowGo.transform;
            shadowRect.anchorMin = new Vector2(0.5f, 0f);
            shadowRect.anchorMax = new Vector2(0.5f, 0f);
            shadowRect.pivot = new Vector2(0.5f, 0f);
            shadowRect.anchoredPosition = new Vector2(16f, 84f);
            shadowRect.sizeDelta = new Vector2(500f, 680f);
            _detectiveShadow = shadowGo.GetComponent<Image>();
            _detectiveShadow.preserveAspect = true;
            _detectiveShadow.raycastTarget = false;
            _detectiveShadow.color = new Color(0f, 0f, 0f, 0.30f);

            var artGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(_detectivePanel, false);
            var artRect = (RectTransform)artGo.transform;
            artRect.anchorMin = new Vector2(0.5f, 0f);
            artRect.anchorMax = new Vector2(0.5f, 0f);
            artRect.pivot = new Vector2(0.5f, 0f);
            artRect.anchoredPosition = new Vector2(0f, 96f);
            artRect.sizeDelta = new Vector2(600f, 820f);

            _detectivePortrait = artGo.GetComponent<Image>();
            _detectivePortrait.preserveAspect = true;
            _detectivePortrait.raycastTarget = true;

            var portraitButton = artGo.AddComponent<Button>();
            portraitButton.transition = Selectable.Transition.None;
            portraitButton.onClick.AddListener(SwitchDetective);

            _detectiveName = UIFactory.MakeText(_detectivePanel, "Name", string.Empty, 26, Palette.Paper,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.PlaceLeftBottom(_detectiveName.rectTransform, 0f, 58f, 640f, 36f);

            Button switchButton = UIFactory.MakeButton(_detectivePanel, "Switch", "切换办案人", SwitchDetective,
                Palette.PaperDark, Palette.Ink, 18);
            UIFactory.PlaceLeftBottom(switchButton.GetComponent<RectTransform>(), 220f, 12f, 200f, 40f);
        }

        private void RefreshDetective()
        {
            if (_detectiveName == null || _session == null)
            {
                return;
            }

            DetectiveDefinition detective = _session.ActiveDetective;
            if (detective == null)
            {
                _detectiveName.text = string.Empty;
                if (_detectivePortrait != null)
                {
                    _detectivePortrait.enabled = false;
                }

                return;
            }

            _detectiveName.text = detective.DisplayName;

            Sprite art = ArtLibrary.LoadCardArt(detective.PortraitArtKey);
            _detectivePortrait.sprite = art;
            _detectivePortrait.enabled = art != null;

            if (_detectiveShadow != null)
            {
                _detectiveShadow.sprite = art;
                _detectiveShadow.enabled = art != null;
            }

            // 透明像素不接收点击：贴图可读时才生效（导入设置里立绘开了 Read/Write）
            if (art != null && art.texture != null && art.texture.isReadable)
            {
                _detectivePortrait.alphaHitTestMinimumThreshold = 0.2f;
            }
        }

        private void SwitchDetective()
        {
            if (_session == null || _session.Case.Detectives == null || _session.Case.Detectives.Count < 2)
            {
                ShowToast("这个案件只有一位办案人。", false);
                return;
            }

            IReadOnlyList<DetectiveDefinition> detectives = _session.Case.Detectives;
            int index = 0;
            for (int i = 0; i < detectives.Count; i++)
            {
                if (detectives[i] != null && detectives[i].Id == _session.ActiveDetectiveId)
                {
                    index = i;
                    break;
                }
            }

            DetectiveDefinition next = detectives[(index + 1) % detectives.Count];
            if (next == null)
            {
                return;
            }

            if (_session.SetDetective(next.Id))
            {
                ShowToast("现在由「" + next.DisplayName + "」办案。", true);
            }
        }

        private void OnNewspaperTriggered(string newspaperId)
        {
            if (_newspaper == null)
            {
                return;
            }

            NewspaperDefinition paper = _session.Case.FindNewspaper(newspaperId);
            if (paper != null)
            {
                _newspaper.Show(paper, null);
            }
        }

        private void OnDetectiveChanged(DetectiveDefinition detective)
        {
            RefreshDetective();

            if (_cinematic != null)
            {
                _cinematic.PulseVignette(0.08f, 0.45f);
            }
        }

        /// <summary>
        /// 灯下区域：桌面上一块被台灯照亮的地方。资料拖进光里就等于「拿到灯下细看」。
        /// 单张 → 浮现「调查」；多张 → 浮现「比对」。操作浮在牌上方，不是固定面板。
        /// </summary>
        private void BuildLampZone(Transform root)
        {
            _lampZone = UIFactory.MakePanel(root, "LampZone", new Color(1f, 0.93f, 0.80f, 0.34f), false);
            _lampZone.anchorMin = new Vector2(0.5f, 0.5f);
            _lampZone.anchorMax = new Vector2(0.5f, 0.5f);
            _lampZone.pivot = new Vector2(0.5f, 0.5f);
            _lampZone.anchoredPosition = new Vector2(0f, -250f);
            _lampZone.sizeDelta = new Vector2(880f, 880f);

            var image = _lampZone.GetComponent<Image>();
            image.sprite = UIFactory.LightPool;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            Text hint = UIFactory.MakeText(_lampZone, "Hint", "把资料拖到灯下", 17,
                new Color(1f, 0.95f, 0.85f, 0.38f), TextAnchor.LowerCenter);
            UIFactory.PlaceLeftBottom(hint.rectTransform, 0f, 16f, 840f, 28f);

            // 操作浮现用美术的「操作框」：手绘纸条 + 红胶带，而不是一块纯色矩形
            var actionGo = new GameObject("LampAction", typeof(RectTransform), typeof(Image), typeof(Button));
            actionGo.transform.SetParent(root, false);
            var actionRect = (RectTransform)actionGo.transform;
            actionRect.anchorMin = new Vector2(0.5f, 0.5f);
            actionRect.anchorMax = new Vector2(0.5f, 0.5f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionRect.anchoredPosition = new Vector2(0f, 40f);
            actionRect.sizeDelta = new Vector2(600f, 150f);

            var actionImage = actionGo.GetComponent<Image>();
            Sprite panelArt = ArtLibrary.LoadCardArt("ui/action_panel");
            if (panelArt != null)
            {
                actionImage.sprite = panelArt;
                actionImage.type = Image.Type.Sliced;
                actionImage.color = Color.white;
            }
            else
            {
                actionImage.color = Palette.Gold;
            }

            _lampAction = actionGo.GetComponent<Button>();
            _lampAction.transition = Selectable.Transition.None;
            _lampAction.onClick.AddListener(RunLampAction);

            Text actionLabel = UIFactory.MakeText(actionRect, "Label", "调查", 26, Palette.Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(actionLabel.rectTransform, 110, 26, 28, 26);

            _lampAction.gameObject.SetActive(false);
        }

        /// <summary>重新判定灯下有哪些牌，并浮现对应操作。</summary>
        private void RefreshLampZone()
        {
            if (_lampZone == null || _lampAction == null)
            {
                return;
            }

            _lampCards.Clear();

            Vector2 lampCenter = _lampZone.position;
            // 判定范围跟着光斑的可见亮区走（比内圈宽一点），牌压在光斑边上也算「在灯下」
            float radius = _lampZone.rect.width * 0.30f;

            for (int i = 0; i < _cardLayer.childCount; i++)
            {
                var rect = (RectTransform)_cardLayer.GetChild(i);
                var view = rect.GetComponent<CardView>();
                if (view == null)
                {
                    continue;
                }

                if (Vector2.Distance(rect.position, lampCenter) <= radius)
                {
                    _lampCards.Add(view.CardId);
                }
            }

            if (_lampCards.Count == 0)
            {
                _lampAction.gameObject.SetActive(false);
                return;
            }

            if (_lampCards.Count == 1)
            {
                if (!HasAvailableAction(_lampCards[0], ActionKind.Investigate))
                {
                    _lampAction.gameObject.SetActive(false);
                    return;
                }

                CardState card = _session.GetCard(_lampCards[0]);
                SetLampActionLabel(card != null ? "调查「" + card.Definition.DisplayName + "」" : "调查");
                return;
            }

            SetLampActionLabel("比对 " + _lampCards.Count + " 张");
        }

        private void SetLampActionLabel(string label)
        {
            Text text = _lampAction.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }

            _lampAction.gameObject.SetActive(true);
        }

        private bool HasAvailableAction(string cardId, ActionKind action)
        {
            IReadOnlyList<ActionOption> options = _session.GetActions(cardId);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Action == action && options[i].IsAvailable)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>点灯下浮现的操作：单张调查，多张依次尝试组合。</summary>
        private void RunLampAction()
        {
            if (_lampCards.Count == 0)
            {
                return;
            }

            if (_lampCards.Count == 1)
            {
                HandleActionResult(_session.Perform(_lampCards[0], ActionKind.Investigate));
                return;
            }

            ActionResult firstFailure = null;
            for (int i = 0; i < _lampCards.Count; i++)
            {
                var context = new List<string>(_lampCards.Count - 1);
                for (int j = 0; j < _lampCards.Count; j++)
                {
                    if (j != i)
                    {
                        context.Add(_lampCards[j]);
                    }
                }

                ActionResult result = _session.Perform(_lampCards[i], ActionKind.Investigate, context.ToArray());
                if (result.Success)
                {
                    HandleActionResult(result);
                    return;
                }

                if (firstFailure == null)
                {
                    firstFailure = result;
                }
            }

            ShowToast(firstFailure != null ? firstFailure.Message : "这样似乎行不通。", false);
        }

        private void BuildToast(Transform root)
        {
            // 提示用美术的「对话框」贴纸，摆在立绘旁边，像莫里森在说话
            RectTransform toast = UIFactory.MakePanel(root, "Toast", new Color(0.86f, 0.82f, 0.74f, 1f));
            toast.anchorMin = new Vector2(0f, 0f);
            toast.anchorMax = new Vector2(0f, 0f);
            toast.pivot = new Vector2(0f, 0f);
            toast.anchoredPosition = new Vector2(452f, 26f);
            toast.sizeDelta = new Vector2(820f, 96f);

            var toastImage = toast.GetComponent<Image>();
            Sprite dialogArt = ArtLibrary.LoadCardArt("ui/dialog");
            if (dialogArt != null)
            {
                toastImage.sprite = dialogArt;
                toastImage.type = Image.Type.Sliced;
                toastImage.color = Color.white;
            }

            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            _toastLabel = UIFactory.MakeText(toast, "Label", string.Empty, 22, Palette.Ink,
                TextAnchor.MiddleLeft);
            UIFactory.Stretch(_toastLabel.rectTransform, 46, 16, 26, 18);
        }

        // ── 刷新 ────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshCards();
            RefreshHud();
            RefreshLampZone();
        }

        private void RefreshCards()
        {
            if (_cardGrid == null)
            {
                return;
            }

            // 重建前先把桌面上每张牌「现在」的位置采集回来。
            // 牌的位置在 RectTransform 上，不采集的话重建一次就跳回默认位。
            for (int i = 0; i < _cardLayer.childCount; i++)
            {
                var existing = _cardLayer.GetChild(i).GetComponent<CardView>();
                if (existing != null)
                {
                    _cardPositions[existing.CardId] = ((RectTransform)existing.transform).anchoredPosition;
                }
            }

            UIFactory.ClearChildren(_cardLayer);
            UIFactory.ClearChildren(_shadowLayer);

            IReadOnlyList<CardState> cards = _session.Cards;
            var visible = new List<CardState>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].IsConsumed)
                {
                    visible.Add(cards[i]);
                }
            }

            // 按「放下顺序」排：先放的先建、后放的叠在上面
            var ordered = new List<CardState>(visible.Count);
            for (int i = 0; i < _cardOrder.Count; i++)
            {
                for (int j = 0; j < visible.Count; j++)
                {
                    if (visible[j].Id == _cardOrder[i])
                    {
                        ordered.Add(visible[j]);
                        visible.RemoveAt(j);
                        break;
                    }
                }
            }

            ordered.AddRange(visible);
            visible = ordered;

            // 自由摆放：每张牌有自己的桌面位置，玩家可以随手拖开/叠起来。
            _cardGrid.sizeDelta = Vector2.zero;

            for (int i = 0; i < visible.Count; i++)
            {
                CardState card = visible[i];
                Vector2 position = ResolveCardPosition(card.Id, i);

                CardView view = CardView.Create(_cardLayer, _shadowLayer, card,
                    card.Id == _selectedCardId, OnCardClicked);
                var rect = (RectTransform)view.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(CardView.CardWidth, CardView.CardHeight);
                rect.anchoredPosition = position;

                // 静止时贴着桌面：没有投影。拿起才由 SetElevated 打开。
                view.SnapToTable();
                view.SetDragHandlers(OnCardDragStarted, OnCardMoved, OnCardDropped, OnCardDragEnded);
                view.SetDraggingHandler(RefreshLampZone);
            }
        }

        /// <summary>桌面可摆放范围：避开顶部信息栏与底部分析台。</summary>
        private static Vector2 ClampToDesk(Vector2 position)
        {
            position.x = Mathf.Clamp(position.x, -860f, 860f);
            position.y = Mathf.Clamp(position.y, -350f, 330f);
            return position;
        }

        /// <summary>取这张牌在桌面上的位置；新牌按「随手摊开」的默认位分配一次并记住。</summary>
        private Vector2 ResolveCardPosition(string cardId, int index)
        {
            Vector2 stored;
            if (_cardPositions.TryGetValue(cardId, out stored))
            {
                return ClampToDesk(stored);
            }

            const int perRow = 6;
            int column = index % perRow;
            int row = index / perRow;
            float jitterX = StableJitter(cardId, 1) * 30f;
            float jitterY = StableJitter(cardId, 2) * 22f;

            var position = ClampToDesk(new Vector2(
                (column - (perRow - 1) * 0.5f) * 186f + jitterX,
                300f - row * 248f + jitterY));

            _cardPositions[cardId] = position;
            return position;
        }

        /// <summary>按 cardId 生成稳定的抖动（不能用 string.GetHashCode，它在不同进程里会变）。</summary>
        private static float StableJitter(string id, int salt)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0f;
            }

            uint hash = 2166136261u;
            for (int i = 0; i < id.Length; i++)
            {
                hash ^= id[i];
                hash *= 16777619u;
            }

            hash ^= (uint)salt * 2654435761u;
            return (hash % 1000) / 1000f - 0.5f;
        }

        private void RefreshHud()
        {
            RefreshDetective();

            if (_stageLabel != null)
            {
                string scene = _session.CurrentScene != null ? _session.CurrentScene.DisplayName : "—";
                string stage = _session.CurrentStage != null ? _session.CurrentStage.DisplayName : "单阶段";
                _stageLabel.text = string.Format("{0} ｜ {1}", scene, stage);
            }

            if (_timeLabel != null)
            {
                _timeLabel.text = _session.TimeBudget >= 0
                    ? string.Format("时间 {0} / {1}", _session.TimeSpent, _session.TimeBudget)
                    : string.Format("行动 {0} 次", _session.TimeSpent);
            }

            if (_notebookButton != null)
            {
                Text label = _notebookButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = _session.Notebook.UnreadCount > 0
                        ? string.Format("笔记本 ({0})", _session.Notebook.UnreadCount)
                        : "笔记本";
                }
            }
        }

        // ── 卡牌交互（草案流程图） ──────────────────────────────────────────

        private void OnCardClicked(string cardId)
        {
            if (_notebook != null && _notebook.Visible)
            {
                return;
            }

            if (_story != null && _story.Visible)
            {
                return;
            }

            CardState card = _session.GetCard(cardId);
            if (card == null || card.IsConsumed)
            {
                return;
            }

            if (!card.IsFaceUp)
            {
                FlipCard(card);
                return;
            }

            if (_panelOpen && cardId != _selectedCardId && HasFreeSlot())
            {
                AddContext(cardId);
                return;
            }

            SelectCard(cardId);
        }

        private void FlipCard(CardState card)
        {
            CardView view = FindCardView(card.Id);
            if (view != null)
            {
                // 先翻转过去再换面：翻到侧面（scaleX≈0）时重建卡片，视觉上就是「翻过来」
                view.PlayReveal(() => RevealNow(card));
                return;
            }

            RevealNow(card);
        }

        private void RevealNow(CardState card)
        {
            int before = _session.Notebook.Count;
            if (!_session.Reveal(card.Id))
            {
                return;
            }

            RefreshCards();
            RefreshHud();

            int gained = _session.Notebook.Count - before;
            ShowToast(gained > 0
                    ? string.Format("翻开「{0}」，线索 +{1}。", card.Definition.DisplayName, gained)
                    : string.Format("翻开「{0}」。", card.Definition.DisplayName),
                true);
        }

        private void SelectCard(string cardId)
        {
            _selectedCardId = cardId;
            _context.Clear();

            // 行动靠拖拽；点击只是「拿起这张资料看看」，所以先把说明文字念出来
            CardState card = _session.GetCard(cardId);
            if (card != null && !string.IsNullOrEmpty(card.Definition.Description))
            {
                ShowToast(card.Definition.Description, true);
            }
            else if (_session.GetActions(cardId).Count == 0)
            {
                ShowToast("这张牌现在没有可用的行动。", false);
            }

            RefreshCards();
        }

        private int RequiredSlotCount()
        {
            if (!_actionSelected || string.IsNullOrEmpty(_selectedCardId))
            {
                return 0;
            }

            IReadOnlyList<ActionOption> options = _session.GetActions(_selectedCardId);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Action == _selectedAction)
                {
                    return options[i].ContextSlotCount;
                }
            }

            return 0;
        }

        private bool HasFreeSlot()
        {
            return _context.Count < RequiredSlotCount();
        }

        private void AddContext(string cardId)
        {
            if (_context.Contains(cardId))
            {
                ShowToast("这张卡已经在卡槽里了。", false);
                return;
            }

            _context.Add(cardId);
            BuildActionPanelContents();
            RefreshCards();
        }

        private void RemoveContext(int index)
        {
            if (index < 0 || index >= _context.Count)
            {
                return;
            }

            _context.RemoveAt(index);
            BuildActionPanelContents();
            RefreshCards();
        }

        private void ChooseAction(ActionKind action)
        {
            if (!_actionSelected || _selectedAction != action)
            {
                _selectedAction = action;
                _actionSelected = true;
                _context.Clear();
            }

            BuildActionPanelContents();
            RefreshCards();
        }

        // ── 行动面板 ────────────────────────────────────────────────────────

        private void BuildActionPanelContents()
        {
            if (_actionPanel == null)
            {
                return;
            }

            UIFactory.ClearChildren(_actionPanel);

            CardState primary = _session.GetCard(_selectedCardId);
            if (primary == null)
            {
                return;
            }

            int slots = RequiredSlotCount();
            _actionPanel.sizeDelta = new Vector2(1200f, slots > 0 ? 360f : 250f);

            // 操作框图左上角有红胶带，内容整体右移避开它
            const float contentLeft = 270f;

            Color titleColor = _actionPanelHasArt ? Palette.Ink : Palette.Paper;
            Text title = UIFactory.MakeText(_actionPanel, "Title",
                string.Format("对「{0}」采取行动", primary.Definition.DisplayName), 24, titleColor,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.PlaceLeftTop(title.rectTransform, contentLeft, 24, 700, 38);

            string hint = slots == 0
                ? "该行动不需要其他卡牌，确认后结算。"
                : (_context.Count < slots ? "点击桌面上的其他卡牌放入卡槽。" : "卡槽已填满，可以确认。");

            Color hintColor = _actionPanelHasArt
                ? new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.62f)
                : new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.66f);
            Text hintText = UIFactory.MakeText(_actionPanel, "Hint", hint, 17, hintColor, TextAnchor.MiddleRight);
            UIFactory.PlaceRightTop(hintText.rectTransform, 120, 24, 560, 38);

            IReadOnlyList<ActionOption> options = _session.GetActions(_selectedCardId);
            for (int i = 0; i < options.Count; i++)
            {
                ActionOption option = options[i];
                bool active = _actionSelected && option.Action == _selectedAction;
                string label = option.ContextSlotCount > 0
                    ? string.Format("{0} +{1}", option.Action.ToChinese(), option.ContextSlotCount)
                    : option.Action.ToChinese();

                ActionKind action = option.Action;
                Button button = UIFactory.MakeButton(_actionPanel, "Action_" + option.Action, label,
                    () => ChooseAction(action), active ? Palette.Gold : Palette.PaperDark, Palette.Ink, 21);
                button.interactable = option.IsAvailable;
                UIFactory.PlaceLeftTop(button.GetComponent<RectTransform>(), contentLeft + i * 214, 76, 200, 56);
            }

            for (int i = 0; i < slots; i++)
            {
                bool filled = i < _context.Count;
                string slotLabel = filled
                    ? _session.GetCard(_context[i]).Definition.DisplayName
                    : "＋\n空卡槽";

                int index = i;
                Button slotButton = UIFactory.MakeButton(_actionPanel, "Slot" + i, slotLabel,
                    () => RemoveContext(index),
                    filled ? Palette.Paper : new Color(0.28f, 0.23f, 0.18f),
                    filled ? Palette.Ink : new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.9f), 17);
                UIFactory.PlaceLeftTop(slotButton.GetComponent<RectTransform>(), contentLeft + i * 136, 150, 122, 112);
                slotButton.interactable = filled;
            }

            Button confirm = UIFactory.MakeButton(_actionPanel, "Confirm", "确认", ConfirmAction, Palette.Green,
                Palette.Paper, 22);
            UIFactory.PlaceRightBottom(confirm.GetComponent<RectTransform>(), 120, 44, 190, 54);

            Button cancel = UIFactory.MakeButton(_actionPanel, "Cancel", "取消", CloseActionPanel, Palette.PaperDark,
                Palette.Ink, 20);
            UIFactory.PlaceRightBottom(cancel.GetComponent<RectTransform>(), 328, 44, 140, 54);
        }

        private void CloseActionPanel()
        {
            _panelOpen = false;
            _selectedCardId = null;
            _context.Clear();
            _actionSelected = false;

            if (_actionPanel != null)
            {
                _actionPanel.gameObject.SetActive(false);
            }

            RefreshCards();
        }

        private void ConfirmAction()
        {
            if (!_panelOpen || string.IsNullOrEmpty(_selectedCardId))
            {
                return;
            }

            HandleActionResult(_session.Perform(_selectedCardId, _selectedAction, _context.ToArray()));
        }

        /// <summary>所有「行动结算」走同一条路：拖拽、分析台、调试都调用它。</summary>
        private void HandleActionResult(ActionResult result)
        {
            RefreshHud();

            if (result == null)
            {
                return;
            }

            if (!result.Success)
            {
                ShowToast(result.Message, false);
                if (_cinematic != null)
                {
                    _cinematic.PulseVignette(0.12f, 0.5f);
                }

                return;
            }

            bool sceneChanged = !string.IsNullOrEmpty(result.SceneId);
            if (sceneChanged && _cinematic != null)
            {
                // 场景切换：黑场里重建桌面，避免玩家看到卡牌瞬移
                _cinematic.Transition(() =>
                {
                    CloseActionPanel();
                    RefreshAll();
                });
            }
            else
            {
                CloseActionPanel();
                RefreshAll();
            }

            if (result.NewClues.Count > 0 && _cinematic != null)
            {
                _cinematic.Flash(new Color(1f, 0.93f, 0.78f), 0.38f, 0.5f);
            }

            ShowToast(result.Message, true);

            // 每次调查/问话后念一段对应的剧情（策划表 VN04–VN25）
            if (!string.IsNullOrEmpty(result.DialogueId) && _dialogue != null)
            {
                DialogueScript script = _session.Case.FindDialogue(result.DialogueId);
                if (script != null)
                {
                    _dialogue.Play(script, null);
                    return;
                }
            }

            StoryNodeDefinition node = string.IsNullOrEmpty(result.StoryNodeId)
                ? null
                : _session.GetStoryNode(result.StoryNodeId);

            if (node != null)
            {
                _story.Show(node, RefreshAll);
            }
        }

        // ── 拖拽：把卡拖到人物 / 地点 / 分析台 ─────────────────────────────

        /// <summary>
        /// 兜底：桌面位置以位置表为准。UI 重建/布局有时会把 RectTransform 改回旧值，
        /// 每帧纠正一次，玩家就不会看到牌「自己跳回去」。拖拽中的那张牌不纠正。
        /// </summary>
        private void LateUpdate()
        {
            if (_cardLayer == null || _session == null)
            {
                return;
            }

            for (int i = 0; i < _cardLayer.childCount; i++)
            {
                var view = _cardLayer.GetChild(i).GetComponent<CardView>();
                if (view == null || view.CardId == _draggingCardId)
                {
                    continue;
                }

                Vector2 wanted;
                if (!_cardPositions.TryGetValue(view.CardId, out wanted))
                {
                    continue;
                }

                var rect = (RectTransform)view.transform;
                if ((rect.anchoredPosition - wanted).sqrMagnitude > 1f)
                {
                    rect.anchoredPosition = wanted;
                }
            }
        }

        private void OnCardDragStarted(string cardId)
        {
            _draggingCardId = cardId;

            // 拿起来 = 放到最上面；放下后它就是最新放下的那张
            _cardOrder.Remove(cardId);
            _cardOrder.Add(cardId);

            for (int i = 0; i < _cardLayer.childCount; i++)
            {
                var view = _cardLayer.GetChild(i).GetComponent<CardView>();
                if (view == null)
                {
                    continue;
                }

                CardState state = _session.GetCard(view.CardId);
                if (state == null || !state.IsUsable)
                {
                    continue;
                }

                bool isTarget = state.Definition.Category == CardCategory.Person
                                || state.Definition.Category == CardCategory.Location;
                view.SetEdgeHighlight(isTarget);
            }
        }

        private void OnCardDragEnded()
        {
            _draggingCardId = null;

            for (int i = 0; i < _cardLayer.childCount; i++)
            {
                var view = _cardLayer.GetChild(i).GetComponent<CardView>();
                if (view != null)
                {
                    view.SetEdgeHighlight(false);
                }
            }
        }

        private void OnCardDropped(string cardId, Vector2 screenPosition, Vector2 localPosition)
        {
            // 只把人物/地点当落点；物品上放牌、放在灯下，都是单纯摆放
            string targetId = FindCardAt(screenPosition, cardId);
            if (targetId != null)
            {
                RestoreCardHome(cardId);
                DropOnCard(cardId, targetId);
                return;
            }

            // 落在桌面（或灯下、别的物品卡上）：位置已经由 OnCardMoved 记住，就停在这儿。
            // 灯下的牌会自动被 RefreshLampZone 收进「可调查/可比对」的集合。
        }

        /// <summary>把牌放在桌面某个位置（屏幕坐标），走完整的落点判定，用于自动化验证。</summary>
        public string DebugSimulateDrop(string cardId, float localX, float localY)
        {
            var local = new Vector2(localX, localY);
            Vector3 world = _cardLayer.TransformPoint(local);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);

            OnCardMoved(cardId, local);
            OnCardDropped(cardId, screen, local);

            CardView view = FindCardView(cardId);
            if (view == null)
            {
                return cardId + " missing";
            }

            Vector2 final = ((RectTransform)view.transform).anchoredPosition;
            return string.Format("{0} -> {1} (want {2})", cardId, final.ToString("F0"), local.ToString("F0"));
        }

        /// <summary>玩家把牌挪到哪儿，就记住哪儿（限制在桌面范围内）。</summary>
        private void OnCardMoved(string cardId, Vector2 localPosition)
        {
            localPosition = ClampToDesk(localPosition);
            _cardPositions[cardId] = localPosition;

            CardView view = FindCardView(cardId);
            if (view != null)
            {
                ((RectTransform)view.transform).anchoredPosition = localPosition;
            }

            // 牌在灯下进出，浮现的操作要跟着变
            RefreshLampZone();
        }

        private void RestoreCardHome(string cardId)
        {
            CardView view = FindCardView(cardId);
            if (view != null)
            {
                view.RestoreHome();
            }

            _cardPositions.Remove(cardId);
        }

        private CardView FindCardView(string cardId)
        {
            for (int i = 0; i < _cardLayer.childCount; i++)
            {
                var view = _cardLayer.GetChild(i).GetComponent<CardView>();
                if (view != null && view.CardId == cardId)
                {
                    return view;
                }
            }

            return null;
        }

        /// <summary>
        /// 松手位置下面压着哪张**能接收拖拽**的卡。
        /// 只有人物和地点才算落点——物品卡上放牌就是单纯摆放（叠上去），不触发任何东西。
        /// </summary>
        private string FindCardAt(Vector2 screenPosition, string excludeCardId)
        {
            for (int i = _cardLayer.childCount - 1; i >= 0; i--)
            {
                var rect = (RectTransform)_cardLayer.GetChild(i);
                var view = rect.GetComponent<CardView>();
                if (view == null || view.CardId == excludeCardId)
                {
                    continue;
                }

                CardState state = _session.GetCard(view.CardId);
                if (state == null
                    || (state.Definition.Category != CardCategory.Person
                        && state.Definition.Category != CardCategory.Location))
                {
                    continue;
                }

                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, null, out local))
                {
                    continue;
                }

                if (rect.rect.Contains(local / 0.7f))
                {
                    return view.CardId;
                }
            }

            return null;
        }

        /// <summary>把 A 拖到 B 上：B 是人物 → 拿着 A 问 B；B 是地点 → 拿着 A 去 B。</summary>
        private void DropOnCard(string draggedCardId, string targetCardId)
        {
            CardState target = _session.GetCard(targetCardId);
            if (target == null || !target.IsUsable)
            {
                ShowToast("那张牌现在还不能用。", false);
                return;
            }

            ActionKind action;
            switch (target.Definition.Category)
            {
                case CardCategory.Person:
                    action = ActionKind.Talk;
                    break;
                case CardCategory.Location:
                    action = ActionKind.Go;
                    break;
                default:
                    ShowToast("只能把牌拖到人物或地点上；想比对就拖到分析台。", false);
                    return;
            }

            // 先按「拿着 A 去问/去」试；这条配方不需要附加卡时，退回「空手去」再试一次
            ActionResult result = _session.Perform(targetCardId, action, new[] { draggedCardId });
            if (!result.Success
                && (result.Failure == ActionFailure.MissingContextCards
                    || result.Failure == ActionFailure.InvalidContextCard))
            {
                ActionResult plain = _session.Perform(targetCardId, action);
                if (plain.Success)
                {
                    result = plain;
                }
            }

            HandleActionResult(result);
        }

        // ── 分析台：把几张牌放一起比对 ─────────────────────────────────────

        // ── 笔记本 / 情节 / 重开 ────────────────────────────────────────────

        private void OpenNotebook(int tabIndex)
        {
            if (_notebook == null)
            {
                return;
            }

            CloseActionPanel();
            _notebook.Show(tabIndex);
        }

        private void Restart()
        {
            if (_story != null)
            {
                _story.gameObject.SetActive(false);
            }

            if (_notebook != null)
            {
                _notebook.Hide();
            }

            StartNewSession();
            _notebook.Bind(_session);
            RefreshAll();
            ShowToast("已重新开始。", true);
        }

        // ── 提示条 ──────────────────────────────────────────────────────────

        private void ShowToast(string message, bool good)
        {
            if (_toastLabel == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            _toastLabel.text = message;
            _toastLabel.color = good ? Palette.Paper : new Color(1f, 0.72f, 0.62f);
            _toastGroup.alpha = 1f;

            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }

            _toastRoutine = StartCoroutine(HideToastAfter(2.8f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);

            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                _toastGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.4f);
                yield return null;
            }

            _toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        // ── 供自动化/调试驱动，不参与玩家操作 ──────────────────────────────

        public void DebugClick(string cardId)
        {
            OnCardClicked(cardId);
        }

        public void DebugChoose(string actionName)
        {
            ActionKind kind;
            if (Enum.TryParse(actionName, true, out kind))
            {
                ChooseAction(kind);
            }
        }

        public void DebugConfirm()
        {
            ConfirmAction();
        }

        public void DebugOpenNotebook()
        {
            OpenNotebook(0);
        }

        public void DebugOpenQuestions()
        {
            OpenNotebook(1);
        }

        public void DebugRestart()
        {
            Restart();
        }

        /// <summary>直接执行一次行动（绕过点击流程，自动化用）。</summary>
        public bool DebugPerform(string cardId, string actionName, string[] context = null)
        {
            ActionKind kind;
            if (!Enum.TryParse(actionName, true, out kind))
            {
                return false;
            }

            ActionResult result = _session.Perform(cardId, kind, context ?? Array.Empty<string>());
            CloseActionPanel();
            RefreshAll();

            if (!result.Success)
            {
                ShowToast(result.Message, false);
            }

            return result.Success;
        }

        /// <summary>模拟「把一张牌挪到桌面某位置」（自动化用，单位是卡片层本地坐标）。</summary>
        public void DebugMoveCard(string cardId, float x, float y)
        {
            OnCardMoved(cardId, new Vector2(x, y));
        }

        /// <summary>模拟「把 A 拖到 B 上」（自动化用）。</summary>
        public void DebugDrop(string draggedCardId, string targetCardId)
        {
            DropOnCard(draggedCardId, targetCardId);
        }

        /// <summary>模拟「把一张牌放到灯下」（自动化用）。</summary>
        public string DebugDropOnLamp(string cardId)
        {
            return DebugSimulateDrop(cardId, _lampZone.anchoredPosition.x, _lampZone.anchoredPosition.y);
        }

        /// <summary>模拟「点灯下浮现的操作」（自动化用）。</summary>
        public void DebugRunLampAction()
        {
            RunLampAction();
        }

        /// <summary>展示一张报纸（自动化用）。</summary>
        public bool DebugShowNewspaper(string newspaperId)
        {
            NewspaperDefinition paper = _session.Case.FindNewspaper(newspaperId);
            if (paper == null || _newspaper == null)
            {
                return false;
            }

            _newspaper.Show(paper, null);
            return true;
        }

        /// <summary>播放一段剧情（自动化用）。</summary>
        public bool DebugPlayDialogue(string dialogueId)
        {
            DialogueScript script = _session.Case.FindDialogue(dialogueId);
            if (script == null || _dialogue == null)
            {
                return false;
            }

            _dialogue.Play(script, null);
            return true;
        }

        /// <summary>切换办案人（自动化用）。</summary>
        public bool DebugSetDetective(string detectiveId)
        {
            bool ok = _session.SetDetective(detectiveId);
            RefreshAll();
            return ok;
        }

        /// <summary>翻开一张牌（自动化用）。</summary>
        public bool DebugReveal(string cardId)
        {
            bool ok = _session.Reveal(cardId);
            if (ok)
            {
                RefreshAll();
            }

            return ok;
        }

        /// <summary>把一条线索放进指定问题页的槽位（第一章的问题页用这个）。</summary>
        public bool DebugPlaceClue(string pageId, string slotId, string clueId)
        {
            bool ok = _session.PlaceClueAnswer(pageId, slotId, clueId);
            RefreshAll();
            if (_notebook != null)
            {
                _notebook.ShowPage(pageId);
            }

            ShowToast(ok ? "已填入线索。" : "填入失败：线索不在笔记本里，或槽位不对。", ok);
            return ok;
        }

        /// <summary>把一张卡放进指定问题页的槽位。</summary>
        public bool DebugPlaceCard(string pageId, string slotId, string cardId)
        {
            bool ok = _session.PlaceAnswer(pageId, slotId, cardId);
            RefreshAll();
            if (_notebook != null)
            {
                _notebook.ShowPage(pageId);
            }

            return ok;
        }

        public void DebugOpenPage(string pageId)
        {
            if (_notebook != null)
            {
                _notebook.ShowPage(pageId);
            }
        }

        /// <summary>结案页提交（与点「结案」按钮同一条代码路径）。</summary>
        public string DebugSubmit()
        {
            if (_notebook == null)
            {
                return "notebook missing";
            }

            _notebook.ShowPage(_session.Case.FinalQuestionPageId);
            string state = string.Format("filled={0} solved={1}", _session.IsCaseFilled, _session.IsCaseSolved);
            _notebook.SubmitFinal();
            return state;
        }

        public string DescribeState()
        {
            int faceDown = 0;
            for (int i = 0; i < _session.Cards.Count; i++)
            {
                if (!_session.Cards[i].IsFaceUp)
                {
                    faceDown++;
                }
            }

            return string.Format(
                "cards={0} facedown={1} clues={2} unread={3} filled={4}/{5} solved={6} time={7} panel={8} selected={9} stage={10} scene={11} pages={12}",
                _session.Cards.Count, faceDown, _session.Notebook.Count, _session.Notebook.UnreadCount,
                _session.QuestionPage.FilledCount, _session.QuestionPage.SlotCount, _session.IsCaseSolved,
                _session.TimeSpent, _panelOpen, _selectedCardId ?? "-",
                _session.CurrentStageId ?? "-", _session.CurrentSceneId ?? "-", _session.QuestionBook.Count);
        }
    }
}
