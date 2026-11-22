using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marble.Core;

namespace Marble.Online
{
    // Deterministic in-memory multi-client tests. These do not claim a live Steam two-account test.
    public static class OnlineSelfTests
    {
        sealed class Hub
        {
            public readonly List<Fake> Peers=new List<Fake>();
            public readonly Queue<Action> Queue=new Queue<Action>();
            public readonly List<Tuple<ulong,ulong,byte[]>> Sent=new List<Tuple<ulong,ulong,byte[]>>();
            public Func<ulong,ulong,OnlinePacket,bool> Drop;
            public Func<ulong,ulong,OnlinePacket,OnlinePacket> Transform;
            public ulong Host;
            public bool Joinable=true;
            public int Capacity=4;
            public RoomPrivacy Privacy;
            public readonly HashSet<ulong> Invited=new HashSet<ulong>();
            public Fake Add(ulong id){var p=new Fake(this,id);Peers.Add(p);return p;}
            public void Notify(){foreach(var p in Peers.Where(p=>p.RoomId!=0).ToArray())p.Changed();}
            public void Pump(){int budget=100000;while(Queue.Count>0){if(--budget<0)throw new Exception("Network loop");Queue.Dequeue()();}}
        }
        sealed class Fake : IRoomService
        {
            readonly Hub hub;
            public Fake(Hub h,ulong id){hub=h;LocalId=id;}
            public bool Available=>true;public bool Busy=>false;public string Status=>"fake";
            public ulong LocalId{get;private set;}public ulong RoomId{get;private set;}public ulong HostId=>hub.Host;
            public string RoomName=>"test";public RoomPrivacy Privacy=>hub.Privacy;
            public IReadOnlyList<RoomMember> Members=>hub.Peers.Where(p=>p.RoomId==RoomId && RoomId!=0).Select(p=>new RoomMember{Id=p.LocalId,Name="사용자"+p.LocalId}).ToArray();
            readonly List<RoomListing> listings=new List<RoomListing>();public IReadOnlyList<RoomListing> Rooms=>listings;
            public event Action RoomChanged;public event Action<ulong,byte[]> MessageReceived;public event Action<string> Error;
            public void Changed()=>RoomChanged?.Invoke();
            public void Tick(){}
            public void CreateRoom(string name,int capacity,RoomPrivacy privacy){hub.Host=LocalId;hub.Capacity=capacity;hub.Privacy=privacy;hub.Joinable=true;RoomId=100;Changed();}
            public void SearchRooms(){listings.Clear();if(hub.Privacy==RoomPrivacy.Public && hub.Joinable && hub.Host!=0)listings.Add(new RoomListing{Id=100,Name="test"});}
            public void JoinRoom(ulong id)
            {
                if(id!=100 || !hub.Joinable || hub.Peers.Count(p=>p.RoomId!=0)>=hub.Capacity || (hub.Privacy==RoomPrivacy.Private && !hub.Invited.Contains(LocalId)))
                {Error?.Invoke("입장 거부");return;}
                RoomId=id;hub.Notify();
            }
            public void LeaveRoom()
            {
                RoomId=0;Changed();if(hub.Host==LocalId)hub.Host=hub.Peers.FirstOrDefault(p=>p.RoomId!=0)?.LocalId??0;hub.Notify();
            }
            public void SetJoinable(bool value){if(LocalId==HostId)hub.Joinable=value;}
            public void Send(ulong recipient,byte[] bytes)
            {
                var target=hub.Peers.FirstOrDefault(p=>p.LocalId==recipient && p.RoomId==RoomId && RoomId!=0);
                if(target==null)throw new IOException("No recipient");
                var p=OnlineProtocol.Decode(bytes);hub.Sent.Add(Tuple.Create(LocalId,recipient,bytes));
                if(hub.Drop!=null && hub.Drop(LocalId,recipient,p))return;
                if(hub.Transform!=null)p=hub.Transform(LocalId,recipient,p);
                var payload=OnlineProtocol.Encode(p);hub.Queue.Enqueue(()=>target.MessageReceived?.Invoke(LocalId,payload));
            }
            public void OpenInviteOverlay(){}public void Dispose(){}
        }
        sealed class Group : IDisposable
        {
            public Hub Hub=new Hub();public Fake[] Peers;public OnlineSession[] Sessions;
            public Group(int count=3,RoomPrivacy privacy=RoomPrivacy.Public)
            {
                Peers=Enumerable.Range(1,count).Select(i=>Hub.Add((ulong)i)).ToArray();Sessions=Peers.Select(p=>new OnlineSession(p)).ToArray();
                Peers[0].CreateRoom("test",4,privacy);
                foreach(var p in Peers.Skip(1)){Hub.Invited.Add(p.LocalId);p.JoinRoom(100);}Hub.Pump();
            }
            public void Start(int seed=1,bool augments=false)
            {foreach(var s in Sessions.Skip(1))s.SetReady(true);Hub.Pump();Assert(Sessions[0].StartMatch(seed,augments),"start");Hub.Pump();Equal();}
            public void Equal()
            {var hashes=Sessions.Select(s=>s.Model==null?"none":OnlineProtocol.StateHash(s.Model)).Distinct();Assert(hashes.Count()==1,"peer state hashes");}
            public void Step()
            {
                var host=Sessions[0];int seat=host.Model.Phase==GamePhase.Augment?host.Model.AugmentActor:host.Model.CurrentPlayerIndex;string action=host.Model.ChooseAiAction();
                Assert(Sessions[seat].Submit(action),"legal local request");Hub.Pump();Equal();
            }
            public void Dispose(){foreach(var s in Sessions)s.Dispose();}
        }
        static void Assert(bool condition,string label){if(!condition)throw new InvalidOperationException("Online test: "+label);}
        public static int Run()
        {
            Action[] cases={WireRoundTrip,MalformedWire,PrivateAdmission,ReadyAndHost,OutOfTurn,DuplicateRequest,
                ForgedCommit,DroppedCommitResync,BadHashResync,LostRequestRecovery,HostDeparture,MemberDeparture,
                HeartbeatTimeout,ReadyResetOnRejoin,FinishedRematch,FullGames,AugmentSelection,AugmentSyncRepair,ConcurrentAugmentRequests,AugmentSettings,SpecialAugmentAuthority,DraftPendingIsPerAction,ConstructionSync,SimultaneousRerolls,RerollSyncRepair,MapAndAppearanceLobby,GuestLobbyReturnAndRematch,ControlledDiceAndCosmeticSync,GoldenRerollRepair,BodySaleAndRepair,GuestBodySale,BodyDuringDraftBlocked};
            foreach(var test in cases)test();
            HealthDamageSync();
            HealthDamageTurnAndReplay();
            return cases.Length + 2;
        }
        static void HealthDamageSync()
        {
            using (var g = new Group(2))
            {
                g.Start();
                var host = g.Sessions[0];
                var guest = g.Sessions[1];
                int target = 1;
                Assert(!guest.ApplyHealthDamage(target, 100), "guest cannot issue damage");
                Assert(!host.Submit(Game.HealthDamageAction(target, 100)), "damage is not a player action");
                Assert(!host.ApplyHealthDamage(-1, 100) && !host.ApplyHealthDamage(target, 0), "invalid damage rejected");
                Assert(host.ApplyHealthDamage(target, 35), "partial damage");
                g.Hub.Pump();
                Assert(guest.Model.Players[target].Health == 65, "partial damage synchronized");
                Assert(host.ApplyHealthDamage(target, int.MaxValue), "lethal damage");
                g.Hub.Pump();
                var victim = guest.Model.Players[target];
                Assert(victim.Health == 0 && victim.IslandTurns == 3 &&
                    guest.Model.Tiles[victim.Position].Definition.Kind == TileKind.Island &&
                    guest.Model.CurrentPlayerIndex == 0, "off-turn knockout preserves active turn");
                Assert(!host.ApplyHealthDamage(target, 1), "zero health cannot retrigger island");
                g.Equal();
            }
        }
        static void HealthDamageTurnAndReplay()
        {
            var config = new GameConfig { Seed = 7, PlayerCount = 2, EnableAugments = false };
            var game = new Game(config);
            Assert(game.Execute("roll") && game.Players.All(p => p.Health == PlayerState.MaxHealth), "ordinary play keeps health");
            int seat = game.CurrentPlayerIndex;
            Assert(game.ApplyHealthDamage(seat, 100) && game.CurrentPlayerIndex != seat, "knockout ends the active turn");
            Assert(game.ActionMovement.Count == 1 && game.ActionMovement[0].Seat == seat && game.ActionMovement[0].Transfer,
                "knockout is a direct transfer");
            game = Game.Replay(config, game.ReplayActions);
            Assert(game.Players[seat].Health == 0 && game.History.Any(line => line.Contains("체력 0")), "knockout replay");
            var escape = new Game(config);
            escape.ApplyHealthDamage(1, 100);
            for (int i = 0; i < 24 && escape.CurrentPlayerIndex != 1; i++)
                Assert(escape.Execute(escape.ChooseAiAction()), "advance toward the island player's turn");
            Assert(escape.CurrentPlayerIndex == 1 && escape.Execute("island_pay") &&
                escape.Players[1].Health == PlayerState.MaxHealth, "escaping restores full health");
        }
        static void WireRoundTrip()
        {
            var p=new OnlinePacket{Kind=PacketKind.Snapshot,Room=100,Nonce=new string('a',32),Sequence=2,Seed=-41,
                Players=new[]{new RoomMember{Id=1,Name="한국어",Ready=true}},Actions=new[]{"roll","buy"}};
            var copy=OnlineProtocol.Decode(OnlineProtocol.Encode(p));Assert(copy.Seed==-41 && copy.Players[0].Name=="한국어" && copy.Actions.SequenceEqual(p.Actions),"wire fields");
        }
        static void MalformedWire()
        {
            var valid=OnlineProtocol.Encode(new OnlinePacket{Kind=PacketKind.Ready});
            var invalid=new List<byte[]>{null,new byte[OnlineProtocol.MaxPacket+1],valid.Take(valid.Length-1).ToArray(),valid.Concat(new byte[]{1}).ToArray()};
            var magic=(byte[])valid.Clone();magic[0]=0;invalid.Add(magic);
            foreach(var bytes in invalid){bool failed=false;try{OnlineProtocol.Decode(bytes);}catch{failed=true;}Assert(failed,"malformed rejected");}
            bool large=false;try{OnlineProtocol.Encode(new OnlinePacket{Kind=PacketKind.Request,Action=new string('x',129)});}catch{large=true;}Assert(large,"bounded string");
        }
        static void PrivateAdmission()
        {
            var hub=new Hub();var a=hub.Add(1);var b=hub.Add(2);a.CreateRoom("private",2,RoomPrivacy.Private);b.SearchRooms();Assert(b.Rooms.Count==0,"private not listed");
            b.JoinRoom(100);Assert(b.RoomId==0,"uninvited cannot join");hub.Invited.Add(2);b.JoinRoom(100);Assert(b.RoomId==100,"invited joins");
            var c=hub.Add(3);hub.Invited.Add(3);c.JoinRoom(100);Assert(c.RoomId==0,"capacity");
        }
        static void ReadyAndHost()
        {
            using(var g=new Group(2)){Assert(!g.Sessions[0].CanStart,"not ready");Assert(!g.Sessions[1].StartMatch(2),"guest cannot start");g.Start();Assert(!g.Hub.Joinable,"lock after start");}
        }
        static OnlinePacket Snapshot(Group g)=>OnlineProtocol.Decode(g.Hub.Sent.Last(t=>OnlineProtocol.Decode(t.Item3).Kind==PacketKind.Snapshot).Item3);
        static void OutOfTurn()
        {
            using(var g=new Group()){g.Start();Assert(!g.Sessions[1].Submit("roll"),"guest local guard");var p=Snapshot(g);p.Kind=PacketKind.Request;p.Sequence=0;p.Action="roll";p.Actions=new string[0];
                g.Peers[1].Send(1,OnlineProtocol.Encode(p));g.Hub.Pump();Assert(g.Sessions[0].Model.ReplayActions.Count==0,"host rejects wrong seat");g.Equal();}
        }
        static void DuplicateRequest()
        {
            using(var g=new Group(2)){g.Start();int budget=100;while(g.Sessions[0].Model.CurrentPlayerIndex!=1 && budget-->0)g.Step();Assert(budget>0,"guest turn reached");
                Assert(g.Sessions[1].Submit(g.Sessions[1].Model.ChooseAiAction()),"request");g.Hub.Pump();int count=g.Sessions[0].Model.ReplayActions.Count;
                var sent=g.Hub.Sent.Last(t=>t.Item1==2 && OnlineProtocol.Decode(t.Item3).Kind==PacketKind.Request);g.Peers[1].Send(1,sent.Item3);g.Hub.Pump();
                Assert(g.Sessions[0].Model.ReplayActions.Count==count,"duplicate not applied");g.Equal();}
        }
        static void ForgedCommit()
        {
            using(var g=new Group()){g.Start();var p=Snapshot(g);p.Kind=PacketKind.Commit;p.Sequence=1;p.Action="roll";
                g.Peers[1].Send(3,OnlineProtocol.Encode(p));g.Hub.Pump();Assert(g.Sessions[2].Model.ReplayActions.Count==0,"guest cannot forge host");g.Equal();}
        }
        static void DroppedCommitResync()
        {
            using(var g=new Group()){g.Start();bool dropped=false;g.Hub.Drop=(s,t,p)=>{if(t==2 && p.Kind==PacketKind.Commit && !dropped){dropped=true;return true;}return false;};
                Assert(g.Sessions[0].Submit("roll"),"host roll");g.Hub.Pump();Assert(g.Sessions[1].Model.ReplayActions.Count==0,"commit dropped");
                int seat=g.Sessions[0].Model.CurrentPlayerIndex;Assert(seat==0,"test seed host landing");g.Sessions[0].Submit(g.Sessions[0].Model.ChooseAiAction());g.Hub.Pump();g.Equal();}
        }
        static void BadHashResync()
        {
            using(var g=new Group()){g.Start();bool changed=false;g.Hub.Transform=(s,t,p)=>{if(t==2 && p.Kind==PacketKind.Commit && !changed){changed=true;p.Hash="wrong";}return p;};g.Step();Assert(changed,"hash corrupted");g.Equal();}
        }
        static void LostRequestRecovery()
        {
            using(var g=new Group(2)){g.Start();int budget=100;while(g.Sessions[0].Model.CurrentPlayerIndex!=1 && budget-->0)g.Step();Assert(budget>0,"guest turn");
                g.Hub.Drop=(s,t,p)=>p.Kind==PacketKind.Request;g.Sessions[1].Submit(g.Sessions[1].Model.ChooseAiAction());g.Hub.Pump();
                for(int i=0;i<10;i++){foreach(var session in g.Sessions)session.Tick(1);g.Hub.Pump();}
                Assert(!g.Sessions[1].Pending,"pending recovers after replay sync");g.Equal();}
        }
        static void HostDeparture()
        {using(var g=new Group()){g.Start();g.Peers[0].LeaveRoom();g.Hub.Pump();Assert(g.Sessions.Skip(1).All(s=>s.Stage==OnlineStage.Faulted),"no silent host migration");}}
        static void MemberDeparture()
        {using(var g=new Group()){g.Start();g.Peers[2].LeaveRoom();g.Hub.Pump();Assert(g.Sessions[0].Stage==OnlineStage.Faulted && g.Sessions[1].Stage==OnlineStage.Faulted,"departure stops match");}}
        static void HeartbeatTimeout()
        {using(var g=new Group(2)){g.Start();for(int i=0;i<23;i++){g.Sessions[0].Tick(1);g.Hub.Pump();}Assert(g.Sessions[0].Stage==OnlineStage.Faulted,"silent client timeout");}}
        static void ReadyResetOnRejoin()
        {using(var g=new Group(2)){g.Sessions[1].SetReady(true);g.Hub.Pump();Assert(g.Sessions[0].CanStart,"ready");g.Peers[1].LeaveRoom();g.Peers[1].JoinRoom(100);g.Hub.Pump();Assert(!g.Sessions[0].CanStart,"rejoin resets ready");}}
        static void FinishedRematch()
        {
            using(var g=new Group(2)){g.Start(9);int budget=10000;while(g.Sessions[0].Model.Phase!=GamePhase.Finished && budget-->0)g.Step();Assert(budget>0,"match completed");
                var stale=g.Hub.Sent.Last(t=>OnlineProtocol.Decode(t.Item3).Kind==PacketKind.Commit);g.Sessions[0].ReturnToLobby();g.Hub.Pump();
                Assert(g.Sessions.All(s=>s.Stage==OnlineStage.Lobby && s.Model==null),"return to lobby");g.Start(10);
                g.Peers[0].Send(stale.Item2,stale.Item3);g.Hub.Pump();Assert(g.Sessions[1].Model.ReplayActions.Count==0,"old match nonce rejected");g.Equal();}
        }
        static void AugmentSelection()
        {
            using(var g=new Group(3))
            {
                g.Start(7,true);Assert(g.Sessions.All(s=>s.CanAct),"every seat can choose at once");
                Assert(!g.Sessions[1].Submit("augment:0:2:2"),"cannot choose for another seat");
                Assert(g.Sessions[2].Submit("augment:0:2:2"),"non-turn seat chooses first");g.Hub.Pump();g.Equal();
                Assert(g.Sessions[0].Model.Phase==GamePhase.Augment && !g.Sessions[2].CanAct && g.Sessions[1].CanAct,"ready player waits while others still choose");
                int budget=10000;while(g.Sessions[0].Model.Phase!=GamePhase.Finished && budget-->0)g.Step();
                Assert(budget>0 && g.Sessions[0].Model.Players.All(p=>p.Augments.Count>=1 && p.Augments.Count<=4),"simultaneous game finishes");
            }
        }
        static void AugmentSyncRepair()
        {
            using(var g=new Group(2))
            {
                g.Start(7,true);bool changed=false;g.Hub.Transform=(s,t,p)=>{if(t==2 && p.Kind==PacketKind.Commit && !changed){changed=true;p.Hash="bad";}return p;};
                g.Sessions[0].Submit("augment:0:0:1");g.Hub.Pump();g.Equal();
                Assert(changed && g.Sessions[1].Model.Players[0].Augments.Single().OptionIndex==1 && g.Sessions[1].CanAct,"snapshot preserves completed and pending seats");
            }
        }
        static void ConcurrentAugmentRequests()
        {
            using(var g=new Group(4))
            {
                g.Start(7,true);
                // All requests carry the same old sequence. The host commits each authenticated seat once.
                foreach(int seat in new[]{3,2,1})Assert(g.Sessions[seat].Submit("augment:0:"+seat+":"+(seat%3)),"parallel request accepted locally");
                Assert(g.Sessions[0].Submit("augment:0:0:2"),"host overlaps peer requests");
                g.Hub.Pump();g.Equal();
                Assert(g.Sessions.All(s=>s.Model.Phase==GamePhase.AwaitRoll && !s.Pending),"all simultaneous requests resolve");
                Assert(g.Sessions[0].Model.Players.All(p=>p.Augments.Count==1),"one per seat despite stale sequence");
                var duplicate=g.Hub.Sent.First(t=>t.Item1==4 && OnlineProtocol.Decode(t.Item3).Kind==PacketKind.Request);
                g.Peers[3].Send(1,duplicate.Item3);g.Hub.Pump();g.Equal();Assert(g.Sessions[0].Model.Players[3].Augments.Count==1,"duplicate not applied twice");
            }
        }
        static void DraftPendingIsPerAction()
        {
            using(var g=new Group(3))
            {
                g.Start(7,true);g.Hub.Drop=(s,t,p)=>s==3 && p.Kind==PacketKind.Request;
                g.Sessions[2].Submit("augment:0:2:1");g.Sessions[0].Submit("augment:0:0:1");g.Hub.Pump();
                Assert(g.Sessions[2].Pending,"another player's commit does not clear own pending request");
                g.Sessions[1].Submit("augment:0:1:2");g.Hub.Pump();Assert(g.Sessions[2].Pending,"still waiting for own action");
                for(int i=0;i<10;i++){g.Sessions[2].Tick(1);g.Hub.Pump();}
                Assert(!g.Sessions[2].Pending && g.Sessions[2].CanAct,"lost choice recovers to unselected state");
                g.Hub.Drop=null;g.Sessions[2].Submit("augment:0:2:1");g.Hub.Pump();g.Equal();
                Assert(g.Sessions.All(s=>s.Model.Phase==GamePhase.AwaitRoll),"recovered choice completes barrier");
            }
        }
        static void AugmentSettings()
        {
            using(var g=new Group(2))
            {
                g.Sessions[1].SetReady(true);g.Hub.Pump();Assert(g.Sessions[0].CanStart,"ready before change");
                Assert(!g.Sessions[1].SetAugmentCount(8),"guest cannot change count");
                Assert(g.Sessions[0].SetAugmentCount(8) && !g.Sessions[0].CanStart,"count change resets ready");g.Hub.Pump();
                Assert(g.Sessions.All(s=>s.AugmentCount==8),"count synchronized");
                Assert(!g.Sessions[0].SetAugmentCount(9),"count cap");g.Start(7,true);
                Assert(g.Sessions.All(s=>s.Model.Config.AugmentRounds.Length==8),"snapshot carries eight-stage setting");
                Assert(!g.Sessions[0].SetAugmentCount(4),"settings locked during play");
            }
        }
        static void SpecialAugmentAuthority()
        {
            using(var g=new Group(2))
            {
                g.Start(7,true);while(g.Sessions[0].Model.Phase==GamePhase.Augment)g.Step();
                Assert(!g.Sessions[1].RequestSpecialAugment() && !g.Sessions[1].Submit("system:augment"),"guest cannot mint rewards");
                Assert(g.Sessions[0].RequestSpecialAugment(),"host system hook");g.Hub.Pump();g.Equal();
                while(g.Sessions[0].Model.Phase==GamePhase.Augment)g.Step();
                Assert(g.Sessions[0].Model.Players.All(p=>p.Augments.Count==2),"special draft synchronizes");
            }
        }
        static void SimultaneousRerolls()
        {
            using(var g=new Group(3))
            {
                g.Start(7,true);string stale=g.Sessions[2].Model.AugmentAction(2,1);
                Assert(!g.Sessions[1].Submit("augment_reroll:0:2:0"),"cannot reroll other's offer");
                Assert(g.Sessions[2].Submit("augment_reroll:0:2:0") && g.Sessions[1].Submit("augment_reroll:0:1:0"),"concurrent personal rerolls");
                g.Sessions[0].Submit("augment:0:0:1");g.Hub.Pump();g.Equal();
                Assert(g.Sessions.All(s=>!s.Pending && s.Model.AugmentRevision(1)==1 && s.Model.AugmentRevision(2)==1),"rerolls and concurrent selection synchronize");
                Assert(!g.Sessions[2].Submit(stale) && !g.Sessions[2].Submit("augment_reroll:0:2:0"),"stale option and repeated reroll blocked");
                g.Sessions[1].Submit(g.Sessions[1].Model.AugmentAction(1,0));g.Sessions[2].Submit(g.Sessions[2].Model.AugmentAction(2,0));g.Hub.Pump();g.Equal();
                Assert(g.Sessions[0].Model.Phase==GamePhase.AwaitRoll && g.Sessions[0].Model.Players[2].Augments.Single().Rerolled,"rerolled selections complete barrier");
            }
        }
        static void RerollSyncRepair()
        {
            using(var g=new Group(2))
            {
                g.Start(7,true);bool corrupt=false;
                g.Hub.Transform=(s,t,p)=>{if(t==2 && p.Kind==PacketKind.Commit && !corrupt){corrupt=true;p.Hash="reroll-repair";}return p;};
                g.Sessions[0].Submit("augment_reroll:0:0:0");g.Hub.Pump();g.Equal();
                Assert(corrupt && !g.Sessions[1].Model.CanRerollAugment(0,0) && g.Sessions[1].Model.CanRerollAugment(1),"repair keeps personal reroll spend");
            }
        }
        static void BodySaleAndRepair()
        {
            using(var g=new Group(2))
            {
                g.Start(91);bool repaired=false;
                g.Hub.Transform=(sender,target,packet)=>{if(target==2 && packet.Kind==PacketKind.Commit && !repaired){repaired=true;packet.Hash="body-repair";}return packet;};
                Assert(!g.Sessions[1].Submit("body_sell:0"),"cannot sell outside own turn");
                Assert(g.Sessions[0].Submit("body_sell:0"),"host sells own eye");g.Hub.Pump();g.Equal();
                Assert(repaired && g.Sessions.All(s=>s.Model.Players[0].SoldBodyMask==1 && s.Model.Players[0].Cash==2400),"snapshot restores sale money and model state");
                Assert(!g.Sessions[0].Submit("body_sell:1"),"one normal sale per turn after repair");
                Assert(g.Sessions[0].Submit("body_restore:0"),"restore own eye");g.Hub.Pump();g.Equal();
                Assert(g.Sessions.All(s=>s.Model.Players[0].SoldBodyMask==0 && s.Model.Players[0].Cash==1800),"150 percent restoration synchronized");
            }
        }
        static void GuestBodySale()
        {
            using(var g=new Group(2))
            {
                g.Start(17);int budget=30;while(g.Sessions[0].Model.CurrentPlayerIndex!=1 && budget-->0)g.Step();Assert(budget>0,"guest turn reached");
                Assert(g.Sessions[1].Submit("body_sell:9"),"guest sells own heart");g.Hub.Pump();g.Equal();
                Assert(g.Sessions.All(s=>(s.Model.Players[1].SoldBodyMask&512)!=0),"peer appearance effect synchronized");
            }
        }
        static void BodyDuringDraftBlocked()
        {
            using(var g=new Group(2)){g.Start(19,true);Assert(!g.Sessions[0].Submit("body_sell:0") && !g.Sessions[1].Submit("body_sell:0"),"body trading cannot bypass simultaneous augment phase");g.Equal();}
        }
        static void GoldenRerollRepair()
        {
            using(var g=new Group(2))
            {
                int seed=0;while(!new Game(new GameConfig{Seed=seed,EnableAugments=true}).GoldenRerollAvailable(0))seed++;
                g.Start(seed,true);bool repaired=false;
                g.Hub.Transform=(sender,target,packet)=>{if(target==2 && packet.Kind==PacketKind.Commit && !repaired){repaired=true;packet.Hash="golden-repair";}return packet;};
                Assert(g.Sessions[0].Submit(g.Sessions[0].Model.AugmentRerollAction(0,2)),"golden arbitrary slot");g.Hub.Pump();g.Equal();
                Assert(repaired && g.Sessions.All(s=>s.Model.GoldenRerollSlot(0)==2 && !s.Model.GoldenRerollAvailable(0) && s.Model.CanRerollAugment(0,0)),"repair preserves single golden and other ordinary rerolls");
                Assert(g.Sessions[0].Submit(g.Sessions[0].Model.AugmentRerollAction(0,0)),"ordinary reroll remains");g.Hub.Pump();g.Equal();
                Assert(g.Sessions.All(s=>s.Model.GetAugmentOfferTier(0,0)==s.Model.GetAugmentTier(0)),"no second promotion after repair");
            }
        }
        static void ControlledDiceAndCosmeticSync()
        {
            using(var g=new Group(2))
            {
                g.Sessions[1].SetAppearance(5,2,4);g.Hub.Pump();g.Start(51);
                Assert(g.Sessions.All(s=>s.Model.Players[1].Avatar==5 && s.Model.Players[1].DiceSkin==4),"unlocked appearance and dice snapshot");
                Assert(!g.Sessions[1].Submit(Game.DiceControlAction(9,1)),"guest cannot control other player's roll");
                bool repaired=false;g.Hub.Transform=(sender,target,packet)=>{if(target==2 && packet.Kind==PacketKind.Commit && !repaired){repaired=true;packet.Hash="control-repair";}return packet;};
                Assert(g.Sessions[0].Submit(Game.DiceControlAction(9,1)),"host controlled roll accepted");g.Hub.Pump();g.Equal();
                Assert(repaired && g.Sessions.All(s=>s.Model.LastDiceControlled && (s.Model.DiceA+s.Model.DiceB)%2==1),"parity and weighted dice synchronize");
            }
        }
        static void MapAndAppearanceLobby()
        {
            using(var g=new Group(3))
            {
                g.Sessions[1].SetReady(true);g.Sessions[2].SetReady(true);g.Hub.Pump();Assert(g.Sessions[0].CanStart,"all ready");
                Assert(!g.Sessions[1].SetMap(1) && !g.Sessions[0].SetMap(2),"host only and valid map");
                Assert(g.Sessions[0].SetMap(1),"host map change");g.Hub.Pump();Assert(g.Sessions.All(s=>s.MapId==1) && !g.Sessions[0].CanStart,"settings propagated and ready cleared");
                int outfit=WardrobeCatalog.Set(WardrobeCatalog.Set(WardrobeCatalog.Set(WardrobeCatalog.DefaultCode,0,1),1,5),3,4);
                g.Sessions[1].SetAppearance(3,2,0,outfit);g.Hub.Pump();Assert(g.Sessions.All(s=>s.Roster[1].Avatar==3 && s.Roster[1].ColorIndex==2 && s.Roster[1].Outfit==outfit),"appearance and wardrobe propagated");
                g.Sessions[1].SetAppearance(3,2,0,1<<25);g.Hub.Pump();Assert(g.Sessions.All(s=>s.Roster[1].Outfit==outfit),"invalid wardrobe rejected");
                g.Start(8,true);Assert(g.Sessions.All(s=>s.Model.Config.MapId==1 && s.Model.Players[1].Avatar==3 && s.Model.Players[1].ColorIndex==2 && s.Model.Players[1].Outfit==outfit && s.Model.Config.EnableBodyEconomy),"snapshot carries map and wardrobe profile");
                Assert(!g.Sessions[0].SetMap(0),"map locked during play");g.Equal();
            }
        }
        static void GuestLobbyReturnAndRematch()
        {
            using(var g=new Group(2))
            {
                g.Sessions[0].SetMap(1);g.Hub.Pump();g.Start(5,true);
                Assert(g.Sessions[1].RequestLobbyReturn(),"guest returns from match");g.Hub.Pump();
                Assert(g.Sessions.All(s=>s.Stage==OnlineStage.Lobby && s.Model==null && s.MapId==1) && g.Hub.Joinable,"return preserves room and map, opens joinability");
                Assert(!g.Sessions[0].CanStart,"rematch requires new ready");g.Start(6,true);g.Equal();
                Assert(g.Sessions.All(s=>s.Stage==OnlineStage.Playing && s.Model.Config.Seed==6),"same-room rematch");
            }
        }
        static void ConstructionSync()
        {
            int seed=1;
            for(;seed<128;seed++)
            {var sample=new Game(seed,0,3);sample.Execute("roll");if(sample.CurrentPlayerIndex==0 && sample.Phase==GamePhase.Decision && sample.Tiles[sample.CurrentPlayer.Position].Definition.Kind==TileKind.City)break;}
            Assert(seed<128,"city landing seed found");
            using(var g=new Group(3))
            {
                g.Start(seed);g.Sessions[0].Submit("roll");g.Hub.Pump();
                int tile=g.Sessions[0].Model.CurrentPlayer.Position;bool corrupted=false;
                g.Hub.Transform=(s,t,p)=>{if(t==2 && p.Kind==PacketKind.Commit && !corrupted){corrupted=true;p.Hash="force-construction-repair";}return p;};
                Assert(!g.Sessions[0].Submit("purchase:7") && !g.Sessions[1].Submit("purchase:1"),"lap lock and non-turn construction rejected");
                Assert(g.Sessions[0].Submit("purchase:1"),"atomic city purchase");g.Hub.Pump();g.Equal();
                Assert(corrupted && g.Sessions.All(s=>s.Model.Tiles[tile].Buildings==1 && !s.Model.Tiles[tile].Landmark),"snapshot restores exact separate buildings");
                Assert(!g.Sessions[0].Submit("construct:1") && !g.Sessions[0].Submit("purchase:1"),"online duplicate purchases rejected");
            }
        }
        static void FullGames()
        {
            for(int seed=1;seed<=12;seed++)using(var g=new Group(2+seed%3))
            {g.Start(seed);int budget=10000;while(g.Sessions[0].Model.Phase!=GamePhase.Finished && budget-->0)g.Step();Assert(budget>0,"whole online match terminates");g.Equal();}
        }
    }
}
