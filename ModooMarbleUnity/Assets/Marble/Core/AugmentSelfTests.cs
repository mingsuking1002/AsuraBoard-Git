using System;
using System.Linq;
namespace Marble.Core
{
    public static class AugmentSelfTests
    {
        static void Assert(bool value,string label){if(!value)throw new InvalidOperationException("Augment test: "+label);}
        static Game New(int[] rounds=null)=>new Game(new GameConfig{Seed=7,HumanCount=0,PlayerCount=3,MaxTurns=80,EnableAugments=true,AugmentRounds=rounds??new[]{1}});
        static void CompleteDraft(Game g)
        {int budget=32;while(g.Phase==GamePhase.Augment && budget-->0)Assert(g.Execute(g.ChooseAugmentAction(g.AugmentActor)),"complete simultaneous draft");Assert(budget>0,"draft ends");}
        static void MoveOnly(Game g)
        {
            string action=g.Phase==GamePhase.Augment?g.ChooseAiAction():g.GetActions().Any(a=>a.Id=="roll")?"roll":g.GetActions().Any(a=>a.Id=="end")?"end":g.GetActions().Any(a=>a.Id=="travel_cancel")?"travel_cancel":g.ChooseAiAction();
            Assert(g.Execute(action),"movement without purchases");
        }
        public static int Run()
        {
            Action[] cases={EmptyContent,GateAndBounds,NoEffectsOrRngChange,EveryPlayerChooses,Replay,Schedule,TravelResume,SkippedBankrupt,ConfigIsolation,TiersAndReplay,DefaultFourAndEight,SpecialRewardsAndCap,SimultaneousOrder,RerollOncePerPlayerAndStage,RerollReplayAndRng,CardRerollsAreIndependent,GoldenRerollOnceAndCap,GoldenOrderIndependent};
            foreach(var test in cases)test();return cases.Length;
        }
        static void EmptyContent()
        {Assert(Game.BlankAugmentOptions.Length==3 && Game.BlankAugmentOptions.All(o=>o.Name=="" && o.Description=="" && o.Icon==""),"no content invented");}
        static void GateAndBounds()
        {
            var g=New();Assert(g.Phase==GamePhase.Augment && g.Players.All(p=>g.GetActionsForPlayer(p.Index).Count==6),"all seats can choose immediately");
            foreach(var illegal in new[]{"roll","end","augment:0:-1:0","augment:0:0:3","augment:1:0:0","augment:0:0:0:extra","augment:00:0:1"})Assert(!g.Execute(illegal),"invalid draft command");
            Assert(g.ReplayActions.Count==0,"invalid commands no mutation");
            Assert(g.Execute("augment:0:2:2") && g.Players[2].Augments.Single().OptionIndex==2 && g.Phase==GamePhase.Augment,"last seat may choose first");
            Assert(!g.Execute("augment:0:2:1") && !g.Execute("roll"),"one selection only and wait for everyone");
            CompleteDraft(g);Assert(g.Phase==GamePhase.AwaitRoll && g.CurrentPlayerIndex==0,"all ready resumes original turn");
        }
        static void NoEffectsOrRngChange()
        {
            var g=New();var plain=new Game(new GameConfig{Seed=7,HumanCount=0,PlayerCount=3,MaxTurns=80});CompleteDraft(g);
            Assert(g.Players.All(p=>p.Cash==2000 && p.Position==0 && p.EscapeCards==0 && p.ShieldCards==0),"no stat effect");
            g.Execute("roll");plain.Execute("roll");Assert(g.DiceA==plain.DiceA && g.DiceB==plain.DiceB && g.CurrentPlayer.Position==plain.CurrentPlayer.Position,"RNG unchanged");
        }
        static void EveryPlayerChooses()
        {var g=New();int budget=3000;while(g.Phase!=GamePhase.Finished && budget-->0)Assert(g.Execute(g.ChooseAiAction()),"AI legal action");Assert(budget>0 && g.Players.All(p=>p.Augments.Count==1),"match finishes");}
        static void Replay()
        {
            var g=New();int budget=160;while(g.Phase!=GamePhase.Finished && budget-->0)Assert(g.Execute(g.ChooseAiAction()),"play");
            var replay=new Game(g.Config);foreach(var action in g.ReplayActions)Assert(replay.Execute(action),"replay valid");
            Assert(replay.Phase==g.Phase && replay.CurrentPlayerIndex==g.CurrentPlayerIndex && replay.AugmentCompletedMask==g.AugmentCompletedMask,"phase and completed stages restore");
            Assert(replay.Players.SelectMany(p=>p.Augments.Select(a=>p.Index+":"+a.DraftIndex+":"+a.OptionIndex+":"+a.Tier)).SequenceEqual(g.Players.SelectMany(p=>p.Augments.Select(a=>p.Index+":"+a.DraftIndex+":"+a.OptionIndex+":"+a.Tier))),"choices restore");
        }
        static void Schedule()
        {var g=New(new[]{1,2});int budget=3000;while(g.Phase!=GamePhase.Finished && budget-->0)Assert(g.Execute(g.ChooseAiAction()),"scheduled game");Assert(budget>0 && g.Players.All(p=>p.Augments.Count==2),"scheduled stages once");bool bad=false;try{New(new[]{2,1});}catch(ArgumentException){bad=true;}Assert(bad,"unordered schedule rejected");}
        static void TravelResume()
        {
            var g=New(new[]{2});g.Players[0].PendingTravel=true;int budget=300;
            while(g.Phase!=GamePhase.Augment && budget-->0)MoveOnly(g);
            Assert(budget>0,"second stage reached");var resume=g.AugmentResumePhase;int seat=g.CurrentPlayerIndex;CompleteDraft(g);
            Assert(g.Phase==resume && g.CurrentPlayerIndex==seat,"resume exact turn and phase");
        }
        static void SkippedBankrupt()
        {var g=New();g.Players[1].Bankrupt=true;CompleteDraft(g);Assert(g.Phase!=GamePhase.Augment && g.Players[1].Augments.Count==0,"bankrupt does not block barrier");}
        static void ConfigIsolation()
        {var config=new GameConfig{EnableAugments=true,AugmentRounds=new[]{1,3}};var g=new Game(config);config.AugmentRounds[0]=2;Assert(g.Config.AugmentRounds[0]==1,"copied schedule");Assert(new Game(new GameConfig{EnableAugments=false}).Phase==GamePhase.AwaitRoll,"legacy config");}
        static void TiersAndReplay()
        {
            var seen=new System.Collections.Generic.HashSet<AugmentTier>();
            for(int seed=0;seed<128;seed++)
            {var g=new Game(new GameConfig{Seed=seed,EnableAugments=true});var tier=g.GetAugmentTier(0);seen.Add(tier);Assert(g.Execute("augment:0:0:2") && g.Players[0].Augments.Single().Tier==tier,"tier stored");var restored=new Game(g.Config);foreach(var action in g.ReplayActions)Assert(restored.Execute(action),"partial draft replay");Assert(restored.Players[0].Augments.Single().Tier==tier && restored.HasPendingAugment(1),"pending players and tier restore");}
            Assert(seen.Count==4,"all tiers available");
        }
        static void DefaultFourAndEight()
        {
            Assert(new GameConfig().AugmentRounds.SequenceEqual(new[]{1,6,11,16}),"default four");
            foreach(int count in new[]{4,8})
            {
                var g=new Game(new GameConfig{Seed=7,EnableAugments=true,HumanCount=0,PlayerCount=2,MaxTurns=100,StartingCash=1000000,AugmentRounds=Game.CreateAugmentSchedule(count)});
                int budget=3000;while(g.Phase!=GamePhase.Finished && budget-->0)MoveOnly(g);
                Assert(budget>0 && g.Players.All(p=>p.Augments.Count==count),"configured count reached");
            }
            bool invalid=false;try{Game.CreateAugmentSchedule(9);}catch(ArgumentOutOfRangeException){invalid=true;}Assert(invalid,"hard eight schedule limit");
        }
        static void SpecialRewardsAndCap()
        {
            var g=New();CompleteDraft(g);
            for(int i=1;i<8;i++){Assert(g.RequestSpecialAugment(),"special stage starts");CompleteDraft(g);}
            Assert(g.Players.All(p=>p.Augments.Count==8) && !g.RequestSpecialAugment(),"eight cap across ordinary and special");
            var copy=new Game(g.Config);foreach(var a in g.ReplayActions)Assert(copy.Execute(a),"special actions replay");Assert(copy.Players.All(p=>p.Augments.Count==8),"special rewards restored");
        }
        static void RerollOncePerPlayerAndStage()
        {
            var g=New();string stale=g.AugmentAction(2,1);
            Assert(g.Execute(g.AugmentRerollAction(2)),"each seat can reroll out of turn");
            Assert(!g.CanRerollAugment(2,0) && g.CanRerollAugment(0) && g.CanRerollAugment(1),"personal reroll budget");
            int count=g.ReplayActions.Count;
            Assert(!g.Execute(g.AugmentRerollAction(2)) && !g.Execute(stale),"second reroll and stale card rejected");
            Assert(g.ReplayActions.Count==count && g.Players[2].Augments.Count==0,"invalid requests do not select");
            Assert(g.Execute(g.AugmentAction(2,0)) && g.Players[2].Augments.Single().Rerolled,"select from refreshed offer");
            Assert(!g.Execute(g.AugmentRerollAction(2)),"cannot reroll after confirmation");CompleteDraft(g);
            Assert(g.RequestSpecialAugment() && g.Players.All(p=>g.CanRerollAugment(p.Index)),"new special draft renews one reroll each");
            Assert(g.Execute(g.AugmentRerollAction(2)),"new stage reroll usable");
        }
        static void RerollReplayAndRng()
        {
            var g=New();var plain=New();var tier=g.GetAugmentTier(0);
            g.Execute(g.AugmentRerollAction(0));g.Execute(g.AugmentRerollAction(2));
            Assert(g.GetAugmentTier(0)==tier,"reroll keeps stage tier");
            var partial=new Game(g.Config);foreach(string action in g.ReplayActions)Assert(partial.Execute(action),"replay reroll");
            Assert(!partial.CanRerollAugment(0,0) && partial.CanRerollAugment(1) && !partial.CanRerollAugment(2,0),"save/load cannot restore spent reroll");
            CompleteDraft(g);CompleteDraft(plain);g.Execute("roll");plain.Execute("roll");
            Assert(g.DiceA==plain.DiceA && g.DiceB==plain.DiceB && g.Players.Select(p=>p.Cash).SequenceEqual(plain.Players.Select(p=>p.Cash)),"reroll does not consume dice RNG or money");
        }
        static void CardRerollsAreIndependent()
        {
            var g=New();Assert(g.GetActionsForPlayer(0).Count==6,"three choices and three rerolls");
            Assert(g.Execute(g.AugmentRerollAction(0,1)),"middle card rerolls");
            Assert(g.CanRerollAugment(0,0) && !g.CanRerollAugment(0,1) && g.CanRerollAugment(0,2),"other cards retain rerolls");
            Assert(g.Execute(g.AugmentRerollAction(0,0)) && g.Execute(g.AugmentRerollAction(0,2)) && !g.CanRerollAugment(0),"three independent budgets");
            Assert(!g.Execute(g.AugmentRerollAction(0,3)),"fourth card invalid");
            var copy=new Game(g.Config);foreach(var a in g.ReplayActions)Assert(copy.Execute(a),"all card masks replay");Assert(copy.AugmentRerollMask(0)==7,"mask preserved");
        }
        static void GoldenRerollOnceAndCap()
        {
            for(int tier=0;tier<4;tier++)
            {
                Game g=null;for(int seed=0;seed<20000;seed++){var candidate=new Game(new GameConfig{Seed=seed,EnableAugments=true});if(candidate.GoldenRerollAvailable(0) && (int)candidate.GetAugmentTier(0)==tier){g=candidate;break;}}
                Assert(g!=null,"golden tier seed available");Assert(g.Execute(g.AugmentRerollAction(0,2)),"golden used on arbitrary card");
                Assert(g.GoldenRerollSlot(0)==2 && !g.GoldenRerollAvailable(0) && g.CanRerollAugment(0,0),"one golden per phase with ordinary budgets remaining");
                Assert((int)g.GetAugmentOfferTier(0,2)==Math.Min(3,tier+1) && (int)g.GetAugmentOfferTier(0,1)==tier,"only chosen card upgraded and prism capped");
                Assert(g.Execute(g.AugmentRerollAction(0,0)) && (int)g.GetAugmentOfferTier(0,0)==tier,"later reroll ordinary");
                Assert(g.Execute(g.AugmentAction(0,2)) && g.Players[0].Augments.Single().GoldenRerolled,"golden selection metadata");
                var copy=new Game(g.Config);foreach(var a in g.ReplayActions)Assert(copy.Execute(a),"golden replay");Assert(copy.Players[0].Augments.Single().Tier==g.Players[0].Augments.Single().Tier,"tier survives load");
            }
        }
        static void GoldenOrderIndependent()
        {
            var a=New();var b=New();a.Execute(a.AugmentRerollAction(0,1));a.Execute(a.AugmentRerollAction(2,0));b.Execute(b.AugmentRerollAction(2,0));b.Execute(b.AugmentRerollAction(0,1));
            Assert(a.GoldenRerollSlot(0)==b.GoldenRerollSlot(0) && a.GetAugmentOfferTier(0,1)==b.GetAugmentOfferTier(0,1),"golden chance independent of network arrival order");
            Assert(a.Execute(a.AugmentAction(0,2)) && !a.Players[0].Augments.Single().Rerolled,"untouched card does not inherit another card's reroll");
        }
        static void SimultaneousOrder()
        {var a=New();var b=New();foreach(int seat in new[]{2,0,1})a.Execute(a.AugmentAction(seat,seat%3));foreach(int seat in new[]{0,1,2})b.Execute(b.AugmentAction(seat,seat%3));Assert(a.Players.SelectMany(p=>p.Augments.Select(s=>s.OptionIndex)).SequenceEqual(b.Players.SelectMany(p=>p.Augments.Select(s=>s.OptionIndex))) && a.CurrentPlayerIndex==b.CurrentPlayerIndex && a.Phase==b.Phase,"arrival order has no gameplay effect");}
    }
}
