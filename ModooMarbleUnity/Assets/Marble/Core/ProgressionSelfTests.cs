using System;
using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public static class ProgressionSelfTests
    {
        static void Assert(bool value,string label){if(!value)throw new InvalidOperationException("Progression test: "+label);}
        public static int Run()
        {
            Action[] tests={DiceBounds,DiceParity,DiceGaugeWeights,DiceReplay,CosmeticGates,AchievementConditions,GoldenAchievement,ControlAchievement,OfflineQueue,StoreAcknowledgement,RemoteImport,MissingSchema};
            foreach(var test in tests)test();return tests.Length;
        }
        static void DiceBounds()
        {
            var g=new Game(new GameConfig());foreach(string id in new[]{"roll_control:-1:0","roll_control:11:0","roll_control:5:3","roll_control:05:0","roll_control:5:0:extra"})Assert(!g.Execute(id),"invalid controls rejected");
            Assert(g.ReplayActions.Count==0,"invalid roll does not consume randomness");
            var draft=new Game(new GameConfig{EnableAugments=true});Assert(!draft.Execute(Game.DiceControlAction(5,0)),"cannot roll during draft");
        }
        static void DiceParity()
        {
            for(int seed=0;seed<180;seed++)for(int parity=1;parity<=2;parity++)
            {var g=new Game(new GameConfig{Seed=seed});Assert(g.Execute(Game.DiceControlAction(seed%11,parity)),"controlled roll legal");Assert(g.LastDiceTarget%2==(parity==1?1:0) && (g.DiceA+g.DiceB)%2==(parity==1?1:0) && g.LastDiceActor==0,"parity guaranteed");}
        }
        static void DiceGaugeWeights()
        {
            double Mean(int power){double total=0,weighted=0;for(int a=1;a<=6;a++)for(int b=1;b<=6;b++){int w=Game.DiceControlWeight(a,b,power,0);Assert(w>0,"every ordinary sum remains possible");total+=w;weighted+=(a+b)*w;}return weighted/total;}
            Assert(Mean(0)<Mean(5) && Mean(5)<Mean(10),"higher gauge raises expected sum");
        }
        static void DiceReplay()
        {
            var g=new Game(new GameConfig{Seed=72,HumanCount=0});g.Execute(Game.DiceControlAction(8,2));int budget=45;while(g.Phase!=GamePhase.Finished && budget-->0)g.Execute(g.ChooseAiAction());
            var copy=new Game(g.Config);foreach(string a in g.ReplayActions)Assert(copy.Execute(a),"controlled replay");Assert(copy.DiceA==g.DiceA && copy.DiceB==g.DiceB && copy.LastDiceTarget==g.LastDiceTarget && copy.LastDiceControlled==g.LastDiceControlled,"control metadata restored");
        }
        static void CosmeticGates()
        {
            var p=new AchievementProgress();Assert(p.CanEquipAvatar(0) && p.CanEquipAvatar(3) && !p.CanEquipAvatar(4) && !p.CanEquipDice(1),"base available reward locked");
            Assert(p.Grant("WC_FIRST_WIN") && p.CanEquipAvatar(4) && !p.Grant("WC_FIRST_WIN") && !p.Grant("unknown"),"known achievement grants once");
            Assert(!p.CanEquipAvatar(6) && !p.CanEquipDice(-1),"cosmetic bounds");
        }
        static void AchievementConditions()
        {
            var p=new AchievementProgress();var g=new Game(new GameConfig{EndOnFirstBankruptcy=true});g.Players[0].Laps=3;g.Tiles[1].OwnerIndex=0;g.Tiles[1].Buildings=1;
            Assert(p.Observe(g,0) && p.Has("WC_FIRST_BUILD") && p.Has("WC_THREE_LAPS") && !p.Has("WC_FIRST_MATCH"),"live conditions");
            g.Tiles[1].OwnerIndex=1;g.Players[0].Cash=0;g.TestLand(0,1);g.Execute("bankrupt");p.Observe(g,0);Assert(p.Has("WC_FIRST_MATCH") && !p.Has("WC_FIRST_WIN"),"results and loss not win");
        }
        static void GoldenAchievement()
        {
            for(int seed=0;seed<300;seed++){var g=new Game(new GameConfig{Seed=seed,EnableAugments=true});if(!g.GoldenRerollAvailable(0))continue;g.Execute(g.AugmentRerollAction(0,1));var p=new AchievementProgress();Assert(p.Observe(g,0) && p.Has("WC_GOLDEN_REROLL"),"golden use unlocks before choosing");return;}Assert(false,"golden seed exists");
        }
        static void ControlAchievement()
        {
            for(int seed=0;seed<300;seed++)
            {
                var g=new Game(new GameConfig{Seed=seed});g.Execute(Game.DiceControlAction(5,0));if(g.DiceA+g.DiceB!=7)continue;
                var own=new AchievementProgress();var other=new AchievementProgress();own.Observe(g,0);other.Observe(g,1);
                Assert(own.Has("WC_DICE_TARGET") && !other.Has("WC_DICE_TARGET"),"target reward belongs to roll owner");return;
            }
            Assert(false,"target hit sample exists");
        }
        sealed class Backend : IAchievementBackend
        {
            public bool Available{get;set;}=true;public bool StoreSucceeds=true,HasSchema=true;public int Sets,Stores;public HashSet<string> Remote=new HashSet<string>();
            public bool TryRead(string id,out bool unlocked){unlocked=Remote.Contains(id);return HasSchema;}
            public bool TryUnlock(string id){Sets++;Remote.Add(id);return true;}
            public bool TryStore(){Stores++;return StoreSucceeds;}
        }
        static void OfflineQueue()
        {var p=new AchievementProgress();p.Grant("WC_FIRST_WIN");var b=new Backend{Available=false};var sync=new AchievementSync(p,b);sync.Tick();Assert(sync.PendingCount==1 && b.Sets==0,"offline rewards queue");b.Available=true;sync.Tick();Assert(sync.Waiting,"retry on reconnect");}
        static void StoreAcknowledgement()
        {
            var p=new AchievementProgress();p.Grant("WC_FIRST_WIN");var b=new Backend{StoreSucceeds=false};var sync=new AchievementSync(p,b);sync.Tick();Assert(!sync.Waiting && sync.PendingCount==1,"failed request pending");
            b.StoreSucceeds=true;sync.Tick();Assert(sync.Waiting && sync.PendingCount==1,"request accepted is not durable acknowledgement");sync.ConfirmStored(false);sync.Tick();Assert(sync.Waiting,"failed callback retries");sync.ConfirmStored(true);Assert(sync.PendingCount==0 && p.CanEquipAvatar(4),"successful callback confirms without duplicate reward");
        }
        static void RemoteImport()
        {var p=new AchievementProgress();var b=new Backend();b.Remote.Add("WC_FIRST_WIN");var sync=new AchievementSync(p,b);Assert(sync.Tick() && p.CanEquipAvatar(4) && sync.PendingCount==0 && b.Sets==0,"remote achievement restores cosmetic");}
        static void MissingSchema()
        {var p=new AchievementProgress();p.Grant("WC_FIRST_WIN");var b=new Backend{HasSchema=false};var sync=new AchievementSync(p,b);sync.Tick();Assert(sync.PendingCount==1 && b.Stores==0,"unregistered Steam IDs remain queued");}
    }
}
