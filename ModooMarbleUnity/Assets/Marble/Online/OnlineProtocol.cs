using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Marble.Core;

namespace Marble.Online
{
    public enum RoomPrivacy { Public, FriendsOnly, Private }
    public enum OnlineStage { Offline, Lobby, Playing, Faulted }
    public sealed class RoomMember { public ulong Id; public string Name; public bool Ready;public int Avatar,ColorIndex,DiceSkin;public int Outfit=WardrobeCatalog.DefaultCode; }
    public sealed class RoomListing { public ulong Id; public string Name; public int Members, Capacity; }
    public interface IRoomService : IDisposable
    {
        bool Available { get; }
        bool Busy { get; }
        string Status { get; }
        ulong LocalId { get; }
        ulong RoomId { get; }
        ulong HostId { get; }
        string RoomName { get; }
        RoomPrivacy Privacy { get; }
        IReadOnlyList<RoomMember> Members { get; }
        IReadOnlyList<RoomListing> Rooms { get; }
        event Action RoomChanged;
        event Action<ulong,byte[]> MessageReceived;
        event Action<string> Error;
        void Tick();
        void CreateRoom(string name,int capacity,RoomPrivacy privacy);
        void SearchRooms();
        void JoinRoom(ulong id);
        void LeaveRoom();
        void SetJoinable(bool value);
        void Send(ulong recipient,byte[] data);
        void OpenInviteOverlay();
    }

    public enum PacketKind : byte { Hello=1, Ready, Lobby, Snapshot, Request, Commit, Sync, Heartbeat, Ended, Rejected, Appearance, ReturnLobby }
    public sealed class OnlinePacket
    {
        public PacketKind Kind;
        public ulong Room;
        public string Nonce="", Action="", Hash="", Reason="";
        public int Sequence,Seed,MaxTurns=240,AugmentCount=4,MapId,Avatar,ColorIndex,DiceSkin;
        public int Outfit=WardrobeCatalog.DefaultCode;
        public bool Flag,Augments;
        public RoomMember[] Players=new RoomMember[0];
        public string[] Actions=new string[0];
    }

