using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Marble.Core
{
    public static class VoiceSettingsSelfTests
    {
        static void Assert(bool value,string label){if(!value)throw new InvalidOperationException("Voice/settings test: "+label);}
        static RoomVoiceContext Room(ulong id=40)=>new RoomVoiceContext{Room=id,LocalId=1,Epoch=new Guid("10000000-0000-0000-0000-000000000001"),Members=new ulong[]{1,2,3}};
        public static int Run()
        {
            Action[] tests={Defaults,InvalidSettings,PowerSaving,PushToTalkPolicy,AlwaysOpenPolicy,MuteAndFocus,ZeroInputGain,WireRoundTrip,WireBounds,SenderAndEpoch,HoldAndRelease,RoomChange,NoRoomNoCapture,UnavailableBackend,DisposeStops,PcmRoundTrip,MouthEffectBeforeTransport,BoundedDecompression,AlwaysOpenModeSwitch,FocusStopsCapture};
            foreach(var test in tests)test();return tests.Length;
        }
        static void Defaults(){var p=new ClientPreferences();Assert(p.Microphone==MicrophoneMode.PushToTalk && p.TalkKey==0 && p.MuteMicWhenUnfocused && p.EffectiveFrameCap==60,"PTT V default and focus mute");}
        static void InvalidSettings(){var p=new ClientPreferences{MasterVolume=float.NaN,EffectsVolume=-2,MicrophoneGain=float.PositiveInfinity,VoiceVolume=8,TalkKey=99,WindowMode=5,Width=13,Height=2,FrameCap=9,Microphone=(MicrophoneMode)44};p.Sanitize();Assert(p.MasterVolume==.75f && p.EffectsVolume==0 && p.MicrophoneGain==1 && p.VoiceVolume==2 && p.TalkKey==0 && p.WindowMode==0 && p.Width==1440 && p.Microphone==MicrophoneMode.PushToTalk,"malformed persisted settings bounded");}
        static void PowerSaving(){var p=new ClientPreferences{FrameCap=144,VSync=true,PowerSaving=true};Assert(p.EffectiveFrameCap==30 && p.EffectiveVSync==0,"power saving overrides runtime values");p.PowerSaving=false;Assert(p.EffectiveFrameCap==144 && p.EffectiveVSync==1,"selected cap and vsync restored");p.FrameCap=0;Assert(p.EffectiveFrameCap==-1,"unlimited cap");}
        static void PushToTalkPolicy(){var p=new ClientPreferences();Assert(!VoicePolicy.ShouldCapture(p,true,false,true) && VoicePolicy.ShouldCapture(p,true,true,true) && !VoicePolicy.ShouldCapture(p,false,true,true),"hold required within room only");}
        static void AlwaysOpenPolicy(){var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};Assert(VoicePolicy.ShouldCapture(p,true,false,true) && !VoicePolicy.ShouldCapture(p,false,false,true),"always-open does not require key and never records outside room");}
        static void MuteAndFocus(){var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};Assert(!VoicePolicy.ShouldCapture(p,true,false,false),"unfocused mute default");p.MuteMicWhenUnfocused=false;Assert(VoicePolicy.ShouldCapture(p,true,false,false),"explicit background permission setting");p.MicrophoneMuted=true;Assert(!VoicePolicy.ShouldCapture(p,true,true,true),"quick mute overrides modes");p.MicrophoneMuted=false;p.Microphone=MicrophoneMode.Off;Assert(!VoicePolicy.ShouldCapture(p,true,true,true),"off overrides key");}
        static void ZeroInputGain(){var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen,MicrophoneGain=0};Assert(!VoicePolicy.ShouldCapture(p,true,false,true),"zero gain avoids capture");}
        static void WireRoundTrip(){var room=Room();byte[] audio={1,2,3,4};var frame=VoiceWire.Decode(VoiceWire.Encode(room,1.5f,audio));Assert(frame.Room==40 && frame.Epoch==room.Epoch && frame.Gain==1.5f && frame.Compressed.SequenceEqual(audio),"voice header and payload intact");}
        static void WireBounds()
        {
            foreach(byte[] bytes in new[]{new byte[0],new byte[VoiceWire.MaxAudioBytes+1]}){bool rejected=false;try{VoiceWire.Encode(Room(),1,bytes);}catch(InvalidDataException){rejected=true;}Assert(rejected,"capture payload bounds");}
            var packet=VoiceWire.Encode(Room(),1,new byte[]{8,9});bool truncated=false;try{VoiceWire.Decode(packet.Take(packet.Length-1).ToArray());}catch(InvalidDataException){truncated=true;}Assert(truncated,"truncated frame rejected");
            packet[0]=0;bool version=false;try{VoiceWire.Decode(packet);}catch(InvalidDataException){version=true;}Assert(version,"bad voice version rejected");
        }
        static void SenderAndEpoch(){var room=Room();var frame=VoiceWire.Decode(VoiceWire.Encode(room,1,new byte[]{7}));Assert(VoiceWire.Accept(room,2,frame) && !VoiceWire.Accept(room,1,frame) && !VoiceWire.Accept(room,99,frame),"authenticated member only and no loopback");var other=Room(41);Assert(!VoiceWire.Accept(other,2,frame),"other room rejected");other.Room=40;other.Epoch=Guid.Empty;Assert(!VoiceWire.Accept(other,2,frame),"previous match rejected");}
        sealed class Capture:IRoomVoiceCapture
        {
            public bool Available{get;set;}=true;public int Starts,Stops,Reads;readonly Queue<byte[]> pending=new Queue<byte[]>();
            public void Start(){Starts++;pending.Enqueue(new byte[]{(byte)Starts,2});}
            public void Stop(){Stops++;pending.Enqueue(new byte[]{9});}
            public bool TryRead(out byte[] audio){Reads++;audio=pending.Count>0?pending.Dequeue():null;return audio!=null;}
        }
        static void HoldAndRelease()
        {
            var capture=new Capture();var sent=new List<Tuple<ulong,byte[]>>();using(var voice=new RoomVoiceController(capture,(id,data)=>sent.Add(Tuple.Create(id,data))))
            {
                var room=Room();var p=new ClientPreferences();voice.Tick(room,p,false,true,0);voice.Tick(room,p,false,true,.6);Assert(capture.Starts==0 && capture.Reads==0,"idle PTT never accesses capture data");
                voice.Tick(room,p,true,true,.7);Assert(voice.Recording && capture.Starts==1 && sent.Count==2 && sent.Select(v=>v.Item1).SequenceEqual(new ulong[]{2,3}),"held key sends only current peers");
                voice.Tick(room,p,false,true,.8);Assert(!voice.Recording && capture.Stops==1 && sent.Count==2,"release stops and discards tail");
            }
        }
        static void RoomChange()
        {
            var capture=new Capture();var sent=new List<Tuple<ulong,byte[]>>();using(var voice=new RoomVoiceController(capture,(id,data)=>sent.Add(Tuple.Create(id,data))))
            {
                var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};voice.Tick(Room(),p,false,true,0);voice.Tick(Room(),p,false,true,.6);int count=sent.Count;
                var next=Room(80);next.Members=new ulong[]{1,4};voice.Tick(next,p,false,true,.7);Assert(!voice.Recording && capture.Stops==1 && sent.Count==count,"room switch immediately stops and drops old tail");
                voice.Tick(next,p,false,true,1.3);Assert(voice.Recording && sent.Count==count+1 && sent.Last().Item1==4 && VoiceWire.Decode(sent.Last().Item2).Room==80,"new room starts after quiet drain interval");
            }
        }
        static void NoRoomNoCapture(){var c=new Capture();using(var v=new RoomVoiceController(c,(_,__)=>throw new Exception("unexpected transmission"))){var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};v.Tick(new RoomVoiceContext(),p,true,true,0);v.Tick(new RoomVoiceContext(),p,true,true,5);Assert(c.Starts==0 && c.Reads==0,"main menu has no capture or reads");var invalid=Room();invalid.Members=new ulong[]{1,0};v.Tick(invalid,p,true,true,6);Assert(c.Starts==0,"invalid membership cannot enable microphone");}}
        static void UnavailableBackend(){var c=new Capture{Available=false};using(var v=new RoomVoiceController(c,(_,__)=>{})){v.Tick(Room(),new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen},false,true,0);v.Tick(Room(),new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen},false,true,1);Assert(!v.Recording && c.Starts==0,"offline Steam backend unused");}}
        static void PcmRoundTrip(){float[] input=Enumerable.Range(0,2400).Select(i=>(float)Math.Sin(i*.1)*.6f).ToArray();var copy=(float[])input.Clone();var decoded=VoicePcmCodec.Decode(VoicePcmCodec.Encode(copy,0,new BodyAudioFilter()));Assert(input.Zip(decoded,(a,b)=>Math.Abs(a-b)).Max()<.0001,"PCM round trip within quantization tolerance");}
        static void MouthEffectBeforeTransport(){float[] original=Enumerable.Range(0,2400).Select(i=>i%2==0?.5f:-.5f).ToArray();var sent=(float[])original.Clone();var decoded=VoicePcmCodec.Decode(VoicePcmCodec.Encode(sent,16,new BodyAudioFilter()));Assert(decoded.Sum(v=>Math.Abs(v))<original.Sum(v=>Math.Abs(v))*.5 && decoded.Any(v=>v!=0),"sold mouth changes transported samples before receiver processing");}
        static void BoundedDecompression()
        {
            bool invalid=false;try{VoicePcmCodec.Decode(new byte[]{255,255,0,0,0});}catch(InvalidDataException){invalid=true;}Assert(invalid,"declared voice size bounded");
            byte[] packet=VoicePcmCodec.Encode(new float[]{.1f,.2f,.3f},0,new BodyAudioFilter());packet[0]=1;packet[1]=0;bool bomb=false;try{VoicePcmCodec.Decode(packet);}catch(InvalidDataException){bomb=true;}Assert(bomb,"decompressed extra bytes rejected");
        }
        static void AlwaysOpenModeSwitch()
        {
            var c=new Capture();int sends=0;using(var v=new RoomVoiceController(c,(_,__)=>sends++))
            {
                var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};v.Tick(Room(),p,false,true,0);v.Tick(Room(),p,false,true,.6);Assert(v.Recording && sends==2,"always open captures without a held key");
                p.Microphone=MicrophoneMode.Off;v.Tick(Room(),p,false,true,.7);Assert(!v.Recording && sends==2 && c.Stops==1,"off stops always-open and sends no tail");
                p.Microphone=MicrophoneMode.PushToTalk;v.Tick(Room(),p,false,true,.8);Assert(!v.Recording && c.Starts==1,"changing to PTT does not keep capturing");
            }
        }
        static void FocusStopsCapture()
        {
            var c=new Capture();using(var v=new RoomVoiceController(c,(_,__)=>{}))
            {
                var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};v.Tick(Room(),p,false,true,0);v.Tick(Room(),p,false,true,.6);v.Tick(Room(),p,false,false,.7);Assert(!v.Recording && c.Stops==1,"focus loss stops capture");
                p.MuteMicWhenUnfocused=false;v.Tick(Room(),p,false,false,.8);Assert(v.Recording && c.Starts==2,"explicit background setting permits always-open");p.MicrophoneMuted=true;v.Tick(Room(),p,false,false,.9);Assert(!v.Recording && c.Stops==2,"mute still overrides background setting");
            }
        }
        static void DisposeStops(){var c=new Capture();var v=new RoomVoiceController(c,(_,__)=>{});var p=new ClientPreferences{Microphone=MicrophoneMode.AlwaysOpen};v.Tick(Room(),p,false,true,0);v.Tick(Room(),p,false,true,1);v.Dispose();Assert(c.Stops==1 && !v.Recording,"shutdown stops recording");}
    }
}
