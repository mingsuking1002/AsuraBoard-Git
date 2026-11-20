using System;
using System.IO;
using System.IO.Compression;
namespace Marble.Core
{
    public static class VoicePcmCodec
    {
        public const int MaxSamples=2400;
        // Process the mouth effect before transport. Unmodified microphone audio is never sent for a sold mouth.
        public static byte[] Encode(float[] samples,int bodyMask,BodyAudioFilter filter)
        {
            if(samples==null || samples.Length<1 || samples.Length>MaxSamples)throw new InvalidDataException("Voice sample count");
            foreach(float value in samples)if(float.IsNaN(value) || float.IsInfinity(value))throw new InvalidDataException("Voice sample value");
            filter.Process(samples,bodyMask&16,24000,0);
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))writer.Write((ushort)samples.Length);
                using(var compressed=new DeflateStream(stream,CompressionLevel.Fastest,true))
                using(var writer=new BinaryWriter(compressed))foreach(float sample in samples)writer.Write((short)Math.Round(Math.Max(-1,Math.Min(1,sample))*32767));
                return stream.ToArray();
            }
        }
        public static float[] Decode(byte[] payload)
        {
            if(payload==null || payload.Length<3 || payload.Length>VoiceWire.MaxAudioBytes)throw new InvalidDataException("Voice PCM size");
            using(var stream=new MemoryStream(payload,false))
            {
                int count;using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))count=reader.ReadUInt16();
                if(count<1 || count>MaxSamples)throw new InvalidDataException("Voice PCM count");
                using(var compressed=new DeflateStream(stream,CompressionMode.Decompress))using(var reader=new BinaryReader(compressed))
                {
                    var samples=new float[count];for(int i=0;i<count;i++)samples[i]=reader.ReadInt16()/32768f;
                    if(compressed.ReadByte()!=-1)throw new InvalidDataException("Extra decompressed voice bytes");return samples;
                }
            }
        }
    }
}
