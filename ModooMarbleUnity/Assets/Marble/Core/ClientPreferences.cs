using System;
namespace Marble.Core
{
    public enum MicrophoneMode { Off,PushToTalk,AlwaysOpen }
    [Serializable] public sealed class ClientPreferences
    {
        public float MasterVolume=.75f,EffectsVolume=1,VoiceVolume=1,MicrophoneGain=1,BodyNoiseVolume=.4f;
        public MicrophoneMode Microphone=MicrophoneMode.PushToTalk;
        public int TalkKey,Width=1440,Height=900,WindowMode,FrameCap=60;
        public bool MicrophoneMuted,Deafened,MuteMicWhenUnfocused=true,QuietWhenUnfocused=true,VSync,PowerSaving,ReducedMotion,ComfortBlur,TurnAlerts=true,BlockInviteJoin;
        static float Clamp(float v,float low,float high,float fallback)=>float.IsNaN(v) || float.IsInfinity(v)?fallback:Math.Max(low,Math.Min(high,v));
        public void Sanitize()
        {
            MasterVolume=Clamp(MasterVolume,0,1,.75f);EffectsVolume=Clamp(EffectsVolume,0,1,1);VoiceVolume=Clamp(VoiceVolume,0,2,1);MicrophoneGain=Clamp(MicrophoneGain,0,2,1);BodyNoiseVolume=Clamp(BodyNoiseVolume,0,1,.4f);
            if(!Enum.IsDefined(typeof(MicrophoneMode),Microphone))Microphone=MicrophoneMode.PushToTalk;
            if(TalkKey<0 || TalkKey>2)TalkKey=0;if(WindowMode<0 || WindowMode>2)WindowMode=0;
            if(!((Width==1280 && Height==720) || (Width==1440 && Height==900) || (Width==1920 && Height==1080))){Width=1440;Height=900;}
            if(FrameCap!=0 && FrameCap!=30 && FrameCap!=60 && FrameCap!=120 && FrameCap!=144)FrameCap=60;
        }
        public int EffectiveFrameCap=>PowerSaving?30:FrameCap==0?-1:FrameCap;
        public int EffectiveVSync=>PowerSaving?0:VSync?1:0;
    }
}
