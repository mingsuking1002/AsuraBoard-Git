using System;
namespace Marble.Core
{
    // Processes locally generated demonstration sound. Does not access a microphone or network.
    public sealed class BodyAudioFilter
    {
        float lowPass;double phase;
        public void Process(float[] samples,int mask,int sampleRate,float noiseVolume)
        {
            if(sampleRate<=0)throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double energy=0;foreach(float s in samples)energy+=s*s;
            float duck=energy/Math.Max(1,samples.Length)>.0004?.22f:1;
            bool mouth=(mask&16)!=0,lung=(mask&256)!=0;
            for(int i=0;i<samples.Length;i++)
            {
                float value=samples[i];if(mouth){lowPass+=.08f*(value-lowPass);value=lowPass*.8f+value*.2f;}
                if(lung)value+=(float)(Math.Sin(phase*2*Math.PI*91)*(.5+.5*Math.Sin(phase*2*Math.PI*.45)))*.025f*Math.Max(0,Math.Min(1,noiseVolume))*duck;
                samples[i]=Math.Max(-1,Math.Min(1,value));phase+=1d/sampleRate;
            }
        }
    }
}
