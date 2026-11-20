using System;
using System.Linq;
namespace Marble.Core
{
    public static class SpecialMechanicsSelfTests
    {
        static void Check(bool result,string message){if(!result)throw new Exception("Special mechanics: "+message);}
        static SpecialMechanics World(long cash=2000,int laps=2,MechanicOptions options=null)=>new SpecialMechanics(
            new[]{new MechanicPlayer(cash,laps:laps),new MechanicPlayer(2000,laps:2),new MechanicPlayer(3000,laps:2)},
            new[]{new MechanicProperty(0,1),new MechanicProperty(1,3),new MechanicProperty(),new MechanicProperty(0),new MechanicProperty(2,4)},options:options);
        static void Turns(SpecialMechanics w,int seat,int count){for(int i=0;i<count;i++)w.BeginTurn(seat);}
        public static int Run()
        {
            int n=0;var w=World();w.Grant(0,SpecialMechanic.DiceCheat);Check(w.AdjustDice(0,6,1)==6,"cheat early");Turns(w,0,3);Check(w.AdjustDice(0,6,-1)==5&&w.AdjustDice(0,6,1)==6,"cheat cadence");Turns(w,0,3);Check(w.AdjustDice(0,12,1)==12&&w.AdjustDice(0,12,-1)==11,"cheat clamp preserves use");n++;
            w=World();w.Grant(0,SpecialMechanic.Pickpocket);Check(w.OnPassPlayer(0,1)==30&&w.Players[0].Cash==2030&&w.Players[1].Cash==1970,"pickpocket conserved");n++;
            w=World();w.Grant(0,SpecialMechanic.SlipperyFloor);Check(w.OnLand(1,0)==2&&w.OnLand(0,0)==0,"slippery");n++;
            w=World();w.Grant(0,SpecialMechanic.IllegalParking);w.OnLand(1,0);Check(w.ConsumeMoveDistance(1,7)==5&&w.ConsumeMoveDistance(1,7)==7,"parking consumed once");n++;
            w=World();w.Grant(0,SpecialMechanic.PropertyBroker);Check(w.PurchaseQuote(0,2)==170&&w.Purchase(0,2)&&w.Players[0].Cash==1830,"broker commit");n++;
            w=World();w.Grant(0,SpecialMechanic.HiddenCamera);Check(w.TollQuote(1,0)==150&&w.TollQuote(1,0)==150,"camera quote pure");Check(w.PayToll(1,0)==150&&w.PayToll(1,0)==100&&w.PayToll(2,0)==150,"camera per visitor");n++;
            w=World();w.Grant(0,SpecialMechanic.Roadblock);Check(!w.ApplyRoadblock(0,1),"roadblock early");Turns(w,0,3);Check(w.ApplyRoadblock(0,1)&&!w.ApplyRoadblock(0,2)&&w.ConsumeMoveDistance(1,5)==4,"roadblock cadence");n++;
            w=World();w.Grant(0,SpecialMechanic.UninvitedGuest);Check(w.PayToll(0,1)==85,"guest discount");n++;
            w=World();w.Grant(0,SpecialMechanic.LuckyPrank);Check(w.OnDiceRolled(0,3,3)==50&&w.OnDiceRolled(0,3,2)==0&&w.Players[0].Cash==2050,"double reward");n++;
            w=World();w.Grant(1,SpecialMechanic.InsuranceFraud);Check(w.DamageBuilding(0,1,1)&&!w.DamageBuilding(0,1,1)&&w.Players[1].Cash==2100,"insurance only real damage");n++;
            w=World();w.Grant(0,SpecialMechanic.InterestCoupon);Check(w.InterestRate(0,5)==3&&w.InterestRate(0,1)==0,"interest floor");n++;
            w=World();w.Grant(0,SpecialMechanic.DamageRefund);w.PayToll(0,1);Check(w.Players[0].Cash==1900,"refund deferred");w.BeginTurn(0);Check(w.Players[0].Cash==1910,"refund next turn");w.BeginTurn(0);Check(w.Players[0].Cash==1910,"refund not repeated");n++;
            w=World();w.Grant(0,SpecialMechanic.BlackMarket);Check(w.BodySaleQuote(0,BodyPart.LeftEye)==440&&w.SellBody(0,BodyPart.LeftEye)&&!w.SellBody(0,BodyPart.LeftEye)&&w.Players[0].Cash==2440,"black market sale");n++;
            w=World();w.Grant(0,SpecialMechanic.BodyInvestment);w.SellBody(0,BodyPart.LeftEye);Check(w.BuildQuote(0,3,1)==80&&w.BuildQuote(0,3,1)==80,"coupon quote pure");Check(w.Build(0,3,1)&&w.BuildQuote(0,3,2)==200&&!w.Build(0,3,1),"coupon consumed on valid build");n++;
            w=World();w.Grant(0,SpecialMechanic.BuildingThief);Check(w.StealBuilding(0,1,0)&&w.Properties[1].Buildings==1&&w.Properties[0].Buildings==3&&!w.StealBuilding(0,1,0),"building transfer distinct type");n++;
            w=World();w.Grant(0,SpecialMechanic.PropertyBomber);w.Grant(1,SpecialMechanic.InsuranceFraud);Check(w.DisableProperty(0,1)&&!w.DisableProperty(0,1)&&w.TollQuote(0,1)==0&&w.Players[1].Cash==2100,"bomb insurance");w.BeginTurn(1);Check(w.TollQuote(0,1)==0,"disabled one turn");w.BeginTurn(1);Check(w.TollQuote(0,1)==100,"disabled expiry");n++;
            w=World();w.Grant(0,SpecialMechanic.EvilLandlord);Check(w.TollQuote(1,0)==140,"landlord bonus");n++;
            w=World();w.Move(1,2);w.Grant(0,SpecialMechanic.PositionSwap);Check(w.SwapPositions(0,1)&&w.Players[0].Position==2&&w.Players[1].Position==0,"position swap");n++;
            w=World();var replay=World();w.Grant(0,SpecialMechanic.DiceHacker);replay.Grant(0,SpecialMechanic.DiceHacker);Turns(w,0,3);Turns(replay,0,3);Check(w.RerollDice(0,out int a,out int b)&&replay.RerollDice(0,out int c,out int d)&&a==c&&b==d&&a>=1&&a<=6&&b>=1&&b<=6&&!w.RerollDice(0,out _,out _),"reroll seeded cadence");n++;
            w=World();w.Grant(0,SpecialMechanic.ChainBomb);Check(w.PayToll(1,0)==100&&w.TollQuote(2,0)==120&&w.TollQuote(2,0)==120&&w.PayToll(2,0)==120&&w.TollQuote(1,0)==124,"chain stored and consumed");n++;
            w=World();w.Grant(0,SpecialMechanic.TaxCollector);Check(w.RichestOpponent(0)==2&&w.CollectTax(0)==100&&w.Players[2].Cash==2900,"tax richest");n++;
            w=new SpecialMechanics(new[]{new MechanicPlayer(),new MechanicPlayer(-700),new MechanicPlayer(3000)},new[]{new MechanicProperty(1,3),new MechanicProperty(0)});w.Grant(0,SpecialMechanic.ForcedAuction);Check(w.StartAuction(0,0)&&!w.StartAuction(0,0)&&w.Bid(0,200)&&w.Bid(2,300)&&w.Players[0].Cash==2000&&w.Players[2].Cash==2700&&w.SettleAuction()&&w.Properties[0].Owner==2&&w.Players[1].Cash==-400,"auction escrow settlement");n++;
            w=World();w.Grant(0,SpecialMechanic.Redevelopment);Check(w.Redevelop(0,0,3)&&w.Properties[0].Buildings==0&&w.Properties[3].Buildings==1&&!w.Redevelop(0,0,3),"redevelopment moves building");n++;
            w=World();w.Grant(0,SpecialMechanic.Revenge);w.PayToll(0,1);Check(w.RevengeTarget(0)==1&&w.AttackPower(0,1,100)==125&&w.AttackPower(0,2,100)==100,"revenge ledger");n++;
            w=World(-500);w.Grant(0,SpecialMechanic.DebtDevil);Check(w.TollQuote(1,0)==125,"debt scaling");n++;
            w=World();w.Grant(0,SpecialMechanic.BodyMoney);Check(w.BodySaleQuote(0,BodyPart.LeftLeg)==840,"body 20 percent");n++;
            w=World();w.Grant(0,SpecialMechanic.CyborgLandlord);w.SellBody(0,BodyPart.LeftEye);Check(w.TollQuote(1,0)==100,"missing eye not artificial");w.SellBody(0,BodyPart.LeftLeg);Check(w.TollQuote(1,0)==105,"artificial leg bonus");w.RestoreBody(0,BodyPart.LeftLeg);Check(w.TollQuote(1,0)==100,"restored part removes bonus");n++;
            w=World(-300);w.Grant(0,SpecialMechanic.BadCreditTycoon);Check(w.CreditLimit(0,1000)==1500&&w.Players[0].DueRound==3,"credit unchanged term");n++;
            w=World(-300);w.Grant(0,SpecialMechanic.DeadlineExtension);Check(w.ExtendDeadline(0)&&w.Players[0].DueRound==4&&!w.ExtendDeadline(0),"extension once");n++;
            w=World();w.Grant(0,SpecialMechanic.HealthyCapital);Check(w.TollQuote(1,0)==115,"healthy income");w.SellBody(0,BodyPart.LeftEye);w.RestoreBody(0,BodyPart.LeftEye);Check(w.TollQuote(1,0)==100,"ever sold stays disqualified");n++;
            w=World();Check(w.AdjustDice(0,6,1)==6&&!w.SwapPositions(0,1)&&w.OnPassPlayer(0,1)==0&&w.InterestRate(0,5)==5&&w.BodySaleQuote(0,BodyPart.LeftEye)==400,"no grants no ability");n++;
            w=World();w.Grant(0,SpecialMechanic.BlackMarket);w.Grant(0,SpecialMechanic.BodyMoney);Check(w.BodySaleQuote(0,BodyPart.LeftEye)==520,"body bonuses additive");n++;
            w=World(laps:0);w.Grant(0,SpecialMechanic.BuildingThief);Check(!w.StealBuilding(0,1,3)&&!w.Build(0,3,2),"lap construction limit");n++;
            w=World();w.Grant(0,SpecialMechanic.Pickpocket);w.Move(1,1);w.Move(0,2);Check(w.Players[0].Position==2&&w.Players[0].Cash==2030,"path pass trigger");n++;
            w=new SpecialMechanics(new[]{new MechanicPlayer(),new MechanicPlayer(-700)},new[]{new MechanicProperty(1,1),new MechanicProperty(0)});w.Grant(0,SpecialMechanic.ForcedAuction);Check(w.StartAuction(0,0)&&w.Bid(0,100)&&w.CancelAuction()&&w.Players[0].Cash==2000&&w.Properties[0].Owner==1,"cancel refunds escrow");n++;
            Check(Enum.GetValues(typeof(SpecialMechanic)).Length==30,"30 opt-in functions");n++;
            w=World();w.Grant(0,SpecialMechanic.DeadlineExtension);Check(!w.ExtendDeadline(0)&&w.Players[0].DueRound==3,"no debt no extension");n++;
            w=World(cash:1);w.Grant(0,SpecialMechanic.BodyInvestment);w.SellBody(0,BodyPart.LeftEye);Check(!w.Build(0,1,1)&&w.BuildQuote(0,3,1)==80,"invalid build preserves coupon");n++;
            w=World();w.Grant(0,SpecialMechanic.Pickpocket);w.Move(1,2);w.Move(0,2);Check(w.Players[0].Cash==2000,"landing player is not passing");n++;
            w=World();w.Grant(0,SpecialMechanic.Redevelopment);w.Grant(0,SpecialMechanic.InsuranceFraud);Check(w.Redevelop(0,0,3)&&w.Players[0].Cash==2000,"self sacrifice no insurance farm");n++;
            w=World(cash:10);w.Grant(1,SpecialMechanic.ChainBomb);w.Grant(0,SpecialMechanic.DamageRefund);Check(w.PayToll(0,1)==10&&w.TollQuote(2,1)==102,"partial payment uses actual amount");w.BeginTurn(0);Check(w.Players[0].Cash==1,"partial refund actual paid");n++;
            w=new SpecialMechanics(new[]{new MechanicPlayer(),new MechanicPlayer(-700)},new[]{new MechanicProperty(1,1),new MechanicProperty(0)});w.Grant(0,SpecialMechanic.ForcedAuction);Check(w.StartAuction(0,0)&&!w.Bid(1,100)&&!w.Bid(0,3000)&&w.Bid(0,100)&&w.Bid(0,150)&&w.Players[0].Cash==1850&&w.CancelAuction()&&w.Players[0].Cash==2000,"seller disallowed and own rebid delta");n++;
            w=World();w.Grant(0,SpecialMechanic.DamageRefund);w.Grant(0,SpecialMechanic.UninvitedGuest);w.PayToll(0,1);w.BeginTurn(0);Check(w.Players[0].Cash==1923,"discount then refund integer rounding");n++;
            return n;
        }
    }
}
