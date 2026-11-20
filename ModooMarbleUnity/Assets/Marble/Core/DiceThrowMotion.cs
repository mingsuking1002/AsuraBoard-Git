using System;
namespace Marble.Core
{
    public readonly struct DiceThrowPose
    {
        public readonly float X,Y,Z,SpinX,SpinY,SpinZ,Settle,CameraSize;
        public DiceThrowPose(float x,float y,float z,float sx,float sy,float sz,float settle,float cameraSize)
        {X=x;Y=y;Z=z;SpinX=sx;SpinY=sy;SpinZ=sz;Settle=settle;CameraSize=cameraSize;}
    }
    // Pure visual motion. It cannot change dice results or consume the game's RNG.
    public static class DiceThrowMotion
    {
        public const float Duration=1.55f;
        static float Clamp(float t)=>Math.Max(0,Math.Min(1,t));
        static float Smooth(float t){t=Clamp(t);return t*t*(3-2*t);}
        static float Mix(float a,float b,float t)=>a+(b-a)*t;
        // The dice land separately, then bounce twice with diminishing energy.
        public static float ImpactAt(int die,int bounce)
        {
            if(die<0 || die>1 || bounce<0 || bounce>2)throw new ArgumentOutOfRangeException();
            return (bounce==0?.34f:bounce==1?.54f:.70f)+die*.035f;
        }
        public static DiceThrowPose Sample(float progress,int die,bool reducedMotion=false)
        {
            if(die<0 || die>1)throw new ArgumentOutOfRangeException(nameof(die));
            float t=Clamp(progress),side=die==0?-1:1,end=side*.72f;
            if(reducedMotion || t>=1)return new DiceThrowPose(end,0,0,0,0,0,1,1.22f);
            float first=ImpactAt(die,0),second=ImpactAt(die,1),third=ImpactAt(die,2),y;
            if(t<first)y=(float)Math.Sin(Math.PI*t/first)*.95f;
            else if(t<second)y=(float)Math.Sin(Math.PI*(t-first)/(second-first))*.30f;
            else if(t<third)y=(float)Math.Sin(Math.PI*(t-second)/(third-second))*.085f;
            else y=0;
            float x=end+side*.70f*(float)Math.Pow(1-t,1.15);
            float z=-1.2f*(float)Math.Pow(1-t,1.3)+.15f*(float)Math.Sin(Math.PI*t);
            // Continuous deceleration, ending at whole turns, followed by a gentle settle.
            float spin=1-(float)Math.Pow(1-t,1.4);
            float settle=Smooth((t-.91f)/.09f);
            return new DiceThrowPose(x,Math.Max(0,y),z,spin*1080*side,spin*720,spin*720*side,
                settle,Mix(2.05f,1.22f,Smooth((t-.62f)/.38f)));
        }
    }
    public static class DiceThrowMotionSelfTests
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Dice throw: "+message);}
        public static int Run()
        {
            for(int die=0;die<2;die++)
            {
                var end=DiceThrowMotion.Sample(1,die);Check(end.Y==0 && end.Z==0 && end.Settle==1 && end.X==(die==0?-.72f:.72f),"settled result pose");
                var previous=DiceThrowMotion.Sample(0,die);
                for(int i=1;i<=1000;i++)
                {
                    var p=DiceThrowMotion.Sample(i/1000f,die);
                    Check(!float.IsNaN(p.Y) && p.Y>=0 && p.Settle>=0 && p.Settle<=1,"bounded pose");
                    Check(Math.Abs(p.X-previous.X)<.03f && Math.Abs(p.Z-previous.Z)<.03f && Math.Abs(p.Y-previous.Y)<.03f,"continuous flight and bounces");
                    Check(Math.Abs(p.Y*.6f+p.Z*.8f)+.97f<p.CameraSize,"dice fits vertical camera bounds");
                    Check(Math.Abs(p.X)+.87f<p.CameraSize*2,"dice fits horizontal camera bounds");
                    previous=p;
                }
            }
            Check(DiceThrowMotion.Sample(.17f,0).Y>.9f && DiceThrowMotion.Sample(.44f,0).Y>.25f,"throw and second bounce");
            for(int die=0;die<2;die++)for(int hit=0;hit<3;hit++)
                Check(DiceThrowMotion.Sample(DiceThrowMotion.ImpactAt(die,hit),die).Y<.0001f,"ground contact at each impact");
            var roll=DiceThrowMotion.Sample(.79f,0);Check(roll.Y==0 && Math.Abs(roll.Z)>.001f && roll.Settle==0 && Math.Abs(roll.SpinX)<1000,"grounded roll before rest");
            for(int i=0;i<10;i++){var p=DiceThrowMotion.Sample(i/10f,0,true);Check(p.Y==0 && p.Z==0 && p.Settle==1,"reduced motion skips throw");}
            return 4;
        }
    }
}
