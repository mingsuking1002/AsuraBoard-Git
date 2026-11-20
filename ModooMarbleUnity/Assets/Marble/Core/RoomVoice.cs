using System;
using System.IO;
using System.Linq;
namespace Marble.Core
{
    public static class VoicePolicy
    {
        public static bool ShouldCapture(ClientPreferences preferences,bool inRoom,bool keyHeld,bool focused)
            =>inRoom && preferences.MicrophoneGain>0 && !preferences.MicrophoneMuted && (!preferences.MuteMicWhenUnfocused || focused) &&
            (preferences.Microphone==MicrophoneMode.AlwaysOpen || preferences.Microphone==MicrophoneMode.PushToTalk && keyHeld);
    }
    public sealed class RoomVoiceContext
    {
        public ulong Room,LocalId;public Guid Epoch;public ulong[] Members=new ulong[0];
        public bool Active=>Room!=0 && LocalId!=0 && Members!=null && Members.Contains(LocalId) && Members.Length<=4 && Members.Distinct().Count()==Members.Length && Members.All(id=>id!=0);
    }
    public sealed class VoiceFrame
    {public ulong Room;public Guid Epoch;public float Gain;public byte[] Compressed;}
    public static class VoiceWire
    {
        public const int MaxAudioBytes=32768,HeaderBytes=34,MaxPacket=MaxAudioBytes+HeaderBytes;
        public static byte[] Encode(RoomVoiceContext room,float gain,byte[] compressed)
        {
            if(!room.Active || compressed==null || compressed.Length==0 || compressed.Length>MaxAudioBytes || float.IsNaN(gain) || float.IsInfinity(gain) || gain<0 || gain>2)throw new InvalidDataException("Invalid voice frame");
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {w.Write(0x32564357u);w.Write(room.Room);w.Write(room.Epoch.ToByteArray());w.Write(gain);w.Write((ushort)compressed.Length);w.Write(compressed);return stream.ToArray();}
        }
        public static VoiceFrame Decode(byte[] packet)
        {
            if(packet==null || packet.Length<=HeaderBytes || packet.Length>MaxPacket)throw new InvalidDataException("Voice size");
            using(var stream=new MemoryStream(packet,false))using(var r=new BinaryReader(stream))
            {
                if(r.ReadUInt32()!=0x32564357u)throw new InvalidDataException("Voice version");
                var frame=new VoiceFrame{Room=r.ReadUInt64(),Epoch=new Guid(r.ReadBytes(16)),Gain=r.ReadSingle()};int length=r.ReadUInt16();
                if(frame.Room==0 || length==0 || length>MaxAudioBytes || length!=stream.Length-stream.Position || float.IsNaN(frame.Gain) || float.IsInfinity(frame.Gain) || frame.Gain<0 || frame.Gain>2)throw new InvalidDataException("Voice payload");
                frame.Compressed=r.ReadBytes(length);return frame;
            }
        }
        public static bool Accept(RoomVoiceContext current,ulong sender,VoiceFrame frame)=>frame!=null && current.Active && sender!=current.LocalId && current.Members.Contains(sender) && frame.Room==current.Room && frame.Epoch==current.Epoch;
    }
    public interface IRoomVoiceCapture
    {
        bool Available{get;}
        void Start();void Stop();bool TryRead(out byte[] compressed);
    }
    // The capture backend is invoked only after room and user-mode checks. Tests use a fake backend.
    public sealed class RoomVoiceController:IDisposable
    {
        readonly IRoomVoiceCapture capture;readonly Action<ulong,byte[]> send;
        ulong room,localId;Guid epoch;double resumeAt,clock,drainUntil;bool hasStarted;
        public bool Recording{get;private set;}
        public RoomVoiceController(IRoomVoiceCapture capture,Action<ulong,byte[]> send){this.capture=capture;this.send=send;}
        public void Tick(RoomVoiceContext context,ClientPreferences preferences,bool keyHeld,bool focused,double now)
        {
            clock=now;
            bool changed=room!=context.Room || localId!=context.LocalId || epoch!=context.Epoch;
            if(changed){Stop();Discard();room=context.Room;localId=context.LocalId;epoch=context.Epoch;resumeAt=now+.5;}
            bool should=now>=resumeAt && capture.Available && VoicePolicy.ShouldCapture(preferences,context.Active,keyHeld,focused);
            if(!should){Stop();Discard();return;}
            if(!Recording){capture.Start();Recording=true;hasStarted=true;}
            for(int i=0;i<4;i++)
            {
                if(!capture.TryRead(out byte[] bytes))break;
                if(bytes==null || bytes.Length==0 || bytes.Length>VoiceWire.MaxAudioBytes)continue;
                byte[] frame=VoiceWire.Encode(context,preferences.MicrophoneGain,bytes);
                foreach(ulong recipient in context.Members)if(recipient!=context.LocalId)send(recipient,frame);
            }
        }
        void Discard(){if(hasStarted && clock<=drainUntil && capture.Available)for(int i=0;i<4 && capture.TryRead(out _);i++){} }
        public void Stop(){if(Recording){if(capture.Available)capture.Stop();Recording=false;drainUntil=clock+.5;}}
        public void Dispose(){Stop();Discard();}
    }
}
