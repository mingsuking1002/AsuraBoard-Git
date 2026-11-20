using System;
using System.Collections.Generic;
using System.Linq;

namespace Marble.Core
{
    public enum AbilityTrigger { TollPaid, TollReceived, BuildPrice, PurchasePrice, Salary, Tax, Dice, Move, Land, TurnStart, TurnEnd, Attack }
    public enum AbilityTarget { Owner, Other, Tile }
    public enum AbilityOperation { Add, Percent, Set, MoveRelative, MoveAbsolute, Status }
    public enum AbilityStatus { Bubble, Root, Reverse, TollShield, AttackShield, Reflect, Pull, Push, StealBlock, BuildBlock, TollBoost, TollSuppression }

    // Definitions are independent of augment tiers, UI and the live Game model.
    public sealed class AbilityModule
    {
        public readonly string Id, Name;
        public readonly AbilityTrigger Trigger;
        public readonly AbilityTarget Target;
        public readonly AbilityOperation Operation;
        public readonly int Amount, ChancePercent, CooldownTurns, MaxUses, DurationTurns;
        public readonly AbilityStatus Status;
        public AbilityModule(string id,string name,AbilityTrigger trigger,AbilityTarget target,AbilityOperation operation,
            int amount,int chancePercent=100,int cooldownTurns=0,int maxUses=0,AbilityStatus status=AbilityStatus.Bubble,int durationTurns=1)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(name)||!Enum.IsDefined(typeof(AbilityTrigger),trigger)||
                !Enum.IsDefined(typeof(AbilityTarget),target)||!Enum.IsDefined(typeof(AbilityOperation),operation)||!Enum.IsDefined(typeof(AbilityStatus),status)||
                chancePercent<0||chancePercent>100||cooldownTurns<0||maxUses<0||durationTurns<1||durationTurns>100||amount < -1000000||amount>1000000)
                throw new ArgumentException("Invalid ability definition.");
            if((operation==AbilityOperation.Add||operation==AbilityOperation.Percent||operation==AbilityOperation.Set)&&target!=AbilityTarget.Owner)
                throw new ArgumentException("Scalar modifiers use the event owner's value.");
            if(operation==AbilityOperation.Set&&amount<0)throw new ArgumentException("Negative scalar set.");
            Id=id;Name=name;Trigger=trigger;Target=target;Operation=operation;Amount=amount;ChancePercent=chancePercent;
            CooldownTurns=cooldownTurns;MaxUses=maxUses;Status=status;DurationTurns=durationTurns;
        }
    }
    public sealed class AbilityEvent
    {
        public readonly long Sequence, Value;
        public readonly int Owner, Other, Tile, OwnerTurn, BoardSize;
        public readonly AbilityTrigger Trigger;
        public AbilityEvent(long sequence,AbilityTrigger trigger,int owner,int other,int tile,int ownerTurn,long value,int boardSize=32)
        {
            if(sequence<0||!Enum.IsDefined(typeof(AbilityTrigger),trigger)||owner<0||owner>3||other < -1||other>3||other==owner||
                boardSize<1||boardSize>1024||tile < -1||tile>=boardSize||ownerTurn<0||value<0||value>1000000000000L)
                throw new ArgumentException("Invalid ability event.");
            Sequence=sequence;Trigger=trigger;Owner=owner;Other=other;Tile=tile;OwnerTurn=ownerTurn;Value=value;BoardSize=boardSize;
        }
    }
    // Intents are data for an authoritative game adapter, never direct Game mutations.
    public sealed class AbilityIntent
    {
        public readonly string ModuleId;
        public readonly AbilityOperation Operation;
        public readonly AbilityTarget Target;
        public readonly int TargetIndex, Amount, DurationTurns;
        public readonly AbilityStatus Status;
        internal AbilityIntent(AbilityModule m,int target){ModuleId=m.Id;Operation=m.Operation;Target=m.Target;TargetIndex=target;Amount=m.Amount;DurationTurns=m.DurationTurns;Status=m.Status;}
    }
    public sealed class AbilityResolution
    {
        public readonly long Value;
        public readonly IReadOnlyList<AbilityIntent> Intents;
        public readonly IReadOnlyList<string> Activated;
        internal AbilityResolution(long value,List<AbilityIntent> intents,List<string> activated)
        {Value=value;Intents=intents.AsReadOnly();Activated=activated.AsReadOnly();}
    }
    public sealed class AbilityLoadout
    {
        sealed class Grant {public AbilityModule Module;public int Uses;public long ReadyTurn;}
        readonly List<Grant> grants=new List<Grant>();
        readonly int owner, seed;
        long lastSequence=-1;
        int lastTurn=-1;
        public AbilityLoadout(int owner,int seed){if(owner<0||owner>3)throw new ArgumentOutOfRangeException(nameof(owner));this.owner=owner;this.seed=seed;}
        public void Add(AbilityModule module)
        {
            if(module==null)throw new ArgumentNullException(nameof(module));
            if(grants.Count>=64||grants.Any(g=>g.Module.Id==module.Id))throw new InvalidOperationException("At most 64 distinct modules per loadout.");
            grants.Add(new Grant{Module=module});
            grants.Sort((a,b)=>string.CompareOrdinal(a.Module.Id,b.Module.Id));
        }
        public AbilityResolution Resolve(AbilityEvent e)
        {
            if(e==null)throw new ArgumentNullException(nameof(e));
            if(e.Owner!=owner||e.Sequence<=lastSequence||e.OwnerTurn<lastTurn)throw new InvalidOperationException("Events must be ordered and belong to this owner.");
            lastSequence=e.Sequence;lastTurn=e.OwnerTurn;
            long value=e.Value;var intents=new List<AbilityIntent>();var activated=new List<string>();
            foreach(var g in grants)
            {
                var m=g.Module;int target=m.Target==AbilityTarget.Owner?owner:m.Target==AbilityTarget.Other?e.Other:e.Tile;
                if(m.Trigger!=e.Trigger||target<0||e.OwnerTurn<g.ReadyTurn||(m.MaxUses>0&&g.Uses>=m.MaxUses)||Roll(m.Id,e.Sequence)>=m.ChancePercent)continue;
                if(m.Operation==AbilityOperation.MoveAbsolute&&(m.Amount<0||m.Amount>=e.BoardSize))continue;
                switch(m.Operation)
                {
                    case AbilityOperation.Add:value=Clamp(value+m.Amount);break;
                    case AbilityOperation.Percent:value=Clamp(value*(100L+m.Amount)/100);break;
                    case AbilityOperation.Set:value=Clamp(m.Amount);break;
                    default:intents.Add(new AbilityIntent(m,target));break;
                }
                g.Uses++;g.ReadyTurn=(long)e.OwnerTurn+m.CooldownTurns;activated.Add(m.Id);
            }
            return new AbilityResolution(value,intents,activated);
        }
        static long Clamp(long n)=>Math.Max(0,Math.Min(1000000000000L,n));
        int Roll(string id,long sequence)
        {
            unchecked {uint h=2166136261u^(uint)seed^(uint)owner;foreach(char c in id){h^=c;h*=16777619u;}h^=(uint)sequence;h*=16777619u;h^=(uint)(sequence>>32);h^=h>>16;h*=0x7feb352du;h^=h>>15;return (int)(h%100);}
        }
    }
    public static class AbilityModules
    {
        static AbilityModule Scalar(string id,string name,AbilityTrigger trigger,AbilityOperation op,int amount,int uses=0)=>new AbilityModule(id,name,trigger,AbilityTarget.Owner,op,amount,maxUses:uses);
        static AbilityModule Status(string id,string name,AbilityTrigger trigger,AbilityTarget target,AbilityStatus status,int duration=1,int chance=100,int cooldown=0)=>new AbilityModule(id,name,trigger,target,AbilityOperation.Status,0,chance,cooldown,status:status,durationTurns:duration);
        static readonly AbilityModule[] modules={
            Scalar("toll.discount","통행료 할인",AbilityTrigger.TollPaid,AbilityOperation.Percent,-25),
            Scalar("toll.free","일회 통행료 면제",AbilityTrigger.TollPaid,AbilityOperation.Set,0,1),
            Scalar("toll.flat","통행료 정액 경감",AbilityTrigger.TollPaid,AbilityOperation.Add,-100),
            Scalar("toll.bonus","통행료 수입 증가",AbilityTrigger.TollReceived,AbilityOperation.Percent,30),
            Scalar("toll.tip","통행료 추가 수입",AbilityTrigger.TollReceived,AbilityOperation.Add,50),
            Scalar("build.discount","건설비 할인",AbilityTrigger.BuildPrice,AbilityOperation.Percent,-20),
            Scalar("build.free","일회 무료 건설",AbilityTrigger.BuildPrice,AbilityOperation.Set,0,1),
            Scalar("purchase.discount","땅값 할인",AbilityTrigger.PurchasePrice,AbilityOperation.Percent,-15),
            Scalar("purchase.coupon","토지 구매 쿠폰",AbilityTrigger.PurchasePrice,AbilityOperation.Add,-100,1),
            Scalar("salary.bonus","월급 인상",AbilityTrigger.Salary,AbilityOperation.Percent,25),
            Scalar("salary.tip","월급 정액 보너스",AbilityTrigger.Salary,AbilityOperation.Add,100),
            Scalar("tax.discount","세금 감면",AbilityTrigger.Tax,AbilityOperation.Percent,-50),
            Scalar("tax.free","일회 세금 면제",AbilityTrigger.Tax,AbilityOperation.Set,0,1),
            Scalar("dice.plus","주사위 합 증가",AbilityTrigger.Dice,AbilityOperation.Add,1),
            Scalar("dice.minus","주사위 합 감소",AbilityTrigger.Dice,AbilityOperation.Add,-1),
            Scalar("move.plus","추가 이동 거리",AbilityTrigger.Move,AbilityOperation.Add,2),
            Scalar("move.half","이동 거리 반감",AbilityTrigger.Move,AbilityOperation.Percent,-50),
            new AbilityModule("move.forward","착지 후 두 칸 전진",AbilityTrigger.Land,AbilityTarget.Owner,AbilityOperation.MoveRelative,2,cooldownTurns:1),
            new AbilityModule("move.backward","착지 후 한 칸 후진",AbilityTrigger.Land,AbilityTarget.Owner,AbilityOperation.MoveRelative,-1,cooldownTurns:1),
            new AbilityModule("move.start","출발지 귀환",AbilityTrigger.TurnStart,AbilityTarget.Owner,AbilityOperation.MoveAbsolute,0,maxUses:1),
            Status("status.bubble","물방울 가두기",AbilityTrigger.Attack,AbilityTarget.Other,AbilityStatus.Bubble,1,35,2),
            Status("status.root","이동 봉쇄",AbilityTrigger.Attack,AbilityTarget.Other,AbilityStatus.Root),
            Status("status.reverse","역방향 저주",AbilityTrigger.Attack,AbilityTarget.Other,AbilityStatus.Reverse,2),
            Status("status.toll-shield","통행료 방어막",AbilityTrigger.TurnStart,AbilityTarget.Owner,AbilityStatus.TollShield),
            Status("status.attack-shield","공격 방어막",AbilityTrigger.TurnStart,AbilityTarget.Owner,AbilityStatus.AttackShield),
            Status("status.reflect","공격 반사",AbilityTrigger.TurnStart,AbilityTarget.Owner,AbilityStatus.Reflect),
            Status("status.pull","끌어당기기 표식",AbilityTrigger.Attack,AbilityTarget.Other,AbilityStatus.Pull),
            Status("status.push","밀어내기 표식",AbilityTrigger.Attack,AbilityTarget.Other,AbilityStatus.Push),
            Status("status.steal-block","강제 인수 방어",AbilityTrigger.TurnStart,AbilityTarget.Owner,AbilityStatus.StealBlock,2),
            Status("status.build-block","건설 봉쇄",AbilityTrigger.Attack,AbilityTarget.Other,AbilityStatus.BuildBlock),
            Status("status.toll-boost","땅 통행료 강화 표식",AbilityTrigger.Land,AbilityTarget.Tile,AbilityStatus.TollBoost,2),
            Status("status.toll-suppression","땅 통행료 억제 표식",AbilityTrigger.Land,AbilityTarget.Tile,AbilityStatus.TollSuppression,2)
        };
        public static IReadOnlyList<AbilityModule> All {get;}=Array.AsReadOnly(modules);
        public static AbilityModule Get(string id)=>modules.FirstOrDefault(m=>m.Id==id)??throw new ArgumentException("Unknown ability: "+id);
    }
}
