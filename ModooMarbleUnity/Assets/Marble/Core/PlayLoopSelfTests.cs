using System;
using System.Linq;
namespace Marble.Core
{
    public static class PlayLoopSelfTests
    {
        static void Assert(bool value,string label){if(!value)throw new InvalidOperationException("Play loop test: "+label);}
        public static int Run()
        {
            Action[] tests={MenuToLobbyToResult,RequireLobby,FirstBankruptcyStandings,MapsAreRealAndIsolated,MapReplay};
            foreach(var test in tests)test();return tests.Length;
        }
        static void MenuToLobbyToResult()
        {
            var flow=new PlayLoop();Assert(flow.Screen==PlayScreen.Main && !flow.InRoom,"main first");
            flow.Navigate(PlayScreen.Customize);flow.Navigate(PlayScreen.Main);flow.Navigate(PlayScreen.Options);flow.Navigate(PlayScreen.CreateRoom);flow.EnterRoom();
            Assert(flow.Screen==PlayScreen.Lobby,"creating room does not start game");flow.StartMatch();flow.EndMatch();Assert(flow.Screen==PlayScreen.Results,"result page");
            flow.ReturnToLobby();flow.StartMatch();flow.ReturnToLobby();Assert(flow.InRoom && flow.Screen==PlayScreen.Lobby,"mid-game exit keeps lobby");flow.LeaveRoom();Assert(flow.Screen==PlayScreen.Main && !flow.InRoom,"leave room returns main");
        }
        static void RequireLobby()
        {
            var flow=new PlayLoop();bool rejected=false;try{flow.StartMatch();}catch(InvalidOperationException){rejected=true;}Assert(rejected,"cannot start directly");
            flow.EnterRoom();rejected=false;try{flow.Navigate(PlayScreen.Main);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"leave required before menu");
        }
        static void FirstBankruptcyStandings()
        {
            var game=new Game(new GameConfig{PlayerCount=4,HumanCount=0,EndOnFirstBankruptcy=true});game.Tiles[1].OwnerIndex=1;game.Players[0].Cash=0;game.TestLand(0,1);
            Assert(game.Execute("bankrupt") && game.Phase==GamePhase.Finished,"first bankruptcy ends match");
            var result=game.GetStandings();Assert(result.Count==4 && result.Select(r=>r.Place).SequenceEqual(new[]{1,2,3,4}),"four placements");Assert(result.Last().PlayerIndex==0 && result.Last().Bankrupt && result[0].PlayerIndex==game.WinnerIndex,"bankrupt last, winner first");
        }
        static void MapsAreRealAndIsolated()
        {
            var world=BoardCatalog.Create(0);var coast=BoardCatalog.Create(1);
            Assert(world.Length==32 && coast.Length==32 && world[1].Name!=coast[1].Name && world[1].Price!=coast[1].Price && coast[5].BaseTolls[0]>world[5].BaseTolls[0],"map affects names and economics");
            coast[1].Name="changed";Assert(BoardCatalog.Create(0)[1].Name=="방콕" && BoardCatalog.Create(1)[1].Name=="부산","map instances isolated");
            bool invalid=false;try{new Game(new GameConfig{MapId=2});}catch(ArgumentOutOfRangeException){invalid=true;}Assert(invalid,"invalid map rejected");
        }
        static void MapReplay()
        {
            var game=new Game(new GameConfig{MapId=1,EndOnFirstBankruptcy=true,HumanCount=0});int budget=150;
            while(game.Phase!=GamePhase.Finished && budget-->0)Assert(game.Execute(game.ChooseAiAction()),"map legal actions");
            var restored=new Game(game.Config);foreach(var action in game.ReplayActions)Assert(restored.Execute(action),"map replay valid");
            Assert(restored.GetStandings().Select(r=>r.NetWorth).SequenceEqual(game.GetStandings().Select(r=>r.NetWorth)),"map accounting restored");
        }
    }
}
