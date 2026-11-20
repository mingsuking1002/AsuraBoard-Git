using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public sealed partial class Game
    {
        public int ConstructionLimit(int player)=>System.Math.Min(3,Players[player].Laps+1);
        public bool ConstructionSpent=>_builtThisLanding;
        public int AvailableBuildings(int tile,int player)
        {
            var t=Tiles[tile];
            if(t.Definition.Kind!=TileKind.City || t.Landmark || (t.OwnerIndex>=0 && t.OwnerIndex!=player))return 0;
            return ((1<<ConstructionLimit(player))-1)&~t.Buildings;
        }
        static long BuildingPrice(TileState tile,int mask)
        {long sum=0;for(int i=0;i<3;i++)if((mask&(1<<i))!=0)sum+=tile.Definition.BuildCosts[i];return sum;}
        IEnumerable<int> LegalBuildingMasks(TileState tile,PlayerState player)
        {
            int available=AvailableBuildings(tile.Index,player.Index);
            for(int mask=1;mask<=7;mask++)if((mask&available)==mask)yield return mask;
        }
        public long ConstructionPrice(int tile,int mask)
        {
            var t=Tiles[tile];return (t.OwnerIndex<0?t.Definition.Price:0)+BuildingPrice(t,mask);
        }
        static long RawCityToll(TileState tile,int mask,bool landmark)
        {
            if(landmark)return tile.Definition.BaseTolls[4];
            long toll=tile.Definition.BaseTolls[0];
            for(int i=0;i<3;i++)if((mask&(1<<i))!=0)toll+=System.Math.Max(0,tile.Definition.BaseTolls[i+1]-tile.Definition.BaseTolls[i]);
            return toll;
        }
        public long ProjectedLandmarkToll(int tile)
        {
            var t=Tiles[tile];return t.Definition.BaseTolls[4]*(OwnsGroup(CurrentPlayerIndex,t.Definition.Group)?2:1)*t.FestivalMultiplier;
        }
        public long ProjectedConstructionToll(int tile,int mask)
        {
            var t=Tiles[tile];int owned=t.Buildings|mask;long toll=RawCityToll(t,owned,t.Landmark);
            if(t.Definition.Group>=0 && owned!=0 && Tiles.Where(x=>x.Definition.Kind==TileKind.City && x.Definition.Group==t.Definition.Group && x.Index!=tile).All(x=>x.OwnerIndex==CurrentPlayerIndex && x.Buildings!=0))toll*=2;
            return toll*t.FestivalMultiplier;
        }
        public static string BuildingNames(int mask)=>string.Join(" + ",Enumerable.Range(1,3).Where(i=>(mask&(1<<(i-1)))!=0).Select(BuildName));
    }
}
