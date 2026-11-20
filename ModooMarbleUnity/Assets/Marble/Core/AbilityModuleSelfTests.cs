using System;
using System.Linq;
namespace Marble.Core
{
    public static class AbilityModuleSelfTests
    {
        static void Check(bool value,string label){if(!value)throw new Exception("Ability modules: "+label);}
        static AbilityEvent E(long n,AbilityTrigger trigger=AbilityTrigger.TollPaid,int turn=0,int other=1,int tile=3,long value=400)=>new AbilityEvent(n,trigger,0,other,tile,turn,value);
        static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
        public static int Run()
        {
            int tests=0;
            Check(AbilityModules.All.Count==32&&AbilityModules.All.Select(m=>m.Id).Distinct().Count()==32,"catalog");tests++;
            var l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("toll.discount"));Check(l.Resolve(E(0)).Value==300,"discount");tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("toll.free"));Check(l.Resolve(E(0)).Value==0&&l.Resolve(E(1)).Value==400,"once only");tests++;
            var a=new AbilityLoadout(0,7);var b=new AbilityLoadout(0,7);a.Add(AbilityModules.Get("toll.discount"));a.Add(AbilityModules.Get("toll.flat"));b.Add(AbilityModules.Get("toll.flat"));b.Add(AbilityModules.Get("toll.discount"));Check(a.Resolve(E(0)).Value==200&&b.Resolve(E(0)).Value==200,"stable ordinal composition");tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("toll.flat"));Check(l.Resolve(E(0,value:2)).Value==0,"zero floor");tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("status.root"));Check(l.Resolve(E(0,AbilityTrigger.Attack,other:-1)).Intents.Count==0&&l.Resolve(E(1,AbilityTrigger.Attack)).Intents.Single().TargetIndex==1,"target required");tests++;
            l=new AbilityLoadout(0,7);l.Add(new AbilityModule("test","test",AbilityTrigger.Attack,AbilityTarget.Other,AbilityOperation.Status,0,cooldownTurns:2,maxUses:2,status:AbilityStatus.Bubble));
            Check(l.Resolve(E(0,AbilityTrigger.Attack)).Intents.Count==1&&l.Resolve(E(1,AbilityTrigger.Attack,1)).Intents.Count==0&&l.Resolve(E(2,AbilityTrigger.Attack,2)).Intents.Count==1&&l.Resolve(E(3,AbilityTrigger.Attack,4)).Intents.Count==0,"cooldown and charges");tests++;
            l=new AbilityLoadout(0,7);Check(l.Resolve(E(0)).Value==400&&Reject(()=>l.Resolve(E(0)))&&Reject(()=>l.Resolve(new AbilityEvent(1,AbilityTrigger.Tax,1,0,2,0,100))),"duplicate and owner rejection");tests++;
            a=new AbilityLoadout(0,77);b=new AbilityLoadout(0,77);a.Add(AbilityModules.Get("status.bubble"));b.Add(AbilityModules.Get("status.bubble"));int hits=0;
            for(int i=0;i<100;i++){var x=a.Resolve(E(i,AbilityTrigger.Attack,i));var y=b.Resolve(E(i,AbilityTrigger.Attack,i));Check(x.Activated.SequenceEqual(y.Activated),"deterministic probability");hits+=x.Activated.Count;}
            Check(hits>0&&hits<100,"probability distribution");tests++;
            Check(Reject(()=>new AbilityModule("x","x",AbilityTrigger.Attack,AbilityTarget.Other,AbilityOperation.Add,1))&&Reject(()=>new AbilityEvent(0,AbilityTrigger.Dice,0,0,2,0,1)),"invalid data");tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("move.backward"));var intent=l.Resolve(E(0,AbilityTrigger.Land)).Intents.Single();Check(intent.Amount==-1&&intent.TargetIndex==0&&intent.Operation==AbilityOperation.MoveRelative,"movement intent");tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("status.toll-boost"));intent=l.Resolve(E(0,AbilityTrigger.Land)).Intents.Single();Check(intent.TargetIndex==3&&intent.DurationTurns==2&&intent.Target==AbilityTarget.Tile,"tile duration intent");tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("salary.bonus"));Check(l.Resolve(E(0,AbilityTrigger.Salary,value:1000000000000)).Value==1000000000000,"numeric cap");tests++;
            foreach(var m in AbilityModules.All){l=new AbilityLoadout(0,1);l.Add(m);Check(Reject(()=>l.Add(m)),"duplicate grant");l.Resolve(E(0,m.Trigger));}tests++;
            l=new AbilityLoadout(0,7);l.Add(AbilityModules.Get("toll.free"));Check(l.Resolve(E(0,AbilityTrigger.Tax)).Activated.Count==0&&l.Resolve(E(1)).Value==0,"unrelated event preserves charge");tests++;
            l=new AbilityLoadout(0,7);l.Add(new AbilityModule("never","never",AbilityTrigger.TollPaid,AbilityTarget.Owner,AbilityOperation.Set,0,chancePercent:0));Check(l.Resolve(E(0)).Value==400,"zero chance");tests++;
            return tests;
        }
    }
}
