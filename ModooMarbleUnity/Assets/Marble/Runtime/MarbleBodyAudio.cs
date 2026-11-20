using System;
using System.Linq;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        bool bodyAudioSettings;
        readonly float[] bodyNoiseVolumes={1f,1f,1f,1f};readonly string[] bodyNoiseKeys=new string[4];
        readonly AudioSource[] bodyNoiseSources=new AudioSource[4],bodyBreathSources=new AudioSource[4];
        AudioClip mechanicalBodyLoop,mechanicalBreathLoop,localMouthPreview;
        void TickBodyAudio()
        {
            bool playing=Model!=null && Model.Config.EnableBodyEconomy && (diagnosticMode || flow.Screen==PlayScreen.Game || flow.Screen==PlayScreen.Results);
            if(!playing){foreach(var source in bodyNoiseSources)if(source!=null)source.Stop();foreach(var source in bodyBreathSources)if(source!=null)source.Stop();return;}
            if(mechanicalBodyLoop==null)
            {
                float[] data=new float[24000];for(int i=0;i<data.Length;i++){double t=i/24000d;data[i]=(float)(Math.Sin(t*Math.PI*2*55)*.045*(.5+.5*Math.Sin(t*Math.PI*2*8)));}
                mechanicalBodyLoop=AudioClip.Create("Original artificial organ motor",data.Length,1,24000,false);mechanicalBodyLoop.SetData(data,0);
            }
            if(mechanicalBreathLoop==null)
            {
                float[] data=new float[48000];for(int i=0;i<data.Length;i++){double t=i/24000d;data[i]=(float)(Math.Sin(t*Math.PI*2*91)*.03*(.5+.5*Math.Sin(t*Math.PI)));}
                mechanicalBreathLoop=AudioClip.Create("Original artificial lung breathing",data.Length,1,24000,false);mechanicalBreathLoop.SetData(data,0);
            }
            for(int i=Model.Players.Count;i<4;i++){if(bodyNoiseSources[i]!=null)bodyNoiseSources[i].Stop();if(bodyBreathSources[i]!=null)bodyBreathSources[i].Stop();}
            for(int i=0;i<Model.Players.Count;i++)
            {
                if(bodyNoiseSources[i]==null){bodyNoiseSources[i]=gameObject.AddComponent<AudioSource>();bodyNoiseSources[i].clip=mechanicalBodyLoop;bodyNoiseSources[i].loop=true;}
                if(bodyBreathSources[i]==null){bodyBreathSources[i]=gameObject.AddComponent<AudioSource>();bodyBreathSources[i].clip=mechanicalBreathLoop;bodyBreathSources[i].loop=true;}
                bool heart=BodyCatalog.IsSold(Model.Players[i],BodyPart.Heart),lung=BodyCatalog.IsSold(Model.Players[i],BodyPart.Lung);
                bodyNoiseSources[i].volume=mutedSound || Model.Players[i].Bankrupt?0:BodyNoiseLevel(i)*preferences.BodyNoiseVolume*SpeakingNoiseDuck(i)*(heart?.48f:0);
                bodyBreathSources[i].volume=mutedSound || Model.Players[i].Bankrupt?0:BodyNoiseLevel(i)*preferences.BodyNoiseVolume*SpeakingNoiseDuck(i)*(lung?.4f:0);
                if(heart && !bodyNoiseSources[i].isPlaying)bodyNoiseSources[i].Play();if(!heart)bodyNoiseSources[i].Stop();
                if(lung && !bodyBreathSources[i].isPlaying)bodyBreathSources[i].Play();if(!lung)bodyBreathSources[i].Stop();
            }
        }
        void PreviewBodySound(int seat)
        {
            if(localMouthPreview!=null)Destroy(localMouthPreview);
            float[] samples=new float[18000];for(int i=0;i<samples.Length;i++){double t=i/24000d;samples[i]=(float)((Math.Sin(t*Math.PI*2*220)+.35*Math.Sin(t*Math.PI*2*1700))*.12*Math.Pow(Math.Sin(t*Math.PI*4),2));}
            new BodyAudioFilter().Process(samples,Model.Players[seat].SoldBodyMask,24000,BodyNoiseLevel(seat)*preferences.BodyNoiseVolume);
            localMouthPreview=AudioClip.Create("Locally synthesized mouth filter example",samples.Length,1,24000,false);localMouthPreview.SetData(samples,0);audioSource.PlayOneShot(localMouthPreview);
        }
        void DisposeBodyAudio(){if(mechanicalBodyLoop!=null)Destroy(mechanicalBodyLoop);if(mechanicalBreathLoop!=null)Destroy(mechanicalBreathLoop);if(localMouthPreview!=null)Destroy(localMouthPreview);}
        string BodyNoiseKey(int seat)
        {
            if(onlineGameAttached && onlineSession!=null && seat<onlineSession.Roster.Count)return "noise.peer."+onlineSession.Roster[seat].Id;
            return "noise.local."+seat;
        }
        float BodyNoiseLevel(int seat)
        {
            string key=BodyNoiseKey(seat);if(bodyNoiseKeys[seat]!=key){bodyNoiseKeys[seat]=key;bodyNoiseVolumes[seat]=Mathf.Clamp01(PlayerPrefs.GetFloat(key,1));}return bodyNoiseVolumes[seat];
        }
        void DrawBodyAudioSettings()
        {
            Shade();PaperTheme.Card(new Rect(200,135,1040,640),cream,true,3);GUI.Label(new Rect(232,157,970,43),"참가자 음성 · 인공 장기 소음",title);
            GUI.Label(new Rect(232,211,970,35),voiceStatus+" · 입력 모드와 전체 음량은 환경설정에서 변경합니다.",small);
            var members=onlineSession!=null && onlineSession.Roster.Count>0?onlineSession.Roster:steamRooms?.Members;
            bool online=steamRooms!=null && steamRooms.RoomId!=0 && members!=null;
            int count=online?members.Count:localRoom && flow.Screen==PlayScreen.Lobby?players:Model?.Players.Count??1;
            for(int i=0;i<count;i++)
            {
                float y=263+i*88;ulong id=online?members[i].Id:0;bool own=online && id==steamRooms.LocalId;
                string name=online?members[i].Name:localRoom && flow.Screen==PlayScreen.Lobby?LocalMemberName(i):Model!=null && i<Model.Players.Count?Model.Players[i].Name:"로컬 플레이어";
                GUI.Label(new Rect(232,y,267,30),name+(own?" · 나":""),label);
                if(online && !own)
                {
                    float level=PeerVoiceLevel(id),next=GUI.HorizontalSlider(new Rect(528,y+10,428,20),level,0,2);
                    if(Mathf.Abs(next-level)>.001f)SetPeerVoiceLevel(id,next);
                    if(Button(new Rect(987,y-1,206,32),level<=0?"음성 켜기":"음소거 · "+Mathf.RoundToInt(level*100)+"%",muted))SetPeerVoiceLevel(id,level<=0?1:0);
                }
                else GUI.Label(new Rect(528,y,665,30),own?"내 음성은 스피커로 재생하지 않습니다.":"로컬 게임 · 음성 전송 없음",small);
                string key=online?"noise.peer."+id:"noise.local."+i;float noise=Mathf.Clamp01(PlayerPrefs.GetFloat(key,1));
                GUI.Label(new Rect(232,y+39,270,26),"기계 소음 "+Mathf.RoundToInt(noise*100)+"%",small);
                float changed=GUI.HorizontalSlider(new Rect(528,y+49,665,20),noise,0,1);
                if(Mathf.Abs(changed-noise)>.001f){PlayerPrefs.SetFloat(key,changed);PlayerPrefs.Save();if(i<4){bodyNoiseKeys[i]=null;}}
            }
            if(Button(new Rect(232,656,465,39),"내 신체 효과 합성 소리 미리듣기",muted,Model!=null))PreviewBodySound(BodyViewSeat);
            if(Button(new Rect(719,656,474,39),"마이크 음소거 [M]",preferences.MicrophoneMuted?dark:muted)){preferences.MicrophoneMuted=!preferences.MicrophoneMuted;SaveClientPreferences();}
            if(Button(new Rect(232,716,961,39),"닫기",dark))bodyAudioSettings=false;
        }
    }
}
