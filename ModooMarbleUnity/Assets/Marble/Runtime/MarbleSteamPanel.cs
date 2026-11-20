using System;
using System.Collections;
using System.Collections.Generic;
using Steamworks;
using System.IO;
using System.Linq;
using Marble.Core;
using Marble.Online;
using UnityEngine;

namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        [Serializable] sealed class SteamSettings { public uint appId=480; public bool developmentAppId=true; }
        [Serializable] sealed class SteamProbe { public string status,message; public uint appId;public int augmentCount; public bool initialized,privateLobbyCreated,twoAccountsVerified,invitesSent; }
        sealed class SteamPortraitEntry { public Texture2D Texture; public int Handle; public float NextCheck; }
        readonly Dictionary<ulong,SteamPortraitEntry> steamPortraits=new Dictionary<ulong,SteamPortraitEntry>();
        Texture SteamPortrait(int seat)
        {
            if(steamRooms==null || !steamRooms.Available || seat<0)return null;
            ulong id=onlineGameAttached && onlineSession!=null && seat<onlineSession.Roster.Count?onlineSession.Roster[seat].Id:
                !onlineGameAttached && seat==0 && Model!=null && seat<Model.Players.Count && Model.Players[seat].IsHuman?steamRooms.LocalId:0;
            return SteamPortraitForId(id);
        }
        Texture SteamPortraitForId(ulong id)
        {
            if(id==0 || steamRooms==null || !steamRooms.Available)return null;
            if(!steamPortraits.TryGetValue(id,out var entry))
            {
                if(steamPortraits.Count>=32){var old=steamPortraits.First();if(old.Value.Texture!=null)Destroy(old.Value.Texture);steamPortraits.Remove(old.Key);}
                entry=new SteamPortraitEntry();steamPortraits.Add(id,entry);
            }
            if(Time.unscaledTime<entry.NextCheck)return entry.Texture;
            entry.NextCheck=Time.unscaledTime+3f;
            try
            {
                var person=new CSteamID(id);SteamFriends.RequestUserInformation(person,false);
                int handle=SteamFriends.GetMediumFriendAvatar(person);
                if(handle<=0){if(handle==0 && entry.Texture!=null){Destroy(entry.Texture);entry.Texture=null;entry.Handle=0;}return entry.Texture;}
                if(handle==entry.Handle && entry.Texture!=null)return entry.Texture;
                if(!SteamUtils.GetImageSize(handle,out uint width,out uint height) || width==0 || height==0 || width>256 || height>256)return entry.Texture;
                int row=(int)width*4;var pixels=new byte[row*(int)height];
                if(!SteamUtils.GetImageRGBA(handle,pixels,pixels.Length))return entry.Texture;
                var flipped=new byte[pixels.Length];for(int y=0;y<(int)height;y++)Array.Copy(pixels,y*row,flipped,((int)height-y-1)*row,row);
                var texture=new Texture2D((int)width,(int)height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(flipped);texture.Apply();
                if(entry.Texture!=null)Destroy(entry.Texture);entry.Texture=texture;entry.Handle=handle;
            }
            catch(Exception){return entry.Texture;}
            return entry.Texture;
        }
        void DisposeSteamPortraits(){foreach(var entry in steamPortraits.Values)if(entry.Texture!=null)Destroy(entry.Texture);steamPortraits.Clear();}
        SteamRoomService steamRooms;
        OnlineSession onlineSession;
        uint steamAppId=480;
        bool onlineOpen,onlineGameAttached;
        string onlineRoomName="친구와 아수라판 한 판",joinRoomId="";
        int roomCapacity=4;
        RoomPrivacy roomPrivacy=RoomPrivacy.Private;
        Vector2 roomListScroll;
        bool OnlineRoom=>(steamRooms!=null && steamRooms.RoomId!=0)||onlineGameAttached;
        bool LocalTurn=>onlineGameAttached ? onlineSession!=null && onlineSession.Stage==OnlineStage.Playing && onlineSession.LocalSeat==Model.CurrentPlayerIndex : !OnlineRoom && Model!=null && Model.CurrentPlayer.IsHuman;

        void InitializeOnline(string[] args)
        {
            try
            {
                string path=Path.Combine(Application.streamingAssetsPath,"steam-config.json");
                if(File.Exists(path)){var config=JsonUtility.FromJson<SteamSettings>(File.ReadAllText(path));if(config!=null)steamAppId=config.appId;}
            }
            catch(Exception){Toast("Steam 설정 파일을 읽지 못했습니다.");}
            if(Array.IndexOf(args,"-marble-front-check")>=0 || Array.IndexOf(args,"-marble-wardrobe-check")>=0 || Array.IndexOf(args,"-marble-loop-preview")>=0)return;
            onlineOpen=Array.IndexOf(args,"-marble-online-panel")>=0;
            ulong lobby=SteamRoomService.LaunchLobby(args);
            if(lobby!=0 && !preferences.BlockInviteJoin){ConnectSteam();if(steamRooms.Available && !steamRooms.Busy && steamRooms.RoomId==0)steamRooms.JoinRoom(lobby);onlineOpen=true;}
            if(!diagnosticMode && (lobby==0 || preferences.BlockInviteJoin))ConnectSteam();
            if(Array.IndexOf(args,"-marble-steam-smoke")>=0)StartCoroutine(SteamHostSmoke(args));
        }
        void ConnectSteam()
        {
            if(OnlineRoom)return;
            DisposeRoomVoice();
            achievementBackend?.Dispose();achievementBackend=null;achievementSync=null;
            if(onlineSession!=null)onlineSession.Dispose();else steamRooms?.Dispose();
            steamRooms=new SteamRoomService(steamAppId){BlockInviteJoin=preferences.BlockInviteJoin};
            onlineSession=new OnlineSession(steamRooms);
            onlineSession.MatchStarted+=model=>
            {
                onlineGameAttached=true;autoHuman=false;InstallModel(model);onlineOpen=false;
                if(!diagnosticMode){if(!flow.InRoom)flow.EnterRoom();if(flow.Screen!=PlayScreen.Lobby)flow.ReturnToLobby();flow.StartMatch();}
            };
            onlineSession.ActionCommitted+=id=>
            {
                Model=onlineSession.Model;Board.Sync(Game.IsRollAction(id));TickAchievements(0);
                if(!mutedSound)audioSource.PlayOneShot(clickSound);
                aiTimer=fast ? .08f : .9f;actionScroll=Vector2.zero;
            };
            onlineSession.MatchStopped+=reason=>
            {
                onlineOpen=diagnosticMode;Toast(reason);
                if(!diagnosticMode){onlineGameAttached=false;if(steamRooms.RoomId!=0){if(!flow.InRoom)flow.EnterRoom();else flow.ReturnToLobby();}else flow.LeaveRoom();}
            };
            steamRooms.RoomChanged+=()=>
            {
                if(steamRooms.RoomId==0)DisposeRoomVoice();
                if(steamRooms.RoomId!=0 && onlineSession.Stage==OnlineStage.Lobby)
                {if(diagnosticMode)onlineOpen=true;else{onlineOpen=false;onlineGameAttached=false;if(!flow.InRoom)flow.EnterRoom();else flow.ReturnToLobby();}}
                else if(!diagnosticMode && steamRooms.RoomId==0 && flow.InRoom){onlineGameAttached=false;flow.LeaveRoom();}
            };
            onlineSession.SetAppearance(profileAvatar,profileColor,profileDiceSkin,profileOutfit);
            steamRooms.Initialize();BindSteamAchievements();
            if(steamRooms.Available)
            {
                ulong invited=steamRooms.PlatformLaunchLobby();
                if(!preferences.BlockInviteJoin && invited!=0 && steamRooms.RoomId==0)steamRooms.JoinRoom(invited);
            }
        }
        void TickOnline(float delta)
        {
            TickClientPreferences();TickAchievements(delta);TickBodyAudio();TickRoomVoice();
            if(onlineSession==null)return;
            onlineSession.Tick(delta);
        }
        void LeaveOnline()
        {
            DisposeRoomVoice();onlineSession?.Leave();onlineGameAttached=false;onlineOpen=false;settings=false;
            if(diagnosticMode)StartGame(new GameConfig{Seed=20261025,HumanCount=humans,PlayerCount=players});else flow.LeaveRoom();
        }
        void OnlineSettings()
        {
            Shade();Panel(new Rect(455,220,530,430),cream);
            GUI.Label(new Rect(480,245,480,40),"온라인 경기 메뉴",modalTitle);
            GUI.Label(new Rect(490,308,440,70),onlineSession?.Status??"Steam 연결을 확인해 주세요.",label);
            if(Button(new Rect(490,397,440,46),"대기실 · 연결 정보",dark)){settings=false;onlineOpen=true;}
            if(Button(new Rect(490,462,440,46),"경기 계속",dark))settings=false;
            if(Button(new Rect(490,537,440,46),"방에서 나가기",muted))LeaveOnline();
        }
        void DrawOnlinePanel()
        {
            Shade();var r=new Rect(350,65,740,770);Panel(r,cream);
            GUI.Label(new Rect(380,87,570,42),"Steam 멀티플레이",title);
            if(Button(new Rect(995,87,65,37),"닫기",muted))onlineOpen=false;
            var statusStyle=new GUIStyle(small){wordWrap=true};
            string status=steamRooms==null?"Steam으로 친구와 함께 플레이하세요.":steamRooms.Status;
            GUI.Label(new Rect(385,142,670,53),status,statusStyle);
            if(steamRooms==null || !steamRooms.Available)
            {
                GUI.Label(new Rect(390,218,650,92),"Steam을 온라인으로 실행한 뒤 연결해 주세요.\n친구도 동일한 빌드를 실행해야 합니다. 로컬 게임은 Steam 없이 사용할 수 있습니다.",label);
                if(Button(new Rect(490,342,460,52),"Steam 연결",dark))ConnectSteam();
                GUI.Label(new Rect(390,425,650,64),steamAppId==480?"현재 빌드는 개발 테스트용 App ID 480을 사용합니다.\nSteam 출시는 전용 App ID 설정과 별도 배포가 필요합니다.":"설정된 Steam App ID: "+steamAppId,small);
                return;
            }
            if(steamRooms.RoomId==0)
            {
                GUI.Label(new Rect(385,202,200,26),"방 만들기",label);
                onlineRoomName=PaperInput(new Rect(385,239,380,36),onlineRoomName,32,label);
                for(int i=2;i<=4;i++)if(Button(new Rect(788+(i-2)*88,239,78,36),i+"명",roomCapacity==i?dark:muted,!steamRooms.Busy))roomCapacity=i;
                string[] privacy={"공개","친구 전용","프라이빗"};
                for(int i=0;i<3;i++)if(Button(new Rect(385+i*165,290,154,38),privacy[i],(int)roomPrivacy==i?dark:muted,!steamRooms.Busy))roomPrivacy=(RoomPrivacy)i;
                if(Button(new Rect(892,288,165,42),"방 만들기",dark,!steamRooms.Busy))steamRooms.CreateRoom(onlineRoomName,roomCapacity,roomPrivacy);
                GUI.Label(new Rect(385,340,665,35),roomPrivacy==RoomPrivacy.Private?"프라이빗 방은 공개 목록에 표시되지 않으며 초대받은 사람만 입장합니다.":roomPrivacy==RoomPrivacy.FriendsOnly?"친구 또는 초대받은 사람이 입장할 수 있습니다.":"공개 방 목록에서 다른 플레이어가 찾아올 수 있습니다.",small);
                GUI.Label(new Rect(385,399,250,26),"공개 방 찾기",label);
                if(Button(new Rect(888,393,170,36),"목록 새로고침",dark,!steamRooms.Busy))steamRooms.SearchRooms();
                roomListScroll=GUI.BeginScrollView(new Rect(385,442,675,211),roomListScroll,new Rect(0,0,650,Math.Max(211,steamRooms.Rooms.Count*53)));
                for(int i=0;i<steamRooms.Rooms.Count;i++)
                {
                    var room=steamRooms.Rooms[i];
                    GUI.Label(new Rect(3,i*53,480,42),room.Name+"   "+room.Members+" / "+room.Capacity,small);
                    if(Button(new Rect(515,i*53,118,38),"입장",dark,!steamRooms.Busy))steamRooms.JoinRoom(room.Id);
                }
                if(steamRooms.Rooms.Count==0)GUI.Label(new Rect(8,18,600,35),"목록 새로고침으로 같은 빌드의 공개 방을 찾습니다.",small);
                GUI.EndScrollView();
                GUI.Label(new Rect(385,679,260,25),"초대받은 방 ID로 입장",small);
                joinRoomId=PaperInput(new Rect(385,714,450,37),joinRoomId,20,label);
                if(Button(new Rect(862,712,196,41),"방 ID로 입장",dark,!steamRooms.Busy))
                {if(ulong.TryParse(joinRoomId,out ulong id))steamRooms.JoinRoom(id);else Toast("숫자로 된 방 ID를 입력해 주세요.");}
                return;
            }
            GUI.Label(new Rect(385,198,650,34),steamRooms.RoomName+"  ·  "+(onlineSession.IsHost?"내가 호스트":"참가자"),modalTitle);
            GUI.Label(new Rect(385,244,490,26),"방 ID  "+steamRooms.RoomId+"   ·   "+PrivacyName(steamRooms.Privacy),small);
            if(Button(new Rect(904,239,150,34),"ID 복사",muted))GUIUtility.systemCopyBuffer=steamRooms.RoomId.ToString();
            var roster=onlineSession.Roster.Count>0?onlineSession.Roster:steamRooms.Members;
            for(int i=0;i<roster.Count;i++)
            {
                var person=roster[i];float y=294+i*56;
                Box(new Rect(385,y,670,47),new Color(.87f,.90f,.84f));
                GUI.Label(new Rect(399,y+8,465,32),(person.Id==steamRooms.HostId?"HOST  ":"P"+(i+1)+"  ")+person.Name+(person.Id==steamRooms.LocalId?"  (나)":""),label);
                GUI.Label(new Rect(887,y+10,155,30),person.Ready?"준비 완료":"준비 대기",small);
            }
            GUI.Label(new Rect(385,532,665,62),onlineSession.Status,label);
            if(onlineSession.Stage==OnlineStage.Lobby)
            {
                if(Button(new Rect(385,608,209,45),"친구 초대",dark))steamRooms.OpenInviteOverlay();
                if(onlineSession.IsHost)
                {
                    GUI.Label(new Rect(610,610,57,30),"시드",small);
                    seedText=PaperInput(new Rect(672,611,162,36),seedText,11,label);
                    if(Button(new Rect(856,608,198,45),"게임 시작",dark,onlineSession.CanStart))
                    {if(!int.TryParse(seedText,out int seed))seed=20261025;onlineSession.StartMatch(seed);}
                }
                else
                {
                    bool ready=onlineSession.Roster.Any(p=>p.Id==steamRooms.LocalId && p.Ready);
                    if(Button(new Rect(626,608,428,45),ready?"준비 취소":"준비 완료",dark))onlineSession.SetReady(!ready);
                }
                GUI.Label(new Rect(385,665,665,23),"증강 선택 횟수 · 기본 4회 / 보유 최대 8개"+(onlineSession.IsHost?"":" · 호스트 설정"),small);
                for(int i=1;i<=8;i++)if(Button(new Rect(385+(i-1)*56,697,48,32),i.ToString(),onlineSession.AugmentCount==i?dark:muted,onlineSession.IsHost))onlineSession.SetAugmentCount(i);
                GUI.Label(new Rect(850,700,210,38),"설정 변경 시 준비 초기화",tiny);
            }
            else if(onlineSession.Stage==OnlineStage.Playing)
            {
                GUI.Label(new Rect(385,610,665,42),"경기 동기화: "+(onlineSession.Model?.ReplayActions.Count??0)+"개 행동 · 내 자리 P"+(onlineSession.LocalSeat+1),small);
                if(Button(new Rect(385,666,669,46),"게임으로 돌아가기",dark))onlineOpen=false;
            }
            if(Button(new Rect(385,759,669,44),"방에서 나가기",muted))LeaveOnline();
        }
        static string PrivacyName(RoomPrivacy p)=>p==RoomPrivacy.Private?"프라이빗":p==RoomPrivacy.FriendsOnly?"친구 전용":"공개";
        IEnumerator SteamHostSmoke(string[] args)
        {
            string destination=Arg(args,"-marble-steam-report")??Path.Combine(Application.persistentDataPath,"steam-probe.json");
            onlineOpen=true;ConnectSteam();
            var report=new SteamProbe{appId=steamAppId,initialized=steamRooms.Available,status="unavailable",message=steamRooms.Status};
            if(steamRooms.Available)
            {
                steamRooms.CreateRoom("모마 연결 검사",2,RoomPrivacy.Private);
                float until=Time.realtimeSinceStartup+22;
                while(steamRooms.Busy && Time.realtimeSinceStartup<until)yield return null;
                if(int.TryParse(Arg(args,"-marble-steam-augment-count"),out int requestedCount))onlineSession.SetAugmentCount(requestedCount);
                report.augmentCount=onlineSession.AugmentCount;
                report.privateLobbyCreated=steamRooms.RoomId!=0 && steamRooms.Privacy==RoomPrivacy.Private && steamRooms.Members.Count==1;
                report.status=report.privateLobbyCreated?"passed":"failed";report.message=steamRooms.Status;
                yield return new WaitForSeconds(1);
            }
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));File.WriteAllText(destination,JsonUtility.ToJson(report,true));
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(destination,".png"));
            onlineSession?.Leave();yield return new WaitForSeconds(1);Application.Quit(report.status=="failed"?1:0);
        }
        void OnDestroy(){DisposeSteamPortraits();DisposeRoomVoice();DisposeBodyAudio();BodyWorldLayer.Dispose();if(menuModels!=null)Destroy(menuModels.gameObject);achievementBackend?.Dispose();onlineSession?.Dispose();CircuitArt.Dispose();PaperTheme.Dispose();if(font!=null)Destroy(font);if(clickSound!=null)Destroy(clickSound);if(stepSound!=null)Destroy(stepSound);if(diceImpactSound!=null)Destroy(diceImpactSound);}
    }
}
