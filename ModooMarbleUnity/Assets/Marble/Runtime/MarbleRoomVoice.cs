using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Marble.Core;
using Marble.Online;
using Steamworks;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        const int VoiceChannel=18,VoiceSampleRate=24000;
        RoomVoiceController roomVoice;
        SteamVoiceCapture voiceCapture;
        readonly Dictionary<ulong,PeerVoicePlayback> voicePeers=new Dictionary<ulong,PeerVoicePlayback>();
        readonly Dictionary<ulong,float> peerVoiceLevels=new Dictionary<ulong,float>();
        readonly Dictionary<ulong,double> peerSpeechAt=new Dictionary<ulong,double>();
        readonly IntPtr[] voiceIncoming=new IntPtr[16];
        ulong voiceRoom;Guid voiceEpoch;
        string voiceStatus="Steam 방에서 사용할 수 있습니다.";double voiceSendErrorUntil;
        string MicrophoneBadge=>preferences==null?"마이크 대기":preferences.Microphone==MicrophoneMode.Off?"마이크 꺼짐":preferences.MicrophoneMuted?"마이크 음소거":roomVoice!=null && roomVoice.Recording?preferences.Microphone==MicrophoneMode.AlwaysOpen?"마이크 항상 열림":"마이크 말하는 중":"마이크 대기 · "+TalkKey;
        static float SafeVoiceLevel(float value,float max)=>float.IsNaN(value) || float.IsInfinity(value)?1:Mathf.Clamp(value,0,max);
        KeyCode TalkKey=>preferences.TalkKey==1?KeyCode.T:preferences.TalkKey==2?KeyCode.LeftControl:KeyCode.V;
        RoomVoiceContext VoiceContext()
        {
            if(diagnosticMode || steamRooms==null || !steamRooms.Available || steamRooms.RoomId==0)return new RoomVoiceContext();
            Guid epoch=Guid.Empty;
            if(onlineSession!=null && onlineSession.Stage==OnlineStage.Playing && !Guid.TryParseExact(onlineSession.MatchId,"N",out epoch))return new RoomVoiceContext();
            return new RoomVoiceContext{Room=steamRooms.RoomId,LocalId=steamRooms.LocalId,Epoch=epoch,Members=steamRooms.Members.Select(p=>p.Id).ToArray()};
        }
        float PeerVoiceLevel(ulong id)
        {if(!peerVoiceLevels.TryGetValue(id,out float value)){value=SafeVoiceLevel(PlayerPrefs.GetFloat("voice.peer."+id,1),2);peerVoiceLevels[id]=value;}return value;}
        void SetPeerVoiceLevel(ulong id,float value)
        {peerVoiceLevels[id]=SafeVoiceLevel(value,2);if(value<=0 && voicePeers.TryGetValue(id,out var voice)){voice.Dispose();voicePeers.Remove(id);peerSpeechAt.Remove(id);}
            PlayerPrefs.SetFloat("voice.peer."+id,peerVoiceLevels[id]);PlayerPrefs.Save();}
        void TickRoomVoice()
        {
            if(preferences==null)return;
            var context=VoiceContext();
            if(roomVoice==null)
            {
                voiceCapture=new SteamVoiceCapture(()=>steamRooms!=null && steamRooms.Available,LocalVoiceBodyMask);
                roomVoice=new RoomVoiceController(voiceCapture,SendRoomVoice);
            }
            try
            {
                if(voiceRoom!=context.Room || voiceEpoch!=context.Epoch){ClearPeerVoices();voiceRoom=context.Room;voiceEpoch=context.Epoch;}
                if(context.Active && Input.GetKeyDown(KeyCode.M)){preferences.MicrophoneMuted=!preferences.MicrophoneMuted;SaveClientPreferences();}
                // Never starts recording outside a joined room. PTT and always-open are separate explicit modes.
                roomVoice.Tick(context,preferences,Input.GetKey(TalkKey),Application.isFocused,Time.unscaledTimeAsDouble);
                if(Time.unscaledTimeAsDouble>=voiceSendErrorUntil)
                    voiceStatus=preferences.Microphone==MicrophoneMode.Off?"마이크 꺼짐":preferences.MicrophoneMuted?"마이크 음소거":!context.Active?"Steam 방 입장 후 사용 가능":preferences.MicrophoneGain<=0?"입력 크기 0% · 마이크 꺼짐":preferences.MuteMicWhenUnfocused && !Application.isFocused?"다른 창 사용 중 · 마이크 음소거":roomVoice.Recording?preferences.Microphone==MicrophoneMode.AlwaysOpen?"항상 열림 · 마이크 활성":"눌러서 말하기 · 마이크 활성":preferences.Microphone==MicrophoneMode.AlwaysOpen?"항상 열림 연결 대기":"눌러서 말하기 대기 · "+TalkKey;
                if(!context.Active)
                {
                    if(steamRooms!=null && steamRooms.Available){int old=SteamNetworkingMessages.ReceiveMessagesOnChannel(VoiceChannel,voiceIncoming,voiceIncoming.Length);for(int i=0;i<old;i++){SteamNetworkingMessage_t.Release(voiceIncoming[i]);voiceIncoming[i]=IntPtr.Zero;}}
                    return;
                }
                if(preferences.Deafened)ClearPeerVoices();
                foreach(var id in voicePeers.Keys.Where(id=>!context.Members.Contains(id)).ToArray()){voicePeers[id].Dispose();voicePeers.Remove(id);peerSpeechAt.Remove(id);}
                int count=SteamNetworkingMessages.ReceiveMessagesOnChannel(VoiceChannel,voiceIncoming,voiceIncoming.Length);
                for(int i=0;i<count;i++)
                {
                    IntPtr pointer=voiceIncoming[i];try
                    {
                        var native=SteamNetworkingMessage_t.FromIntPtr(pointer);ulong sender=native.m_identityPeer.GetSteamID64();
                        if(preferences.Deafened || native.m_cbSize<=VoiceWire.HeaderBytes || native.m_cbSize>VoiceWire.MaxPacket || !context.Members.Contains(sender) || sender==context.LocalId)continue;
                        float level=PeerVoiceLevel(sender);if(level<=0)continue;
                        byte[] packet=new byte[native.m_cbSize];Marshal.Copy(native.m_pData,packet,0,packet.Length);
                        VoiceFrame frame;try{frame=VoiceWire.Decode(packet);}catch{continue;}
                        if(!VoiceWire.Accept(context,sender,frame))continue;
                        float[] samples;try{samples=VoicePcmCodec.Decode(frame.Compressed);}catch{continue;}
                        for(int n=0;n<samples.Length;n++)samples[n]=Mathf.Clamp(samples[n]*frame.Gain,-1,1);
                        if(!voicePeers.TryGetValue(sender,out var playback)){playback=new PeerVoicePlayback(transform);voicePeers[sender]=playback;}
                        int seat=onlineSession?.Roster.ToList().FindIndex(p=>p.Id==sender)??-1;
                        int mask=onlineGameAttached && Model!=null && seat>=0 && seat<Model.Players.Count && Model.Config.EnableBodyEconomy?Model.Players[seat].SoldBodyMask:0;
                        // Body muffling is applied to the received player's audio; no extra microphone read is needed.
                                                double energy=0;foreach(float sample in samples)energy+=sample*sample;
                        playback.Filter.Process(samples,mask&256,VoiceSampleRate,preferences.BodyNoiseVolume*(onlineGameAttached && seat>=0 && seat<4?BodyNoiseLevel(seat):1));
                        playback.Push(samples);if(energy/samples.Length>.0004)peerSpeechAt[sender]=Time.unscaledTimeAsDouble;
                    }
                    finally{SteamNetworkingMessage_t.Release(pointer);voiceIncoming[i]=IntPtr.Zero;}
                }
                foreach(var peer in voicePeers)peer.Value.Volume=preferences.Deafened?0:preferences.VoiceVolume*PeerVoiceLevel(peer.Key);
            }
            catch(Exception error){roomVoice.Stop();voiceStatus="음성 연결 대기 · "+error.GetType().Name;ClearPeerVoices();}
        }
        int LocalVoiceBodyMask()
        {
            int seat=onlineSession?.LocalSeat??-1;return onlineGameAttached && Model!=null && Model.Config.EnableBodyEconomy && seat>=0 && seat<Model.Players.Count?Model.Players[seat].SoldBodyMask:0;
        }
        void SendRoomVoice(ulong recipient,byte[] frame)
        {
            var context=VoiceContext();
            if(!context.Active || recipient==context.LocalId || !context.Members.Contains(recipient) || frame==null || frame.Length>VoiceWire.MaxPacket)return;
            var remote=new SteamNetworkingIdentity();remote.SetSteamID64(recipient);var pinned=GCHandle.Alloc(frame,GCHandleType.Pinned);
            try{var result=SteamNetworkingMessages.SendMessageToUser(ref remote,pinned.AddrOfPinnedObject(),(uint)frame.Length,Constants.k_nSteamNetworkingSend_Reliable,VoiceChannel);if(result!=EResult.k_EResultOK){voiceStatus="음성 전송 연결 대기 · "+result;voiceSendErrorUntil=Time.unscaledTimeAsDouble+3;}}
            finally{pinned.Free();}
        }
        void ClearPeerVoices(){foreach(var voice in voicePeers.Values)voice.Dispose();voicePeers.Clear();peerSpeechAt.Clear();}
        void DisposeRoomVoice(){try{roomVoice?.Dispose();}catch{}roomVoice=null;voiceCapture=null;ClearPeerVoices();voiceRoom=0;voiceEpoch=Guid.Empty;}
        float SpeakingNoiseDuck(int seat)
        {
            if(preferences==null || preferences.VoiceVolume<=0 || preferences.Deafened || onlineSession==null || seat<0 || seat>=onlineSession.Roster.Count)return 1;
            ulong id=onlineSession.Roster[seat].Id;return peerSpeechAt.TryGetValue(id,out double time) && Time.unscaledTimeAsDouble-time<.3?.22f:1;
        }
        sealed class SteamVoiceCapture:IRoomVoiceCapture
        {
            readonly Func<bool> available;readonly Func<int> bodyMask;
            readonly byte[] buffer=new byte[VoiceWire.MaxAudioBytes],decoded=new byte[196608];
            readonly Queue<byte[]> outgoing=new Queue<byte[]>();BodyAudioFilter filter=new BodyAudioFilter();
            public SteamVoiceCapture(Func<bool> available,Func<int> bodyMask){this.available=available;this.bodyMask=bodyMask;}
            public bool Available=>available();
            public void Start(){outgoing.Clear();filter=new BodyAudioFilter();SteamUser.StartVoiceRecording();}
            public void Stop(){SteamUser.StopVoiceRecording();outgoing.Clear();}
            public bool TryRead(out byte[] data)
            {
                data=null;if(!Available)return false;
                if(outgoing.Count>0){data=outgoing.Dequeue();return true;}
                var availability=SteamUser.GetAvailableVoice(out uint size);if(availability==EVoiceResult.k_EVoiceResultNoData)return false;if(availability!=EVoiceResult.k_EVoiceResultOK)throw new InvalidOperationException("Steam voice "+availability);if(size==0)return false;
                if(size>buffer.Length){if(size>262144)throw new InvalidOperationException("Voice buffer limit");byte[] discard=new byte[size];SteamUser.GetVoice(true,discard,size,out _);return false;}
                if(SteamUser.GetVoice(true,buffer,(uint)buffer.Length,out uint written)!=EVoiceResult.k_EVoiceResultOK || written==0 || written>buffer.Length)return false;
                if(SteamUser.DecompressVoice(buffer,written,decoded,(uint)decoded.Length,out uint pcmBytes,VoiceSampleRate)!=EVoiceResult.k_EVoiceResultOK || pcmBytes==0 || pcmBytes>decoded.Length || pcmBytes%2!=0)return false;
                int count=(int)pcmBytes/2,mask=bodyMask();
                for(int start=0;start<count;start+=VoicePcmCodec.MaxSamples)
                {
                    int length=Math.Min(VoicePcmCodec.MaxSamples,count-start);var samples=new float[length];
                    for(int i=0;i<length;i++){int pos=(start+i)*2;samples[i]=(short)(decoded[pos]|decoded[pos+1]<<8)/32768f;}
                    byte[] packet=VoicePcmCodec.Encode(samples,mask,filter);if(outgoing.Count>=8)outgoing.Dequeue();outgoing.Enqueue(packet);
                }
                if(outgoing.Count==0)return false;data=outgoing.Dequeue();return true;
            }
        }
        sealed class PeerVoicePlayback:IDisposable
        {
            readonly GameObject root;readonly AudioClip clip;readonly AudioSource source;
            readonly float[] samples=new float[VoiceSampleRate/2];readonly object gate=new object();int read,write,count;
            public readonly BodyAudioFilter Filter=new BodyAudioFilter();
            volatile float gain=1;
            public float Volume{set{gain=Mathf.Clamp(value,0,4);}}
            public PeerVoicePlayback(Transform parent)
            {root=new GameObject("Steam peer voice");root.transform.SetParent(parent);source=root.AddComponent<AudioSource>();clip=AudioClip.Create("Live Steam room speech",VoiceSampleRate/5,1,VoiceSampleRate,true,Read);source.clip=clip;source.loop=true;source.Play();}
            public void Push(float[] input){lock(gate)foreach(float value in input){if(count==samples.Length){read=(read+1)%samples.Length;count--;}samples[write]=value;write=(write+1)%samples.Length;count++;}}
            void Read(float[] output){lock(gate)for(int i=0;i<output.Length;i++){output[i]=count>0?Math.Max(-1,Math.Min(1,samples[read]*gain)):0;if(count>0){read=(read+1)%samples.Length;count--;}}}
            public void Dispose(){source.Stop();Destroy(root);Destroy(clip);}
        }
    }
}
