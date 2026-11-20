using System;
using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public sealed class AchievementDefinition
    {
        public readonly string Id,Name,Description,Reward;
        public AchievementDefinition(string id,string name,string description,string reward){Id=id;Name=name;Description=description;Reward=reward;}
    }
    public static class CosmeticCatalog
    {
        public const int AvatarCount=6,DiceSkinCount=5;
        public static readonly string[] AvatarNames={"동그리 탐험가","모자 탐험가","수신기 탐험가","깃발 탐험가","왕관 탐험가","프리즘 탐험가"};
        public static readonly string[] DiceNames={"종이 주사위","구릿빛 주사위","바다 주사위","황금 주사위","프리즘 주사위"};
        public static readonly AchievementDefinition[] Achievements={
            new AchievementDefinition("WC_FIRST_MATCH","여행의 시작","경기 결과에 처음 도달하기","구릿빛 주사위"),
            new AchievementDefinition("WC_FIRST_BUILD","도시 설계자","내 도시에 첫 건물 짓기","바다 주사위"),
            new AchievementDefinition("WC_FIRST_WIN","첫 번째 우승","한 경기에서 1위 달성하기","왕관 탐험가"),
            new AchievementDefinition("WC_THREE_LAPS","세계 여행자","한 경기에서 출발지 세 번 통과","황금 주사위"),
            new AchievementDefinition("WC_GOLDEN_REROLL","황금빛 행운","황금 리롤 한 번 사용하기","프리즘 탐험가"),
            new AchievementDefinition("WC_DICE_TARGET","노린 한 수","주사위 컨트롤 목표 합계 맞히기","프리즘 주사위")};
        public static string AvatarRequirement(int id)=>id==4?"WC_FIRST_WIN":id==5?"WC_GOLDEN_REROLL":null;
        public static string DiceRequirement(int id)=>id==1?"WC_FIRST_MATCH":id==2?"WC_FIRST_BUILD":id==3?"WC_THREE_LAPS":id==4?"WC_DICE_TARGET":null;
        public static AchievementDefinition Find(string id)=>Achievements.FirstOrDefault(a=>a.Id==id);
    }
    [Serializable] public sealed class AchievementProgress
    {
        public string[] Unlocked=new string[0];
        public bool Has(string id)=>id!=null && Unlocked!=null && Array.IndexOf(Unlocked,id)>=0;
        public bool Grant(string id)
        {if(CosmeticCatalog.Find(id)==null || Has(id))return false;Unlocked=(Unlocked??new string[0]).Concat(new[]{id}).Distinct().ToArray();return true;}
        public bool CanEquipAvatar(int id)=>id>=0 && id<CosmeticCatalog.AvatarCount && (CosmeticCatalog.AvatarRequirement(id)==null || Has(CosmeticCatalog.AvatarRequirement(id)));
        public bool CanEquipDice(int id)=>id>=0 && id<CosmeticCatalog.DiceSkinCount && (CosmeticCatalog.DiceRequirement(id)==null || Has(CosmeticCatalog.DiceRequirement(id)));
        public bool Observe(Game game,int seat)
        {
            if(game==null || seat<0 || seat>=game.Players.Count)return false;bool changed=false;var player=game.Players[seat];
            if(game.Phase==GamePhase.Finished){changed|=Grant("WC_FIRST_MATCH");if(game.WinnerIndex==seat)changed|=Grant("WC_FIRST_WIN");}
            if(game.Tiles.Any(t=>t.OwnerIndex==seat && t.Level>0))changed|=Grant("WC_FIRST_BUILD");
            if(player.Laps>=3)changed|=Grant("WC_THREE_LAPS");
            if(game.GoldenRerollSlot(seat)>=0 || player.Augments.Any(a=>a.GoldenRerolled))changed|=Grant("WC_GOLDEN_REROLL");
            if(game.LastDiceControlled && game.LastDiceActor==seat && game.DiceA+game.DiceB==game.LastDiceTarget)changed|=Grant("WC_DICE_TARGET");
            return changed;
        }
    }
    public interface IAchievementBackend
    {
        bool Available{get;}
        bool TryRead(string id,out bool unlocked);
        bool TryUnlock(string id);
        bool TryStore();
    }
    // A StoreStats request is not an acknowledgement. Only the callback confirms delivery.
    public sealed class AchievementSync
    {
        readonly AchievementProgress progress;readonly IAchievementBackend backend;
        readonly HashSet<string> confirmed=new HashSet<string>(),staged=new HashSet<string>();
        public bool Waiting{get;private set;}
        public int PendingCount=>CosmeticCatalog.Achievements.Count(a=>progress.Has(a.Id) && !confirmed.Contains(a.Id));
        public AchievementSync(AchievementProgress progress,IAchievementBackend backend){this.progress=progress;this.backend=backend;}
        public bool Tick()
        {
            if(!backend.Available || Waiting)return false;bool changed=false;
            foreach(var definition in CosmeticCatalog.Achievements)
            {
                string id=definition.Id;if(confirmed.Contains(id) || staged.Contains(id))continue;
                if(!backend.TryRead(id,out bool unlocked))continue;
                if(unlocked){changed|=progress.Grant(id);confirmed.Add(id);}
                else if(progress.Has(id) && backend.TryUnlock(id))staged.Add(id);
            }
            if(staged.Count>0)Waiting=backend.TryStore();return changed;
        }
        public void ConfirmStored(bool success)
        {if(!Waiting)return;if(success)foreach(string id in staged)confirmed.Add(id);if(success)staged.Clear();Waiting=false;}
        public void Retry(){Waiting=false;}
    }
}
