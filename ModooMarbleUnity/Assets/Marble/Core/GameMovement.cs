using System;
using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public readonly struct BoardMovementStep
    {
        public readonly int Seat,From,To;
        public readonly bool Transfer,Fall,Dice;
        public readonly string Reason;
        public BoardMovementStep(int seat,int from,int to,bool transfer=false,bool fall=false,bool dice=false,string reason=null)
        {Seat=seat;From=from;To=to;Transfer=transfer;Fall=fall;Dice=dice;Reason=reason;}
    }
    public sealed partial class Game
    {
        // Presentation trace of the accepted action. It does not consume RNG or change rules.
        readonly List<BoardMovementStep> actionMovement=new List<BoardMovementStep>();
        bool recordingDiceMovement;
        void MoveRolledSteps(int steps){recordingDiceMovement=true;try{MoveDiceSteps(steps);}finally{recordingDiceMovement=false;}}
        public IReadOnlyList<BoardMovementStep> ActionMovement=>actionMovement;
        public long ActionMovementRevision{get;private set;}
        void BeginActionMovement(){actionMovement.Clear();ActionMovementRevision++;}
        void RecordMovement(int from,int to,bool transfer=false,bool fall=false)
        {actionMovement.Add(new BoardMovementStep(CurrentPlayerIndex,from,to,transfer,fall,recordingDiceMovement,recordingDiceMovement?null:Message));}
    }
    public static class MovementSelfTests
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Movement: "+message);}
        public static int Run()
        {
            foreach(bool body in new[]{false,true})for(int start=0;start<32;start++)for(int a=1;a<=6;a++)for(int b=1;b<=6;b++)
            {
                var g=new Game(new GameConfig{Seed=start*36+a*6+b,PlayerCount=2,EnableBodyEconomy=body});
                g.Players[0].Position=start;g.TestRoll(a,b);
                Check(g.ActionMovement.Count>=a+b,"dice path exists");
                Check(g.ActionMovement.Count(step=>step.Dice)==a+b,"exact dice hop count including high rolls");
                for(int n=0;n<a+b;n++)
                {var step=g.ActionMovement[n];Check(step.Seat==0 && step.From==(start+n)%32 && step.To==(start+n+1)%32 && !step.Transfer && step.Dice,"one step per dice pip");}
                foreach(var group in g.ActionMovement.GroupBy(s=>s.Seat))Check(group.Last().To==g.Players[group.Key].Position,"trace ends at actual position");
            }
            int count=2;bool eventFound=false,returnedToStart=false;
            for(int seed=0;seed<100;seed++)
            {
                var g=new Game(new GameConfig{Seed=seed,PlayerCount=2});g.TestRoll(1,3);
                if(g.ActionMovement.Count<=4)continue;
                eventFound=true;Check(g.ActionMovement[3].To==4,"chance cell visited before event");
                Check(g.ActionMovement[4].From==4 && !g.ActionMovement[4].Dice,"event starts at chance cell");
                Check(!string.IsNullOrEmpty(g.ActionMovement[4].Reason),"event movement retains its cause");
                returnedToStart|=g.Players[0].Position==0;
            }
            Check(eventFound && returnedToStart,"event relocation and same-endpoint route covered");count++;
            foreach(bool body in new[]{false,true})for(int seed=0;seed<100;seed++)
            {
                var g=new Game(new GameConfig{Seed=seed,PlayerCount=2,EnableBodyEconomy=body});int start=seed%32;g.Players[0].Position=start;
                Check(g.Execute(Game.DiceControlAction(seed%11,0)),"controlled roll accepted");
                int sum=g.DiceA+g.DiceB;Check(g.ActionMovement.Count>=sum,"controlled dice path exists");
                for(int n=0;n<sum;n++)Check(g.ActionMovement[n].To==(start+n+1)%32 && g.ActionMovement[n].Dice,"controlled result matches path");
            }
            count++;
            var island=new Game(new GameConfig{PlayerCount=2});island.Players[0].Position=8;island.Players[0].IslandTurns=3;island.TestRoll(1,2);
            Check(island.ActionMovement.Count==0 && island.LastDiceActor==0 && island.CurrentPlayerIndex==1,"failed island roll does not move next player");count++;
            var bodyGame=new Game(new GameConfig{Seed=22,PlayerCount=2,EnableBodyEconomy=true});bodyGame.Players[0].SoldBodyMask=1023;bodyGame.TestRoll(6,6);
            Check(bodyGame.LastMovementPath.SequenceEqual(bodyGame.ActionMovement.Take(12).Select(s=>s.To)),"body movement trace is exact");count++;
            var reset=new Game(new GameConfig{Seed=7,PlayerCount=2});reset.TestRoll(1,2);Check(reset.ActionMovement.Count==3,"initial path");reset.Execute("end");
            Check(reset.ActionMovement.Count==0,"next command cannot replay previous movement");count++;
            return count;
        }
    }
}