    public static class OnlineProtocol
    {
        public const string Compatibility="marble-classic-online-12";
        public const string GameKey="modoo-marble-local-reconstruction";
        public const int MaxPacket=262144, MaxActions=8192;
        static readonly Encoding Utf8=new UTF8Encoding(false,true);
        public static byte[] Encode(OnlinePacket p)
        {
            if(!WardrobeCatalog.IsValid(p.Outfit) || p.AugmentCount<1 || p.AugmentCount>8 || p.MapId<0 || p.MapId>=BoardCatalog.Count || p.Avatar<0 || p.Avatar>=CosmeticCatalog.AvatarCount || p.ColorIndex<0 || p.ColorIndex>3 || p.DiceSkin<0 || p.DiceSkin>=CosmeticCatalog.DiceSkinCount)throw new InvalidDataException("Augment count");
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Utf8,true))
            {
                w.Write(0x314F4D4Du);w.Write((byte)p.Kind);w.Write(p.Room);
                String(w,p.Nonce,64);w.Write(p.Sequence);w.Write(p.Seed);w.Write(p.MaxTurns);w.Write(p.Flag);w.Write(p.Augments);w.Write((byte)p.AugmentCount);w.Write((byte)p.MapId);w.Write((byte)p.Avatar);w.Write((byte)p.ColorIndex);w.Write((byte)p.DiceSkin);w.Write(p.Outfit);
                String(w,p.Action,128);String(w,p.Hash,64);String(w,p.Reason,512);
                if(p.Players==null || p.Players.Length>4)throw new InvalidDataException("Invalid roster size");
                w.Write((byte)p.Players.Length);
                foreach(var member in p.Players){if(!WardrobeCatalog.IsValid(member.Outfit) || member.Avatar<0 || member.Avatar>=CosmeticCatalog.AvatarCount || member.ColorIndex<0 || member.ColorIndex>3 || member.DiceSkin<0 || member.DiceSkin>=CosmeticCatalog.DiceSkinCount)throw new InvalidDataException("Appearance range");w.Write(member.Id);String(w,member.Name,160);w.Write(member.Ready);w.Write((byte)member.Avatar);w.Write((byte)member.ColorIndex);w.Write((byte)member.DiceSkin);w.Write(member.Outfit);}
                if(p.Actions==null || p.Actions.Length>MaxActions)throw new InvalidDataException("Replay limit");
                w.Write((ushort)p.Actions.Length);foreach(string action in p.Actions)String(w,action,128);
                if(stream.Length>MaxPacket)throw new InvalidDataException("Packet limit");
                return stream.ToArray();
            }
        }
        public static OnlinePacket Decode(byte[] bytes)
        {
            if(bytes==null || bytes.Length>MaxPacket || bytes.Length<24)throw new InvalidDataException("Packet size");
            using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream,Utf8))
            {
                if(r.ReadUInt32()!=0x314F4D4Du)throw new InvalidDataException("Protocol header");
                var p=new OnlinePacket{Kind=(PacketKind)r.ReadByte(),Room=r.ReadUInt64()};
                if(!Enum.IsDefined(typeof(PacketKind),p.Kind))throw new InvalidDataException("Packet kind");
                p.Nonce=String(r,64);p.Sequence=r.ReadInt32();p.Seed=r.ReadInt32();p.MaxTurns=r.ReadInt32();p.Flag=r.ReadBoolean();p.Augments=r.ReadBoolean();p.AugmentCount=r.ReadByte();p.MapId=r.ReadByte();p.Avatar=r.ReadByte();p.ColorIndex=r.ReadByte();p.DiceSkin=r.ReadByte();p.Outfit=r.ReadInt32();if(!WardrobeCatalog.IsValid(p.Outfit) || p.AugmentCount<1 || p.AugmentCount>8 || p.MapId<0 || p.MapId>=BoardCatalog.Count || p.Avatar<0 || p.Avatar>=CosmeticCatalog.AvatarCount || p.ColorIndex<0 || p.ColorIndex>3 || p.DiceSkin<0 || p.DiceSkin>=CosmeticCatalog.DiceSkinCount)throw new InvalidDataException("Augment count");
                p.Action=String(r,128);p.Hash=String(r,64);p.Reason=String(r,512);
                int count=r.ReadByte();if(count>4)throw new InvalidDataException("Roster limit");
                p.Players=new RoomMember[count];
                for(int i=0;i<count;i++)p.Players[i]=new RoomMember{Id=r.ReadUInt64(),Name=String(r,160),Ready=r.ReadBoolean(),Avatar=r.ReadByte(),ColorIndex=r.ReadByte(),DiceSkin=r.ReadByte(),Outfit=r.ReadInt32()};
                if(p.Players.Any(m=>!WardrobeCatalog.IsValid(m.Outfit) || m.Avatar>=CosmeticCatalog.AvatarCount || m.ColorIndex>3 || m.DiceSkin>=CosmeticCatalog.DiceSkinCount))throw new InvalidDataException("Appearance range");
                count=r.ReadUInt16();if(count>MaxActions)throw new InvalidDataException("Replay limit");
                p.Actions=new string[count];for(int i=0;i<count;i++)p.Actions[i]=String(r,128);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing data");
                return p;
            }
        }
        static void String(BinaryWriter w,string value,int max)
        {
            byte[] data=Utf8.GetBytes(value??"");if(data.Length>max)throw new InvalidDataException("String limit");
            w.Write((ushort)data.Length);w.Write(data);
        }
        static string String(BinaryReader r,int max)
        {
            int n=r.ReadUInt16();if(n>max || n>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("String length");
            return Utf8.GetString(r.ReadBytes(n));
        }
        public static string CleanName(string value)
        {
            string clean=new string((value??"").Where(c=>!char.IsControl(c)&&!char.IsSurrogate(c)).Take(32).ToArray()).Trim();
            return string.IsNullOrEmpty(clean)?"플레이어":clean;
        }
        public static string StateHash(Game g)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Utf8,true))
            {
                w.Write(Compatibility);w.Write(g.Config.Seed);w.Write(g.Config.PlayerCount);w.Write(g.Config.MaxTurns);w.Write(g.Config.EnableAugments);w.Write(g.Config.MapId);w.Write(g.Config.EndOnFirstBankruptcy);w.Write(g.Config.EnableBodyEconomy);w.Write(g.Config.CreditLimit);w.Write(g.Config.CreditTermRounds);w.Write(g.Config.CreditInterestPercent);w.Write(g.Config.MaxEconomyRounds);
                w.Write(g.EconomyRound);w.Write(g.PendingEconomySeat);w.Write(g.PendingRepayments.Length);foreach(int seat in g.PendingRepayments)w.Write(seat);
                w.Write(g.LastMovementSeat);w.Write(g.MovementSerial);w.Write(g.LastBrainReverseSteps);w.Write(g.LastMovementFallMask);w.Write(g.LastMovementPath.Count);foreach(int tile in g.LastMovementPath)w.Write(tile);
                w.Write(g.Config.AugmentRounds.Length);foreach(int round in g.Config.AugmentRounds)w.Write(round);
                w.Write(g.AugmentDraftIndex);w.Write(g.AugmentCompletedMask);w.Write(g.NextSpecialAugmentDraft);w.Write((int)g.AugmentResumePhase);
                w.Write(g.CurrentPlayerIndex);w.Write((int)g.Phase);w.Write(g.TurnNumber);w.Write(g.DiceA);w.Write(g.DiceB);w.Write(g.LastDiceControlled);w.Write(g.LastDiceTarget);w.Write(g.LastDiceParity);w.Write(g.LastDiceActor);
                w.Write(g.WinnerIndex);w.Write(g.WinnerReason??"");w.Write(g.DebtAmount);w.Write(g.DebtCreditorIndex);w.Write(g.ExtraRollPending);
                foreach(var p in g.Players){w.Write(p.Index);w.Write(p.Avatar);w.Write(p.ColorIndex);w.Write(p.DiceSkin);w.Write(p.Outfit);w.Write(p.Cash);w.Write(p.Position);w.Write(p.Laps);w.Write(p.IslandTurns);
                    w.Write(p.EscapeCards);w.Write(p.ShieldCards);w.Write(p.PendingTravel);w.Write(p.LoanUsed);w.Write(p.LoanBalance);w.Write(p.Bankrupt);w.Write(p.SoldBodyMask);w.Write(p.LastBodySaleTurn);w.Write(p.CreditDueRound);w.Write(p.Health);
                    w.Write(g.AugmentRerollMask(p.Index));w.Write(g.GoldenRerollSlot(p.Index));w.Write(p.Augments.Count);foreach(var choice in p.Augments){w.Write(choice.DraftIndex);w.Write(choice.OptionIndex);w.Write((int)choice.Tier);w.Write(choice.Rerolled);w.Write(choice.GoldenRerolled);}}
                foreach(var t in g.Tiles){w.Write(t.Index);w.Write(t.OwnerIndex);w.Write(t.Level);w.Write(t.Buildings);w.Write(t.Landmark);w.Write(t.FestivalMultiplier);w.Write(t.ResortArrivals);}
                w.Write(g.ReplayActions.Count);foreach(var action in g.ReplayActions)w.Write(action);
                w.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
    }
}
