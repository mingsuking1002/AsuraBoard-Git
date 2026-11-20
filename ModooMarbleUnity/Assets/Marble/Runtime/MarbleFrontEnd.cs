using System;
using System.Collections;
using System.IO;
using System.Linq;
using Marble.Core;
using Marble.Online;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        readonly PlayLoop flow=new PlayLoop();
        bool diagnosticMode,localRoom,steamRoomMode=true,frontLeaveConfirm;
        int profileAvatar,profileColor,lobbyMap;int profileOutfit=WardrobeCatalog.DefaultCode;
        string lastFrontSteamStatus="";
        string localName="플레이어",frontStatus="친구와 방을 만들거나 찾아서 시작하세요.";
        CircuitModels menuModels;
        static readonly string[] RobotNames=CosmeticCatalog.AvatarNames;
        void InitializeFrontEnd()
        {
            profileAvatar=Mathf.Clamp(PlayerPrefs.GetInt("profile.avatar",0),0,3);profileColor=Mathf.Clamp(PlayerPrefs.GetInt("profile.color",0),0,3);
            profileOutfit=PlayerPrefs.GetInt("profile.outfit",WardrobeCatalog.DefaultCode);if(!WardrobeCatalog.IsValid(profileOutfit))profileOutfit=WardrobeCatalog.DefaultCode;
            localName=PlayerPrefs.GetString("profile.name","플레이어");LoadClientPreferences();
            LoadAchievements("offline");
            if(!diagnosticMode)RefreshMenuModel();
        }
        void RefreshMenuModel()
        {
            if(menuModels!=null)Destroy(menuModels.gameObject);
            var root=new GameObject("Menu character preview");root.transform.SetParent(transform);menuModels=root.AddComponent<CircuitModels>();
            menuModels.Initialize(new[]{profileAvatar,1,2,3},new[]{profileColor,LocalAiColor(1),LocalAiColor(2),LocalAiColor(3)},new[]{profileOutfit,LocalAiOutfit(1),LocalAiOutfit(2),LocalAiOutfit(3)});menuModels.SetDiceSkin(profileDiceSkin);
        }
        static int LocalAiColorFor(int myColor,int seat)
        {int remaining=seat;for(int color=0;color<4;color++)if(color!=myColor && --remaining==0)return color;return 0;}
        int LocalAiColor(int seat)=>LocalAiColorFor(profileColor,seat);
        static int LocalAiOutfit(int seat)
        {
            int outfit=WardrobeCatalog.DefaultCode;
            if(seat==1)return WardrobeCatalog.Set(outfit,0,1);
            if(seat==2)return WardrobeCatalog.Set(outfit,4,2);
            if(seat==3){outfit=WardrobeCatalog.Set(outfit,1,3);outfit=WardrobeCatalog.Set(outfit,2,4);return WardrobeCatalog.Set(outfit,3,1);}
            return outfit;
        }
        void SaveProfile()
        {
            localName=OnlineProtocol.CleanName(localName);PlayerPrefs.SetString("profile.name",localName);PlayerPrefs.SetInt("profile.avatar",profileAvatar);PlayerPrefs.SetInt(CosmeticKey("avatar"),profileAvatar);PlayerPrefs.SetInt(CosmeticKey("dice"),profileDiceSkin);PlayerPrefs.SetInt("profile.color",profileColor);PlayerPrefs.SetInt("profile.outfit",profileOutfit);PlayerPrefs.Save();
            onlineSession?.SetAppearance(profileAvatar,profileColor,profileDiceSkin,profileOutfit);frontStatus="커스터마이징을 저장했습니다.";flow.Navigate(PlayScreen.Main);
        }
        void TickFrontEnd()
        {
            if(!localRoom && steamRooms!=null && lastFrontSteamStatus!=steamRooms.Status){lastFrontSteamStatus=steamRooms.Status;frontStatus=lastFrontSteamStatus;}
            if(bodyAudioSettings){if(Input.GetKeyDown(KeyCode.Escape))bodyAudioSettings=false;return;}
            if(frontLeaveConfirm){if(Input.GetKeyDown(KeyCode.Escape))frontLeaveConfirm=false;return;}
            if(preferencesOpen){if(Input.GetKeyDown(KeyCode.Escape))ClosePreferences(false);return;}
            if(achievementSheet){if(Input.GetKeyDown(KeyCode.Escape))achievementSheet=false;return;}
            if(Input.GetKeyDown(KeyCode.Escape))FrontBack();
        }
        void FrontBack(){if(flow.Screen==PlayScreen.Customize){CancelWardrobe();return;}if(flow.Screen==PlayScreen.Lobby)frontLeaveConfirm=true;else if(flow.Screen!=PlayScreen.Main)flow.Navigate(PlayScreen.Main);}
        void OpenFrontPreferences(){preferencesOpen=true;}
        void OpenRoomMenu(PlayScreen screen)
        {flow.Navigate(screen);if(steamRoomMode && (steamRooms==null || !steamRooms.Available))ConnectSteam();}
        void CreatePlayRoom()
        {
            if(flow.InRoom)return;
            if(steamRoomMode)
            {
                if(steamRooms==null || !steamRooms.Available)ConnectSteam();
                if(steamRooms!=null && steamRooms.Available)steamRooms.CreateRoom(onlineRoomName,roomCapacity,roomPrivacy);
                else frontStatus="Steam 연결을 확인하거나 로컬 방으로 전환해 주세요.";
            }
            else{localRoom=true;players=roomCapacity;humans=1;flow.EnterRoom();frontStatus="로컬 대기 로비입니다. 빈자리는 AI가 참가합니다.";}
        }
        void BeginLobbyMatch()
        {
            if(flow.Screen!=PlayScreen.Lobby)return;
            if(!int.TryParse(seedText,out int seed))seed=20261025;
            if(!localRoom){onlineSession?.StartMatch(seed);return;}
            Model=new Game(new GameConfig{Seed=seed,HumanCount=humans,PlayerCount=players,EnableAugments=true,AugmentRounds=Game.CreateAugmentSchedule(augmentChoices),MapId=lobbyMap,EndOnFirstBankruptcy=false,EnableBodyEconomy=true});
            Model.Players[0].Name=localName;Model.Players[0].Avatar=profileAvatar;Model.Players[0].ColorIndex=profileColor;Model.Players[0].DiceSkin=profileDiceSkin;Model.Players[0].Outfit=profileOutfit;
            for(int i=1;i<Model.Players.Count;i++){Model.Players[i].ColorIndex=LocalAiColor(i);Model.Players[i].Outfit=LocalAiOutfit(i);}
            InstallModel(Model);flow.StartMatch();
        }
        void ReturnMatchToLobby()
        {
            settings=help=onlineOpen=false;autoHuman=false;
            if(localRoom)flow.ReturnToLobby();
            else if(onlineSession==null || !onlineSession.RequestLobbyReturn())
            {if(steamRooms!=null && steamRooms.RoomId!=0)flow.ReturnToLobby();else flow.LeaveRoom();}
        }
        void LeavePlayRoom()
        {
            DisposeRoomVoice();bodyAudioSettings=preferencesOpen=frontLeaveConfirm=false;
            if(!localRoom)onlineSession?.Leave();
            localRoom=false;onlineGameAttached=false;settings=help=onlineOpen=false;flow.LeaveRoom();
        }
        void DrawFrontEnd()
        {
            SetupStyles();var origin=CanvasOffset;GUI.matrix=Matrix4x4.TRS(new Vector3(origin.x,origin.y,0),Quaternion.identity,new Vector3(CanvasScale,CanvasScale,1));GUI.skin.font=font;
            PaperTheme.Surface(new Rect(0,0,1440,900),PaperTheme.FrontTable);
            CircuitArt.Circle(new Rect(55,115,615,615),new Color(1,.95f,.78f,.035f));
            CircuitArt.Circle(new Rect(1010,400,480,480),new Color(1,.95f,.78f,.025f));
            // Quiet hand-drawn marks add character without competing with controls.
            for(int i=0;i<12;i++)
            {
                float x=54+i*124,y=116+(i%4)*178;var mark=new Color(1,.95f,.8f,.14f);
                CircuitArt.Line(new Vector2(x,y),new Vector2(x+10,y+10),mark,2);
                CircuitArt.Line(new Vector2(x+10,y),new Vector2(x,y+10),mark,2);
            }
            bool previous=GUI.enabled;GUI.enabled=previous && !achievementSheet && !bodyAudioSettings && !preferencesOpen && !frontLeaveConfirm;
            DrawFrontHeader();
            switch(flow.Screen)
            {
                case PlayScreen.Main:DrawMainMenu();break;
                case PlayScreen.Customize:DrawCharacterMenu();break;
                case PlayScreen.Options:DrawEnvironmentMenu();break;
                case PlayScreen.CreateRoom:DrawCreateMenu();break;
                case PlayScreen.FindRoom:DrawFindMenu();break;
                case PlayScreen.Lobby:DrawPlayLobby();break;
            }
            PaperTheme.Card(new Rect(32,840,1376,36),PaperTheme.Paper,false,1.6f);
            GUI.Label(new Rect(49,846,1340,23),frontStatus,small);
            GUI.enabled=previous;
            if(preferencesOpen){Shade();DrawPreferencesWindow(false);}
            if(achievementSheet)DrawAchievements();
            if(bodyAudioSettings)DrawBodyAudioSettings();
            if(frontLeaveConfirm)DrawFrontLeaveConfirmation();
        }
        void DrawFrontHeader()
        {
            string heading=flow.Screen==PlayScreen.Lobby?"게임 로비":flow.Screen==PlayScreen.CreateRoom?"방 만들기":flow.Screen==PlayScreen.FindRoom?"방 찾기":flow.Screen==PlayScreen.Customize?"캐릭터 꾸미기":flow.Screen==PlayScreen.Options?"환경설정":DisplayGameName;
            PaperTheme.Card(new Rect(32,24,1376,72),PaperTheme.Paper,true,2.3f);
            CircuitArt.Fill(new Rect(40,32,5,56),PaperTheme.Forest);
            GUI.Label(new Rect(57,35,610,39),heading,title);
            GUI.Label(new Rect(60,72,740,20),flow.Screen==PlayScreen.Lobby?"사람과 규칙을 확인하고, 준비되면 한 판 시작해요.":"굴리고 사고 버티는 보드 파티",tiny);
            var statusStyle=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12,wordWrap=false};
            PaperTheme.Chip(new Rect(872,47,177,27),PaperTheme.Wash(steamRooms!=null && steamRooms.Available?PaperTheme.Forest:PaperTheme.Muted,.16f));
            GUI.Label(new Rect(876,47,169,27),steamRooms!=null && steamRooms.Available?"●  Steam 연결됨":"○  로컬 플레이 가능",statusStyle);
            if(flow.Screen!=PlayScreen.Options && Button(new Rect(1080,40,147,42),"환경설정",muted))OpenFrontPreferences();
            if(flow.Screen!=PlayScreen.Main && Button(new Rect(1242,40,140,42),flow.Screen==PlayScreen.Lobby?"방 나가기":"뒤로",muted,!RoomOperationBusy))FrontBack();
        }
        void DrawFrontLeaveConfirmation()
        {
            Shade();PaperTheme.Card(new Rect(430,290,580,290),PaperTheme.Paper,true,3);
            GUI.Label(new Rect(463,320,514,45),"대기실을 나갈까요?",title);
            GUI.Label(new Rect(463,385,514,65),"다시 함께하려면 방 찾기나 친구 초대로 참가해야 해요.",label);
            if(Button(new Rect(463,493,245,48),"계속 머물기",muted))frontLeaveConfirm=false;
            if(Button(new Rect(727,493,250,48),"방 나가기",MarbleBoardView.PlayerColors[1]))LeavePlayRoom();
        }
        void DrawMainMenu()
        {
            PaperTheme.Chip(new Rect(102,145,214,33),PaperTheme.Sun);
            CircuitArt.Label(new Rect(106,146,206,30),"2–4인 · 보드 파티",font,14,ink,true);
            var logo=new GUIStyle(title){fontSize=86,wordWrap=false};logo.normal.textColor=PaperTheme.Paper;
            var shadow=new GUIStyle(logo);shadow.normal.textColor=new Color(.08f,.12f,.09f,.46f);
            GUI.Label(new Rect(103,191,625,122),DisplayGameName,shadow);
            GUI.Label(new Rect(99,187,625,122),DisplayGameName,logo);
            CircuitArt.Fill(new Rect(103,311,285,7),PaperTheme.Coral);
            var slogan=new GUIStyle(label){fontSize=23,fontStyle=FontStyle.Bold};slogan.normal.textColor=PaperTheme.Paper;
            GUI.Label(new Rect(102,335,596,52),"굴리고, 사고, 끝까지 버텨라.",slogan);
            var feature=new Rect(96,414,646,178);PaperTheme.Card(feature,PaperTheme.Paper,true,2.2f);
            CircuitArt.Fill(new Rect(106,424,5,158),PaperTheme.Coral);
            var featureTitle=new GUIStyle(label){fontSize=21,fontStyle=FontStyle.Bold};
            GUI.Label(new Rect(126,440,270,35),"돈도, 운도 한 판 승부",featureTitle);
            GUI.Label(new Rect(126,485,270,74),"땅을 사고 통행료를 받으세요.\n필요하면 팔아서 빚을 갚을 수도!",small);
            DrawMiniBoardArtwork(new Rect(408,429,302,145));
            PaperTheme.Card(new Rect(96,619,646,148),PaperTheme.Wash(MarbleBoardView.PlayerColors[profileColor],.14f),true,2.2f);
            var portrait=SteamPortraitForId(steamRooms!=null && steamRooms.Available?steamRooms.LocalId:0);
            if(portrait==null && menuModels!=null)portrait=menuModels.Portrait(0);
            if(portrait!=null)GUI.DrawTexture(new Rect(115,630,107,123),portrait,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(236,640,340,34),localName,label);GUI.Label(new Rect(236,679,325,26),"내 캐릭터 · 옷과 외형을 꾸며요",small);
            if(Button(new Rect(548,677,165,43),"옷장 열기",muted))OpenWardrobe();
            PaperTheme.Card(new Rect(800,145,510,642),PaperTheme.Paper,true,2.8f);
            GUI.Label(new Rect(837,177,434,45),"한 판 시작",title);
            GUI.Label(new Rect(839,225,425,27),"친구들과 함께, 또는 AI와 가볍게.",small);
            if(DrawMainAction(new Rect(837,273,434,93),"방 만들기","친구를 초대하거나 AI와 시작",true,true))OpenRoomMenu(PlayScreen.CreateRoom);
            if(DrawMainAction(new Rect(837,383,434,93),"방 찾기","공개 방 목록 또는 방 ID로 참가",false,false))OpenRoomMenu(PlayScreen.FindRoom);
            PaperTheme.Underline(new Rect(839,495,430,1),new Color(.2f,.23f,.14f,.24f));
            if(Button(new Rect(837,518,208,52),"캐릭터 옷장",muted))OpenWardrobe();
            if(Button(new Rect(1063,518,208,52),"환경설정",muted))OpenFrontPreferences();
            if(Button(new Rect(837,585,434,48),"게임 종료",muted))Application.Quit();
            GUI.Label(new Rect(840,710,424,40),steamRooms!=null && steamRooms.Available?"Steam 연결됨 · 친구 초대 가능":"로컬 플레이 또는 Steam 친구와 함께",small);
        }
        bool DrawMainAction(Rect r,string heading,string detail,bool primary,bool create)
        {
            bool hit=Button(r,"",primary?dark:muted);Color textColor=primary?PaperTheme.Paper:ink;
            var icon=new Rect(r.x+19,r.y+22,48,48);PaperTheme.Chip(icon,create?PaperTheme.Sun:PaperTheme.Wash(PaperTheme.Coral,.65f));
            if(create)
            {
                PaperTheme.Frame(new Rect(icon.x+11,icon.y+10,26,27),ink,2);
                CircuitArt.Circle(new Rect(icon.x+16,icon.y+15,5,5),ink);CircuitArt.Circle(new Rect(icon.x+27,icon.y+26,5,5),ink);CircuitArt.Circle(new Rect(icon.x+21.5f,icon.y+20.5f,5,5),ink);
            }
            else
            {
                CircuitArt.Circle(new Rect(icon.x+11,icon.y+9,23,23),ink);CircuitArt.Circle(new Rect(icon.x+14,icon.y+12,17,17),PaperTheme.Wash(PaperTheme.Coral,.65f));
                CircuitArt.Line(new Vector2(icon.x+30,icon.y+29),new Vector2(icon.x+38,icon.y+37),ink,3);
            }
            var head=new GUIStyle(label){fontSize=21,fontStyle=FontStyle.Bold,wordWrap=false};head.normal.textColor=textColor;
            var sub=new GUIStyle(small){fontSize=13,wordWrap=false};sub.normal.textColor=primary?new Color(1,1,.96f,.86f):PaperTheme.Muted;
            GUI.Label(new Rect(r.x+84,r.y+17,305,32),heading,head);
            GUI.Label(new Rect(r.x+85,r.y+53,305,25),detail,sub);
            CircuitArt.Label(new Rect(r.xMax-45,r.y+27,28,32),"→",font,21,textColor,true);
            return hit;
        }
        void DrawMiniBoardArtwork(Rect r)
        {
            var center=new Vector2(r.center.x,r.center.y);
            for(int i=0;i<32;i++)
            {
                var p=MarbleBoardView.CellCenter(i);
                float x=center.x+(p.x-720)*r.width/1180f,y=center.y+(p.y-440)*r.height/720f;
                bool corner=i%8==0;var cell=new Rect(x-(corner?8:6),y-(corner?6:4),corner?16:12,corner?12:8);
                CircuitArt.Fill(cell,corner?PaperTheme.Sun:PaperTheme.Wash(MarbleBoardView.LineColors[i/8],.75f));
                PaperTheme.Frame(cell,ink,1);
            }
            if(menuModels!=null && menuModels.DiceTexture!=null)GUI.DrawTexture(new Rect(center.x-47,center.y-22,94,45),menuModels.DiceTexture,ScaleMode.ScaleToFit,true);
        }
        void DrawCharacterMenu()=>DrawWardrobe();
        void DrawEnvironmentMenu()=>DrawPreferencesWindow(false);
        void SaveEnvironment()=>SaveClientPreferences();
        bool RoomOperationBusy=>steamRooms!=null && steamRooms.Busy;
        void DrawCreateMenu()
        {
            PaperTheme.Card(new Rect(200,145,1040,650),PaperTheme.Paper,true,3);
            GUI.Label(new Rect(240,174,930,42),"먼저 대기실부터.",title);
            GUI.Label(new Rect(242,225,930,29),"방을 만든 뒤 친구를 초대하고 맵과 규칙을 정해요.",small);
            GUI.Label(new Rect(244,291,420,28),"방 이름",label);
            onlineRoomName=PaperInput(new Rect(244,332,420,46),onlineRoomName,32,label);
            GUI.Label(new Rect(244,420,420,28),"공개 범위",label);
            string[] privacy={"공개","친구 전용","프라이빗"};
            for(int i=0;i<3;i++)if(Button(new Rect(244+i*143,460,133,46),privacy[i],(int)roomPrivacy==i?dark:muted,!RoomOperationBusy))roomPrivacy=(RoomPrivacy)i;
            GUI.Label(new Rect(244,525,420,77),roomPrivacy==RoomPrivacy.Private?"프라이빗 방은 공개 목록에 표시되지 않아요. 초대 또는 방 ID로 함께하세요.":roomPrivacy==RoomPrivacy.FriendsOnly?"친구나 초대받은 사람이 참가할 수 있어요.":"공개 방 목록에서 다른 플레이어가 참가할 수 있어요.",small);
            GUI.Label(new Rect(756,291,420,28),"누구와 플레이할까요?",label);
            if(Button(new Rect(756,332,202,46),"Steam 친구와",steamRoomMode?dark:muted,!RoomOperationBusy))steamRoomMode=true;
            if(Button(new Rect(974,332,202,46),"로컬 · AI와",!steamRoomMode?dark:muted,!RoomOperationBusy))steamRoomMode=false;
            GUI.Label(new Rect(756,420,420,28),"참가 인원",label);
            for(int i=2;i<=4;i++)if(Button(new Rect(756+(i-2)*143,460,133,46),i+"명",roomCapacity==i?dark:muted,!RoomOperationBusy))roomCapacity=i;
            GUI.Label(new Rect(756,525,420,77),steamRoomMode?"친구 초대와 준비 상태는 대기실에서 확인해요.":"빈자리는 AI가 채워요. 게임은 대기실의 시작 버튼으로 시작해요.",small);
            PaperTheme.Underline(new Rect(242,653,934,1),ink);
            GUI.Label(new Rect(244,680,534,66),RoomOperationBusy?"방을 준비하고 있어요…":steamRoomMode?(steamRooms?.Status??"Steam 연결이 필요합니다."):"로컬 대기실을 만들 준비가 됐어요.",small);
            bool valid=!string.IsNullOrWhiteSpace(onlineRoomName) && !RoomOperationBusy;
            if(Button(new Rect(827,694,349,52),RoomOperationBusy?"만드는 중…":"대기실 만들기 →",dark,valid))CreatePlayRoom();
        }
        void DrawFindMenu()
        {
            PaperTheme.Card(new Rect(96,145,1248,645),PaperTheme.Paper,true,3);
            GUI.Label(new Rect(130,174,750,43),"같이 굴릴 사람?",title);
            bool available=steamRooms!=null && steamRooms.Available,busy=RoomOperationBusy;
            if(Button(new Rect(973,178,163,44),available?"Steam 연결됨":"Steam 연결",muted,!busy && !available))ConnectSteam();
            if(Button(new Rect(1151,178,160,44),busy?"검색 중…":"새로고침",dark,available && !busy))steamRooms.SearchRooms();
            GUI.Label(new Rect(132,239,1175,31),steamRooms?.Status??"Steam을 연결하면 참가할 수 있는 공개 방이 표시돼요.",small);
            PaperTheme.Surface(new Rect(130,291,1180,42),PaperTheme.Wash(PaperTheme.Table,.22f));
            GUI.Label(new Rect(148,300,670,26),"방 이름",small);GUI.Label(new Rect(853,300,160,26),"인원",small);GUI.Label(new Rect(1046,300,180,26),"참가",small);
            int count=steamRooms?.Rooms.Count??0;
            roomListScroll=GUI.BeginScrollView(new Rect(130,342,1180,265),roomListScroll,new Rect(0,0,1152,Math.Max(265,count*75)));
            if(count==0)
            {
                CircuitArt.Circle(new Rect(550,32,52,52),PaperTheme.Wash(PaperTheme.Table,.25f));
                CircuitArt.Label(new Rect(550,32,52,52),"?",font,24,ink,true);
                GUI.Label(new Rect(130,103,880,44),busy?"방을 찾고 있어요…":available?"열린 방이 없어요. 새로고침하거나 직접 방을 만들어 보세요.":"Steam 연결 후 방을 찾아보세요.",center);
            }
            if(steamRooms!=null)for(int i=0;i<count;i++)
            {
                var room=steamRooms.Rooms[i];bool full=room.Members>=room.Capacity;
                if(i%2==0)PaperTheme.Surface(new Rect(8,i*75+2,1124,66),PaperTheme.Wash(PaperTheme.Table,.08f));
                CircuitArt.Circle(new Rect(19,i*75+24,14,14),full?PaperTheme.Muted:PaperTheme.Forest);
                var roomNameStyle=new GUIStyle(label){fontStyle=FontStyle.Bold,wordWrap=false};while(roomNameStyle.fontSize>12 && roomNameStyle.CalcSize(new GUIContent(room.Name)).x>650)roomNameStyle.fontSize--;
                GUI.Label(new Rect(48,i*75+16,650,40),room.Name,roomNameStyle);
                GUI.Label(new Rect(723,i*75+17,160,33),room.Members+" / "+room.Capacity,label);
                if(Button(new Rect(915,i*75+10,213,46),full?"정원 초과":busy?"처리 중…":"들어가기",dark,available && !busy && !full))steamRooms.JoinRoom(room.Id);
                PaperTheme.Underline(new Rect(12,i*75+69,1120,1),new Color(.2f,.23f,.14f,.25f));
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(134,648,290,29),"초대받은 방 ID로 입장",label);
            joinRoomId=PaperInput(new Rect(429,642,610,46),joinRoomId,20,label);
            if(Button(new Rect(1058,642,250,46),"방 ID로 입장",dark,available && !busy))
            {if(ulong.TryParse(joinRoomId,out ulong id) && id!=0)steamRooms.JoinRoom(id);else frontStatus="숫자로 된 방 ID를 입력해 주세요.";}
            GUI.Label(new Rect(134,720,1158,30),"프라이빗 방은 목록에 나오지 않아요. 친구 초대나 방 ID를 이용하세요.",small);
        }
        void SetLobbyMap(int value){if(localRoom)lobbyMap=value;else onlineSession?.SetMap(value);}
        void SetLobbyAugments(int value){if(localRoom)augmentChoices=value;else onlineSession?.SetAugmentCount(value);}
        string LocalMemberName(int seat)=>seat==0?localName:"CPU "+seat;
        void DrawPlayLobby()
        {
            bool online=!localRoom,host=localRoom || (onlineSession!=null && onlineSession.IsHost);
            int map=localRoom?lobbyMap:onlineSession?.MapId??0,count=localRoom?augmentChoices:onlineSession?.AugmentCount??4;
            var lobbyTitle=new GUIStyle(title);lobbyTitle.normal.textColor=PaperTheme.Paper;
            var lobbyCaption=new GUIStyle(small);lobbyCaption.normal.textColor=PaperTheme.Paper;
            GUI.Label(new Rect(64,129,820,44),localRoom?onlineRoomName:steamRooms?.RoomName??"게임 로비",lobbyTitle);
            GUI.Label(new Rect(67,177,700,25),localRoom?"로컬 파티 · 빈자리는 AI로 채워집니다":"참가자와 규칙을 확인한 뒤 준비해 주세요.",lobbyCaption);
            if(Button(new Rect(1125,133,230,45),"＋ 친구 초대",dark,online && onlineSession!=null && onlineSession.Stage==OnlineStage.Lobby))steamRooms.OpenInviteOverlay();
            for(int i=0;i<4;i++)DrawLobbyPlayer(i,new Rect(64+i*329,210,314,220));
            PaperTheme.Card(new Rect(64,450,1306,252),PaperTheme.Paper,true,2.4f);
            var sectionTitle=new GUIStyle(label){fontSize=22,fontStyle=FontStyle.Bold};
            GUI.Label(new Rect(89,467,250,33),"이번 판 규칙",sectionTitle);
            PaperTheme.Chip(new Rect(1194,468,148,27),PaperTheme.Wash(host?PaperTheme.Sun:PaperTheme.Muted,.25f));
            var authority=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12,wordWrap=false};
            GUI.Label(new Rect(1198,468,140,27),host?"방장이 변경":"읽기 전용",authority);
            DrawMapMini(new Rect(88,515,350,142),map);
            GUI.Label(new Rect(461,508,405,26),"맵 선택",small);
            for(int i=0;i<BoardCatalog.Count;i++)if(Button(new Rect(458+i*205,540,192,42),BoardCatalog.Name(i),map==i?dark:muted,host))SetLobbyMap(i);
            GUI.Label(new Rect(460,597,410,26),"증강 선택 횟수 · 보유 최대 8개",small);
            for(int i=1;i<=8;i++)if(Button(new Rect(458+(i-1)*52,630,45,39),i.ToString(),count==i?dark:muted,host))SetLobbyAugments(i);
            PaperTheme.Chip(new Rect(911,515,431,142),PaperTheme.Wash(PaperTheme.Sun,.15f));
            GUI.Label(new Rect(930,527,390,29),"건설 규칙",label);
            GUI.Label(new Rect(930,560,390,75),"첫 바퀴는 별장까지 · 완주마다 건설 단계 +1\n같은 종류 건물은 한 번만 건설\n온라인 설정 변경 시 참가자 준비 해제",small);
            PaperTheme.Card(new Rect(64,730,1306,78),PaperTheme.Paper,true,2.5f);
            bool canStart=localRoom || (onlineSession!=null && onlineSession.CanStart);
            GUI.Label(new Rect(88,744,710,28),localRoom?"로컬 방 · AI 준비 완료":host?(canStart?"모든 참가자가 준비했어요.":"참가자의 준비를 기다리고 있어요."):"준비를 마친 뒤 방장을 기다려 주세요.",label);
            string detail=localRoom?"게임은 시작 버튼을 누른 뒤 입장해요.":PrivacyName(steamRooms.Privacy)+" · 방 ID "+steamRooms.RoomId;
            GUI.Label(new Rect(88,778,706,22),detail,small);
            if(online && Button(new Rect(822,746,182,45),"방 ID 복사",muted))GUIUtility.systemCopyBuffer=steamRooms.RoomId.ToString();
            if(host)
            {if(Button(new Rect(1020,746,320,45),"게임 시작 →",dark,canStart))BeginLobbyMatch();}
            else if(onlineSession!=null)
            {
                bool ready=onlineSession.Roster.Any(member=>member.Id==steamRooms.LocalId && member.Ready);
                if(Button(new Rect(1020,746,320,45),ready?"준비 취소":"준비하기",dark,onlineSession.Stage==OnlineStage.Lobby))onlineSession.SetReady(!ready);
            }
        }
        void DrawLobbyPlayer(int seat,Rect r)
        {
            bool occupied=localRoom?seat<players:onlineSession!=null && seat<onlineSession.Roster.Count;
            var member=!localRoom && occupied?onlineSession.Roster[seat]:null;
            int color=localRoom?(seat==0?profileColor:LocalAiColor(seat)):member?.ColorIndex??0;
            var tint=MarbleBoardView.PlayerColors[Mathf.Clamp(color,0,3)];
            PaperTheme.Card(r,occupied?PaperTheme.Wash(tint,.09f):PaperTheme.Paper,true,2.3f,seat);
            if(occupied)CircuitArt.Fill(new Rect(r.x+6,r.y+6,r.width-12,6),tint);
            if(!occupied)
            {
                CircuitArt.Circle(new Rect(r.center.x-24,r.y+45,48,48),PaperTheme.Wash(PaperTheme.Table,.24f));
                CircuitArt.Label(new Rect(r.center.x-24,r.y+45,48,48),"＋",font,28,PaperTheme.Muted,true);
                GUI.Label(new Rect(r.x+30,r.y+103,r.width-60,34),"친구를 기다리는 중",center);
                var hint=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
                GUI.Label(new Rect(r.x+30,r.y+143,r.width-60,40),localRoom?"선택한 정원 밖의 자리예요.":"초대하면 이 자리에 참가해요.",hint);
                return;
            }
            string name=localRoom?LocalMemberName(seat):member.Name;
            bool owner=localRoom?seat==0:member.Id==steamRooms.HostId,ready=localRoom || member.Ready;
            var nameStyle=new GUIStyle(label){fontStyle=FontStyle.Bold,wordWrap=false};
            while(nameStyle.fontSize>12 && nameStyle.CalcSize(new GUIContent(name)).x>r.width-150)nameStyle.fontSize--;
            GUI.Label(new Rect(r.x+20,r.y+25,r.width-150,31),name,nameStyle);
            PaperTheme.Chip(new Rect(r.xMax-126,r.y+25,106,30),PaperTheme.Wash(owner?PaperTheme.Sun:ready?PaperTheme.Forest:PaperTheme.Muted,.18f));
            var stateStyle=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12,wordWrap=false};GUI.Label(new Rect(r.xMax-124,r.y+25,102,30),owner?"방장":ready?"준비 완료":"준비 중",stateStyle);
            Texture portrait=localRoom?(menuModels!=null?menuModels.Portrait(seat):null):SteamPortraitForId(member.Id);
            PaperTheme.Card(new Rect(r.x+20,r.y+70,88,100),PaperTheme.Wash(tint,.21f),false,1.6f);
            if(portrait!=null)GUI.DrawTexture(new Rect(r.x+23,r.y+73,82,94),portrait,ScaleMode.ScaleToFit,true);
            else {CircuitArt.Circle(new Rect(r.x+50,r.y+91,31,31),tint);CircuitArt.Circle(new Rect(r.x+39,r.y+127,54,36),tint);}
            GUI.Label(new Rect(r.x+124,r.y+82,r.width-143,31),"자리 "+(seat+1),label);
            GUI.Label(new Rect(r.x+124,r.y+122,r.width-143,43),localRoom?(seat==0?"나 · 로컬 플레이어":"AI · 준비 완료"):"연결됨 · "+(member.Id==steamRooms.LocalId?"나":"참가자"),small);
            if(Button(new Rect(r.x+20,r.y+181,r.width-40,30),"참가자별 소리 설정",muted))bodyAudioSettings=true;
        }
        void DrawMapMini(Rect r,int map)
        {
            PaperTheme.Card(r,PaperTheme.Wash(map==0?Color.green:Color.cyan,.13f),false,2.5f);
            for(int i=0;i<32;i++)
            {var c=MarbleBoardView.CellCenter(i);var mini=new Rect(r.x+8+(c.x-180)*(r.width-28)/1080f,r.y+8+(c.y-107)*(r.height-64)/666f,14,12);CircuitArt.Fill(mini,PaperTheme.Wash(MarbleBoardView.GroupColors[(i/4)%8],.5f));PaperTheme.Frame(mini,ink,1);}
            CircuitArt.Label(new Rect(r.x+35,r.y+r.height*.24f,r.width-70,30),BoardCatalog.Name(map),font,18,ink,true);
            CircuitArt.Label(new Rect(r.x+22,r.y+r.height-26,r.width-44,22),map==0?"기본 도시 · 균형 경제":"도시 비용 -20% · 관광지 통행료 +20%",font,11,ink,false);
        }
        void DrawMatchMenu()
        {
            Shade();Panel(new Rect(420,205,600,490),cream);GUI.Label(new Rect(455,235,530,44),"게임 메뉴",title);
            if(Button(new Rect(461,320,516,48),"게임 계속",dark))settings=false;
            if(Button(new Rect(461,386,516,48),"환경설정 · 마이크 · 소리",muted))OpenInGamePreferences();
            if(Button(new Rect(461,452,516,48),"게임 나가기 → 같은 방 로비",muted))ReturnMatchToLobby();
            GUI.Label(new Rect(461,534,516,72),"온라인에서는 현재 경기를 종료하고 참가자 모두가 같은 로비로 돌아갑니다. 방과 설정은 유지됩니다.",small);
            if(localRoom && Button(new Rect(461,621,516,40),"현재 로컬 게임 저장",muted))Save();
        }
        void DrawMatchResults()
        {
            if(settings || help)return;
            Shade();Panel(new Rect(350,151,740,607),cream);
            GUI.Label(new Rect(390,177,660,47),"이번 경기 결과",title);
            PaperTheme.Chip(new Rect(390,238,660,48),PaperTheme.Wash(PaperTheme.Sun,.30f));
            GUI.Label(new Rect(402,242,636,40),Model.WinnerReason,center);
            var standings=Model.GetStandings();
            for(int row=0;row<standings.Count;row++)
            {
                var entry=standings[row];float y=311+row*77;var player=Model.Players[entry.PlayerIndex];Color c=MarbleBoardView.PlayerColors[player.ColorIndex];
                PaperTheme.Card(new Rect(389,y,660,64),PaperTheme.Wash(c,entry.Place==1?.20f:.08f),false,1.8f);
                CircuitArt.Circle(new Rect(402,y+9,46,46),entry.Place==1?PaperTheme.Sun:PaperTheme.Wash(c,.35f));
                CircuitArt.Label(new Rect(402,y+9,46,46),entry.Place+"위",font,16,ink,true);
                var avatar=SteamPortrait(entry.PlayerIndex)??Board.CharacterPortrait(entry.PlayerIndex);
                if(avatar!=null)GUI.DrawTexture(new Rect(462,y+5,49,54),avatar,ScaleMode.ScaleToFit,true);
                var nameStyle=new GUIStyle(label){fontStyle=FontStyle.Bold,wordWrap=false};
                while(nameStyle.fontSize>12 && nameStyle.CalcSize(new GUIContent(player.Name)).x>225)nameStyle.fontSize--;
                GUI.Label(new Rect(519,y+17,225,32),player.Name,nameStyle);
                var right=new GUIStyle(small){alignment=TextAnchor.MiddleRight};GUI.Label(new Rect(757,y+12,275,37),entry.Bankrupt?(Model.Config.EnableBodyEconomy?"탈락":"파산"):"총자산 "+Money(entry.NetWorth),right);
            }
            if(Button(new Rect(449,665,542,49),"같은 방 로비로 돌아가기",dark))ReturnMatchToLobby();
        }
        [Serializable] sealed class LoopPreviewReport
        {public string status;public bool mainFirst,lobbyBeforeGame,customizationApplied,mapApplied,bankruptcyEnds,placements,returnLobby,restart;}
        IEnumerator PlayLoopPreview(string path)
        {
            wardrobeRuntimeChecking=true;
            var report=new LoopPreviewReport{status="failed",mainFirst=flow.Screen==PlayScreen.Main && Model==null};
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-main.png");
            flow.Navigate(PlayScreen.Customize);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-customize.png");
            flow.Navigate(PlayScreen.Options);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-options.png");
            flow.Navigate(PlayScreen.CreateRoom);steamRoomMode=false;CreatePlayRoom();lobbyMap=1;
            report.lobbyBeforeGame=flow.Screen==PlayScreen.Lobby && Model==null;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-lobby.png");
            BeginLobbyMatch();report.customizationApplied=Model.Players[0].Avatar==profileAvatar && Model.Players[0].ColorIndex==profileColor;report.mapApplied=Model.Config.MapId==1 && Model.Tiles[1].Definition.Name=="부산";
            settings=true;while(Model.Phase==GamePhase.Augment)Model.Execute(Model.ChooseAiAction());
            Model.Players[0].Cash=-100;Model.Players[0].CreditDueRound=Model.EconomyRound;
            for(int i=2;i<Model.Players.Count;i++)Model.Players[i].Bankrupt=true;
            int settlementBudget=8;while(Model.Phase!=GamePhase.Repayment && settlementBudget-->0)Model.TestAdvanceEconomyTurn();
            Model.Execute("repayment_confirm");settings=false;
            if(Model.Phase==GamePhase.Finished && flow.Screen==PlayScreen.Game)flow.EndMatch();report.bankruptcyEnds=Model.Phase==GamePhase.Finished;
            report.placements=Model.GetStandings().Count==players && Model.GetStandings().Last().PlayerIndex==0;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-results.png");
            ReturnMatchToLobby();report.returnLobby=flow.Screen==PlayScreen.Lobby && localRoom;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-returned-lobby.png");
            BeginLobbyMatch();report.restart=flow.Screen==PlayScreen.Game && Model.Phase!=GamePhase.Finished;
            if(report.mainFirst && report.lobbyBeforeGame && report.customizationApplied && report.mapApplied && report.bankruptcyEnds && report.placements && report.returnLobby && report.restart)report.status="passed";
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
    }
}
