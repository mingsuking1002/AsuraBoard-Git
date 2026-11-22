using System;
using System.Collections.Generic;
using System.Linq;
using Marble.Core;

namespace Marble.Online
{
    // The host is the sole action authority. Platform-authenticated sender IDs never come from payload claims.
    public sealed class OnlineSession : IDisposable
    {
        readonly IRoomService service;
        readonly Dictionary<ulong,double> lastReceived=new Dictionary<ulong,double>();
        readonly Dictionary<ulong,int[]> appearance=new Dictionary<ulong,int[]>();
        int localAvatar,localColor,localDiceSkin;int localOutfit=WardrobeCatalog.DefaultCode;
        public int MapId {get;private set;}
        readonly Dictionary<ulong,bool> ready=new Dictionary<ulong,bool>();
        RoomMember[] roster=new RoomMember[0];
        ulong boundRoom,boundHost;
        string nonce="";
        double clock,nextHeartbeat,lastHostReceived,requestAt;
        bool pending,syncPending;
        string pendingAction="";
        public int AugmentCount {get;private set;}=4;
        public OnlineStage Stage {get;private set;}
        public Game Model {get;private set;}
        public string Status {get;private set;}="Steam 멀티플레이";
        public IReadOnlyList<RoomMember> Roster=>roster;
        public IRoomService Service=>service;
        public bool IsHost=>service.RoomId!=0 && service.LocalId==boundHost;
        public int LocalSeat=>Array.FindIndex(roster,p=>p.Id==service.LocalId);
        public bool CanAct=>Stage==OnlineStage.Playing && Model!=null && !pending && !syncPending && Model.CanPlayerAct(LocalSeat);
        public bool CanStart=>Stage==OnlineStage.Lobby && IsHost && roster.Length>=2 && roster.Length<=4 && roster.All(p=>p.Ready);
        public bool Pending=>pending||syncPending;
        public event Action<Game> MatchStarted;
        public event Action<string> ActionCommitted;
        public event Action<string> MatchStopped;

