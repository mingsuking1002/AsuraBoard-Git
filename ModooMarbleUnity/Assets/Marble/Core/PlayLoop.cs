using System;
using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public enum PlayScreen { Main, Customize, Options, CreateRoom, FindRoom, Lobby, Game, Results }
    public sealed class PlayLoop
    {
        public PlayScreen Screen{get;private set;}=PlayScreen.Main;
        public bool InRoom{get;private set;}
        public void Navigate(PlayScreen screen)
        {
            if(screen==PlayScreen.Lobby || screen==PlayScreen.Game || screen==PlayScreen.Results)throw new InvalidOperationException("Use room/game transitions");
            if(InRoom)throw new InvalidOperationException("Leave the room before main-menu navigation");
            Screen=screen;
        }
        public void EnterRoom(){InRoom=true;Screen=PlayScreen.Lobby;}
        public void StartMatch(){if(!InRoom || Screen!=PlayScreen.Lobby)throw new InvalidOperationException("A match starts from its lobby");Screen=PlayScreen.Game;}
        public void EndMatch(){if(!InRoom || Screen!=PlayScreen.Game)throw new InvalidOperationException("No active match");Screen=PlayScreen.Results;}
        public void ReturnToLobby(){if(!InRoom)throw new InvalidOperationException("Room required");Screen=PlayScreen.Lobby;}
        public void LeaveRoom(){InRoom=false;Screen=PlayScreen.Main;}
    }
    public static class BoardCatalog
    {
        public const int Count=2;
        public static string Name(int id)=>id==0?"세계 한 바퀴":"해안 한 바퀴";
        public static TileDefinition[] Create(int id)
        {
            if(id<0 || id>=Count)throw new ArgumentOutOfRangeException(nameof(id));
            var board=DefaultBoard.Create();if(id==0)return board;
            string[] names={"부산","후쿠오카","오키나와","세부","다낭","나트랑","싱가포르","코타키나발루","퍼스","브리즈번","오클랜드","산토리니","나폴리","니스","리스본","바르셀로나","마이애미","밴쿠버"};
            int n=0;foreach(var city in board.Where(t=>t.Kind==TileKind.City))
            {city.Name=names[n++];city.Price=city.Price*4/5;for(int i=0;i<4;i++)city.BuildCosts[i]=city.BuildCosts[i]*4/5;}
            string[] resorts={"제주","보라카이","몰디브","랑카위","사이판"};n=0;
            foreach(var resort in board.Where(t=>t.Kind==TileKind.Resort)){resort.Name=resorts[n++];for(int i=0;i<5;i++)resort.BaseTolls[i]=resort.BaseTolls[i]*6/5;}
            return board;
        }
    }
    public sealed class MatchStanding
    {public int PlayerIndex,Place;public long NetWorth;public bool Bankrupt;}
    public sealed partial class Game
    {
        public IReadOnlyList<MatchStanding> GetStandings()
        {
            return Players.OrderBy(p=>p.Index==WinnerIndex?0:1).ThenBy(p=>p.Bankrupt?1:0).ThenByDescending(p=>GetNetWorth(p.Index)).ThenBy(p=>p.Index)
                .Select((p,i)=>new MatchStanding{PlayerIndex=p.Index,Place=i+1,NetWorth=GetNetWorth(p.Index),Bankrupt=p.Bankrupt}).ToArray();
        }
    }
}
