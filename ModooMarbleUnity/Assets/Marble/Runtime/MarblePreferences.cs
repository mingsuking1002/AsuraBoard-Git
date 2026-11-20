using System;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        ClientPreferences preferences;
        bool preferencesOpen;int preferencesTab;string preferencesStatus="변경 사항은 저장 버튼으로 보관합니다.";
        GUIStyle preferenceSliderTrack,preferenceSliderThumb;
        int alertSeat=-1,alertDraft=-2;Game alertModel;
        void LoadClientPreferences()
        {
            try{preferences=JsonUtility.FromJson<ClientPreferences>(PlayerPrefs.GetString("client.preferences.v1",""));}catch{preferences=null;}
            if(preferences==null)preferences=new ClientPreferences{MasterVolume=Mathf.Clamp01(PlayerPrefs.GetFloat("settings.volume",.75f))};
            preferences.Sanitize();mutedSound=PlayerPrefs.GetInt("settings.mute",0)==1;ApplyClientPreferences(true);
        }
        void ApplyClientPreferences(bool display)
        {
            preferences.Sanitize();AudioListener.volume=preferences.MasterVolume*(preferences.QuietWhenUnfocused && !Application.isFocused?.25f:1);
            if(audioSource!=null)audioSource.volume=mutedSound?0:preferences.EffectsVolume;
            if(Application.targetFrameRate!=preferences.EffectiveFrameCap)Application.targetFrameRate=preferences.EffectiveFrameCap;
            if(QualitySettings.vSyncCount!=preferences.EffectiveVSync)QualitySettings.vSyncCount=preferences.EffectiveVSync;
            if(steamRooms!=null)steamRooms.BlockInviteJoin=preferences.BlockInviteJoin;
            if(Board!=null)Board.ReducedMotion=preferences.ReducedMotion;
            if(menuModels!=null)menuModels.ReducedMotion=preferences.ReducedMotion;
            if(display)
            {
                var mode=preferences.WindowMode==1?FullScreenMode.FullScreenWindow:preferences.WindowMode==2?FullScreenMode.ExclusiveFullScreen:FullScreenMode.Windowed;
                if(Screen.width!=preferences.Width || Screen.height!=preferences.Height || Screen.fullScreenMode!=mode)Screen.SetResolution(preferences.Width,preferences.Height,mode);
            }
        }
        void SaveClientPreferences()
        {
            preferences.Sanitize();PlayerPrefs.SetString("client.preferences.v1",JsonUtility.ToJson(preferences));PlayerPrefs.SetFloat("settings.volume",preferences.MasterVolume);PlayerPrefs.SetInt("settings.mute",mutedSound?1:0);PlayerPrefs.Save();
            ApplyClientPreferences(true);preferencesStatus="설정을 저장했습니다.";frontStatus=preferencesStatus;
        }
        void TickClientPreferences()
        {
            if(preferences==null)return;ApplyClientPreferences(false);
            if(Model==null || Board==null || flow.Screen!=PlayScreen.Game){alertModel=null;return;}
            bool mine=onlineGameAttached?onlineSession.LocalSeat==Model.CurrentPlayerIndex:Model.CurrentPlayer.IsHuman;
            if(alertModel!=Model){alertModel=Model;alertSeat=Model.CurrentPlayerIndex;alertDraft=Model.AugmentDraftIndex;return;}
            if(preferences.TurnAlerts && !mutedSound && ((alertSeat!=Model.CurrentPlayerIndex && mine) || (alertDraft!=Model.AugmentDraftIndex && Model.Phase==GamePhase.Augment)))audioSource.PlayOneShot(clickSound);
            alertSeat=Model.CurrentPlayerIndex;alertDraft=Model.AugmentDraftIndex;
        }
        void OpenInGamePreferences(){preferencesOpen=true;settings=help=onlineOpen=false;}
        void ClosePreferences(bool match){preferencesOpen=false;if(match)settings=true;else if(flow.Screen==PlayScreen.Options)flow.Navigate(PlayScreen.Main);}
        float PreferenceSlider(string titleText,float value,float max,float y)
        {
            GUI.Label(new Rect(265,y,340,30),titleText,label);
            var right=new GUIStyle(small){alignment=TextAnchor.MiddleRight};GUI.Label(new Rect(1110,y+2,60,26),Mathf.RoundToInt(value*100)+"%",right);
            PaperTheme.Underline(new Rect(265,y+44,905,1),new Color(.2f,.23f,.14f,.18f));
            if(preferenceSliderTrack==null){preferenceSliderTrack=new GUIStyle(GUIStyle.none);preferenceSliderThumb=new GUIStyle(GUIStyle.none){fixedWidth=20,fixedHeight=20};}
            var slider=new Rect(670,y+7,416,26);
            CircuitArt.Fill(new Rect(slider.x+10,slider.y+10,slider.width-20,6),PaperTheme.Wash(PaperTheme.Muted,.25f));
            GUI.SetNextControlName("preference-slider:"+titleText);
            float next=GUI.HorizontalSlider(slider,value,0,max,preferenceSliderTrack,preferenceSliderThumb);
            float fraction=max>0?Mathf.Clamp01(next/max):0;
            CircuitArt.Fill(new Rect(slider.x+10,slider.y+10,(slider.width-20)*fraction,6),PaperTheme.Forest);
            float thumbX=slider.x+(slider.width-20)*fraction;
            CircuitArt.Circle(new Rect(thumbX,slider.y+2,20,20),ink);
            CircuitArt.Circle(new Rect(thumbX+2,slider.y+4,16,16),PaperTheme.Paper);
            CircuitArt.Circle(new Rect(thumbX+5,slider.y+7,10,10),PaperTheme.Forest);
            return next;
        }
        bool PreferenceToggle(string titleText,bool value,float y)
        {
            GUI.Label(new Rect(265,y+5,690,30),titleText,label);
            PaperTheme.Underline(new Rect(265,y+44,905,1),new Color(.2f,.23f,.14f,.18f));
            if(Button(new Rect(1020,y,150,38),value?"켜짐  ✓":"꺼짐",value?dark:muted))return !value;return value;
        }
        void DrawPreferencesWindow(bool match)
        {
            bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled && !bodyAudioSettings;
            Panel(new Rect(220,120,1000,682),cream);GUI.Label(new Rect(262,145,790,43),"환경설정",title);
            GUI.Label(new Rect(265,195,815,26),"나에게 맞게 조절하세요. 개인 설정은 방 규칙과 별도로 적용됩니다.",small);
            if(Button(new Rect(1114,146,59,43),"×",muted))ClosePreferences(match);
            string[] tabs={"소리","마이크 · 음성","화면 · 성능","게임 · 편의"};
            for(int i=0;i<tabs.Length;i++)if(Button(new Rect(262+i*231,235,215,41),tabs[i],preferencesTab==i?dark:muted))preferencesTab=i;
            if(preferencesTab==0)
            {
                preferences.MasterVolume=PreferenceSlider("전체 음량",preferences.MasterVolume,1,302);
                preferences.EffectsVolume=PreferenceSlider("효과음",preferences.EffectsVolume,1,358);
                preferences.VoiceVolume=PreferenceSlider("음성 수신",preferences.VoiceVolume,2,414);
                preferences.BodyNoiseVolume=PreferenceSlider("인공 장기 소음",preferences.BodyNoiseVolume,1,470);
                mutedSound=PreferenceToggle("효과음 음소거",mutedSound,537);
                preferences.QuietWhenUnfocused=PreferenceToggle("다른 창 사용 시 전체 소리 줄이기",preferences.QuietWhenUnfocused,590);
            }
            if(preferencesTab==1)
            {
                string[] modes={"마이크 끄기","눌러서 말하기","항상 열림"};
                for(int i=0;i<3;i++)if(Button(new Rect(265+i*307,300,291,42),modes[i],(int)preferences.Microphone==i?dark:muted))preferences.Microphone=(MicrophoneMode)i;
                string[] keys={"V","T","왼쪽 Ctrl"};GUI.Label(new Rect(265,365,190,30),"말하기 키",label);
                for(int i=0;i<3;i++)if(Button(new Rect(490+i*225,359,207,37),keys[i],preferences.TalkKey==i?dark:muted))preferences.TalkKey=i;
                preferences.MicrophoneGain=PreferenceSlider("마이크 입력 크기",preferences.MicrophoneGain,2,418);
                preferences.MicrophoneMuted=PreferenceToggle("내 마이크 음소거 [M]",preferences.MicrophoneMuted,473);
                preferences.MuteMicWhenUnfocused=PreferenceToggle("다른 창 사용 시 마이크 음소거",preferences.MuteMicWhenUnfocused,521);
                preferences.Deafened=PreferenceToggle("상대 음성 음소거",preferences.Deafened,569);
                GUI.Label(new Rect(265,619,670,45),voiceStatus+"\nSteam 기본 입력 장치 사용 · 현재 방에만 전달",small);
                if(Button(new Rect(971,619,200,38),"참가자별 음량",muted))bodyAudioSettings=true;
            }
            if(preferencesTab==2)
            {
                int[] widths={1280,1440,1920},heights={720,900,1080};GUI.Label(new Rect(265,294,250,30),"해상도",label);
                for(int i=0;i<3;i++)if(Button(new Rect(490+i*225,291,207,37),widths[i]+" × "+heights[i],preferences.Width==widths[i]?dark:muted)){preferences.Width=widths[i];preferences.Height=heights[i];ApplyClientPreferences(true);}
                string[] modes={"창 모드","테두리 없는 전체 화면","전체 화면"};
                for(int i=0;i<3;i++)if(Button(new Rect(265+i*307,349,291,39),modes[i],preferences.WindowMode==i?dark:muted)){preferences.WindowMode=i;ApplyClientPreferences(true);}
                GUI.Label(new Rect(265,413,200,30),"FPS 제한",label);int[] rates={30,60,120,144,0};
                for(int i=0;i<5;i++)if(Button(new Rect(490+i*135,408,122,38),rates[i]==0?"무제한":rates[i].ToString(),preferences.FrameCap==rates[i]?dark:muted))preferences.FrameCap=rates[i];
                preferences.VSync=PreferenceToggle("수직 동기화",preferences.VSync,471);
                preferences.PowerSaving=PreferenceToggle("절전 모드 · 30 FPS",preferences.PowerSaving,523);
                GUI.Label(new Rect(265,587,900,55),"절전 모드에서는 30 FPS와 수직 동기화 해제를 적용합니다.\n수직 동기화를 켜면 실제 FPS는 모니터 주사율에도 영향을 받습니다.",small);
            }
            if(preferencesTab==3)
            {
                preferences.ReducedMotion=PreferenceToggle("이동·주사위 연출 완화",preferences.ReducedMotion,302);
                preferences.ComfortBlur=PreferenceToggle("눈 매각 시 시야 가림 완화",preferences.ComfortBlur,357);
                preferences.TurnAlerts=PreferenceToggle("내 차례·증강 선택 알림음",preferences.TurnAlerts,412);
                preferences.BlockInviteJoin=PreferenceToggle("Steam 초대의 자동 입장 거부",preferences.BlockInviteJoin,467);
                GUI.Label(new Rect(265,532,900,93),"연출 완화는 게임 판정에 영향을 주지 않습니다.\n초대 거부는 이 게임의 자동 입장 처리에 적용됩니다.\n방 찾기·방 ID를 통한 직접 입장은 계속 사용할 수 있습니다.",small);
            }
            GUI.Label(new Rect(265,668,905,27),"변경 즉시 적용 · 저장하면 다음 실행에도 유지됩니다.",small);
            if(Button(new Rect(265,717,347,44),"저장하고 닫기",dark)){SaveClientPreferences();ClosePreferences(match);}
            if(Button(new Rect(629,717,251,44),"기본값 복원",muted)){preferences=new ClientPreferences();mutedSound=false;ApplyClientPreferences(true);preferencesStatus="기본값으로 변경했습니다. 저장하면 유지됩니다.";}
            if(Button(new Rect(897,717,273,44),"닫기",muted))ClosePreferences(match);
            GUI.enabled=previousEnabled;
        }
        void DrawPreferencesOverlay(){Shade();DrawPreferencesWindow(true);}
    }
}
