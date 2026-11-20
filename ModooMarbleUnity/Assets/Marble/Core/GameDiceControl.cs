using System;
namespace Marble.Core
{
    // Gauge and parity affect probabilities, never a guaranteed destination.
    public sealed partial class Game
    {
        public int LastDiceTarget{get;private set;}
        public int LastDiceParity{get;private set;}
        public int LastDiceActor{get;private set;}=-1;
        public bool LastDiceControlled{get;private set;}
        public static bool IsRollAction(string id)=>id=="roll" || (id!=null && id.StartsWith("roll_control:",StringComparison.Ordinal));
        public static string DiceControlAction(int power,int parity)=>"roll_control:"+power+":"+parity;
        public static int DiceControlTarget(int power,int parity)
        {
            if(power<0 || power>10 || parity<0 || parity>2)throw new ArgumentOutOfRangeException();
            int target=2+power;if(parity==1 && target%2==0)target+=target==12?-1:1;if(parity==2 && target%2!=0)target++;return target;
        }
        public static int DiceControlWeight(int a,int b,int power,int parity)
        {
            if(a<1 || a>6 || b<1 || b>6 || power<0 || power>10 || parity<0 || parity>2)throw new ArgumentOutOfRangeException();
            int sum=a+b;if(parity==1 && sum%2==0 || parity==2 && sum%2!=0)return 0;
            int proximity=13-Math.Abs(sum-DiceControlTarget(power,parity));return proximity*proximity*proximity;
        }
        void RollControlled(int power,int parity)
        {
            int total=0;for(int a=1;a<=6;a++)for(int b=1;b<=6;b++)total+=DiceControlWeight(a,b,power,parity);
            int pick=Next(0,total);
            for(int a=1;a<=6;a++)for(int b=1;b<=6;b++)
            {
                pick-=DiceControlWeight(a,b,power,parity);if(pick>=0)continue;
                DiceA=a;DiceB=b;LastDiceControlled=true;LastDiceTarget=DiceControlTarget(power,parity);LastDiceParity=parity;LastDiceActor=CurrentPlayerIndex;
                ProcessRoll();return;
            }
        }
    }
}
