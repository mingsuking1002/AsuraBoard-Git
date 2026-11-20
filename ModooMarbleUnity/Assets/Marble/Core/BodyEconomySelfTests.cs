using System;
using System.Linq;
namespace Marble.Core
{
    public static class BodyEconomySelfTests
    {
        static void Assert(bool value,string label){if(!value)throw new InvalidOperationException("Body economy test: "+label);}
        static Game New(int players=2,int seed=9)=>new Game(new GameConfig{PlayerCount=players,HumanCount=0,Seed=seed,EnableBodyEconomy=true,MaxTurns=200});
        static void Round(Game g){int round=g.EconomyRound,budget=8;while(g.EconomyRound==round && g.Phase!=GamePhase.Repayment && g.Phase!=GamePhase.Finished && budget-->0)g.TestAdvanceEconomyTurn();Assert(budget>0,"round closes");}
        public static int Run()
        {
            Action[] tests={Catalog,VoluntarySale,DuplicateAndQuota,DoubleDoesNotResetQuota,RestoreCostAndEffects,NoDebtRequired,CreditInvestment,ForcedPaymentBeyondLimit,DeadlineDoesNotExtend,InterestAtRoundEnd,RepayAndReborrow,EmergencyMultipleSales,VoluntaryLandmarkSale,MaturityElimination,AllEliminated,SoldAllStillPlays,BrainAndFallBounds,ReplayAndCompletion,RoundLimitAfterRescue,SkippedSeatsCloseRound,BodyAudio};
            foreach(var test in tests)test();return tests.Length;
        }
        static void Catalog(){Assert(BodyCatalog.Parts.Length==10 && BodyCatalog.Parts.Sum(p=>p.Price)==6800 && BodyCatalog.Get(BodyPart.Heart).RestorePrice==2250,"v1.4 prices and 150% restoration");}
        static void VoluntarySale(){var g=New();long before=g.CurrentPlayer.Cash;Assert(g.Execute("body_sell:0") && g.CurrentPlayer.Cash==before+400 && BodyCatalog.MissingEyes(g.CurrentPlayer)==1 && g.CurrentPlayer.CreditDueRound==0,"positive account sale");Assert(g.GetNetWorth(0)==before+400,"no separate body value deduction");}
        static void DuplicateAndQuota(){var g=New();g.Execute("body_sell:0");int count=g.ReplayActions.Count;Assert(!g.Execute("body_sell:0") && !g.Execute("body_sell:1") && !g.Execute("body_sell:10") && g.ReplayActions.Count==count,"duplicate, quota and invalid part rejected");Round(g);Assert(g.Execute("body_sell:1"),"next own turn renews quota");}
        static void DoubleDoesNotResetQuota(){var g=New();g.Execute("body_sell:0");g.CurrentPlayer.Position=1;g.TestRoll(1,1);g.Execute("end");Assert(g.CurrentPlayerIndex==0 && g.Phase==GamePhase.AwaitRoll && !g.Execute("body_sell:1"),"double remains same turn quota");}
        static void RestoreCostAndEffects(){var g=New();g.Execute("body_sell:0");long before=g.CurrentPlayer.Cash;Assert(g.Execute("body_restore:0") && g.CurrentPlayer.Cash==before-600 && g.CurrentPlayer.SoldBodyMask==0,"restore clears linked effect");Assert(!g.Execute("body_sell:0"),"restore does not renew current turn quota");}
        static void NoDebtRequired(){var g=New();Assert(g.GetActions().Any(a=>a.Id=="body_sell:9"),"heart can fund debt-free investment");var legacy=new Game(new GameConfig());Assert(!legacy.Execute("body_sell:0"),"legacy mode preserved");}
        static void CreditInvestment(){var g=New();g.CurrentPlayer.Cash=0;g.TestLand(0,1);Assert(g.Execute("purchase:1") && g.Players[0].Cash<0 && g.Players[0].CreditDueRound==4,"voluntary investment uses credit");g.CurrentPlayer.Cash=-1000;g.TestLand(0,3);Assert(!g.GetActions().Any(a=>a.Id=="buy" || a.Id.StartsWith("purchase:")),"investment stops at credit limit");}
        static void ForcedPaymentBeyondLimit(){var g=New();g.Tiles[1].OwnerIndex=1;g.Tiles[1].Level=4;g.Tiles[1].FestivalMultiplier=5;g.Players[0].Cash=1;long toll=g.GetToll(1),creditor=g.Players[1].Cash;g.TestLand(0,1);Assert(g.Phase!=GamePhase.Debt && !g.Players[0].Bankrupt && g.Players[0].Cash==1-toll && g.Players[1].Cash==creditor+toll && g.Players[0].Cash< -1000,"full mandatory toll even past limit");}
        static void DeadlineDoesNotExtend(){var g=New();g.Players[0].Cash=-100;g.TestCreditSync();int due=g.Players[0].CreditDueRound;Round(g);g.Players[0].Cash-=300;g.TestCreditSync();Assert(due==4 && g.Players[0].CreditDueRound==due,"extra borrowing does not extend maturity");}
        static void InterestAtRoundEnd(){var g=New();g.Players[0].Cash=-101;g.TestCreditSync();g.TestAdvanceEconomyTurn();Assert(g.Players[0].Cash==-101,"no per-seat interest");g.TestAdvanceEconomyTurn();Assert(g.Players[0].Cash==-107 && g.EconomyRound==2,"5% rounded upward once per round");}
        static void RepayAndReborrow(){var g=New();g.Players[0].Cash=-100;g.TestCreditSync();Round(g);g.Players[0].Cash=0;g.TestCreditSync();Assert(g.Players[0].CreditDueRound==0,"zero closes loan");g.Players[0].Cash=-100;g.TestCreditSync();Assert(g.Players[0].CreditDueRound==5,"new cycle begins at reborrowing round");}
        static void EmergencyMultipleSales(){var g=New();g.Players[0].Cash=-1000;g.Players[0].CreditDueRound=1;Round(g);Assert(g.Phase==GamePhase.Repayment && g.CurrentPlayerIndex==0,"last chance before elimination");Assert(g.Execute("body_sell:0") && g.Execute("body_sell:1") && g.Execute("body_sell:4"),"multiple emergency sales same settlement");Assert(g.Players[0].Cash>=0 && g.Execute("repayment_confirm") && !g.Players[0].Bankrupt && g.EconomyRound==2,"rescued and original next turn resumes");}
        static void VoluntaryLandmarkSale(){var g=New();g.Tiles[1].OwnerIndex=0;g.Tiles[1].Level=4;long value=g.GetLiquidationValue(1),cash=g.CurrentPlayer.Cash;Assert(g.Execute("asset_sell:1") && g.CurrentPlayer.Cash==cash+value && g.Tiles[1].OwnerIndex==-1,"landmark liquidation permitted by new economy");}
        static void MaturityElimination(){var g=New(4);g.Players[0].Cash=-100000;g.Players[0].CreditDueRound=1;Round(g);Assert(g.Phase==GamePhase.Repayment && !g.Players[0].Bankrupt,"not instant bankruptcy");g.Execute("repayment_confirm");Assert(g.Players[0].Bankrupt && g.Phase!=GamePhase.Finished && g.Players.Count(p=>!p.Bankrupt)==3,"first elimination does not end new economy match");}
        static void AllEliminated(){var g=New();foreach(var p in g.Players){p.Cash=-100000;p.CreditDueRound=1;}Round(g);g.Execute("repayment_confirm");Assert(g.Phase==GamePhase.Repayment,"all due players receive their final chance");g.Execute("repayment_confirm");Assert(g.Phase==GamePhase.Finished && g.WinnerIndex==-1 && g.GetStandings().Count==2,"simultaneous total elimination has no bankrupt winner");}
        static void SoldAllStillPlays(){var g=New();for(int i=0;i<10;i++){Assert(g.Execute("body_sell:"+i),"each body part sale");if(i<9)Round(g);}Assert(g.Players[0].SoldBodyMask==1023 && !g.Players[0].Bankrupt && g.Execute("roll"),"all parts sold and dice still works");Assert(g.Players[0].Cash>=0,"finite sale proceeds");}
        static void BrainAndFallBounds(){bool reverse=false,fall=false;for(int seed=0;seed<300;seed++){var g=New(2,seed);g.CurrentPlayer.SoldBodyMask=1023;g.TestBodyMovement(12);Assert(g.LastMovementPath.Count==12 && g.LastMovementPath.All(p=>p>=0 && p<32) && g.LastBrainReverseSteps<=2,"bounded route and one reverse segment");reverse|=g.LastBrainReverseSteps>0;fall|=g.LastMovementFallMask>0;}Assert(reverse && fall,"both effects can occur with all parts missing");}
        static void ReplayAndCompletion(){for(int seed=0;seed<12;seed++){var g=New(4,seed);g.Execute("body_sell:5");int budget=5000;while(g.Phase!=GamePhase.Finished && budget-->0)Assert(g.Execute(g.ChooseAiAction()),"economy AI action legal");Assert(budget>0,"complete economy match");var copy=new Game(g.Config);foreach(string a in g.ReplayActions)Assert(copy.Execute(a),"body economy replay");Assert(copy.EconomyRound==g.EconomyRound && copy.Players.Select(p=>p.Cash).SequenceEqual(g.Players.Select(p=>p.Cash)) && copy.Players.Select(p=>p.SoldBodyMask).SequenceEqual(g.Players.Select(p=>p.SoldBodyMask)),"economy state restored");}}
        static void RoundLimitAfterRescue()
        {
            var g=new Game(new GameConfig{EnableBodyEconomy=true,PlayerCount=2,MaxEconomyRounds=1});g.Players[0].Cash=-100;g.Players[0].CreditDueRound=1;Round(g);
            Assert(g.Phase==GamePhase.Repayment,"final repayment precedes round limit scoring");g.Execute("body_sell:0");g.Execute("repayment_confirm");Assert(g.Phase==GamePhase.Finished && g.WinnerIndex==1 && !g.Players[0].Bankrupt,"round limit scores rescued player normally");
        }
        static void SkippedSeatsCloseRound()
        {
            var g=New(4);g.Players[0].Bankrupt=true;g.Players[2].Bankrupt=true;g.Players[1].Cash=-100;g.Players[1].CreditDueRound=4;
            g.TestAdvanceEconomyTurn();Assert(g.CurrentPlayerIndex==1,"dead seat skipped");g.TestAdvanceEconomyTurn();Assert(g.CurrentPlayerIndex==3,"second dead seat skipped");g.TestAdvanceEconomyTurn();Assert(g.EconomyRound==2 && g.Players[1].Cash==-105,"round interest once despite skipped seats");
        }
        static void BodyAudio(){var a=Enumerable.Range(0,1000).Select(i=>i%2==0?.5f:-.5f).ToArray();new BodyAudioFilter().Process(a,16|256,24000,1);Assert(a.All(v=>!float.IsNaN(v) && Math.Abs(v)<=1) && a.Sum(v=>Math.Abs(v))>0,"local composite filter bounded and not silent");}
    }
}
