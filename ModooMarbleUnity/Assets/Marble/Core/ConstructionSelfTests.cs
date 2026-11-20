using System;
using System.Linq;
namespace Marble.Core
{
    public static class ConstructionSelfTests
    {
        static void Assert(bool value,string label){if(!value)throw new InvalidOperationException("Construction test: "+label);}
        static Game New()=>new Game(7,0,4);
        public static int Run()
        {
            Action[] tests={FirstLapAndRealWrap,AtomicPurchase,IndependentBuildings,NoDuplicates,NoPartialPayment,LandmarkGate,RemoteGate,Accounting,Replay,Resorts,LandmarkQuote};
            foreach(var test in tests)test();return tests.Length;
        }
        static void FirstLapAndRealWrap()
        {
            var g=New();g.TestLand(0,1);Assert(g.AvailableBuildings(1,0)==1 && !g.Execute("purchase:3") && !g.Execute("build:2"),"initial villa only");
            g.CurrentPlayer.Position=31;g.TestMove(1);g.TestLand(0,1);
            Assert(g.CurrentPlayer.Laps==1 && g.AvailableBuildings(1,0)==3 && g.GetActions().Any(a=>a.Id=="purchase:3") && !g.Execute("purchase:7"),"one physical lap unlocks building");
            g.CurrentPlayer.Position=31;g.TestMove(1);g.TestLand(0,1);
            Assert(g.CurrentPlayer.Laps==2 && g.AvailableBuildings(1,0)==7 && g.GetActions().Any(a=>a.Id=="purchase:7"),"two laps unlock hotel");
        }
        static void AtomicPurchase()
        {
            var g=New();g.CurrentPlayer.Laps=2;g.TestLand(0,1);long cash=g.CurrentPlayer.Cash,cost=g.ConstructionPrice(1,7);
            Assert(g.Execute("purchase:7") && g.Tiles[1].OwnerIndex==0 && g.Tiles[1].Buildings==7,"land and all chosen buildings in one action");
            Assert(g.CurrentPlayer.Cash==cash-cost && g.ReplayActions.Count==1 && g.ConstructionSpent,"one payment and one visit construction");
        }
        static void IndependentBuildings()
        {
            var g=New();g.CurrentPlayer.Laps=2;g.TestLand(0,1);long cash=g.CurrentPlayer.Cash;
            Assert(g.Execute("purchase:4") && g.Tiles[1].Buildings==4 && !g.Tiles[1].HasBuilding(1) && !g.Tiles[1].HasBuilding(2),"hotel can be selected independently");
            Assert(g.CurrentPlayer.Cash==cash-g.Tiles[1].Definition.Price-g.Tiles[1].Definition.BuildCosts[2],"unselected lower buildings not charged");
            g.TestLand(0,1);cash=g.CurrentPlayer.Cash;
            Assert(g.Execute("construct:3") && g.Tiles[1].Buildings==7,"fill missing villa and building only");
            Assert(g.CurrentPlayer.Cash==cash-g.Tiles[1].Definition.BuildCosts[0]-g.Tiles[1].Definition.BuildCosts[1],"hotel not charged again");
        }
        static void NoDuplicates()
        {
            var g=New();g.TestLand(0,1);g.Execute("purchase:1");g.CurrentPlayer.Laps=2;g.TestLand(0,1);long cash=g.CurrentPlayer.Cash;int count=g.ReplayActions.Count;
            Assert(!g.Execute("build:1") && !g.Execute("construct:1") && !g.Execute("construct:3") && !g.Execute("purchase:1"),"any mask overlapping an existing type rejected");
            Assert(g.CurrentPlayer.Cash==cash && g.ReplayActions.Count==count && g.Tiles[1].Buildings==1,"duplicate rejection has no effects");
        }
        static void NoPartialPayment()
        {
            var g=New();g.TestLand(0,1);g.CurrentPlayer.Cash=g.ConstructionPrice(1,1)-1;long cash=g.CurrentPlayer.Cash;
            Assert(!g.Execute("purchase:1") && g.Tiles[1].OwnerIndex==-1 && g.Tiles[1].Buildings==0 && g.CurrentPlayer.Cash==cash && g.ReplayActions.Count==0,"unaffordable bundle does not partly buy land");
            Assert(g.Execute("buy") && g.Tiles[1].Buildings==0,"land only remains possible");
        }
        static void LandmarkGate()
        {
            var g=New();g.CurrentPlayer.Laps=2;g.TestLand(0,1);g.Execute("purchase:4");g.TestLand(0,1);
            Assert(!g.Execute("build:4"),"hotel alone insufficient for landmark");
            g.Execute("construct:3");Assert(!g.Execute("build:4"),"all buildings must precede a later landing");
            g.TestLand(0,1);Assert(g.Execute("build:4") && g.Tiles[1].Landmark && g.Tiles[1].Buildings==7,"landmark after all three");
            g.TestLand(0,1);Assert(!g.Execute("build:4") && !g.Execute("construct:1"),"landmark and components not duplicated");
        }
        static void RemoteGate()
        {
            var g=New();g.Tiles[1].OwnerIndex=0;g.TestLand(0,0);Assert(!g.Execute("remote:1:2") && !g.Execute("remote:1:8"),"remote respects lap and landmark prerequisites");
            g.CurrentPlayer.Laps=2;g.TestLand(0,0);Assert(g.Execute("remote:1:4") && g.Tiles[1].Buildings==4,"remote independent hotel");
            g.TestLand(0,0);Assert(!g.Execute("remote:1:4") && !g.Execute("remote:1:7"),"remote rejects duplicate masks");
        }
        static void Accounting()
        {
            var g=New();g.CurrentPlayer.Laps=2;g.TestLand(0,1);long projected=g.ProjectedConstructionToll(1,4);g.Execute("purchase:4");
            var tile=g.Tiles[1];long invested=tile.Definition.Price+tile.Definition.BuildCosts[2];
            Assert(g.GetLiquidationValue(1)==invested/2 && g.GetTakeoverPrice(1)==invested*2 && g.GetToll(1)==projected,"accounting uses actual buildings");
            g.TestLand(1,1);Assert(g.Execute("takeover") && tile.OwnerIndex==1 && tile.Buildings==4,"takeover retains exact buildings");
        }
        static void Replay()
        {
            var g=New();for(int i=0;i<250 && g.Phase!=GamePhase.Finished;i++)Assert(g.Execute(g.ChooseAiAction()),"AI action valid");
            var copy=Game.Replay(7,0,4,g.ReplayActions);
            Assert(copy.Tiles.Select(t=>t.OwnerIndex+":"+t.Buildings+":"+t.Landmark).SequenceEqual(g.Tiles.Select(t=>t.OwnerIndex+":"+t.Buildings+":"+t.Landmark)),"independent building states replay");
            Assert(copy.Players.Select(p=>p.Cash).SequenceEqual(g.Players.Select(p=>p.Cash)),"replay payments");
        }
        static void LandmarkQuote()
        {
            var g=New();g.CurrentPlayer.Laps=2;g.Tiles[3].OwnerIndex=0;g.Tiles[3].Buildings=1;g.TestLand(0,1);
            long projected=g.ProjectedConstructionToll(1,7);g.Execute("purchase:7");Assert(g.GetToll(1)==projected,"bundle quote includes completed color set");
            g.TestLand(0,1);projected=g.ProjectedLandmarkToll(1);g.Execute("build:4");Assert(g.GetToll(1)==projected,"landmark quote includes color multiplier");
        }
        static void Resorts()
        {var g=New();g.TestLand(0,5);Assert(g.Execute("buy") && g.AvailableBuildings(5,0)==0 && !g.Execute("construct:1") && !g.Execute("build:1"),"resorts are land only");}
    }
}
