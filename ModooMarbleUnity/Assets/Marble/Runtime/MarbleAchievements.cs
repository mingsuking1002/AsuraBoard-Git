using System;
using Marble.Core;
using Steamworks;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        AchievementProgress achievements;AchievementSync achievementSync;SteamAchievementBackend achievementBackend;
        string achievementScope="offline";float achievementTimer,achievementWaitAt;bool achievementSheet;
        int profileDiceSkin;
        string CosmeticKey(string suffix)=>"cosmetic."+achievementScope+"."+suffix;
        void LoadAchievements(string scope)
        {
            achievementBackend?.Dispose();achievementBackend=null;achievementSync=null;achievementScope=scope;
            try{achievements=JsonUtility.FromJson<AchievementProgress>(PlayerPrefs.GetString("achievements."+scope,"{}"))??new AchievementProgress();}catch{achievements=new AchievementProgress();}
            profileAvatar=PlayerPrefs.GetInt(CosmeticKey("avatar"),profileAvatar);profileDiceSkin=PlayerPrefs.GetInt(CosmeticKey("dice"),0);
            CoerceCosmetics();
        }
        void CoerceCosmetics()
        {if(!achievements.CanEquipAvatar(profileAvatar))profileAvatar=0;if(!achievements.CanEquipDice(profileDiceSkin))profileDiceSkin=0;}
        void PersistAchievements(){PlayerPrefs.SetString("achievements."+achievementScope,JsonUtility.ToJson(achievements));PlayerPrefs.Save();}
        void BindSteamAchievements()
        {
            if(steamRooms==null || !steamRooms.Available)return;
            string scope=steamRooms.LocalId.ToString();
            if(achievementScope!=scope){LoadAchievements(scope);if(!diagnosticMode)RefreshMenuModel();onlineSession.SetAppearance(profileAvatar,profileColor,profileDiceSkin,profileOutfit);}
            if(steamRooms.AppId==480 || achievementBackend!=null)return;
            achievementBackend=new SteamAchievementBackend(steamRooms.AppId,()=>steamRooms!=null && steamRooms.Available);
            achievementSync=new AchievementSync(achievements,achievementBackend);
            achievementBackend.Stored+=success=>achievementSync?.ConfirmStored(success);
            achievementTimer=0;
        }
        void TickAchievements(float delta)
        {
            if(achievements==null)return;
            BindSteamAchievements();
            bool active=diagnosticMode || wardrobeRuntimeChecking?false:flow.Screen==PlayScreen.Game || flow.Screen==PlayScreen.Results;
            int seat=onlineGameAttached?onlineSession.LocalSeat:0;
            if(active && achievements.Observe(Model,seat)){PersistAchievements();Toast("도전과제 달성 · 새로운 외형이 해금되었습니다!");}
            if(achievementSync==null)return;
            if(achievementSync.Waiting && Time.unscaledTime-achievementWaitAt>20)achievementSync.Retry();
            achievementTimer-=delta;if(achievementTimer>0)return;achievementTimer=5;
            bool wasWaiting=achievementSync.Waiting;
            if(achievementSync.Tick())PersistAchievements();
            if(!wasWaiting && achievementSync.Waiting)achievementWaitAt=Time.unscaledTime;
        }
        void DrawAchievements()
        {
            Shade();PaperTheme.Card(new Rect(300,112,840,687),PaperTheme.Paper,true,3);
            GUI.Label(new Rect(330,134,760,45),"도전과제 · 외형 해금",title);
            for(int i=0;i<CosmeticCatalog.Achievements.Length;i++)
            {
                var a=CosmeticCatalog.Achievements[i];bool unlocked=achievements.Has(a.Id);float y=199+i*77;
                PaperTheme.Card(new Rect(329,y,780,67),PaperTheme.Wash(unlocked?MarbleBoardView.PlayerColors[2]:Color.gray,unlocked?.25f:.08f),false,2);
                GUI.Label(new Rect(346,y+5,740,28),(unlocked?"✓ ":"○ ")+a.Name+"  ·  보상: "+a.Reward,label);
                GUI.Label(new Rect(346,y+34,740,23),a.Description,small);
            }
            string status=steamRooms?.AppId==480?"개발 ID: 게임 내 해금 저장 · 전용 Steam 앱에서 동기화":achievementSync==null?"게임 내 해금 저장 · Steam 연결 시 해당 계정과 동기화":"Steam 동기화 대기 "+achievementSync.PendingCount+"개";
            GUI.Label(new Rect(331,671,777,40),status,small);
            if(Button(new Rect(331,731,777,43),"돌아가기",dark))achievementSheet=false;
        }
    }
    sealed class SteamAchievementBackend : IAchievementBackend,IDisposable
    {
        readonly Func<bool> available;readonly uint appId;readonly Callback<UserStatsStored_t> stored;
        public event Action<bool> Stored;
        public bool Available=>available();
        public SteamAchievementBackend(uint appId,Func<bool> available)
        {
            this.appId=appId;this.available=available;
            stored=Callback<UserStatsStored_t>.Create(p=>{if(p.m_nGameID==this.appId)Stored?.Invoke(p.m_eResult==EResult.k_EResultOK);});
        }
        public bool TryRead(string id,out bool unlocked)
        {unlocked=false;try{return Available && SteamUserStats.GetAchievement(id,out unlocked);}catch{return false;}}
        public bool TryUnlock(string id){try{return Available && SteamUserStats.SetAchievement(id);}catch{return false;}}
        public bool TryStore(){try{return Available && SteamUserStats.StoreStats();}catch{return false;}}
        public void Dispose(){stored.Dispose();}
    }
}
