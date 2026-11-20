using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Steamworks;

namespace Marble.Online
{
    // Steam lobby membership authenticates peers; reliable NetworkingMessages carry game actions.
    public sealed class SteamRoomService : IRoomService
    {
        const int Channel=17;
        readonly uint appId;
        readonly List<RoomMember> members=new List<RoomMember>();
        readonly List<RoomListing> rooms=new List<RoomListing>();
        readonly List<IDisposable> callbacks=new List<IDisposable>();
        readonly IntPtr[] incoming=new IntPtr[32];
        CallResult<LobbyCreated_t> created;
        CallResult<LobbyEnter_t> entered;
        CallResult<LobbyMatchList_t> listed;
        string desiredName;
        int desiredCapacity;
        RoomPrivacy desiredPrivacy;
        DateTime operationAt, nextMemberRefresh;
        bool ownsApi,disposed;
        string deferredFatal;
        public bool BlockInviteJoin{get;set;}
        public bool Available {get;private set;}
        public bool Busy {get;private set;}
        public string Status {get;private set;}="Steam에 연결해 주세요.";
        public ulong LocalId {get;private set;}
        public ulong RoomId {get;private set;}
        public ulong HostId {get;private set;}
        public string RoomName {get;private set;}="";
        public RoomPrivacy Privacy {get;private set;}
        public IReadOnlyList<RoomMember> Members=>members;
        public IReadOnlyList<RoomListing> Rooms=>rooms;
        public event Action RoomChanged;
        public event Action<ulong,byte[]> MessageReceived;
        public event Action<string> Error;
        public uint AppId=>appId;
        public SteamRoomService(uint configuredAppId){appId=configuredAppId;}
        public bool Initialize()
        {
            if(disposed)return false;if(Available)return true;
            if(appId==0){Status="Steam App ID를 설정해 주세요.";return false;}
            try
            {
                // Process-local identity only. No changes to Steam installation/account settings.
                Environment.SetEnvironmentVariable("SteamAppId",appId.ToString());
                if(SteamAPI.InitEx(out string detail)!=ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {Status="Steam 연결 실패. Steam을 온라인으로 실행하고 다시 시도해 주세요. "+detail;return false;}
                ownsApi=true;
                if(SteamUtils.GetAppID().m_AppId!=appId || !SteamUser.BLoggedOn())
                {Status="설정한 App ID와 Steam 로그인 상태를 확인해 주세요.";SteamAPI.Shutdown();ownsApi=false;return false;}
                LocalId=SteamUser.GetSteamID().m_SteamID;Available=true;
                created=CallResult<LobbyCreated_t>.Create(OnCreated);
                entered=CallResult<LobbyEnter_t>.Create(OnEntered);
                listed=CallResult<LobbyMatchList_t>.Create(OnListed);
                callbacks.Add(created);callbacks.Add(entered);callbacks.Add(listed);
                callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(p=>{if(!BlockInviteJoin)JoinRoom(p.m_steamIDLobby.m_SteamID);else Status="초대 자동 입장 거부 설정이 켜져 있습니다.";}));
                callbacks.Add(Callback<LobbyChatUpdate_t>.Create(p=>{if(p.m_ulSteamIDLobby==RoomId)RefreshMembers(true);}));
                callbacks.Add(Callback<LobbyDataUpdate_t>.Create(p=>{if(p.m_ulSteamIDLobby==RoomId)RefreshMembers(false);}));
                callbacks.Add(Callback<PersonaStateChange_t>.Create(p=>{if(members.Any(m=>m.Id==p.m_ulSteamID))RefreshMembers(false);}));
                callbacks.Add(Callback<SteamServersDisconnected_t>.Create(p=>deferredFatal="Steam 연결이 끊겼습니다. 다시 연결해 주세요."));
                callbacks.Add(Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest));
                callbacks.Add(Callback<SteamNetworkingMessagesSessionFailed_t>.Create(p=>
                {if(RoomId!=0 && members.Any(m=>m.Id==p.m_info.m_identityRemote.GetSteamID64()))Report("참가자와 P2P 연결을 만들지 못했습니다. 방을 나가 다시 연결해 주세요.");}));
                SteamNetworkingUtils.InitRelayNetworkAccess();
                Status="Steam 연결됨 · "+OnlineProtocol.CleanName(SteamFriends.GetPersonaName())+(appId==480?" · 개발 테스트 ID 480":"");
                return true;
            }
            catch(Exception ex)
            {
                Status="Steam 초기화 실패: "+ex.GetType().Name;
                if(ownsApi){try{SteamAPI.Shutdown();}catch{}ownsApi=false;}
                Available=false;return false;
            }
        }
        bool Begin()
        {
            if(!Available || disposed){Report("먼저 Steam에 연결해 주세요.");return false;}
            if(Busy){Report("이전 요청이 끝날 때까지 기다려 주세요.");return false;}
            Busy=true;operationAt=DateTime.UtcNow;return true;
        }
        public void CreateRoom(string name,int capacity,RoomPrivacy privacy)
        {
            if(RoomId!=0){Report("현재 방을 나간 뒤 새 방을 만들어 주세요.");return;}
            if(capacity<2 || capacity>4){Report("방 인원은 2~4명입니다.");return;}
            if(!Begin())return;
            desiredName=OnlineProtocol.CleanName(name);desiredCapacity=capacity;desiredPrivacy=privacy;
            Status="방을 만드는 중입니다…";
            created.Set(SteamMatchmaking.CreateLobby(ToSteam(privacy),capacity));
        }
        void OnCreated(LobbyCreated_t p,bool ioFailure)
        {
            Busy=false;
            if(ioFailure || p.m_eResult!=EResult.k_EResultOK){Report("방 생성 실패: "+p.m_eResult);return;}
            var id=new CSteamID(p.m_ulSteamIDLobby);
            bool ok=SteamMatchmaking.SetLobbyData(id,"game",OnlineProtocol.GameKey);
            ok&=SteamMatchmaking.SetLobbyData(id,"compat",OnlineProtocol.Compatibility);
            ok&=SteamMatchmaking.SetLobbyData(id,"name",desiredName);
            ok&=SteamMatchmaking.SetLobbyData(id,"privacy",desiredPrivacy.ToString());
            ok&=SteamMatchmaking.SetLobbyData(id,"state","lobby");
            if(!ok){SteamMatchmaking.LeaveLobby(id);Report("방 설정을 저장하지 못했습니다.");return;}
            BindRoom(id);Status="방을 만들었습니다. 친구를 초대해 주세요.";
        }
        public void SearchRooms()
        {
            if(RoomId!=0){Report("방을 나간 뒤 공개 방을 찾아 주세요.");return;}
            if(!Begin())return;rooms.Clear();Status="공개 방을 찾는 중입니다…";
            SteamMatchmaking.AddRequestLobbyListStringFilter("game",OnlineProtocol.GameKey,ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("compat",OnlineProtocol.Compatibility,ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("state","lobby",ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);listed.Set(SteamMatchmaking.RequestLobbyList());
        }
        void OnListed(LobbyMatchList_t p,bool ioFailure)
        {
            Busy=false;if(ioFailure){Report("공개 방을 불러오지 못했습니다.");return;}
            rooms.Clear();
            for(int i=0;i<Math.Min(50,(int)p.m_nLobbiesMatching);i++)
            {
                var id=SteamMatchmaking.GetLobbyByIndex(i);if(!Compatible(id))continue;
                int count=SteamMatchmaking.GetNumLobbyMembers(id),limit=SteamMatchmaking.GetLobbyMemberLimit(id);
                if(limit<2 || limit>4 || count>=limit)continue;
                rooms.Add(new RoomListing{Id=id.m_SteamID,Name=OnlineProtocol.CleanName(SteamMatchmaking.GetLobbyData(id,"name")),Members=count,Capacity=limit});
            }
            Status=rooms.Count+"개의 공개 방을 찾았습니다.";
        }
        public void JoinRoom(ulong id)
        {
            if(id==0){Report("올바른 방 ID가 아닙니다.");return;}
            if(RoomId==id)return;
            if(RoomId!=0){Report("현재 방에서 나온 뒤 초대를 다시 수락해 주세요.");return;}
            if(!Begin())return;
            Status="방에 입장하는 중입니다…";entered.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));
        }
        void OnEntered(LobbyEnter_t p,bool ioFailure)
        {
            Busy=false;
            if(ioFailure || p.m_EChatRoomEnterResponse!=(uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {Report("방 입장 실패. 초대 권한·빈자리·방 상태를 확인해 주세요. ("+p.m_EChatRoomEnterResponse+")");return;}
            var id=new CSteamID(p.m_ulSteamIDLobby);
            if(!Compatible(id) || SteamMatchmaking.GetLobbyData(id,"state")!="lobby" || SteamMatchmaking.GetLobbyMemberLimit(id)>4)
            {SteamMatchmaking.LeaveLobby(id);Report("이 빌드와 호환되는 모마 대기실이 아닙니다.");return;}
            BindRoom(id);Status="방에 입장했습니다.";
        }
        bool Compatible(CSteamID id)=>SteamMatchmaking.GetLobbyData(id,"game")==OnlineProtocol.GameKey && SteamMatchmaking.GetLobbyData(id,"compat")==OnlineProtocol.Compatibility;
        void BindRoom(CSteamID id)
        {
            RoomId=id.m_SteamID;RefreshMembers(true);
            SteamFriends.SetRichPresence("connect","+connect_lobby "+RoomId);
            SteamFriends.SetRichPresence("status","모마 대기실");
        }
        void RefreshMembers(bool force)
        {
            if(RoomId==0 || !Available)return;
            var id=new CSteamID(RoomId);ulong host=SteamMatchmaking.GetLobbyOwner(id).m_SteamID;
            var next=new List<RoomMember>();int count=SteamMatchmaking.GetNumLobbyMembers(id);
            if(count>4){Report("허용된 방 인원을 초과했습니다.");LeaveRoom();return;}
            for(int i=0;i<count;i++)
            {
                var person=SteamMatchmaking.GetLobbyMemberByIndex(id,i);
                next.Add(new RoomMember{Id=person.m_SteamID,Name=OnlineProtocol.CleanName(SteamFriends.GetFriendPersonaName(person))});
            }
            next=next.OrderBy(p=>p.Id==host?0:1).ThenBy(p=>p.Id).ToList();
            string name=OnlineProtocol.CleanName(SteamMatchmaking.GetLobbyData(id,"name"));
            Enum.TryParse(SteamMatchmaking.GetLobbyData(id,"privacy"),out RoomPrivacy privacy);
            bool changed=host!=HostId || RoomName!=name || Privacy!=privacy || !members.Select(p=>p.Id+":"+p.Name).SequenceEqual(next.Select(p=>p.Id+":"+p.Name));
            HostId=host;RoomName=name;Privacy=privacy;members.Clear();members.AddRange(next);
            if(changed || force)RoomChanged?.Invoke();
        }
        public void SetJoinable(bool value)
        {
            if(!Available || RoomId==0 || HostId!=LocalId)return;
            var id=new CSteamID(RoomId);
            if(!SteamMatchmaking.SetLobbyJoinable(id,value) || !SteamMatchmaking.SetLobbyData(id,"state",value?"lobby":"playing"))
                Report("방 입장 설정을 변경하지 못했습니다.");
            SteamFriends.SetRichPresence("status",value?"모마 대기실":"모마 온라인 경기");
        }
        public void OpenInviteOverlay()
        {
            if(!Available || RoomId==0)return;
            if(!SteamUtils.IsOverlayEnabled()){Report("Steam 오버레이를 켜고 Steam에서 이 빌드를 실행해 주세요.");return;}
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(RoomId));
        }
        void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t p)
        {
            var identity=p.m_identityRemote;ulong id=identity.GetSteamID64();
            if(RoomId!=0 && members.Any(m=>m.Id==id))SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
            else SteamNetworkingMessages.CloseSessionWithUser(ref identity);
        }
        public void Send(ulong recipient,byte[] data)
        {
            if(!Available || RoomId==0 || !members.Any(p=>p.Id==recipient) || data==null || data.Length>OnlineProtocol.MaxPacket)throw new InvalidOperationException("Invalid peer/message");
            var remote=new SteamNetworkingIdentity();remote.SetSteamID64(recipient);
            var handle=GCHandle.Alloc(data,GCHandleType.Pinned);
            try
            {
                var result=SteamNetworkingMessages.SendMessageToUser(ref remote,handle.AddrOfPinnedObject(),(uint)data.Length,
                    Constants.k_nSteamNetworkingSend_Reliable|Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,Channel);
                if(result!=EResult.k_EResultOK)throw new InvalidOperationException("Steam send: "+result);
            }
            finally{handle.Free();}
        }
        public void Tick()
        {
            if(!Available || disposed)return;
            try
            {
                SteamAPI.RunCallbacks();
                if(deferredFatal!=null){string reason=deferredFatal;deferredFatal=null;Fatal(reason);return;}
                if(!Available)return;
                if(Busy && (DateTime.UtcNow-operationAt).TotalSeconds>20){Fatal("Steam 요청 시간이 초과됐습니다. 다시 연결해 주세요.");return;}
                if(RoomId!=0 && DateTime.UtcNow>=nextMemberRefresh){nextMemberRefresh=DateTime.UtcNow.AddSeconds(1);RefreshMembers(false);}
                int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel,incoming,incoming.Length);
                for(int i=0;i<count;i++)
                {
                    IntPtr pointer=incoming[i];
                    try
                    {
                        var message=SteamNetworkingMessage_t.FromIntPtr(pointer);ulong sender=message.m_identityPeer.GetSteamID64();
                        if(RoomId==0 || message.m_cbSize<1 || message.m_cbSize>OnlineProtocol.MaxPacket || !members.Any(m=>m.Id==sender))continue;
                        byte[] bytes=new byte[message.m_cbSize];Marshal.Copy(message.m_pData,bytes,0,bytes.Length);MessageReceived?.Invoke(sender,bytes);
                    }
                    finally{SteamNetworkingMessage_t.Release(pointer);incoming[i]=IntPtr.Zero;}
                }
            }
            catch(Exception ex){Fatal("Steam 처리 오류: "+ex.GetType().Name+". 다시 연결해 주세요.");}
        }
        public void LeaveRoom()
        {
            if(Available && RoomId!=0)
            {
                foreach(var p in members)if(p.Id!=LocalId){var identity=new SteamNetworkingIdentity();identity.SetSteamID64(p.Id);SteamNetworkingMessages.CloseSessionWithUser(ref identity);}
                SteamMatchmaking.LeaveLobby(new CSteamID(RoomId));SteamFriends.ClearRichPresence();
            }
            RoomId=HostId=0;RoomName="";members.Clear();Status="방에서 나왔습니다.";RoomChanged?.Invoke();
        }
        void Report(string text){Status=text;Error?.Invoke(text);}
        void Fatal(string text)
        {
            Report(text);Dispose();Status=text;
        }
        public void Dispose()
        {
            if(disposed)return;
            try{LeaveRoom();}catch{}disposed=true;
            foreach(var item in callbacks)try{item.Dispose();}catch{}callbacks.Clear();
            if(ownsApi){try{SteamAPI.Shutdown();}catch{}ownsApi=false;}
            Available=false;Busy=false;
        }
        static ELobbyType ToSteam(RoomPrivacy p)=>p==RoomPrivacy.Private?ELobbyType.k_ELobbyTypePrivate:
            p==RoomPrivacy.FriendsOnly?ELobbyType.k_ELobbyTypeFriendsOnly:ELobbyType.k_ELobbyTypePublic;
        public static ulong LaunchLobby(string[] args)
        {
            for(int i=0;i<args.Length-1;i++)if(args[i]=="+connect_lobby" && ulong.TryParse(args[i+1],out ulong id))return id;
            return 0;
        }
        public ulong PlatformLaunchLobby()
        {
            if(!Available)return 0;
            SteamApps.GetLaunchCommandLine(out string command,2048);return LaunchLobby(command.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
