using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Marble.Core;

internal static class BalanceRunner
{
    static readonly string[] Card = { "절약 계약", "정기 수입", "위험 투자", "견제 사격", "보호막" };
    static readonly string[] Styles = { "공격", "방어", "금전", "도박", "시너지" };
    static readonly int[,] Preference = { {1,1,1,5,1}, {4,2,0,1,5}, {4,5,1,0,1}, {1,2,5,2,0}, {2,2,2,3,3} };

    sealed class Match
    {
        readonly Game game;
        readonly int seed, map, noise, rotation, forced;
        readonly int[,] held = new int[4,5];
        readonly List<object> choices = new List<object>();
        readonly List<object> events = new List<object>();
        int victimSeat;
        public Match(int seed, int map, int noise, int rotation, int forced)
        {
            this.seed=seed; this.map=map; this.noise=noise; this.rotation=rotation; this.forced=forced;
            victimSeat=rotation%4;
            game=new Game(new GameConfig { Seed=seed, MapId=map, PlayerCount=4, HumanCount=0,
                EnableAugments=true, EnableBodyEconomy=true, AugmentRounds=Game.CreateAugmentSchedule(4) });
            game.TollAdjustment=DiscountToll;
        }
        Random RandomFor(int purpose, int seat, int step) =>
            new Random(unchecked(seed*1099087573 + purpose*100003 + (seat+1)*7919 + step*104729));
        int Style(int seat)=>(rotation+seat)%Styles.Length;
        void Effect(int seat, int card, int target=-1, long cash=0, long rivalCash=0, int damage=0,
                    int prevented=0, int excess=0, int island=0, long? expectedCash=null, string synergy=null)
        {
            events.Add(new { turn=game.TurnNumber, round=game.RoundNumber, seat, card, target, cash, rivalCash,
                damage, prevented, excess, island, expectedCash, synergy });
        }
        long DiscountToll(int seat,int owner,long bill)
        {
            int rank=held[seat,0];
            if(rank==0)return bill;
            long saving=bill*(15L+5L*rank)/100;
            if(saving>0)Effect(seat,0,owner,cash:saving,rivalCash:-saving);
            return bill-saving;
        }
        void Grant(int seat, int slot, int[] offers, double[] scores, bool normal)
        {
            int card=offers[slot];
            int tier=(int)game.GetAugmentOfferTier(seat,slot);
            int draft=game.AugmentDraftIndex;
            var previews=offers.Select((id,i)=>id==1?(long?)(35+5*(int)game.GetAugmentOfferTier(seat,i)):
                id==2?0L:null).ToArray();
            if(!game.Execute(game.AugmentAction(seat,slot)))throw new InvalidOperationException("Invalid draft pick");
            if(card>=0)held[seat,card]=Math.Max(held[seat,card],tier+1);
            choices.Add(new { turn=game.TurnNumber, seat, draft, offers=(int[])offers.Clone(), scores, previews, card, slot, tier, normal });
        }
        void Draft()
        {
            int seat=game.AugmentActor, draft=game.AugmentDraftIndex;
            var rng=RandomFor(1,seat,draft);
            var offers=Enumerable.Range(0,Card.Length).OrderBy(_=>rng.Next()).Take(3).ToArray();
            bool normal=forced==-2 || seat!=victimSeat || draft!=0;
            if(!normal)offers[0]=forced;
            for(int tries=0;;tries++)
            {
                var scores=offers.Select((id,slot)=> id<0?0:
                    Preference[Style(seat),id]*2 + (int)game.GetAugmentOfferTier(seat,slot)*.55
                    - (held[seat,id]>0?6:0)
                    + (Style(seat)==4 && ((id==3 && held[seat,4]>0)||(id==4 && held[seat,3]>0))?5:0)
                    + (noise==0?0:(rng.NextDouble()*2-1)*noise*2)).ToArray();
                int slot=normal?Enumerable.Range(0,3).OrderByDescending(i=>scores[i]).First():0;
                if(normal && tries==0 && scores[slot]<7)
                {
                    int replace=Enumerable.Range(0,3).OrderBy(i=>scores[i]).First();
                    var replacements=Enumerable.Range(0,Card.Length).Except(offers).ToArray();
                    int next=replacements[rng.Next(replacements.Length)];
                    if(!game.Execute(game.AugmentRerollAction(seat,replace)))throw new InvalidOperationException("Invalid reroll");
                    choices.Add(new { turn=game.TurnNumber,seat,draft,reroll=replace,before=(int[])offers.Clone(),scores,replacement=next,golden=game.GoldenRerollSlot(seat)==replace });
                    offers[replace]=next;
                    continue;
                }
                Grant(seat,slot,offers,scores,normal);
                return;
            }
        }
        void TurnEffects(int seat)
        {
            var player=game.Players[seat];
            int income=held[seat,1], gamble=held[seat,2], attack=held[seat,3];
            if(income>0)
            {
                long amount=35+5*(income-1);
                player.Cash+=amount;
                Effect(seat,1,cash:amount,expectedCash:amount);
            }
            if(gamble>0 && player.Cash>=50 && (Style(seat)==3 || player.Cash>=250))
            {
                long stake=30+5*gamble;
                long amount=RandomFor(2,seat,game.TurnNumber).Next(2)==0?stake:-stake;
                player.Cash+=amount;
                Effect(seat,2,cash:amount,expectedCash:0);
            }
            if (attack==0 || player.IslandTurns>0 || game.TurnNumber%3!=0)return;
            var eligible=game.Players.Where(p=>p.Index!=seat && !p.Bankrupt && p.Health>0 && p.IslandTurns==0);
            var target=(Style(seat)==0 || Style(seat)==4
                ? eligible.OrderBy(p=>p.Health).ThenByDescending(p=>game.GetNetWorth(p.Index))
                : eligible.OrderByDescending(p=>game.GetNetWorth(p.Index)).ThenBy(p=>p.Health)).FirstOrDefault();
            if(target==null)return;
            int shield=held[target.Index,4];
            int blocked=shield==0?0:8+shield*3;
            int bonus=held[seat,4]>0?4:0;
            int raw=16+attack*4+bonus;
            blocked=Math.Min(raw,blocked);
            int before=target.Health;
            int applied=raw-blocked;
            if(applied>0 && !game.ApplyHealthDamage(target.Index,applied))throw new InvalidOperationException("Damage rejected");
            if(blocked>0)Effect(target.Index,4,seat,prevented:blocked);
            Effect(seat,3,target.Index,damage:before-target.Health,excess:Math.Max(0,applied-before),
                island:before>0 && target.Health==0?1:0,synergy:bonus>0?"attack+shield":null);
        }
        string ChooseTurnAction(int seat)
        {
            string action=game.ChooseAiAction();
            if(game.Phase==GamePhase.Bonus && Style(seat)==3 && game.GetActions().Any(a=>a.Id=="bonus:tails"))
                return "bonus:tails";
            if(game.Phase!=GamePhase.Decision)return action;
            var options=game.GetActions();
            var selected=options.FirstOrDefault(a=>a.Id==action);
            if(Style(seat)==1 && selected!=null && selected.Cost>0 && game.CurrentPlayer.Cash-selected.Cost<400 &&
                options.Any(a=>a.Id=="end"))
                return "end";
            if(Style(seat)==2)
            {
                var purchase=options.Where(a=>a.Id.StartsWith("purchase:",StringComparison.Ordinal) &&
                    game.CurrentPlayer.Cash-a.Cost>=100).OrderBy(a=>a.Cost).FirstOrDefault();
                if(purchase!=null)return purchase.Id;
            }
            return action;
        }
        public object Run()
        {
            var last=new int[4];
            int actions=0;
            while(game.Phase!=GamePhase.Finished)
            {
                if(++actions>12000)throw new InvalidOperationException("Match exceeded action limit: "+seed);
                if(game.Phase==GamePhase.Augment){Draft();continue;}
                int seat=game.CurrentPlayerIndex;
                if(game.Phase==GamePhase.AwaitRoll && last[seat]!=game.TurnNumber)
                {
                    last[seat]=game.TurnNumber;
                    TurnEffects(seat);
                }
                string action=ChooseTurnAction(seat);
                if(!game.Execute(action))throw new InvalidOperationException("Invalid action: "+action);
            }
            return new { seed,map,noise,rotation,forced,subject=victimSeat,turns=game.TurnNumber,winner=game.WinnerIndex,
                seats=game.GetStandings().Select(s=>new { seat=s.PlayerIndex, place=s.Place, netWorth=s.NetWorth,
                    profile=Styles[Style(s.PlayerIndex)], health=game.Players[s.PlayerIndex].Health,
                    cards=Enumerable.Range(0,Card.Length).Where(id=>held[s.PlayerIndex,id]>0).ToArray() }).ToArray(),
                choices,events };
        }
    }

    static int Main(string[] args)
    {
        string output=args[0];
        int count=int.Parse(args[1]), firstSeed=int.Parse(args[2]);
        try
        {
            using var writer=new StreamWriter(output);
            if(args.Length==7)
            {
                writer.WriteLine(JsonSerializer.Serialize(new Match(firstSeed,int.Parse(args[3]),int.Parse(args[4]),
                    int.Parse(args[5]),int.Parse(args[6])).Run()));
                return 0;
            }
            for(int i=0;i<count;i++)
                writer.WriteLine(JsonSerializer.Serialize(new Match(firstSeed+i,i%2,i%3,i%20,-2).Run()));
            for(int trial=0;trial<count/10;trial++)
            {
                int seed=firstSeed+count+trial, rotation=trial%20;
                for(int card=-1;card<Card.Length;card++)
                    writer.WriteLine(JsonSerializer.Serialize(new Match(seed,trial%2,trial%3,rotation,card).Run()));
            }
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
