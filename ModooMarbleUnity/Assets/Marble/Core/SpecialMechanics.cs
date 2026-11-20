using System;
using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public enum SpecialMechanic
    {
        DiceCheat,Pickpocket,SlipperyFloor,IllegalParking,PropertyBroker,HiddenCamera,Roadblock,UninvitedGuest,LuckyPrank,InsuranceFraud,InterestCoupon,DamageRefund,BlackMarket,BodyInvestment,
        BuildingThief,PropertyBomber,EvilLandlord,PositionSwap,DiceHacker,ChainBomb,TaxCollector,ForcedAuction,Redevelopment,Revenge,DebtDevil,BodyMoney,CyborgLandlord,BadCreditTycoon,DeadlineExtension,HealthyCapital
    }
    [Serializable] public sealed class MechanicOptions
    {
        public long PickpocketCash=30,CameraCash=50,DoubleCash=50,InsuranceCash=100,TaxCash=100,AuctionDebtThreshold=500;
        public int ArtificialBodyMask=780;
        public int DicePeriod=3,RoadblockPeriod=3,RerollPeriod=3,InterestReductionPoints=2,RefundPercent=10,BodyBuildDiscountPercent=20;
        public int ChainPercent=20,RevengePercent=25,DebtBonusPer100=5,DebtBonusCap=100,CyborgPercent=5,CreditBonusPercent=50,HealthyPercent=15,AuctionMinimumBuildings=1;
        internal MechanicOptions Copy()
        {
            var c=(MechanicOptions)MemberwiseClone();
            foreach(var f in typeof(MechanicOptions).GetFields()) {long v=Convert.ToInt64(f.GetValue(c));if(v<0||v>1000000)throw new ArgumentException("Invalid mechanic option: "+f.Name);}
            if(c.DicePeriod<1||c.RoadblockPeriod<1||c.RerollPeriod<1||c.ArtificialBodyMask>BodyCatalog.AllMask||c.AuctionMinimumBuildings>3||c.RefundPercent>100||c.BodyBuildDiscountPercent>100||c.ChainPercent>100)throw new ArgumentException("Invalid mechanic option range.");
            return c;
        }
    }
    public sealed class MechanicPlayer
    {
        public long Cash {get;internal set;}
        public long Debt=>Math.Max(0,-Cash);
        public int Position {get;internal set;}
        public int Turns {get;internal set;}
        public int Laps {get;internal set;}
        public int SoldBodyMask {get;internal set;}
        public bool EverSoldBody {get;internal set;}
        public int DueRound {get;internal set;}
        internal long Refund;
        internal int NextMovePenalty;
        internal bool BuildCoupon,Extended;
        internal readonly Dictionary<SpecialMechanic,int> LastUse=new Dictionary<SpecialMechanic,int>();
        internal readonly HashSet<SpecialMechanic> Grants=new HashSet<SpecialMechanic>();
        public MechanicPlayer(long cash=2000,int dueRound=3,int laps=0){if(Math.Abs((decimal)cash)>1000000000||dueRound<0||laps<0)throw new ArgumentException();Cash=cash;DueRound=dueRound;Laps=laps;}
        public bool Has(SpecialMechanic mechanic)=>Grants.Contains(mechanic);
    }
    public sealed class MechanicProperty
    {
        public int Owner {get;internal set;}
        public int Buildings {get;internal set;}
        public bool Landmark {get;internal set;}
        public long Price {get;}
        public long Toll {get;}
        public IReadOnlyList<long> BuildCosts=>Array.AsReadOnly(costs);
        internal readonly long[] costs;
        internal int CameraVisitors,DisabledOwner=-1;
        internal long DisabledUntil;
        internal long ChainCharge;
        public MechanicProperty(int owner=-1,int buildings=0,long price=200,long toll=100,long[] buildCosts=null,bool landmark=false)
        {
            if(owner < -1||owner>3||buildings<0||buildings>7||price<0||price>1000000000||toll<0||toll>1000000000||landmark&&buildings!=7)throw new ArgumentException();
            costs=buildCosts==null?new long[]{100,200,300}:(long[])buildCosts.Clone();
            if(costs.Length!=3||costs.Any(c=>c<0||c>1000000000))throw new ArgumentException();
            Owner=owner;Buildings=buildings;Price=price;Toll=toll;Landmark=landmark;
        }
    }
    public sealed class MechanicAuction
    {
        public int Tile {get;internal set;}
        public int Seller {get;internal set;}
        public int Bidder {get;internal set;}=-1;
        public long Bid {get;internal set;}
        internal MechanicAuction(){}
    }
    // Explicitly owned simulation state. Never attaches to Game or grants augments automatically.
    public sealed class SpecialMechanics
    {
        const long MoneyCap=1000000000000L;
        readonly MechanicPlayer[] players;
        readonly MechanicProperty[] properties;
        readonly long[,] paid;
        readonly MechanicOptions options;
        uint rng;
        public IReadOnlyList<MechanicPlayer> Players=>Array.AsReadOnly(players);
        public IReadOnlyList<MechanicProperty> Properties=>Array.AsReadOnly(properties);
        public MechanicAuction Auction {get;private set;}
        public SpecialMechanics(MechanicPlayer[] players,MechanicProperty[] properties,int seed=1,MechanicOptions options=null)
        {
            if(players==null||players.Length<2||players.Length>4||players.Any(p=>p==null)||players.Distinct().Count()!=players.Length||properties==null||properties.Length<2||properties.Length>1024||properties.Any(t=>t==null||t.Owner>=players.Length)||properties.Distinct().Count()!=properties.Length)throw new ArgumentException();
            this.players=(MechanicPlayer[])players.Clone();this.properties=(MechanicProperty[])properties.Clone();this.options=(options??new MechanicOptions()).Copy();paid=new long[players.Length,players.Length];rng=unchecked((uint)seed);if(rng==0)rng=1;
        }
        MechanicPlayer P(int seat){if(seat<0||seat>=players.Length)throw new ArgumentOutOfRangeException(nameof(seat));return players[seat];}
        MechanicProperty T(int tile){if(tile<0||tile>=properties.Length)throw new ArgumentOutOfRangeException(nameof(tile));return properties[tile];}
        static int Count(int bits){int n=0;for(;bits!=0;bits&=bits-1)n++;return n;}
        static long Scale(long value,int percent)=>Math.Min(MoneyCap,(long)Math.Min((decimal)MoneyCap,(decimal)value*percent/100));
        static void Value(long v){if(v<0||v>MoneyCap)throw new ArgumentOutOfRangeException(nameof(v));}
        static void Credit(MechanicPlayer p,long amount){p.Cash=Math.Min(MoneyCap,p.Cash+amount);}
        static bool Charge(MechanicPlayer p,long amount){if(amount<0||p.Cash<amount)return false;p.Cash-=amount;return true;}
        bool Ready(int seat,SpecialMechanic skill,int period){var p=P(seat);return p.Has(skill)&&p.Turns>=period&&(!p.LastUse.TryGetValue(skill,out int last)||p.Turns-last>=period);}
        void Used(int seat,SpecialMechanic skill)=>P(seat).LastUse[skill]=P(seat).Turns;
        bool Opponents(int seat,int other)=>seat!=other&&P(seat)!=null&&P(other)!=null;
        bool Locked(int tile)=>Auction!=null&&Auction.Tile==tile;
        public void Grant(int seat,SpecialMechanic skill){if(!Enum.IsDefined(typeof(SpecialMechanic),skill))throw new ArgumentException();P(seat).Grants.Add(skill);}
        public void BeginTurn(int seat){var p=P(seat);if(p.Turns==int.MaxValue)throw new InvalidOperationException();p.Turns++;Credit(p,p.Refund);p.Refund=0;}
        public int AdjustDice(int seat,int sum,int adjustment)
        {
            if(sum<2||sum>12||Math.Abs((long)adjustment)!=1)throw new ArgumentException();
            if(!Ready(seat,SpecialMechanic.DiceCheat,options.DicePeriod))return sum;
            int adjusted=Math.Max(2,Math.Min(12,sum+adjustment));if(adjusted!=sum)Used(seat,SpecialMechanic.DiceCheat);return adjusted;
        }
        public bool ApplyRoadblock(int seat,int other)
        {if(!Opponents(seat,other)||!Ready(seat,SpecialMechanic.Roadblock,options.RoadblockPeriod))return false;P(other).NextMovePenalty=Math.Min(12,P(other).NextMovePenalty+1);Used(seat,SpecialMechanic.Roadblock);return true;}
        public int ConsumeMoveDistance(int seat,int rolledDistance)
        {if(rolledDistance<0||rolledDistance>1024)throw new ArgumentException();var p=P(seat);int result=Math.Max(0,rolledDistance-p.NextMovePenalty);p.NextMovePenalty=0;return result;}
        public long OnPassPlayer(int mover,int other)
        {if(!Opponents(mover,other)||!P(mover).Has(SpecialMechanic.Pickpocket))return 0;return TransferAvailable(other,mover,options.PickpocketCash);}
        long TransferAvailable(int from,int to,long requested)
        {var a=P(from);var b=P(to);long amount=Math.Min(Math.Max(0,a.Cash),Math.Min(requested,MoneyCap-b.Cash));a.Cash-=amount;b.Cash+=amount;return amount;}
        public int OnLand(int visitor,int tile)
        {
            var p=P(visitor);var t=T(tile);if(t.Owner<0||t.Owner==visitor)return 0;var owner=P(t.Owner);
            if(owner.Has(SpecialMechanic.IllegalParking))p.NextMovePenalty=Math.Min(12,p.NextMovePenalty+2);
            return owner.Has(SpecialMechanic.SlipperyFloor)?2:0;
        }
        // Movement is an explicit separate commit: caller chooses when to resolve the returned landing distance.
        public void Move(int seat,int distance)
        {
            if(distance < -1024||distance>1024)throw new ArgumentException();var p=P(seat);int direction=Math.Sign(distance);
            for(int step=0;step<Math.Abs(distance);step++)
            {
                p.Position=(p.Position+direction+properties.Length)%properties.Length;if(direction>0&&p.Position==0)p.Laps++;
                if(step+1<Math.Abs(distance))for(int i=0;i<players.Length;i++)if(i!=seat&&players[i].Position==p.Position)OnPassPlayer(seat,i);
            }
        }
        public long PurchaseQuote(int seat,int tile)
        {var p=P(seat);var t=T(tile);return t.Owner<0?Scale(t.Price,p.Has(SpecialMechanic.PropertyBroker)?85:100):t.Price;}
        public bool Purchase(int seat,int tile)
        {var p=P(seat);var t=T(tile);if(t.Owner>=0||Locked(tile)||!Charge(p,PurchaseQuote(seat,tile)))return false;ChangeOwner(t,seat);return true;}
        void ChangeOwner(MechanicProperty t,int owner){t.Owner=owner;t.CameraVisitors=0;t.ChainCharge=0;t.DisabledOwner=-1;t.DisabledUntil=0;}
        public long BuildQuote(int seat,int tile,int type)
        {var p=P(seat);var t=T(tile);if(type<1||type>3)throw new ArgumentException();return Scale(t.costs[type-1],p.Has(SpecialMechanic.BodyInvestment)&&p.BuildCoupon?100-options.BodyBuildDiscountPercent:100);}
        public bool Build(int seat,int tile,int type)
        {var p=P(seat);var t=T(tile);if(type<1||type>3||type>Math.Min(3L,(long)p.Laps+1)||t.Owner!=seat||t.Landmark||Locked(tile)||(t.Buildings&(1<<(type-1)))!=0)return false;if(!Charge(p,BuildQuote(seat,tile,type)))return false;t.Buildings|=1<<(type-1);if(p.Has(SpecialMechanic.BodyInvestment))p.BuildCoupon=false;return true;}
        public long TollQuote(int visitor,int tile)
        {
            var payer=P(visitor);var t=T(tile);if(t.Owner<0||t.Owner==visitor)return 0;var owner=P(t.Owner);
            if(t.DisabledOwner==t.Owner&&owner.Turns<t.DisabledUntil)return 0;
            int bonus=100;
            if(owner.Has(SpecialMechanic.EvilLandlord))bonus+=40;
            if(owner.Has(SpecialMechanic.DebtDevil))bonus+=(int)Math.Min(options.DebtBonusCap,owner.Debt/100*options.DebtBonusPer100);
            if(owner.Has(SpecialMechanic.CyborgLandlord))bonus+=Count(owner.SoldBodyMask&options.ArtificialBodyMask)*options.CyborgPercent;
            if(owner.Has(SpecialMechanic.HealthyCapital)&&!owner.EverSoldBody)bonus+=options.HealthyPercent;
            long value=Math.Min(MoneyCap,Scale(t.Toll,bonus)+t.ChainCharge);
            if(owner.Has(SpecialMechanic.HiddenCamera)&&(t.CameraVisitors&(1<<visitor))==0)value=Math.Min(MoneyCap,value+options.CameraCash);
            return Scale(value,payer.Has(SpecialMechanic.UninvitedGuest)?85:100);
        }
        public long PayToll(int visitor,int tile)
        {
            long quote=TollQuote(visitor,tile);var t=T(tile);if(quote==0)return 0;
            long actual=TransferAvailable(visitor,t.Owner,quote);if(actual==0)return 0;
            paid[visitor,t.Owner]=Math.Min(MoneyCap,paid[visitor,t.Owner]+actual);t.CameraVisitors|=1<<visitor;
            t.ChainCharge=P(t.Owner).Has(SpecialMechanic.ChainBomb)?Scale(actual,options.ChainPercent):0;
            if(P(visitor).Has(SpecialMechanic.DamageRefund))P(visitor).Refund=Math.Min(MoneyCap,P(visitor).Refund+Scale(actual,options.RefundPercent));return actual;
        }
        public long OnDiceRolled(int seat,int a,int b)
        {if(a<1||a>6||b<1||b>6)throw new ArgumentException();var p=P(seat);long reward=p.Has(SpecialMechanic.LuckyPrank)&&a==b?options.DoubleCash:0;Credit(p,reward);return reward;}
        public bool RerollDice(int seat,out int a,out int b)
        {a=b=0;if(!Ready(seat,SpecialMechanic.DiceHacker,options.RerollPeriod))return false;Used(seat,SpecialMechanic.DiceHacker);a=NextDie();b=NextDie();return true;}
        int NextDie(){unchecked{rng^=rng<<13;rng^=rng>>17;rng^=rng<<5;return (int)(rng%6)+1;}}
        public int InterestRate(int seat,int basePercent)
        {if(basePercent<0||basePercent>100)throw new ArgumentException();return Math.Max(0,basePercent-(P(seat).Has(SpecialMechanic.InterestCoupon)?options.InterestReductionPoints:0));}
        public long BodySaleQuote(int seat,BodyPart part)
        {if(!Enum.IsDefined(typeof(BodyPart),part))throw new ArgumentException();var p=P(seat);int bonus=100+(p.Has(SpecialMechanic.BlackMarket)?10:0)+(p.Has(SpecialMechanic.BodyMoney)?20:0);return Scale(BodyCatalog.Get(part).Price,bonus);}
        public bool SellBody(int seat,BodyPart part)
        {long price=BodySaleQuote(seat,part);var p=P(seat);int bit=1<<(int)part;if((p.SoldBodyMask&bit)!=0||p.Cash>MoneyCap-price)return false;p.SoldBodyMask|=bit;p.EverSoldBody=true;Credit(p,price);if(p.Has(SpecialMechanic.BodyInvestment))p.BuildCoupon=true;return true;}
        public bool RestoreBody(int seat,BodyPart part)
        {if(!Enum.IsDefined(typeof(BodyPart),part))throw new ArgumentException();var p=P(seat);int bit=1<<(int)part;if((p.SoldBodyMask&bit)==0||!Charge(p,BodyCatalog.Get(part).RestorePrice))return false;p.SoldBodyMask&=~bit;return true;}
        public long CreditLimit(int seat,long baseLimit)
        {Value(baseLimit);return Scale(baseLimit,P(seat).Has(SpecialMechanic.BadCreditTycoon)?100+options.CreditBonusPercent:100);}
        public bool ExtendDeadline(int seat)
        {var p=P(seat);if(!p.Has(SpecialMechanic.DeadlineExtension)||p.Extended||p.Debt==0||p.DueRound<=0||p.DueRound==int.MaxValue)return false;p.DueRound++;p.Extended=true;return true;}
        public bool SwapPositions(int seat,int other)
        {if(!Opponents(seat,other)||!P(seat).Has(SpecialMechanic.PositionSwap))return false;int n=P(seat).Position;P(seat).Position=P(other).Position;P(other).Position=n;return true;}
        int HighestBuilding(MechanicProperty t)=>t.Landmark?0:(t.Buildings&4)!=0?3:(t.Buildings&2)!=0?2:(t.Buildings&1)!=0?1:0;
        public bool StealBuilding(int seat,int source,int destination)
        {
            var p=P(seat);var src=T(source);var dst=T(destination);int type=HighestBuilding(src);
            if(!p.Has(SpecialMechanic.BuildingThief)||src.Owner<0||src.Owner==seat||dst.Owner!=seat||dst.Landmark||type==0||type>Math.Min(3L,(long)p.Laps+1)||(dst.Buildings&(1<<(type-1)))!=0||Locked(source)||Locked(destination))return false;
            src.Buildings&=~(1<<(type-1));dst.Buildings|=1<<(type-1);Compensate(src.Owner);return true;
        }
        public bool DamageBuilding(int attacker,int tile,int type)
        {
            var t=T(tile);P(attacker);if(type<1||type>3)throw new ArgumentException();
            if(t.Owner<0||t.Owner==attacker||t.Landmark||Locked(tile)||(t.Buildings&(1<<(type-1)))==0)return false;
            t.Buildings&=~(1<<(type-1));Compensate(t.Owner);return true;
        }
        public bool DisableProperty(int seat,int tile)
        {var t=T(tile);if(!P(seat).Has(SpecialMechanic.PropertyBomber)||t.Owner<0||t.Owner==seat||Locked(tile)||t.DisabledOwner==t.Owner&&P(t.Owner).Turns<t.DisabledUntil)return false;t.DisabledOwner=t.Owner;t.DisabledUntil=(long)P(t.Owner).Turns+2;Compensate(t.Owner);return true;}
        long Compensate(int seat){var p=P(seat);long amount=p.Has(SpecialMechanic.InsuranceFraud)?options.InsuranceCash:0;Credit(p,amount);return amount;}
        public bool Redevelop(int seat,int source,int destination)
        {
            var p=P(seat);var src=T(source);var dst=T(destination);int old=HighestBuilding(src),next=0;
            for(int type=1;type<=Math.Min(3L,(long)p.Laps+1);type++)if((dst.Buildings&(1<<(type-1)))==0){next=type;break;}
            if(!p.Has(SpecialMechanic.Redevelopment)||source==destination||src.Owner!=seat||dst.Owner!=seat||src.Landmark||dst.Landmark||old==0||next==0||Locked(source)||Locked(destination))return false;
            src.Buildings&=~(1<<(old-1));dst.Buildings|=1<<(next-1);return true;
        }
        public int RevengeTarget(int seat)
        {P(seat);long max=0;int target=-1;for(int i=0;i<players.Length;i++)if(i!=seat&&paid[seat,i]>max){max=paid[seat,i];target=i;}return target;}
        public long AttackPower(int seat,int other,long basePower)
        {Value(basePower);if(!Opponents(seat,other))return basePower;return Scale(basePower,P(seat).Has(SpecialMechanic.Revenge)&&RevengeTarget(seat)==other?100+options.RevengePercent:100);}
        public int RichestOpponent(int seat)
        {P(seat);return Enumerable.Range(0,players.Length).Where(i=>i!=seat).OrderByDescending(i=>players[i].Cash).ThenBy(i=>i).First();}
        public long CollectTax(int seat)
        {if(!P(seat).Has(SpecialMechanic.TaxCollector))return 0;return TransferAvailable(RichestOpponent(seat),seat,options.TaxCash);}
        public bool StartAuction(int seat,int tile)
        {
            var t=T(tile);if(!P(seat).Has(SpecialMechanic.ForcedAuction)||Auction!=null||t.Owner<0||t.Owner==seat||P(t.Owner).Debt<options.AuctionDebtThreshold||Count(t.Buildings)<options.AuctionMinimumBuildings)return false;
            Auction=new MechanicAuction{Tile=tile,Seller=t.Owner};return true;
        }
        public bool Bid(int seat,long amount)
        {
            var p=P(seat);Value(amount);if(Auction==null||seat==Auction.Seller||amount<=Auction.Bid)return false;
            long previous=Auction.Bidder==seat?Auction.Bid:0;long needed=amount-previous;if(p.Cash<needed)return false;
            if(Auction.Bidder>=0&&Auction.Bidder!=seat&&P(Auction.Bidder).Cash>MoneyCap-Auction.Bid)return false;
            if(Auction.Bidder>=0&&Auction.Bidder!=seat)Credit(P(Auction.Bidder),Auction.Bid);
            p.Cash-=needed;Auction.Bidder=seat;Auction.Bid=amount;return true;
        }
        public bool SettleAuction()
        {
            if(Auction==null)return false;var a=Auction;
            if(a.Bidder>=0&&(T(a.Tile).Owner==a.Seller?P(a.Seller):P(a.Bidder)).Cash>MoneyCap-a.Bid)return false;
            Auction=null;if(a.Bidder<0)return false;
            if(T(a.Tile).Owner!=a.Seller){Credit(P(a.Bidder),a.Bid);return false;}
            Credit(P(a.Seller),a.Bid);ChangeOwner(T(a.Tile),a.Bidder);return true;
        }
        public bool CancelAuction(){if(Auction==null)return false;if(Auction.Bidder>=0&&P(Auction.Bidder).Cash>MoneyCap-Auction.Bid)return false;if(Auction.Bidder>=0)Credit(P(Auction.Bidder),Auction.Bid);Auction=null;return true;}
    }
}