        public OnlineSession(IRoomService value)
        {
            service=value??throw new ArgumentNullException(nameof(value));
            service.RoomChanged+=RoomChanged;service.MessageReceived+=Received;service.Error+=OnError;
        }
        public void Tick(float deltaTime)
        {
            clock+=Math.Max(0,Math.Min(deltaTime,1));
            service.Tick();
            if(boundRoom==0 || Stage==OnlineStage.Faulted)return;
            if(clock>=nextHeartbeat)
            {
                nextHeartbeat=clock+2;
                if(IsHost){foreach(var p in roster)if(p.Id!=service.LocalId)Send(p.Id,Packet(PacketKind.Heartbeat));}
                else Send(boundHost,Packet(PacketKind.Heartbeat));
            }
            if(Stage!=OnlineStage.Playing)return;
            if(!IsHost && clock-lastHostReceived>20){Fail("호스트와 연결이 끊겼습니다. 방을 나가 다시 연결해 주세요.",false);return;}
            if(IsHost)
                foreach(var p in roster)
                    if(p.Id!=service.LocalId && lastReceived.TryGetValue(p.Id,out double at) && clock-at>20)
                    {Fail(p.Name+"의 응답이 없어 경기를 종료했습니다.",true);return;}
            if(pending && clock-requestAt>8){pending=false;RequestSync();}
            if(syncPending && clock-requestAt>15)Fail("호스트의 동기화 응답이 없습니다. 다시 연결해 주세요.",false);
        }
        void RoomChanged()
        {
            if(service.RoomId==0)
            {
                bool hadMatch=Model!=null;
                boundRoom=boundHost=0;nonce="";pending=syncPending=false;ready.Clear();roster=new RoomMember[0];Model=null;Stage=OnlineStage.Offline;
                Status="방에서 나왔습니다.";if(hadMatch)MatchStopped?.Invoke(Status);return;
            }
            if(boundRoom!=service.RoomId)
            {
                boundRoom=service.RoomId;boundHost=service.HostId;nonce="";Model=null;Stage=OnlineStage.Lobby;pending=syncPending=false;
                ready.Clear();appearance.Clear();appearance[service.LocalId]=new[]{localAvatar,localColor,localDiceSkin,localOutfit};roster=new RoomMember[0];lastReceived.Clear();lastHostReceived=clock;nextHeartbeat=clock;
                Status="대기실에 입장했습니다.";
                if(IsHost)RefreshRoster();else Send(boundHost,Packet(PacketKind.Hello));
                return;
            }
            if(boundHost!=service.HostId){Fail("호스트가 방을 나갔습니다. 이 버전은 호스트 이관을 지원하지 않습니다.",false);return;}
            if(Stage==OnlineStage.Playing && !SameMembers(roster,service.Members))
            {Fail("참가자가 방을 나가 경기를 종료했습니다.",IsHost);return;}
            if(Stage==OnlineStage.Lobby && IsHost)RefreshRoster();
        }
        bool SameMembers(IEnumerable<RoomMember> a,IEnumerable<RoomMember> b)=>a.Select(p=>p.Id).OrderBy(id=>id).SequenceEqual(b.Select(p=>p.Id).OrderBy(id=>id));
        void RefreshRoster()
        {
            if(!IsHost || Stage!=OnlineStage.Lobby)return;
            foreach(var id in ready.Keys.Where(id=>!service.Members.Any(p=>p.Id==id)).ToArray())ready.Remove(id);
            roster=service.Members.OrderBy(p=>p.Id==boundHost?0:1).ThenBy(p=>p.Id)
                .Select(p=>new RoomMember{Id=p.Id,Name=OnlineProtocol.CleanName(p.Name),Ready=p.Id==boundHost || (ready.TryGetValue(p.Id,out bool r)&&r),Avatar=appearance.TryGetValue(p.Id,out var avatar)?avatar[0]:0,ColorIndex=appearance.TryGetValue(p.Id,out var color)?color[1]:0,DiceSkin=appearance.TryGetValue(p.Id,out var dice)?dice[2]:0,Outfit=appearance.TryGetValue(p.Id,out var dress)?dress[3]:WardrobeCatalog.DefaultCode}).ToArray();
            foreach(var p in roster)if(!lastReceived.ContainsKey(p.Id))lastReceived[p.Id]=clock;
            Status="참가자 준비를 기다립니다.";
            var packet=Packet(PacketKind.Lobby);packet.Players=CloneRoster();Broadcast(packet);
        }
        public void SetAppearance(int avatar,int color,int diceSkin=0,int outfit=WardrobeCatalog.DefaultCode)
        {
            if(!WardrobeCatalog.IsValid(outfit) || avatar<0 || avatar>=CosmeticCatalog.AvatarCount || color<0 || color>3 || diceSkin<0 || diceSkin>=CosmeticCatalog.DiceSkinCount || Stage==OnlineStage.Playing)return;
            localAvatar=avatar;localColor=color;localDiceSkin=diceSkin;localOutfit=outfit;
            if(IsHost){appearance[service.LocalId]=new[]{avatar,color,diceSkin,outfit};RefreshRoster();}
            else if(Stage==OnlineStage.Lobby)Send(boundHost,Packet(PacketKind.Appearance));
        }
        public bool SetMap(int id)
        {
            if(Stage!=OnlineStage.Lobby || !IsHost || id<0 || id>=BoardCatalog.Count)return false;
            if(MapId==id)return true;MapId=id;ready.Clear();RefreshRoster();return true;
        }
        public bool SetAugmentCount(int count)
        {
            if(Stage!=OnlineStage.Lobby || !IsHost || count<1 || count>Game.MaxAugments)return false;
            if(count==AugmentCount)return true;
            AugmentCount=count;ready.Clear();RefreshRoster();return true;
        }
        public void SetReady(bool value)
        {
            if(Stage!=OnlineStage.Lobby)return;
            if(IsHost){ready[service.LocalId]=true;RefreshRoster();}
            else{var packet=Packet(PacketKind.Ready);packet.Flag=value;Send(boundHost,packet);}
        }
        public bool StartMatch(int seed,bool enableAugments=true)
        {
            if(!CanStart){Status="최소 2명과 모든 참가자의 준비가 필요합니다.";return false;}
            nonce=Guid.NewGuid().ToString("N");
            Model=CreateGame(seed,240,roster,enableAugments,AugmentCount,MapId);Stage=OnlineStage.Playing;
            foreach(var p in roster)lastReceived[p.Id]=clock;
            service.SetJoinable(false);if(Stage!=OnlineStage.Playing)return false;
            Status="온라인 경기를 시작했습니다.";
            Broadcast(Snapshot(true));if(Stage!=OnlineStage.Playing)return false;
            MatchStarted?.Invoke(Model);return true;
        }
        static Game CreateGame(int seed,int maxTurns,RoomMember[] peers,bool augments,int augmentCount,int mapId)
        {
            var game=new Game(new GameConfig{Seed=seed,HumanCount=peers.Length,PlayerCount=peers.Length,MaxTurns=maxTurns,EnableAugments=augments,AugmentRounds=Game.CreateAugmentSchedule(augmentCount),MapId=mapId,EndOnFirstBankruptcy=false,EnableBodyEconomy=true});
            for(int i=0;i<peers.Length;i++){game.Players[i].Name=OnlineProtocol.CleanName(peers[i].Name);game.Players[i].Avatar=peers[i].Avatar;game.Players[i].ColorIndex=peers[i].ColorIndex;game.Players[i].DiceSkin=peers[i].DiceSkin;game.Players[i].Outfit=peers[i].Outfit;}
            return game;
        }
        public string MatchId=>nonce;
        public bool Submit(string action)
        {
            if(!CanAct || string.IsNullOrEmpty(action) || !Model.GetActionsForPlayer(LocalSeat).Any(a=>a.Id==action))return false;
            if(IsHost)return ApplyHost(service.LocalId,Model.ReplayActions.Count,action);
            var packet=Packet(PacketKind.Request);packet.Sequence=Model.ReplayActions.Count;packet.Action=action;
            pendingAction=action;pending=true;requestAt=clock;Send(boundHost,packet);return true;
        }
        bool ApplyHost(ulong sender,int expected,string action)
        {
            if(!IsHost || Stage!=OnlineStage.Playing || Model==null)return false;
            int seat=Array.FindIndex(roster,p=>p.Id==sender);
            bool draftRequest=Model.Phase==GamePhase.Augment && action!=null && (action.StartsWith("augment:",StringComparison.Ordinal) || action.StartsWith("augment_reroll:",StringComparison.Ordinal));
            if(expected>Model.ReplayActions.Count || (expected!=Model.ReplayActions.Count && !draftRequest)){Send(sender,Snapshot(false));return false;}
            if(!Model.CanPlayerAct(seat) || !Model.GetActionsForPlayer(seat).Any(a=>a.Id==action) || !Model.Execute(action))
            {var rejected=Packet(PacketKind.Rejected);rejected.Reason="현재 차례에 허용되지 않은 행동입니다.";Send(sender,rejected);return false;}
            var packet=Packet(PacketKind.Commit);packet.Sequence=Model.ReplayActions.Count;packet.Action=action;packet.Hash=OnlineProtocol.StateHash(Model);
            Broadcast(packet);ActionCommitted?.Invoke(action);return true;
        }
        public bool RequestSpecialAugment()
        {
            if(!IsHost || Stage!=OnlineStage.Playing || !Model.CanRequestSpecialAugment || !Model.RequestSpecialAugment())return false;
            var packet=Packet(PacketKind.Commit);packet.Sequence=Model.ReplayActions.Count;packet.Action="system:augment";packet.Hash=OnlineProtocol.StateHash(Model);
            Broadcast(packet);ActionCommitted?.Invoke(packet.Action);return true;
        }
        // Host game rules call this after validating the effect; client requests cannot submit system actions.
        public bool ApplyHealthDamage(int targetSeat, int amount)
        {
            if (!IsHost || Stage != OnlineStage.Playing || Model == null || !Model.ApplyHealthDamage(targetSeat, amount))
                return false;
            var packet = Packet(PacketKind.Commit);
            packet.Sequence = Model.ReplayActions.Count;
            packet.Action = Game.HealthDamageAction(targetSeat, amount);
            packet.Hash = OnlineProtocol.StateHash(Model);
            Broadcast(packet);
            ActionCommitted?.Invoke(packet.Action);
            return true;
        }
        void Received(ulong sender,byte[] bytes)
        {
            if(boundRoom==0 || Stage==OnlineStage.Faulted || !service.Members.Any(p=>p.Id==sender))return;
            OnlinePacket p;
            try{p=OnlineProtocol.Decode(bytes);}catch(Exception){if(sender==boundHost && !IsHost)Fail("호스트에서 잘못된 데이터를 받았습니다.",false);return;}
            if(p.Room!=boundRoom)return;
            lastReceived[sender]=clock;if(sender==boundHost)lastHostReceived=clock;
            if(p.Kind==PacketKind.Heartbeat)return;
            if(IsHost)
            {
                if(sender==service.LocalId)return;
                if((p.Kind==PacketKind.Hello || p.Kind==PacketKind.Appearance) && Stage==OnlineStage.Lobby)
                {appearance[sender]=new[]{p.Avatar,p.ColorIndex,p.DiceSkin,p.Outfit};ready[sender]=false;RefreshRoster();return;}
                if(p.Kind==PacketKind.Ready && Stage==OnlineStage.Lobby && p.AugmentCount==AugmentCount && p.MapId==MapId){ready[sender]=p.Flag;RefreshRoster();return;}
                if(p.Nonce!=nonce)return;
                if(p.Kind==PacketKind.ReturnLobby && Stage==OnlineStage.Playing){ReturnToLobbyInternal();return;}
                if(p.Kind==PacketKind.Request)ApplyHost(sender,p.Sequence,p.Action);
                else if(p.Kind==PacketKind.Sync && Stage==OnlineStage.Playing)Send(sender,Snapshot(false));
                return;
            }
            if(sender!=boundHost)return;
            if(p.Kind==PacketKind.Lobby)
            {
                if(Stage==OnlineStage.Playing && !p.Flag)return;
                if(!ValidRoster(p.Players))return;
                AugmentCount=p.AugmentCount;MapId=p.MapId;
                bool stopped=Model!=null;roster=p.Players;Stage=OnlineStage.Lobby;Model=null;nonce="";pending=syncPending=false;
                Status="대기실 · 준비 버튼을 눌러 주세요.";if(stopped)MatchStopped?.Invoke("대기실로 돌아왔습니다.");return;
            }
            if(p.Kind==PacketKind.Snapshot){ApplySnapshot(p);return;}
            if(p.Nonce!=nonce || Stage!=OnlineStage.Playing || Model==null)return;
            if(p.Kind==PacketKind.Ended){Fail(p.Reason,false);return;}
            if(p.Kind==PacketKind.Rejected){pending=false;Status=p.Reason;return;}
            if(p.Kind!=PacketKind.Commit)return;
            if(p.Sequence<=Model.ReplayActions.Count)return;
            if(p.Sequence!=Model.ReplayActions.Count+1 || !Model.Execute(p.Action) || OnlineProtocol.StateHash(Model)!=p.Hash)
            {RequestSync();return;}
            if(pending && p.Action==pendingAction)pending=false;
            syncPending=false;Status=Model.Phase==GamePhase.Augment?"증강 동시 선택 중":"동기화됨 · "+Model.CurrentPlayer.Name+" 차례";ActionCommitted?.Invoke(p.Action);
        }
        bool ValidRoster(RoomMember[] peers)=>peers!=null && peers.Length>=1 && peers.Length<=4 && peers[0].Id==boundHost &&
            peers.Select(p=>p.Id).Distinct().Count()==peers.Length && peers.Any(p=>p.Id==service.LocalId) && SameMembers(peers,service.Members);
        void ApplySnapshot(OnlinePacket p)
        {
            if(!ValidRoster(p.Players) || p.Players.Length<2 || p.MaxTurns!=240 || p.Nonce.Length!=32 || p.Nonce.Any(c=>!Uri.IsHexDigit(c)))
            {Fail("경기 설정이 올바르지 않습니다.",false);return;}
            if(Stage==OnlineStage.Playing && p.Nonce!=nonce)return;
            if(Stage==OnlineStage.Lobby && !p.Flag)return;
            try
            {
                var restored=CreateGame(p.Seed,p.MaxTurns,p.Players,p.Augments,p.AugmentCount,p.MapId);
                foreach(var action in p.Actions)if(!restored.Execute(action))throw new InvalidOperationException("Invalid replay action");
                if(p.Sequence!=restored.ReplayActions.Count || OnlineProtocol.StateHash(restored)!=p.Hash)throw new InvalidOperationException("Replay mismatch");
                AugmentCount=p.AugmentCount;MapId=p.MapId;roster=p.Players;nonce=p.Nonce;Model=restored;Stage=OnlineStage.Playing;pending=syncPending=false;lastHostReceived=clock;
                Status=p.Actions.Length>0?"경기 상태를 다시 동기화했습니다.":"온라인 경기를 시작했습니다.";
                MatchStarted?.Invoke(Model);
            }
            catch(Exception){Fail("경기 상태를 복구하지 못했습니다. 방을 다시 만들어 주세요.",false);}
        }
        void RequestSync()
        {
            if(syncPending)return;
            syncPending=true;pending=false;requestAt=clock;
            Status="호스트의 경기 상태를 다시 확인 중입니다.";Send(boundHost,Packet(PacketKind.Sync));
        }
        OnlinePacket Snapshot(bool start)
        {
            var p=Packet(PacketKind.Snapshot);p.Flag=start;p.Players=CloneRoster();p.Seed=Model.Config.Seed;p.MaxTurns=Model.Config.MaxTurns;p.Augments=Model.Config.EnableAugments;
            p.Actions=Model.ReplayActions.ToArray();p.Sequence=p.Actions.Length;p.Hash=OnlineProtocol.StateHash(Model);return p;
        }
        RoomMember[] CloneRoster()=>roster.Select(p=>new RoomMember{Id=p.Id,Name=p.Name,Ready=p.Ready,Avatar=p.Avatar,ColorIndex=p.ColorIndex,DiceSkin=p.DiceSkin,Outfit=p.Outfit}).ToArray();
        OnlinePacket Packet(PacketKind kind)=>new OnlinePacket{Kind=kind,Room=boundRoom,Nonce=nonce,AugmentCount=AugmentCount,MapId=MapId,Avatar=localAvatar,ColorIndex=localColor,DiceSkin=localDiceSkin,Outfit=localOutfit};
        void Broadcast(OnlinePacket p){foreach(var member in roster)if(member.Id!=service.LocalId)Send(member.Id,p);}
        void Send(ulong id,OnlinePacket p)
        {
            if(id==service.LocalId || id==0)return;
            try{service.Send(id,OnlineProtocol.Encode(p));}catch(Exception){Fail("네트워크 전송에 실패했습니다.",false);}
        }
        void OnError(string reason)
        {
            Status=reason;if(Stage==OnlineStage.Playing)Fail(reason,false);
        }
        void Fail(string reason,bool notify)
        {
            if(Stage==OnlineStage.Faulted)return;
            if(notify && IsHost){var ended=Packet(PacketKind.Ended);ended.Reason=reason;Broadcast(ended);}
            Stage=OnlineStage.Faulted;pending=syncPending=false;Status=reason;MatchStopped?.Invoke(reason);
        }
        public void ReturnToLobby()
        {if(IsHost && Model!=null && Model.Phase==GamePhase.Finished)ReturnToLobbyInternal();}
        public bool RequestLobbyReturn()
        {
            if(Stage!=OnlineStage.Playing || Model==null)return false;
            if(IsHost)ReturnToLobbyInternal();else Send(boundHost,Packet(PacketKind.ReturnLobby));return true;
        }
        void ReturnToLobbyInternal()
        {
            Stage=OnlineStage.Lobby;Model=null;nonce="";pending=syncPending=false;ready.Clear();service.SetJoinable(true);RefreshRoster();
            var packet=Packet(PacketKind.Lobby);packet.Flag=true;packet.Players=CloneRoster();Broadcast(packet);MatchStopped?.Invoke("대기실로 돌아왔습니다.");
        }
        public void Leave(){service.LeaveRoom();RoomChanged();}
        public void Dispose(){service.RoomChanged-=RoomChanged;service.MessageReceived-=Received;service.Error-=OnError;service.Dispose();}
    }
}
