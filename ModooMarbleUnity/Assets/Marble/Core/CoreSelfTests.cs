using System;
using System.Linq;

namespace Marble.Core
{
    // Tests run both in Unity batch validation and a standalone Mono executable.
    public static class CoreSelfTests
    {
        private static void Assert(bool value,string message)
        { if(!value) throw new InvalidOperationException("Core test failed: "+message); }
        private static Game New(int seed=1) { return new Game(seed,0,4,240); }
        public static int Run()
        {
            Action[] cases={ BoardIntegrity,InvalidAction,PurchaseAndBuild,ConstructionLimits,LandmarkProtection,
                TollTransfer,ColorSetRequiresBuildings,Takeover,SalaryAndRemoteBuild,TravelNextTurn,
                IslandDoubleEscape,TripleDoubleIsland,DebtSale,EmergencyLoan,BankruptcyTransfer,
                ResortScaling,Bonus,VictoryPredicates,LastSurvivor,TurnLimit,ReplayDeterminism,FullGameSimulations };
            foreach(var test in cases) test();
            return cases.Length;
        }
        private static void BoardIntegrity()
        {
            var g=New(); Assert(g.Tiles.Count==32,"32 tiles");
            Assert(g.Tiles[2].Definition.Kind==TileKind.Bonus && g.Tiles[31].Definition.Name=="서울","APK topology");
            Assert(g.Tiles.Count(t=>t.Definition.Kind==TileKind.Resort)==5,"five resorts");
            Assert(g.Players.All(p=>p.Cash==2000),"equal starting cash");
        }
        private static void InvalidAction()
        {
            var g=New(); Assert(!g.Execute("buy"),"invalid command rejected");
            Assert(g.ReplayActions.Count==0 && g.CurrentPlayer.Cash==2000,"no mutation on invalid command");
        }
        private static void PurchaseAndBuild()
        {
            var g=New(); g.TestLand(0,1); long before=g.CurrentPlayer.Cash;
            Assert(g.Execute("buy"),"purchase legal"); var t=g.Tiles[1];
            Assert(t.OwnerIndex==0 && g.CurrentPlayer.Cash==before-t.Definition.Price,"purchase accounting");
            Assert(g.Execute("build:1"),"villa legal");
            Assert(t.Level==1 && g.CurrentPlayer.Cash==before-t.Definition.Price-t.Definition.BuildCosts[0],"build accounting");
            Assert(!g.Execute("build:2"),"one construction per landing");
        }
        private static void ConstructionLimits()
        {
            var g=New(); g.Tiles[1].OwnerIndex=0; g.TestLand(0,1);
            Assert(!g.GetActions().Any(a=>a.Id=="build:2"),"building locked first lap");
            g.CurrentPlayer.Laps=2; g.TestLand(0,1);
            Assert(g.Execute("construct:7") && g.Tiles[1].Buildings==7,"three distinct buildings after two laps");
            g.TestLand(0,1); Assert(g.Execute("build:4") && g.Tiles[1].Level==4,"landmark next visit");
        }
        private static void LandmarkProtection()
        {
            var g=New(); g.Tiles[1].OwnerIndex=1; g.Tiles[1].Level=4; g.TestLand(0,1);
            Assert(!g.GetActions().Any(a=>a.Id=="takeover"),"landmark cannot takeover");
            g.Tiles[3].OwnerIndex=0; g.Tiles[3].Level=4; g.CurrentPlayer.Cash=0;
            g.TestLand(0,1); Assert(g.Phase==GamePhase.Debt,"debt entered");
            Assert(!g.GetActions().Any(a=>a.Id=="sell:3"),"landmark cannot forced sale");
        }
        private static void TollTransfer()
        {
            var g=New(); g.Tiles[1].OwnerIndex=1; g.Tiles[1].Level=2;
            long toll=g.GetToll(1), a=g.Players[0].Cash,b=g.Players[1].Cash; g.TestLand(0,1);
            Assert(g.Players[0].Cash==a-toll && g.Players[1].Cash==b+toll,"toll conserving transfer");
        }
        private static void ColorSetRequiresBuildings()
        {
            var g=New(); g.Tiles[1].OwnerIndex=g.Tiles[3].OwnerIndex=0; g.Tiles[1].Level=1;
            long single=g.GetToll(1); Assert(!g.OwnsGroup(0,1),"unbuilt city prevents complete color");
            g.Tiles[3].Level=1; Assert(g.OwnsGroup(0,1) && g.GetToll(1)==single*2,"built color doubles toll");
            g.TestCheckVictory(0); Assert(g.WinnerIndex<0,"single color is not default win");
        }
        private static void Takeover()
        {
            var g=New(); g.Tiles[1].OwnerIndex=1; g.TestLand(0,1);
            long before=g.CurrentPlayer.Cash, seller=g.Players[1].Cash,price=g.GetTakeoverPrice(1);
            Assert(g.Execute("takeover"),"takeover allowed after toll");
            Assert(g.Tiles[1].OwnerIndex==0 && g.CurrentPlayer.Cash==before-price && g.Players[1].Cash==seller+price,"takeover funds to seller");
        }
        private static void SalaryAndRemoteBuild()
        {
            var g=New(); g.CurrentPlayer.Position=31; long cash=g.CurrentPlayer.Cash;
            g.TestMove(2); Assert(g.CurrentPlayer.Position==1 && g.CurrentPlayer.Laps==1 && g.CurrentPlayer.Cash==cash+g.Config.Salary,"salary wrap");
            g.Tiles[1].OwnerIndex=0; g.TestLand(0,0);
            Assert(g.GetActions().Any(a=>a.Id=="remote:1:1"),"start remote construction available");
            Assert(g.Execute("remote:1:1") && g.Tiles[1].Level==1,"remote construction applies");
        }
        private static void TravelNextTurn()
        {
            var g=New(); g.TestLand(0,24);
            Assert(g.CurrentPlayerIndex==1 && g.Players[0].PendingTravel,"travel waits next turn");
            for(int i=1;i<=3;i++) { g.TestLand(i,0); Assert(g.Execute("end"),"advance travel waiting players"); }
            Assert(g.CurrentPlayerIndex==0 && g.Phase==GamePhase.Travel,"travel prompt next turn");
            long cash=g.CurrentPlayer.Cash; Assert(g.Execute("travel:1"),"destination legal");
            Assert(!g.CurrentPlayer.PendingTravel && g.CurrentPlayer.Position==1 && g.CurrentPlayer.Cash==cash-g.Config.TravelFee+g.Config.Salary,"travel clockwise salary and fee");
        }
        private static void IslandDoubleEscape()
        {
            var g=New(); g.TestLand(0,8); Assert(g.Players[0].IslandTurns==3,"island three attempts");
            g.TestLand(0,0); g.TestRoll(2,2);
            Assert(g.Players[0].IslandTurns==0 && !g.ExtraRollPending,"escape double no additional roll");
        }
        private static void TripleDoubleIsland()
        {
            var g=New(); g.TestRoll(1,1); Assert(g.Execute("end"),"skip bonus");
            g.TestRoll(1,1); Assert(g.Execute("end"),"finish chance");
            g.TestRoll(1,1); Assert(g.Players[0].Position==8 && g.Players[0].IslandTurns==3,"third double island");
            Assert(g.CurrentPlayerIndex==1,"third double ends turn");
        }
        private static void DebtSale()
        {
            var g=New(); g.Tiles[31].OwnerIndex=0; g.Tiles[31].Level=3; g.Tiles[1].OwnerIndex=1;
            g.Players[0].Cash=0; long paid=g.GetToll(1), seller=g.Players[1].Cash, liquidation=g.GetLiquidationValue(31);
            g.TestLand(0,1); Assert(g.Phase==GamePhase.Debt,"insufficient toll enters debt");
            Assert(g.Execute("sell:31"),"sell to cover debt");
            Assert(g.Phase==GamePhase.Decision && g.Tiles[31].OwnerIndex==-1,"sale resumes landing");
            Assert(g.CurrentPlayer.Cash==liquidation-paid && g.Players[1].Cash==seller+paid,"sale and creditor amount");
        }
        private static void EmergencyLoan()
        {
            var g=New(); g.Tiles[1].OwnerIndex=1; g.Players[0].Cash=0; g.TestLand(0,1);
            Assert(g.Execute("loan"),"one emergency loan available");
            Assert(g.Players[0].LoanUsed && g.Players[0].LoanBalance>0 && g.Phase==GamePhase.Decision,"loan pays debt");
            g.TestLand(0,1); Assert(g.Phase==GamePhase.Debt && !g.GetActions().Any(a=>a.Id=="loan"),"loan only once");
        }
        private static void BankruptcyTransfer()
        {
            var g=New(); g.Tiles[31].OwnerIndex=0; g.Tiles[31].Level=2; g.Tiles[1].OwnerIndex=1;
            g.Players[0].Cash=3; long creditor=g.Players[1].Cash; g.TestLand(0,1);
            Assert(g.Execute("bankrupt"),"bankruptcy action");
            Assert(g.Players[0].Bankrupt && g.Players[0].Cash==0 && g.Tiles[31].OwnerIndex==1 && g.Tiles[31].Level==2,"creditor inherits built property");
            Assert(g.Players[1].Cash==creditor+3 && g.CurrentPlayerIndex==1,"remaining cash transfer and next player");
        }
        private static void ResortScaling()
        {
            var g=New(); g.Tiles[5].OwnerIndex=0; long first=g.GetToll(5); g.Tiles[14].OwnerIndex=0;
            Assert(g.GetToll(5)==first*2,"blue count doubles");
            g.Tiles[9].OwnerIndex=0; long pink=g.GetToll(9);
            g.TestLand(0,9); g.TestLand(0,9); g.TestLand(0,9); g.TestLand(0,9);
            Assert(g.GetToll(9)==pink*8 && g.Tiles[9].ResortArrivals==3,"pink arrivals capped three doubles");
        }
        private static void Bonus()
        {
            var g=New(); g.TestLand(0,2); long cash=g.CurrentPlayer.Cash; Assert(g.Phase==GamePhase.Bonus,"bonus phase");
            Assert(g.Execute("bonus:heads"),"bonus playable");
            Assert(g.Players[0].Cash==cash-100 || g.Players[0].Cash==cash+100,"bonus payout 2x entry");
        }
        private static void VictoryPredicates()
        {
            var g=New(); foreach(var t in g.Tiles.Where(t=>t.Definition.Kind==TileKind.Resort))t.OwnerIndex=0;
            g.TestCheckVictory(0); Assert(g.WinnerIndex==0 && g.WinnerReason=="관광지 독점","tourism win");
            g=New(); foreach(var t in g.Tiles.Where(t=>t.IsPurchasable && t.Index<8))t.OwnerIndex=1;
            g.TestCheckVictory(1); Assert(g.WinnerIndex==1 && g.WinnerReason=="라인 독점","line includes resorts");
            g=New(); foreach(var t in g.Tiles.Where(t=>t.Definition.Kind==TileKind.City && t.Definition.Group<=3)) {t.OwnerIndex=2;t.Level=1;}
            g.TestCheckVictory(2); Assert(g.WinnerIndex==2 && g.WinnerReason=="트리플 독점","three built colors win");
        }
        private static void TurnLimit()
        {
            var g=new Game(1,0,2,1); g.Players[1].Cash+=10; g.TestLand(0,0); g.Execute("end");
            Assert(g.Phase==GamePhase.Finished && g.WinnerIndex==1,"turn cap assets ranking");
        }
        private static void LastSurvivor()
        {
            var g=new Game(1,0,2); g.Tiles[1].OwnerIndex=1; g.Players[0].Cash=0;
            g.TestLand(0,1); g.Execute("bankrupt");
            Assert(g.WinnerIndex==1 && g.WinnerReason=="최후의 생존자" && g.Phase==GamePhase.Finished,"last solvent player wins");
        }
        private static string Snapshot(Game g)
        {
            return g.Phase+"|"+g.CurrentPlayerIndex+"|"+g.TurnNumber+"|"+g.DiceA+"|"+g.DiceB+"|"+g.WinnerIndex+"|"+
                string.Join(";",g.Players.Select(p=>p.Position+","+p.Cash+","+p.Laps+","+p.Bankrupt+","+p.IslandTurns+","+p.LoanBalance))+"|"+
                string.Join(";",g.Tiles.Select(t=>t.OwnerIndex+","+t.Buildings+","+t.Landmark+","+t.FestivalMultiplier+","+t.ResortArrivals));
        }
        private static void ReplayDeterminism()
        {
            var g=New(925); for(int i=0;i<200 && g.Phase!=GamePhase.Finished;i++) Assert(g.Execute(g.ChooseAiAction()),"AI legal replay action");
            var replay=Game.Replay(925,0,4,g.ReplayActions);
            Assert(Snapshot(g)==Snapshot(replay),"seed+actions deterministic replay");
            Assert(g.History.SequenceEqual(replay.History),"replay event log identical");
        }
        private static void FullGameSimulations()
        {
            for(int seed=1;seed<=64;seed++)
            {
                var g=New(seed); int actions=0;
                while(g.Phase!=GamePhase.Finished && actions++<10000)
                {
                    string selected=g.ChooseAiAction(); Assert(selected!=null && g.Execute(selected),"AI always legal, seed "+seed);
                    Assert(g.Players.All(p=>p.Cash>=0),"no negative cash, seed "+seed);
                    Assert(g.Tiles.All(t=>t.Level>=0 && t.Level<=4 && t.Buildings>=0 && t.Buildings<=7 && (!t.Landmark || t.Buildings==7) && t.OwnerIndex>=-1 && t.OwnerIndex<g.Players.Count),"valid property state");
                    Assert(g.Players.All(p=>p.Position>=0 && p.Position<32),"position bounds");
                    Assert(g.Tiles.All(t=>t.OwnerIndex<0 || !g.Players[t.OwnerIndex].Bankrupt),"bankrupt owns no property");
                }
                Assert(g.Phase==GamePhase.Finished && g.WinnerIndex>=0,"simulation terminates, seed "+seed);
                Assert(g.TurnNumber<=g.Config.MaxTurns,"turn cap respected");
            }
        }
    }
}
