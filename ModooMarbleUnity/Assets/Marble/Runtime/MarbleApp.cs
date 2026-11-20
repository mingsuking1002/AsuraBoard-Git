using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marble.Core;
using UnityEngine;

namespace Marble.Unity
{
    public sealed partial class MarbleApp : MonoBehaviour
    {
        const string DisplayGameName="아수라판";
        [Serializable] sealed class SaveData { public int Version = 6; public GameConfig Config; public string[] Actions; public string[] Names; public int[] Avatars,Colors,DiceSkins,Outfits; }
        [Serializable] sealed class SmokeReport { public string status; public int turn; public string phase; public int actions; public int winner; public string message; }
        public Game Model { get; private set; }
        public MarbleBoardView Board { get; private set; }
        Font font;
        GUIStyle label, small, tiny, title, button, center, big, money, modalTitle;
        readonly Color ink = PaperTheme.Ink, muted = PaperTheme.Muted;
        readonly Color cream = PaperTheme.Paper, dark = MarbleBoardView.Hex("405136");
        bool initialized, settings, help, autoHuman, paused, mutedSound, smokeDone;
        bool fast, showHistory;
        int selected = -1, humans = 1, players = 4,augmentChoices=4;
        string seedText = "20261025", toast = "";
        float toastUntil, aiTimer, elapsed;
        Vector2 actionScroll, historyScroll, helpScroll;
        AudioSource audioSource;
        AudioClip clickSound, stepSound, diceImpactSound;
        string capturePath, smokePath;
        int exitTurn;
        string SavePath => Path.Combine(Application.persistentDataPath, "classic-save.json");
        float CanvasScale => Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
        Vector2 CanvasOffset => new Vector2((Screen.width - 1440f * CanvasScale) / 2f, (Screen.height - 900f * CanvasScale) / 2f);

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 18);
            audioSource = gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<AudioListener>();
            var backdrop = new GameObject("Background clear camera").AddComponent<Camera>();
            backdrop.transform.SetParent(transform); backdrop.cullingMask = 0;
            backdrop.clearFlags=CameraClearFlags.SolidColor; backdrop.backgroundColor=Color.black;
            clickSound = TactileSound("Paper button",410,.075f,.10f,0);
            stepSound = TactileSound("Wooden footstep",300,.06f,.065f,1);
            diceImpactSound = TactileSound("Dice on tabletop",220,.14f,.17f,2);
            var args = Environment.GetCommandLineArgs();
            capturePath = Arg(args, "-marble-capture");
            smokePath = Arg(args, "-marble-smoke");
            int.TryParse(Arg(args, "-marble-exit-turn"), out exitTurn);
            if (exitTurn > 0) { autoHuman = true; fast = true; }
            diagnosticMode=args.Any(a=>a.StartsWith("-marble-",StringComparison.Ordinal) && a!="-marble-loop-preview" && a!="-marble-front-check" && a!="-marble-wardrobe-check");
            InitializeFrontEnd();
            if(diagnosticMode)StartGame(new GameConfig { Seed = 20261025, HumanCount = humans, PlayerCount = players,AugmentRounds=Game.CreateAugmentSchedule(augmentChoices) });
            InitializeOnline(args);
            string loopPreview=Arg(args,"-marble-loop-preview");
            if(!string.IsNullOrEmpty(loopPreview))StartCoroutine(PlayLoopPreview(loopPreview));
            string rerollIconPreview=Arg(args,"-marble-reroll-icon-preview");
            if(!string.IsNullOrEmpty(rerollIconPreview))StartCoroutine(RerollIconPreview(rerollIconPreview));
            string paperPreview=Arg(args,"-marble-paper-preview");
            if(!string.IsNullOrEmpty(paperPreview))StartCoroutine(PaperPreview(paperPreview));
            string constructionPreview=Arg(args,"-marble-construction-preview");
            if(!string.IsNullOrEmpty(constructionPreview))StartCoroutine(ConstructionPreview(constructionPreview));
            string collectionPreview=Arg(args,"-marble-augment-collection-preview");
            if(!string.IsNullOrEmpty(collectionPreview))StartCoroutine(AugmentCollectionPreview(collectionPreview));
            string tierPreview=Arg(args,"-marble-augment-tier-preview");
            if(!string.IsNullOrEmpty(tierPreview))StartCoroutine(AugmentTierPreview(tierPreview));
            string augmentPreview=Arg(args,"-marble-augment-preview");
            if(!string.IsNullOrEmpty(augmentPreview))StartCoroutine(AugmentPreview(augmentPreview));
            string frontCheck=Arg(args,"-marble-front-check");
            if(!string.IsNullOrEmpty(frontCheck))StartCoroutine(FrontRuntimeCheck(frontCheck));
            string visionCheck=Arg(args,"-marble-vision-check");
            if(!string.IsNullOrEmpty(visionCheck))StartCoroutine(VisionRuntimeCheck(visionCheck));
            string wardrobeCheck=Arg(args,"-marble-wardrobe-check");
            if(!string.IsNullOrEmpty(wardrobeCheck))StartCoroutine(WardrobeRuntimeCheck(wardrobeCheck));
            string diceCheck=Arg(args,"-marble-dice-check");
            if(!string.IsNullOrEmpty(diceCheck))StartCoroutine(DiceRuntimeCheck(diceCheck));
            if (!string.IsNullOrEmpty(capturePath)) StartCoroutine(InitialCapture());
        }
        static string Arg(string[] args, string key)
        {
            int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        IEnumerator InitialCapture()
        {
            yield return new WaitForSeconds(.8f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(capturePath);
        }
        public void StartGame(GameConfig config)
        {
            if(OnlineRoom){Toast("온라인 방을 나간 뒤 로컬 게임을 시작해 주세요.");return;}
            config.EnableAugments=true;
            InstallModel(new Game(config));
        }
        void InstallModel(Game model)
        {
            if (Board != null) Destroy(Board.gameObject);
            Model = model;
            ResetAugmentCandidate();diceCharging=false;bodyMarketOpen=bodyAudioSettings=preferencesOpen=false;
            var boardRoot = new GameObject("Classic world board");
            boardRoot.transform.SetParent(transform);
            Board = boardRoot.AddComponent<MarbleBoardView>();
            Board.Initialize(Model);
            Board.StepSound += () => { if (!mutedSound) audioSource.PlayOneShot(stepSound); };
            Board.DiceImpact += impact => { if (!mutedSound) audioSource.PlayOneShot(diceImpactSound,(impact/2==0?1:impact/2==1?.66f:.4f)*(impact%2==0?1:.8f)); };
            selected = -1;
            aiTimer = .8f;
            elapsed = 0;
            settings = false;
            paused = false;
            actionScroll = Vector2.zero;
        }
        void Update()
        {
            TickOnline(Time.unscaledDeltaTime);
            if(!diagnosticMode)
            {
                if(flow.Screen!=PlayScreen.Game && flow.Screen!=PlayScreen.Results){TickFrontEnd();return;}
                if(Model!=null && Model.Phase==GamePhase.Finished && flow.Screen==PlayScreen.Game)flow.EndMatch();
            }
            if (Model == null || Board == null) return;
            if(preferencesOpen){if(Input.GetKeyDown(KeyCode.Escape)){if(bodyAudioSettings)bodyAudioSettings=false;else ClosePreferences(true);}return;}
            if(Input.GetKeyDown(KeyCode.B) && Model.Config.EnableBodyEconomy && !AugmentOpen && Model.Phase!=GamePhase.Finished)bodyMarketOpen=!bodyMarketOpen;
            if (Input.GetKeyDown(KeyCode.Escape)){if(bodyAudioSettings){bodyAudioSettings=false;return;}if(bodyMarketOpen){bodyMarketOpen=false;return;}if(onlineOpen)onlineOpen=false;else settings=!settings;}
            if (Input.GetKeyDown(KeyCode.F1)) help = !help;
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
            TickDiceControl();
            if (settings || help || paused || onlineOpen || bodyMarketOpen || bodyAudioSettings) return;
            if(OnlineRoom && (onlineSession==null || onlineSession.Stage!=Marble.Online.OnlineStage.Playing))return;
            elapsed += Time.deltaTime;
            if(AugmentOpen){AugmentKeyboard();if(AugmentOpen){TickAugmentAutomation(Time.deltaTime);CheckSmoke();return;}}
            if (Model.Phase == GamePhase.Finished) { CheckSmoke(); return; }
            if (Board.IsAnimating) return;
            bool automatic = onlineGameAttached ? LocalTurn && autoHuman && onlineSession.CanAct : !Model.CurrentPlayer.IsHuman || autoHuman;
            if (automatic)
            {
                aiTimer -= Time.deltaTime;
                if (aiTimer <= 0) { Act(Model.ChooseAiAction()); aiTimer = fast ? .08f : .9f; }
            }
            else if (!AugmentOpen && LocalTurn && Model.Phase!=GamePhase.AwaitRoll && Input.GetKeyDown(KeyCode.Space))
            {
                var actions = BoardActions();
                if (actions.Count == 1) Act(actions[0].Id);
            }
            CheckSmoke();
        }
        public bool Act(string id)
        {
            if (string.IsNullOrEmpty(id) || Board.IsAnimating || Model.Phase == GamePhase.Finished) return false;
            if(OnlineRoom)return onlineGameAttached && onlineSession!=null && onlineSession.Submit(id);
            bool rolled = Game.IsRollAction(id);
            if (!Model.Execute(id)) return false;
            Board.Sync(rolled);TickAchievements(0);
            if (!mutedSound) audioSource.PlayOneShot(clickSound);
            aiTimer = fast ? .08f : .9f;
            actionScroll = Vector2.zero;
            return true;
        }
        void CheckSmoke()
        {
            if (smokeDone || exitTurn < 1 || Board.IsAnimating) return;
            if (Model.TurnNumber < exitTurn && Model.Phase != GamePhase.Finished) return;
            smokeDone = true;
            StartCoroutine(FinishSmoke());
        }
        IEnumerator FinishSmoke()
        {
            yield return new WaitForEndOfFrame();
            if (!string.IsNullOrEmpty(smokePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(smokePath));
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(smokePath, ".png"));
                File.WriteAllText(smokePath, JsonUtility.ToJson(new SmokeReport { status = "passed", turn = Model.TurnNumber,
                    phase = Model.Phase.ToString(), actions = Model.ReplayActions.Count, winner = Model.WinnerIndex, message = Model.Message }, true));
            }
            yield return new WaitForSeconds(1.5f);
            Application.Quit(0);
        }
        void SetupStyles()
        {
            if (initialized) return;
            initialized = true;
            PaperTheme.ApplySkin(GUI.skin);
            label = Text(18, ink);
            small = Text(14, ink);
            tiny = Text(12, ink);
            title = Text(32, dark, FontStyle.Bold);
            center = Text(18, ink, FontStyle.Bold); center.alignment = TextAnchor.MiddleCenter;
            big = Text(44, ink, FontStyle.Bold); big.alignment = TextAnchor.MiddleCenter;
            money = Text(25, ink, FontStyle.Bold);
            modalTitle = Text(25, ink, FontStyle.Bold); modalTitle.alignment = TextAnchor.MiddleCenter;
            button = Text(17, ink, FontStyle.Bold); button.alignment = TextAnchor.MiddleCenter;
            button.normal.background = null;
            button.hover.background = button.normal.background; button.active.background = button.normal.background;
            button.border = new RectOffset(12,12,8,8);
            button.hover.textColor = ink; button.active.textColor = ink;
            button.padding = new RectOffset(10, 10, 5, 5);
            button.wordWrap = true;
        }
        GUIStyle Text(int size, Color c, FontStyle weight = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = weight, wordWrap = true, richText = false };
            s.normal.textColor = c;
            return s;
        }
        void Box(Rect r, Color c, bool round = true)
        {
            if(!round){CircuitArt.Fill(r,c);return;}
            PaperTheme.Card(r,PaperTheme.Wash(c,.20f),false,2);
        }
        void Panel(Rect r, Color c)=>PaperTheme.Card(r,PaperTheme.Paper,true,3.4f);
        bool Button(Rect r, string text, Color color, bool enabled = true)
        {
            bool before=GUI.enabled,active=before && enabled;
            bool hover=active && r.Contains(Event.current.mousePosition);
            bool primary=color.r+color.g+color.b<1.1f;
            bool colored=!primary && Mathf.Abs(color.r-muted.r)+Mathf.Abs(color.g-muted.g)+Mathf.Abs(color.b-muted.b)>.24f;
            Color fill=active?(primary?Color.Lerp(PaperTheme.Forest,Color.white,hover?.10f:0):PaperTheme.Wash(color,hover?(colored?.45f:.24f):(colored?.34f:.10f))):PaperTheme.Wash(PaperTheme.Muted,.12f);
            var drawn=new Rect(r.x,r.y+(hover?-1:0),r.width,r.height);
            PaperTheme.Card(drawn,fill,false,hover?2.4f:1.8f);
            var style=new GUIStyle(button);style.normal.textColor=active?(primary?PaperTheme.Paper:ink):muted;
            style.fontSize=r.height>=58?19:17;style.wordWrap=false;
            while(style.fontSize>11 && style.CalcSize(new GUIContent(text)).x>r.width-18)style.fontSize--;
            GUI.Label(new Rect(drawn.x+3,drawn.y+2,drawn.width-6,drawn.height-4),text,style);
            string focusName="button:"+Mathf.RoundToInt(r.x)+":"+Mathf.RoundToInt(r.y)+":"+text;
            if(active && GUI.GetNameOfFocusedControl()==focusName)PaperTheme.Frame(new Rect(drawn.x-3,drawn.y-3,drawn.width+6,drawn.height+6),PaperTheme.Sun,2.4f);
            GUI.SetNextControlName(focusName);GUI.enabled=active;bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=before;return hit;
        }
        string PaperInput(Rect r,string value,int max,GUIStyle style)
        {
            PaperTheme.Card(r,PaperTheme.Paper,false,1.9f);
            return GUI.TextField(new Rect(r.x+7,r.y+3,r.width-14,r.height-6),value,max,style);
        }
        string Money(long n) => n.ToString("N0") + (Model!=null && Model.Config.EnableBodyEconomy?"G":"만");
        string TileSubtitle(TileState t)
        {
            return t.IsPurchasable?"통행료 "+Money(Model.GetToll(t.Index)):"";
        }
        void OnGUI()
        {
            if(Event.current.type==EventType.Repaint)runtimeGuiRepaints++;
            if(!diagnosticMode && flow.Screen!=PlayScreen.Game && flow.Screen!=PlayScreen.Results){DrawFrontEnd();return;}
            if (Model == null || Board == null) return;
            SetupStyles();
            var origin=CanvasOffset;
            GUI.matrix = Matrix4x4.TRS(new Vector3(origin.x,origin.y,0), Quaternion.identity, new Vector3(CanvasScale,CanvasScale,1));
            GUI.skin.font=font;
            GUI.depth = 0;
            augmentHoverSeat=augmentHoverIndex=-1;
            bool boardEnabled=GUI.enabled;
            GUI.enabled=boardEnabled && !AugmentOpen && !bodyMarketOpen && !bodyAudioSettings && !preferencesOpen && !settings && !help && !onlineOpen && Model.Phase!=GamePhase.Repayment;
            var canvasMatrix=GUI.matrix;
            GUI.matrix=canvasMatrix*Board.ViewTransform;
            DrawBodyWorld();
            DrawTiles();
            // Pieces must sit above printed tile names and tolls.
            Board.DrawTokens();
            GUI.matrix=canvasMatrix;
            DrawBodyVision();
            Header();
            for (int i = 0; i < Model.Players.Count; i++) PlayerCard(i);
            CenterUi();
            DrawSelection();
            if(showHistory) DrawHistory();
            Footer();
            if (Time.unscaledTime < toastUntil)
            {
                Panel(new Rect(470, 815, 500, 45), cream);
                GUI.Label(new Rect(480, 820, 480, 35), toast, center);
            }
            if (Model.Phase == GamePhase.Finished) Victory();
            GUI.enabled=boardEnabled;
            if(AugmentOpen)DrawAugmentOverlay();
            if(bodyMarketOpen || Model.Phase==GamePhase.Repayment)DrawBodyMarket();
            if (settings) Settings();
            if (help) Help();
            if (onlineOpen) DrawOnlinePanel();
            if(preferencesOpen)DrawPreferencesOverlay();
            if(bodyAudioSettings)DrawBodyAudioSettings();
            DrawOwnedAugmentTooltip();
        }
        void Header()
        {
            PaperTheme.Surface(new Rect(0,0,1440,44),PaperTheme.FrontTable);CircuitArt.Fill(new Rect(0,41,1440,3),PaperTheme.Sun);
            var logoStyle=new GUIStyle(title); logoStyle.fontSize=23; logoStyle.normal.textColor=PaperTheme.Paper;
            GUI.Label(new Rect(24, 5, 220, 36), DisplayGameName, logoStyle);
            var captionStyle=new GUIStyle(tiny);captionStyle.normal.textColor=muted;
            captionStyle.normal.textColor=roomVoice!=null && roomVoice.Recording?MarbleBoardView.PlayerColors[1]:muted;
            PaperTheme.Chip(new Rect(255,8,243,28),PaperTheme.Wash(PaperTheme.Table,.13f));
            GUI.Label(new Rect(267, 13, 224, 23),MicrophoneBadge,captionStyle);
            var whiteCenter = new GUIStyle(center); whiteCenter.normal.textColor = PaperTheme.Paper;
            int sec = (int)elapsed;
            GUI.Label(new Rect(550, 10, 340, 36), string.Format(Model.Config.EnableBodyEconomy?"{0:00}:{1:00}     ROUND {2} / {3}":"{0:00}:{1:00}     TURN {2} / {3}", sec / 60, sec % 60, Model.Config.EnableBodyEconomy?Model.EconomyRound:Model.TurnNumber, Model.Config.EnableBodyEconomy?Model.Config.MaxEconomyRounds:Model.Config.MaxTurns), whiteCenter);
            bool previousNavigation=GUI.enabled;GUI.enabled=!bodyMarketOpen && !bodyAudioSettings && !preferencesOpen && !settings && !help && !onlineOpen && Model.Phase!=GamePhase.Repayment;
            if (Button(new Rect(1120, 5, 136, 32), "게임 방법", muted)) help = true;
            if (Button(new Rect(1268, 5, 148, 32), "메뉴  ≡", dark)) settings = true;
            if(Button(new Rect(936,5,172,32),diagnosticMode?"Steam 멀티":"로비 · 메뉴",muted)){if(diagnosticMode){onlineOpen=true;settings=false;}else settings=true;}
            GUI.enabled=previousNavigation;
        }
        Rect PlayerCardRect(int seat)
        {
            int local=onlineGameAttached && onlineSession!=null?onlineSession.LocalSeat:Model.Players.FindIndex(player=>player.IsHuman);
            if(local<0 || local>=Model.Players.Count)local=0;
            int slot=(seat-local+4)%4;
            return new Rect(slot>=2?1120:20,slot==0 || slot==3?681:67,300,149);
        }
        void PlayerCard(int i)
        {
            var p = Model.Players[i];
            var card=PlayerCardRect(i);float x=card.x,y=card.y;
            Color c = Board.PlayerColor(i);
            bool active = i == Board.PresentationSeat && Model.Phase != GamePhase.Finished;
            DrawOwnedAugments(i,x,y);
            var r = new Rect(x, y, 300, 149);
            PaperTheme.Card(r,PaperTheme.Wash(c,p.Bankrupt?.05f:active?.18f:.10f),true,active?3.3f:2.2f,i);
            CircuitArt.Fill(new Rect(x+7,y+6,286,4),c);
            // The separate left-hand rank badge stays distinct from portrait and balance.
            PaperTheme.Surface(new Rect(x+67,y+11,223,25),PaperTheme.Wash(c,.42f));
            CircuitArt.Line(new Vector2(x+63,y+11),new Vector2(x+63,y+139),new Color(ink.r,ink.g,ink.b,.65f),1.2f);
            var bright=new GUIStyle(label){fontStyle=FontStyle.Bold,wordWrap=false};bright.normal.textColor=ink;
            var brightSmall=new GUIStyle(small);brightSmall.normal.textColor=ink;
            var brightMoney=new GUIStyle(money);brightMoney.normal.textColor=ink;
            var brightRank=new GUIStyle(center);brightRank.normal.textColor=ink;brightRank.fontStyle=FontStyle.Bold;
            int rank = 1 + Model.Players.Count(other => !other.Bankrupt && Model.GetNetWorth(other.Index) > Model.GetNetWorth(i));
            var rankRect=new Rect(x+5,y+47,53,53);
            CircuitArt.Circle(rankRect,ink);CircuitArt.Circle(new Rect(rankRect.x+3,rankRect.y+3,47,47),PaperTheme.Wash(c,.48f));
            GUI.Label(rankRect,p.Bankrupt?"—":rank+"위",brightRank);
            string playerName=p.Name+(p.IsHuman?"":" · AI");while(bright.fontSize>12 && bright.CalcSize(new GUIContent(playerName)).x>211)bright.fontSize--;
            GUI.Label(new Rect(x+72,y+8,215,27),playerName,bright);
            PaperTheme.Card(new Rect(x+70,y+44,58,68),PaperTheme.Wash(c,.35f),false,1.8f);
            var portrait=SteamPortrait(i) ?? Board.CharacterPortrait(i);
            if(portrait!=null)GUI.DrawTexture(new Rect(x+70,y+40,58,76),portrait,ScaleMode.ScaleToFit,true);
            string cashText=p.Bankrupt?(Model.Config.EnableBodyEconomy?"탈락":"파산"):Money(p.Cash);
            brightMoney.wordWrap=false;
            while(brightMoney.fontSize>14 && brightMoney.CalcSize(new GUIContent(cashText)).x>153)brightMoney.fontSize--;
            GUI.Label(new Rect(x+136,y+42,153,35),cashText,brightMoney);
            GUI.Label(new Rect(x+136,y+79,153,24),(p.CreditDueRound>0?p.CreditDueRound+"R 만기 · ":"총자산 ")+Money(p.CreditDueRound>0?-p.Cash:Model.GetNetWorth(i)),brightSmall);
            GUI.Label(new Rect(x+70,y+119,218,23),p.Bankrupt?"게임 종료":
                Model.Tiles[p.Position].Definition.Name+"  ·  "+p.Laps+"바퀴"+(p.IslandTurns>0?"  ·  무인도":p.PendingTravel?"  ·  여행 대기":active?"  ◀ 현재 차례":""),brightSmall);
        }
        void DrawTiles()
        {
            bool choose = !Board.IsAnimating && !settings && !help && !onlineOpen && !AugmentOpen && LocalTurn && !autoHuman;
            var actions = BoardActions();
            foreach (var t in Model.Tiles)
            {
                var p = Board.TileLabelPoint(t.Index);
                bool target = actions.Any(a => a.TileIndex == t.Index && (a.Id.StartsWith("travel:") || a.Id.StartsWith("festival:") || a.Id.StartsWith("remote:")));
                if(target && choose)Board.DrawTileOutline(t.Index,new Color(1,.88f,.25f,.95f),3);
                if(selected==t.Index)Board.DrawTileOutline(t.Index,new Color(.20f,.75f,.9f,1),3);
                var nameStyle=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=13,wordWrap=false};
                nameStyle.normal.textColor=ink;
                var sub=new GUIStyle(tiny){alignment=TextAnchor.MiddleCenter,fontSize=9,wordWrap=false};
                sub.normal.textColor=dark;
                if(t.Index%8==0)
                {
                    GUI.Label(Board.TileNameRect(t.Index),t.Definition.Name,nameStyle);
                    GUI.Label(new Rect(p.x-48,p.y+2,96,15),TileSubtitle(t),sub);
                }
                else
                {
                    // Run text along the board edge, as printed on each tile.
                    int edge=t.Index/8;
                    DiamondBoardLayout.Vertex(t.Index,edge,out float ax,out float ay);
                    DiamondBoardLayout.Vertex(t.Index,(edge+1)%4,out float bx,out float by);
                    var axis=new Vector2(bx-ax,by-ay).normalized;if(axis.x<0)axis=-axis;
                    // Both axes follow the projected board plane so the lettering sits on the tile.
                    var across=new Vector2(axis.y<0?axis.x:-axis.x,Mathf.Abs(axis.y));
                    var outward=(p-Board.SignatureColorPoint(t.Index)).normalized;
                    var anchor=p+outward*10;
                    var matrix=GUI.matrix;
                    var tileMatrix=Matrix4x4.identity;tileMatrix.m00=axis.x;tileMatrix.m10=axis.y;tileMatrix.m01=across.x;tileMatrix.m11=across.y;tileMatrix.m03=anchor.x;tileMatrix.m13=anchor.y;
                    GUI.matrix=matrix*tileMatrix;
                    nameStyle.fontSize=15;while(nameStyle.fontSize>9 && nameStyle.CalcSize(new GUIContent(t.Definition.Name)).x>67)nameStyle.fontSize--;
                    sub.fontSize=12;string subtitle=t.IsPurchasable?Money(Model.GetToll(t.Index)):"";while(sub.fontSize>8 && sub.CalcSize(new GUIContent(subtitle)).x>67)sub.fontSize--;
                    GUI.Label(new Rect(-35,-17,70,19),t.Definition.Name,nameStyle);
                    GUI.Label(new Rect(-35,2,70,15),subtitle,sub);
                    GUI.matrix=matrix;
                }
                if (t.FestivalMultiplier > 1)
                {
                    Box(new Rect(p.x + 8, p.y - 12, 31, 20), MarbleBoardView.Hex("EC7760"));
                    var fs = new GUIStyle(tiny) { alignment = TextAnchor.MiddleCenter }; fs.normal.textColor = ink;
                    GUI.Label(new Rect(p.x + 8, p.y - 12, 31, 20), "×" + t.FestivalMultiplier, fs);
                }
            }
            var input=Event.current;
            if(input.type==EventType.MouseDown && input.button==0 && !Board.IsAnimating && !settings && !help && !onlineOpen && !AugmentOpen)
            {
                // Input.mousePosition is unambiguously screen pixels, independent of
                // IMGUI's current matrix/clip stack. Flip Y into the top-left canvas.
                var screen=Input.mousePosition;
                var canvas=(new Vector2(screen.x,Screen.height-screen.y)-CanvasOffset)/CanvasScale;
                if(!BoardInputCovered(canvas,actions))
                {
                    int hit=Board.HitTest(canvas);
                    if(hit>=0)
                    {
                        selected=hit;
                        if(choose)
                        {
                            var action=actions.FirstOrDefault(a=>a.TileIndex==hit && (a.Id.StartsWith("travel:")||a.Id.StartsWith("festival:")||a.Id.StartsWith("remote:")));
                            if(action!=null)Act(action.Id);
                        }
                        input.Use();
                    }
                }
            }
        }
        bool BoardInputCovered(Vector2 point,List<GameAction> actions)
        {
            if(CityConstructionContext && LocalTurn && !autoHuman && ConstructionWindow.Contains(point))return true;
            if(bodyMarketOpen || Model.Phase==GamePhase.Repayment)return true;
            if(point.y<44 || point.y>=840 || Model.Phase==GamePhase.Finished)return true;
            for(int i=0;i<Model.Players.Count;i++)
                if(PlayerCardRect(i).Contains(point))return true;
            if(selected>=0 && new Rect(20,284,285,294).Contains(point))return true;
            if(showHistory && new Rect(1142,290,278,268).Contains(point))return true;
            if(!Board.IsAnimating && LocalTurn && !autoHuman && Model.Phase!=GamePhase.AwaitRoll)
            {
                bool destination=Model.Phase==GamePhase.Travel || Model.Phase==GamePhase.Olympics || actions.Any(a=>a.Id.StartsWith("remote:"));
                float height=destination?175:Mathf.Min(290,70+actions.Count*51);
                if(new Rect(485,410,470,height).Contains(point))return true;
            }
            return false;
        }
        System.Collections.Generic.List<GameAction> BoardActions()=>Model.GetActions().Where(a=>!Game.IsEconomyAction(a.Id)).ToList();
        void CenterUi()
        {
            if(Model.Phase==GamePhase.Repayment){DrawRepaymentSummary();return;}
            if(AugmentOpen)return;
            bool human = LocalTurn && !autoHuman;
            bool ready = human && !Board.IsAnimating && !settings && !help && !onlineOpen && !paused && (!onlineGameAttached || onlineSession.CanAct);
            if(human && !Board.IsAnimating && CityConstructionContext){DrawConstructionPanel(ready);return;}
            var messageRect = new Rect(500,262,440,58);
            PaperTheme.Card(messageRect,PaperTheme.Paper,true,2.2f);
            var turnColor=Board.PlayerColor(Board.PresentationSeat);
            CircuitArt.Circle(new Rect(516,277,28,28),turnColor);
            var turnTitle=new GUIStyle(small){fontStyle=FontStyle.Bold,fontSize=15,wordWrap=false};
            string current=Model.Players[Board.PresentationSeat].Name+(Board.IsAnimating?" · 이동 중":human?" · 내 차례":" · 차례 진행 중");
            while(turnTitle.fontSize>11 && turnTitle.CalcSize(new GUIContent(current)).x>370)turnTitle.fontSize--;
            GUI.Label(new Rect(555,265,366,24),current,turnTitle);
            var messageStyle=new GUIStyle(small){fontSize=13,wordWrap=false};string message=Board.IsAnimating?Board.MovementCaption:Model.Message;
            while(messageStyle.fontSize>10 && messageStyle.CalcSize(new GUIContent(message)).x>370)messageStyle.fontSize--;
            GUI.Label(new Rect(555,290,366,24),message,messageStyle);
            if (Model.Phase == GamePhase.AwaitRoll || Board.IsAnimating || !human)
            {
                float y = 520;
                Board.DrawDice(new Rect(590,345,260,120));
                if (Model.DiceA > 0 && !Board.DiceInFlight)
                    GUI.Label(new Rect(600, 465, 240, 36), Model.DiceA + " + " + Model.DiceB + (Model.DiceA == Model.DiceB ? "  더블!" : ""), modalTitle);
                if(Model.Phase==GamePhase.AwaitRoll)DrawDiceControl(ready);
                if (human && Model.CurrentPlayer.IslandTurns > 0 && !Board.IsAnimating)
                {
                    int k = 0;
                    foreach (var a in BoardActions().Where(a => !Game.IsRollAction(a.Id)))
                    {
                        if (Button(new Rect(535, y + 166 + k * 40, 370, 35), a.Label + (a.Cost > 0 ? " " + Money(a.Cost) : ""), dark, ready)) Act(a.Id);
                        k++;
                    }
                }
            }
            else if (Model.Phase != GamePhase.Finished)
            {
                var actions = BoardActions();
                bool destination = Model.Phase == GamePhase.Travel || Model.Phase == GamePhase.Olympics || actions.Any(a => a.Id.StartsWith("remote:"));
                float height = destination ? 175 : Mathf.Min(290, 70 + actions.Count * 51);
                var r = new Rect(485, 410, 470, height);
                Panel(r, cream);
                GUI.Label(new Rect(r.x + 15, r.y + 12, r.width - 30, 31), destination ? "보드의 목적지를 눌러 선택하세요" : PhaseTitle(), modalTitle);
                actionScroll = GUI.BeginScrollView(new Rect(r.x + 16, r.y + 50, r.width - 32, r.height - 62), actionScroll,
                    new Rect(0, 0, r.width - 54, Mathf.Max(r.height - 62, actions.Count * 48)));
                for (int i = 0; i < actions.Count; i++)
                {
                    var a = actions[i];
                    string caption = a.Label + (a.Cost > 0 ? "  " + Money(a.Cost) : "");
                    bool risky = a.Id == "bankrupt";
                    if (Button(new Rect(0, i * 48, r.width - 56, 41), caption, risky ? MarbleBoardView.Hex("BC6470") : a.Id == "end" ? muted : dark, ready)) { Act(a.Id); break; }
                }
                GUI.EndScrollView();
            }
        }
        string PhaseTitle()
        {
            switch (Model.Phase)
            {
                case GamePhase.Debt: return "자금 부족 · 정산 " + Money(Model.DebtAmount);
                case GamePhase.Bonus: return GameLabels.Bonus+" · 앞면일까, 뒷면일까?";
                case GamePhase.Travel: return "세계여행";
                case GamePhase.Olympics: return GameLabels.Festival+" 개최";
                default: return Model.Tiles[Model.CurrentPlayer.Position].Definition.Name;
            }
        }
        void DrawSelection()
        {
            if (selected < 0) return;
            var t = Model.Tiles[selected];
            var r = new Rect(20, 284, 285, 294);
            Panel(r, cream);
            Box(new Rect(r.x, r.y, r.width, 8), t.Definition.Group >= 0 ? MarbleBoardView.GroupColors[t.Definition.Group % 8] : dark);
            GUI.Label(new Rect(35, 303, 240, 35), t.Definition.Name, modalTitle);
            GUI.Label(new Rect(40, 350, 240, 29), t.OwnerIndex < 0 ? "소유자 없음" : "소유자  " + Model.Players[t.OwnerIndex].Name, label);
            GUI.Label(new Rect(40, 387, 240, 29), "구매가  " + Money(t.Definition.Price), label);
            GUI.Label(new Rect(40, 423, 240, 30), "통행료  " + Money(Model.GetToll(selected)), label);
            GUI.Label(new Rect(40, 463, 240, 53), t.Level >= 4 ? "랜드마크 · 인수 불가" : t.Definition.Kind == TileKind.City ? t.Buildings==0?"땅만 보유":Game.BuildingNames(t.Buildings)+" 보유" : TileSubtitle(t), small);
            if (Button(new Rect(45, 529, 234, 33), "닫기", muted)) selected = -1;
        }
        void DrawHistory()
        {
            var r = new Rect(1142, 290, 278, 268);
            Panel(r, new Color(1, .98f, .89f, .88f));
            GUI.Label(new Rect(r.x + 14, r.y + 12, 245, 25), "게임 소식", label);
            int start = Mathf.Max(0, Model.History.Count - 5);
            float y = r.y + 48;
            for (int i = start; i < Model.History.Count; i++)
            {
                GUI.Label(new Rect(r.x + 14, y, 247, 38), Model.History[i], small);
                y += 40;
            }
        }
        void Footer()
        {
            CircuitArt.Fill(new Rect(0,840,1440,60),PaperTheme.FrontTable);
            var keyHint=new GUIStyle(tiny);keyHint.normal.textColor=PaperTheme.Paper;
            GUI.Label(new Rect(22, 857, 630, 30), "SPACE 굴림   "+TalkKey+" 말하기   M 마이크 음소거   ESC 메뉴", keyHint);
            if(Model.Config.EnableBodyEconomy && Button(new Rect(664,850,198,34),"신체 · 자산 거래 [B]",dark))bodyMarketOpen=!bodyMarketOpen;
            if(Button(new Rect(878,850,128,34),showHistory?"소식 닫기":"게임 소식",muted))showHistory=!showHistory;
            if (Button(new Rect(1018, 850, 128, 34), autoHuman ? "자동 ON" : "자동 OFF", autoHuman ? dark : muted)) autoHuman = !autoHuman;
            if (Button(new Rect(1158, 850, 112, 34), fast ? "빠르게" : "보통 속도", muted)) fast = !fast;
            if (Button(new Rect(1282, 850, 136, 34), mutedSound ? "소리 OFF" : "소리 ON", muted)) mutedSound = !mutedSound;
        }
        void Shade() => CircuitArt.Fill(new Rect(0,0,1440,900),new Color(.16f,.19f,.12f,.48f));
        void Settings()
        {
            if(!diagnosticMode){DrawMatchMenu();return;}
            if(OnlineRoom){OnlineSettings();return;}
            Shade(); Panel(new Rect(455, 160, 530, 590), cream);
            GUI.Label(new Rect(480, 185, 480, 45), "게임 메뉴", title);
            GUI.Label(new Rect(490, 250, 220, 29), "전체 인원", label);
            for (int i = 2; i <= 4; i++) if (Button(new Rect(690 + (i - 2) * 82, 245, 72, 38), i + "명", players == i ? dark : muted)) { players = i; humans = Mathf.Min(humans, players); }
            GUI.Label(new Rect(490, 306, 220, 29), "사람 인원 · 나머지는 AI", small);
            for (int i = 1; i <= players; i++) if (Button(new Rect(644 + (i - 1) * 76, 301, 66, 38), i + "명", humans == i ? dark : muted)) humans = i;
            GUI.Label(new Rect(490, 365, 120, 29), "시드", label);
            seedText = PaperInput(new Rect(610, 362, 320, 36), seedText, 11, label);
            GUI.Label(new Rect(490,408,440,22),"증강 선택 횟수 · 기본 4회 / 보유 최대 8개",small);
            for(int i=1;i<=8;i++)if(Button(new Rect(490+(i-1)*55,438,48,29),i.ToString(),augmentChoices==i?dark:muted))augmentChoices=i;
            if (Button(new Rect(490, 476, 440, 48), "새 게임 시작", MarbleBoardView.Hex("E37B85")))
            {
                if (!int.TryParse(seedText, out int seed)) seed = 20261025;
                autoHuman = false;
                StartGame(new GameConfig { Seed = seed, PlayerCount = players, HumanCount = humans,AugmentRounds=Game.CreateAugmentSchedule(augmentChoices) });
            }
            if (Button(new Rect(490, 540, 210, 42), "현재 게임 저장", dark)) Save();
            if (Button(new Rect(720, 540, 210, 42), "저장 불러오기", dark)) Load();
            if (Button(new Rect(490, 600, 440, 42), "게임으로 돌아가기", muted)) settings = false;
            if (Button(new Rect(490, 664, 440, 39), "종료", muted)) Application.Quit();
        }
        void Help()
        {
            Shade(); Panel(new Rect(330, 100, 780, 700), cream);
            GUI.Label(new Rect(370, 125, 700, 45), "한 판의 규칙", title);
            string ending=Model.Config.EnableBodyEconomy?"마지막 생존자가 이깁니다. 제한 라운드까지 남은 사람이 여럿이면 총자산으로 순위를 정합니다.":
                (Model.Config.EndOnFirstBankruptcy?"한 사람이 파산하면 경기가 끝나며 생존자 중 총자산이 가장 많은 사람이 이깁니다. ":"상대 전원이 파산하면 승리합니다. ")+
                (Model.Config.ColorSetWins?"한 컬러 그룹을 모두 가져도 승리합니다. ":"")+
                "세 그룹 독점, 한 변의 모든 땅 독점, 관광지 독점으로도 이기며 제한 턴에는 총자산으로 순위를 정합니다.";
            string moneyRules=Model.Config.EnableBodyEconomy?
                "잔액이 마이너스가 되면 대출이 시작됩니다. 신용한도 내에서 투자할 수 있고, 만기까지 빚을 갚지 못하면 탈락합니다. 내 차례에 땅과 신체를 팔거나, 판 신체를 복구할 수 있습니다.":
                "통행료를 낼 돈이 부족하면 보유 도시를 팔거나 긴급 대출을 사용할 수 있습니다. 끝내 정산하지 못하면 파산합니다.";
            string text = "1. 굴리고 이동\n주사위 두 개의 합만큼 이동합니다. ROLL을 길게 누르거나 SPACE를 누른 뒤 놓으면 목표 칸수 쪽으로 확률이 조정됩니다. 원하는 눈이 확정되는 것은 아닙니다.\n\n" +
                "2. 사고 짓고 받기\n빈 도시를 사고 별장·빌딩·호텔을 종류별로 한 채씩 지어요. 첫 바퀴엔 별장, 한 바퀴 완주 후 빌딩, 두 바퀴 완주 후 호텔이 열립니다. 세 건물을 지은 뒤 다시 도착하면 인수할 수 없는 랜드마크로 바꿀 수 있습니다.\n\n" +
                "상대 땅에 도착하면 통행료를 지불합니다. 같은 색 도시를 모두 가지면 통행료가 오르고, 랜드마크가 아닌 상대 땅은 조건에 따라 인수할 수 있습니다.\n\n" +
                "3. 특별한 칸\n돌발 카드는 돈을 받거나 내고, 다른 칸으로 이동하는 등 예상 밖의 사건을 일으킵니다. 한탕 찬스에서는 앞면·뒷면을 고릅니다. 도시 축제는 내 땅의 통행료를 높이고, 세계여행은 다음 차례에 목적지를 선택합니다.\n\n" +
                "무인도는 더블·탈출비·탈출 카드로 벗어납니다. 출발지를 지나면 월급을 받고, 출발지에 정확히 도착하면 내 도시에 원격 건설할 수 있습니다.\n\n" +
                "4. 돈이 부족하다면\n"+moneyRules+"\n\n"+
                "5. 승리와 결과\n"+ending+"\n\n"+
                "보드의 칸을 누르면 상세 정보를 볼 수 있습니다. 게임 메뉴에서 환경설정을 열거나 로비로 돌아갈 수 있습니다.";
            float textHeight=Mathf.Max(500,label.CalcHeight(new GUIContent(text),663)+20);
            helpScroll = GUI.BeginScrollView(new Rect(370, 190, 700, 500), helpScroll, new Rect(0, 0, 670, textHeight));
            GUI.Label(new Rect(0, 0, 663, textHeight), text, label);
            GUI.EndScrollView();
            if (Button(new Rect(570, 727, 300, 43), "확인", dark)) help = false;
        }
        void Victory()
        {
            if(!diagnosticMode){DrawMatchResults();return;}
            if (settings || help) return;
            Panel(new Rect(467, 268, 506, 366), cream);
            GUI.Label(new Rect(487, 290, 466, 60), "VICTORY!", big);
            GUI.Label(new Rect(487, 365, 466, 42), Model.WinnerIndex >= 0 ? Model.Players[Model.WinnerIndex].Name + " 승리" : "게임 종료", modalTitle);
            GUI.Label(new Rect(487, 421, 466, 58), Model.WinnerReason, center);
            if (Model.WinnerIndex >= 0) GUI.Label(new Rect(487, 490, 466, 39), "총자산 " + Money(Model.GetNetWorth(Model.WinnerIndex)), modalTitle);
            if(onlineGameAttached)
            {
                if(Button(new Rect(537,562,366,46),onlineSession.IsHost?"대기실로 돌아가기":"호스트의 대기실 복귀 대기",dark,onlineSession.IsHost)){onlineSession.ReturnToLobby();onlineOpen=true;}
            }
            else if (Button(new Rect(537, 562, 366, 46), "새 게임", dark)) settings = true;
        }
        void Save()
        {
            if(OnlineRoom){Toast("온라인 경기는 호스트와 실시간 동기화합니다. 로컬 저장은 사용할 수 없습니다.");return;}
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                var data = new SaveData { Config = Model.Config, Actions = Model.ReplayActions.ToArray(), Names=Model.Players.Select(p=>p.Name).ToArray(), Avatars=Model.Players.Select(p=>p.Avatar).ToArray(), Colors=Model.Players.Select(p=>p.ColorIndex).ToArray(), DiceSkins=Model.Players.Select(p=>p.DiceSkin).ToArray(),Outfits=Model.Players.Select(p=>p.Outfit).ToArray() };
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                File.Copy(temp, SavePath, true); File.Delete(temp);
                Toast("게임을 저장했습니다");
            }
            catch (Exception ex) { Toast("저장 실패: " + ex.Message); Debug.LogException(ex); }
        }
        void Load()
        {
            if(OnlineRoom){Toast("온라인 방에서는 로컬 저장을 불러올 수 없습니다.");return;}
            try
            {
                if (!File.Exists(SavePath)) { Toast("저장된 게임이 없습니다"); return; }
                if (new FileInfo(SavePath).Length > 8 * 1024 * 1024) throw new InvalidDataException("저장 파일 크기 초과");
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null || (data.Version < 1 || data.Version > 6) || data.Config == null || data.Actions == null || data.Actions.Length > 100000) throw new InvalidDataException("잘못된 저장 형식");
                var restored = new Game(data.Config);
                if(data.Version>=3)
                {
                    int count=restored.Players.Count;
                    if(data.Names==null || data.Avatars==null || data.Colors==null || data.Names.Length!=count || data.Avatars.Length!=count || data.Colors.Length!=count || data.Avatars.Any(v=>v<0 || v>=CosmeticCatalog.AvatarCount) || data.Colors.Any(v=>v<0 || v>3))throw new InvalidDataException("잘못된 외형 정보");
                    if(data.Version>=4 && (data.DiceSkins==null || data.DiceSkins.Length!=count || data.DiceSkins.Any(v=>v<0 || v>=CosmeticCatalog.DiceSkinCount)))throw new InvalidDataException("잘못된 주사위 외형");
                    if(data.Version>=6 && (data.Outfits==null || data.Outfits.Length!=count || data.Outfits.Any(v=>!WardrobeCatalog.IsValid(v))))throw new InvalidDataException("잘못된 옷장 정보");
                    for(int i=0;i<count;i++){restored.Players[i].Outfit=data.Version>=6?data.Outfits[i]:WardrobeCatalog.DefaultCode;restored.Players[i].DiceSkin=data.Version>=4?data.DiceSkins[i]:0;restored.Players[i].Name=Marble.Online.OnlineProtocol.CleanName(data.Names[i]);restored.Players[i].Avatar=data.Avatars[i];restored.Players[i].ColorIndex=data.Colors[i];}
                }
                foreach (var action in data.Actions) if (!restored.Execute(action)) throw new InvalidDataException("재현할 수 없는 행동");
                InstallModel(restored);autoHuman=false;
                if(!diagnosticMode)
                {
                    flow.ReturnToLobby();flow.StartMatch();
                    if(Model.Phase==GamePhase.Finished)flow.EndMatch();
                    lobbyMap=Model.Config.MapId;
                }
                humans = Model.Config.HumanCount; players = Model.Config.PlayerCount; seedText = Model.Config.Seed.ToString();
                Toast("저장된 게임을 불러왔습니다");
            }
            catch (Exception ex) { Toast("불러오기 실패: " + ex.Message); Debug.LogException(ex); }
        }
        void Toast(string text) { toast = text; toastUntil = Time.unscaledTime + 4; }
        static AudioClip TactileSound(string name,float frequency,float duration,float amplitude,int variation)
        {
            int length = Mathf.CeilToInt(22050 * duration);
            var data = new float[length];uint noise=(uint)(9743+variation*7919);
            for(int i=0;i<length;i++)
            {
                float t=i/22050f;noise=unchecked(noise*1664525u+1013904223u);
                float transient=((noise>>8)/8388608f-1)*Mathf.Exp(-t*110);
                float resonance=Mathf.Sin(2*Mathf.PI*frequency*t)*Mathf.Exp(-t*32)+.35f*Mathf.Sin(2*Mathf.PI*frequency*1.73f*t)*Mathf.Exp(-t*48);
                float fade=Mathf.Min(1,i/18f)*Mathf.Min(1,(length-1-i)/45f);
                data[i]=(resonance*.62f+transient*.24f)*amplitude*fade;
            }
            var clip=AudioClip.Create(name,length,1,22050,false);clip.SetData(data,0);return clip;
        }
    }
}
