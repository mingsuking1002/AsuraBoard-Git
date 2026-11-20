using System;
using System.Linq;
namespace Marble.Core
{
    public enum AugmentTier { Bronze, Silver, Gold, Prism }
    [Serializable] public sealed class AugmentSelection
    {public int DraftIndex;public int OptionIndex;public AugmentTier Tier;public bool Rerolled,GoldenRerolled;}
    public sealed class AugmentOption
    {public string Name=>string.Empty;public string Description=>string.Empty;public string Icon=>string.Empty;}
    public sealed partial class Game
    {
        public const int MaxAugments=8;
        public static readonly AugmentOption[] BlankAugmentOptions={new AugmentOption(),new AugmentOption(),new AugmentOption()};
        int activeAugmentDraft=-1,completedAugmentMask,nextSpecialAugmentDraft=8;
        GamePhase augmentResumePhase;
        readonly int[] augmentRerollMasks=new int[4],goldenSlots={-1,-1,-1,-1};
        public const int GoldenRerollChancePercent=10;
        public int AugmentRerollMask(int seat)=>seat>=0 && seat<Players.Count?augmentRerollMasks[seat]:0;
        public int AugmentRevision(int seat){int mask=AugmentRerollMask(seat);return (mask&1)+((mask>>1)&1)+((mask>>2)&1);}
        public bool CanRerollAugment(int seat)=>HasPendingAugment(seat) && AugmentRerollMask(seat)!=7;
        public bool CanRerollAugment(int seat,int slot)=>slot>=0 && slot<3 && HasPendingAugment(seat) && (AugmentRerollMask(seat)&(1<<slot))==0;
        public int GoldenRerollSlot(int seat)=>seat>=0 && seat<Players.Count?goldenSlots[seat]:-1;
        public bool GoldenRerollOffered(int seat)
        {
            if(seat<0 || seat>=Players.Count || activeAugmentDraft<0)return false;
            unchecked{uint v=(uint)Config.Seed ^ ((uint)(activeAugmentDraft+1)*0x85EBCA6Bu) ^ ((uint)(seat+1)*0xC2B2AE35u);v^=v>>16;v*=0x7FEB352Du;v^=v>>15;return v%100<GoldenRerollChancePercent;}
        }
        public bool GoldenRerollAvailable(int seat)=>HasPendingAugment(seat) && GoldenRerollOffered(seat) && GoldenRerollSlot(seat)<0;
        public AugmentTier GetAugmentOfferTier(int seat,int slot)
        {
            if(seat<0 || seat>=Players.Count || slot<0 || slot>2 || activeAugmentDraft<0)throw new ArgumentOutOfRangeException();
            return (AugmentTier)Math.Min((int)AugmentTier.Prism,(int)GetAugmentTier(activeAugmentDraft)+(goldenSlots[seat]==slot?1:0));
        }
        public string AugmentRerollAction(int seat,int slot=0)=>"augment_reroll:"+activeAugmentDraft+":"+seat+":"+slot;
        public int AugmentDraftIndex=>activeAugmentDraft;
        public int AugmentCompletedMask=>completedAugmentMask;
        public int NextSpecialAugmentDraft=>nextSpecialAugmentDraft;
        public GamePhase AugmentResumePhase=>augmentResumePhase;
        public int RoundNumber=>Config.EnableBodyEconomy?EconomyRound:(TurnNumber-1)/Config.PlayerCount+1;
        public int AugmentActor=>Players.FindIndex(p=>HasPendingAugment(p.Index));
        public bool HasPendingAugment(int seat)=>Phase==GamePhase.Augment && activeAugmentDraft>=0 && seat>=0 && seat<Players.Count &&
            !Players[seat].Bankrupt && Players[seat].Augments.Count<MaxAugments && !Players[seat].Augments.Any(a=>a.DraftIndex==activeAugmentDraft);
        public bool CanPlayerAct(int seat)=>seat>=0 && seat<Players.Count && (Phase==GamePhase.Augment?HasPendingAugment(seat):Phase!=GamePhase.Finished && seat==CurrentPlayerIndex);
        public static int[] CreateAugmentSchedule(int count)
        {
            if(count<1 || count>MaxAugments)throw new ArgumentOutOfRangeException(nameof(count));
            return Enumerable.Range(0,count).Select(i=>1+i*5).ToArray();
        }
        public AugmentTier GetAugmentTier(int draftIndex)
        {
            if(draftIndex<0 || draftIndex>=16)throw new ArgumentOutOfRangeException(nameof(draftIndex));
            unchecked
            {
                uint value=(uint)Config.Seed ^ (0x9E3779B9u*((uint)draftIndex+1));
                value^=value>>16;value*=0x7FEB352Du;value^=value>>15;value*=0x846CA68Bu;value^=value>>16;
                return (AugmentTier)(value%4);
            }
        }
        public string AugmentAction(int seat,int option)=>"augment:"+activeAugmentDraft+":"+seat+":"+option+(AugmentRerollMask(seat)==0?"":":v"+AugmentRerollMask(seat));
        public string ChooseAugmentAction(int seat)=>HasPendingAugment(seat)?AugmentAction(seat,(seat+activeAugmentDraft)%3):null;
        static int[] ValidateAugmentRounds(int[] rounds)
        {
            if(rounds==null)return CreateAugmentSchedule(4);
            if(rounds.Length>MaxAugments || rounds.Any(r=>r<1 || r>10000) || !rounds.SequenceEqual(rounds.Distinct().OrderBy(r=>r)))
                throw new ArgumentException("Augment schedule requires up to eight unique, increasing positive rounds.");
            return (int[])rounds.Clone();
        }
        void EnterAugmentIfDue()
        {
            if(!Config.EnableAugments || Phase==GamePhase.Finished || Phase==GamePhase.Augment)return;
            for(int i=0;i<Config.AugmentRounds.Length;i++)
                if(Config.AugmentRounds[i]<=RoundNumber && (completedAugmentMask & (1<<i))==0)
                {
                    completedAugmentMask|=1<<i;
                    if(BeginAugmentDraft(i))return;
                }
        }
        bool BeginAugmentDraft(int draft)
        {
            if(!Players.Any(p=>!p.Bankrupt && p.Augments.Count<MaxAugments))return false;
            augmentResumePhase=Phase;activeAugmentDraft=draft;Array.Clear(augmentRerollMasks,0,augmentRerollMasks.Length);for(int i=0;i<goldenSlots.Length;i++)goldenSlots[i]=-1;Phase=GamePhase.Augment;
            Note("모든 플레이어 증강 동시 선택");return true;
        }
        // Reserved system action: callers must authorize it at the host, never expose it in player actions.
        public bool CanRequestSpecialAugment=>Config.EnableAugments && Phase==GamePhase.AwaitRoll && nextSpecialAugmentDraft<16 && Players.Any(p=>!p.Bankrupt && p.Augments.Count<MaxAugments);
        public bool RequestSpecialAugment()=>Execute("system:augment");
        bool ExecuteSpecialAugment()
        {
            if(!CanRequestSpecialAugment)return false;
            ReplayActions.Add("system:augment");return BeginAugmentDraft(nextSpecialAugmentDraft++);
        }
        bool TryExecuteAugment(string action)
        {
            if(action==null)return false;var parts=action.Split(':');
            if(parts.Length==4 && parts[0]=="augment_reroll" && int.TryParse(parts[1],out int rerollDraft) && int.TryParse(parts[2],out int rerollSeat) && int.TryParse(parts[3],out int slot) &&
                rerollDraft==activeAugmentDraft && CanRerollAugment(rerollSeat,slot) && action==AugmentRerollAction(rerollSeat,slot))
            {
                bool golden=GoldenRerollAvailable(rerollSeat);
                ReplayActions.Add(action);augmentRerollMasks[rerollSeat]|=1<<slot;if(golden)goldenSlots[rerollSeat]=slot;
                Note(Players[rerollSeat].Name+" · 카드 "+(slot+1)+(golden?" 황금 리롤 · 한 등급 상승":" 일반 리롤"));return true;
            }
            if((parts.Length!=4 && parts.Length!=5) || parts[0]!="augment" || !int.TryParse(parts[1],out int draft) || !int.TryParse(parts[2],out int seat) || !int.TryParse(parts[3],out int option) ||
                draft!=activeAugmentDraft || option<0 || option>2 || !HasPendingAugment(seat) || action!=AugmentAction(seat,option))return false;
            ReplayActions.Add(action);Players[seat].Augments.Add(new AugmentSelection{DraftIndex=draft,OptionIndex=option,Tier=GetAugmentOfferTier(seat,option),Rerolled=(AugmentRerollMask(seat)&(1<<option))!=0,GoldenRerolled=GoldenRerollSlot(seat)==option});
            Note(Players[seat].Name+" · 증강 선택 완료 ("+Players[seat].Augments.Count+" / "+MaxAugments+")");
            if(AugmentActor<0)
            {
                activeAugmentDraft=-1;Phase=augmentResumePhase;Note("전원 선택 완료 · 게임 재개");EnterAugmentIfDue();
            }
            return true;
        }
    }
}
