using System;
using System.Collections.Generic;
using System.Linq;
namespace Marble.Core
{
    public enum BodyPart { LeftEye,RightEye,LeftLeg,RightLeg,Mouth,Brain,Kidney,Liver,Lung,Heart }
    public sealed class BodyPartDefinition
    {
        public readonly BodyPart Part;public readonly string Name,Effect;public readonly long Price;
        public long RestorePrice=>Price*3/2;
        public BodyPartDefinition(BodyPart part,string name,long price,string effect){Part=part;Name=name;Price=price;Effect=effect;}
    }
    public static class BodyCatalog
    {
        public const int AllMask=1023;
        public static readonly BodyPartDefinition[] Parts={
            new BodyPartDefinition(BodyPart.LeftEye,"왼눈",400,"눈 외형 제거 · 왼쪽 시야 가림"),
            new BodyPartDefinition(BodyPart.RightEye,"오른눈",400,"눈 외형 제거 · 오른쪽 시야 가림"),
            new BodyPartDefinition(BodyPart.LeftLeg,"왼다리",700,"인공 다리 · 이동 칸마다 넘어짐 +1%p"),
            new BodyPartDefinition(BodyPart.RightLeg,"오른다리",700,"인공 다리 · 이동 칸마다 넘어짐 +1%p"),
            new BodyPartDefinition(BodyPart.Mouth,"입",400,"입 외형 제거 · 음성 먹먹함"),
            new BodyPartDefinition(BodyPart.Brain,"뇌 일부",900,"이동 중 확률로 1~2칸 역주행"),
            new BodyPartDefinition(BodyPart.Kidney,"신장",500,"가끔 웅덩이가 생기는 개그 연출"),
            new BodyPartDefinition(BodyPart.Liver,"간",700,"몸체 색상을 흙색으로 고정"),
            new BodyPartDefinition(BodyPart.Lung,"폐",600,"인공 호흡 장치 · 기계 호흡음"),
            new BodyPartDefinition(BodyPart.Heart,"심장",1500,"저가 인공심장 · 기계 소음")};
        public static BodyPartDefinition Get(BodyPart part)=>Parts[(int)part];
        public static bool IsSold(PlayerState player,BodyPart part)=>(player.SoldBodyMask&(1<<(int)part))!=0;
        public static int MissingEyes(PlayerState p)=>(IsSold(p,BodyPart.LeftEye)?1:0)+(IsSold(p,BodyPart.RightEye)?1:0);
        public static int MissingLegs(PlayerState p)=>(IsSold(p,BodyPart.LeftLeg)?1:0)+(IsSold(p,BodyPart.RightLeg)?1:0);
    }
    public sealed partial class Game
    {
        public const int BrainReverseChancePercent=10;
        public int EconomyRound{get;private set;}=1;
        public int PendingEconomySeat{get;private set;}=-1;
        public int LastMovementSeat{get;private set;}=-1;
        public int LastBrainReverseSteps{get;private set;}
        public int MovementSerial{get;private set;}
        public uint LastMovementFallMask{get;private set;}
        public readonly List<int> LastMovementPath=new List<int>();
        readonly Queue<int> repaymentQueue=new Queue<int>();
        public int[] PendingRepayments=>repaymentQueue.ToArray();
        public long InvestmentCapacity(int seat)=>Math.Max(0,Players[seat].Cash+(Config.EnableBodyEconomy?Config.CreditLimit:0));
        public bool CanInvest(int seat,long amount)=>amount>=0 && InvestmentCapacity(seat)>=amount;
        public bool CanSellBody(int seat,BodyPart part)=>Config.EnableBodyEconomy && seat==CurrentPlayerIndex && CanPlayerAct(seat) &&
            Phase!=GamePhase.Augment && !BodyCatalog.IsSold(Players[seat],part) &&
            (Phase==GamePhase.Repayment || Players[seat].LastBodySaleTurn!=TurnNumber);
        public bool CanRestoreBody(int seat,BodyPart part)=>Config.EnableBodyEconomy && seat==CurrentPlayerIndex && CanPlayerAct(seat) &&
            Phase!=GamePhase.Augment && Phase!=GamePhase.Repayment && BodyCatalog.IsSold(Players[seat],part) && Players[seat].Cash>=BodyCatalog.Get(part).RestorePrice;
        public static bool IsEconomyAction(string id)=>id!=null && (id.StartsWith("body_sell:",StringComparison.Ordinal) || id.StartsWith("body_restore:",StringComparison.Ordinal) || id.StartsWith("asset_sell:",StringComparison.Ordinal) || id=="repayment_confirm");
        void AddEconomyActions(List<GameAction> actions,int seat)
        {
            if(!Config.EnableBodyEconomy || Phase==GamePhase.Augment || Phase==GamePhase.Finished)return;
            foreach(var part in BodyCatalog.Parts)
            {
                if(CanSellBody(seat,part.Part))actions.Add(new GameAction("body_sell:"+(int)part.Part,part.Name+" 매각",-1,-part.Price));
                if(CanRestoreBody(seat,part.Part))actions.Add(new GameAction("body_restore:"+(int)part.Part,part.Name+" 복구",-1,part.RestorePrice));
            }
            foreach(var t in Tiles.Where(t=>t.OwnerIndex==seat))actions.Add(new GameAction("asset_sell:"+t.Index,t.Definition.Name+" 자발적 매각",t.Index,-GetLiquidationValue(t.Index)));
            if(Phase==GamePhase.Repayment)actions.Add(new GameAction("repayment_confirm",Players[seat].Cash>=0?"상환 완료 · 정산 확정":"미상환 탈락 확정"));
        }
        void ExecuteEconomyAction(string id,GameAction action)
        {
            var player=CurrentPlayer;
            if(id.StartsWith("body_sell:",StringComparison.Ordinal))
            {int part=int.Parse(id.Substring(10));player.SoldBodyMask|=1<<part;player.Cash+=BodyCatalog.Parts[part].Price;player.LastBodySaleTurn=TurnNumber;Note(BodyCatalog.Parts[part].Name+" 매각 · +"+BodyCatalog.Parts[part].Price);}
            else if(id.StartsWith("body_restore:",StringComparison.Ordinal))
            {int part=int.Parse(id.Substring(13));player.Cash-=BodyCatalog.Parts[part].RestorePrice;player.SoldBodyMask&=~(1<<part);Note(BodyCatalog.Parts[part].Name+" 복구");}
            else if(id.StartsWith("asset_sell:",StringComparison.Ordinal))
            {var tile=Tiles[action.TileIndex];player.Cash+=GetLiquidationValue(tile.Index);tile.OwnerIndex=-1;tile.Level=0;tile.FestivalMultiplier=1;tile.ResortArrivals=0;Note(tile.Definition.Name+" 자발적 매각");}
            SyncCreditAccounts();
            if(id=="repayment_confirm")
            {
                if(player.Cash<0)
                {
                    player.Bankrupt=true;player.IslandTurns=0;player.PendingTravel=false;player.CreditDueRound=0;
                    foreach(var tile in Tiles.Where(t=>t.OwnerIndex==player.Index)){tile.OwnerIndex=-1;tile.Level=0;tile.FestivalMultiplier=1;tile.ResortArrivals=0;}
                    Note(player.Name+" 채무 만기 미상환 · 탈락");
                }
                else Note(player.Name+" 상환 완료");
                NextRepaymentOrResume();
            }
        }
        void SyncCreditAccounts()
        {
            if(!Config.EnableBodyEconomy)return;
            foreach(var p in Players.Where(p=>!p.Bankrupt))
            {
                if(p.Cash>=0)p.CreditDueRound=0;
                else if(p.CreditDueRound==0){p.CreditDueRound=EconomyRound+Config.CreditTermRounds;Note(p.Name+" 마이너스 통장 · "+p.CreditDueRound+"R 종료 만기");}
            }
        }
        void EndEconomyTurn()
        {
            SyncCreditAccounts();int old=CurrentPlayerIndex,next=old;
            do{next=(next+1)%Players.Count;}while(Players[next].Bankrupt);
            PendingEconomySeat=next;
            if(next<=old)
            {
                foreach(var p in Players.Where(p=>!p.Bankrupt && p.Cash<0))
                {
                    long debt=-p.Cash,interest=debt/100*Config.CreditInterestPercent+(debt%100*Config.CreditInterestPercent+99)/100;
                    p.Cash-=interest;Note(p.Name+" "+EconomyRound+"R 이자 -"+interest);
                    if(p.CreditDueRound<=EconomyRound)repaymentQueue.Enqueue(p.Index);
                }
                NextRepaymentOrResume();return;
            }
            ResumeEconomyTurn(false);
        }
        void NextRepaymentOrResume()
        {
            while(repaymentQueue.Count>0)
            {
                int seat=repaymentQueue.Dequeue();if(Players[seat].Bankrupt || Players[seat].Cash>=0)continue;
                CurrentPlayerIndex=seat;Phase=GamePhase.Repayment;Note(CurrentPlayer.Name+" 최종 상환 · 신체 여러 부위와 부동산 매각 가능");return;
            }
            ResumeEconomyTurn(true);
        }
        void ResumeEconomyTurn(bool closedRound)
        {
            int alive=Players.Count(p=>!p.Bankrupt);
            if(alive==0){WinnerIndex=-1;WinnerReason="전원 채무 만기 미상환";Phase=GamePhase.Finished;Note("전원 탈락 · 승자 없음");PendingEconomySeat=-1;return;}
            if(alive==1){Finish(Players.Find(p=>!p.Bankrupt).Index,"최후의 생존자");PendingEconomySeat=-1;return;}
            if(TurnNumber>=Config.MaxTurns || (closedRound && EconomyRound>=Config.MaxEconomyRounds))
            {Finish(Players.Where(p=>!p.Bankrupt).OrderByDescending(p=>GetNetWorth(p.Index)).ThenBy(p=>p.Index).First().Index,"제한 라운드 종료 · 총자산 1위");PendingEconomySeat=-1;return;}
            if(closedRound)EconomyRound++;
            CurrentPlayerIndex=PendingEconomySeat;while(Players[CurrentPlayerIndex].Bankrupt)CurrentPlayerIndex=(CurrentPlayerIndex+1)%Players.Count;
            PendingEconomySeat=-1;TurnNumber++;Phase=CurrentPlayer.PendingTravel?GamePhase.Travel:GamePhase.AwaitRoll;
            Note(CurrentPlayer.Name+" 차례 · "+EconomyRound+"R");EnterAugmentIfDue();
        }
        string ChooseEconomyRescue(List<GameAction> actions)
        {
            if(Phase!=GamePhase.Repayment)return null;
            if(CurrentPlayer.Cash>=0)return "repayment_confirm";
            var body=actions.Where(a=>a.Id.StartsWith("body_sell:",StringComparison.Ordinal)).OrderBy(a=>a.Cost).FirstOrDefault();
            if(body!=null)return body.Id;
            return actions.Where(a=>a.Id.StartsWith("asset_sell:",StringComparison.Ordinal)).OrderByDescending(a=>-a.Cost).Select(a=>a.Id).FirstOrDefault()??"repayment_confirm";
        }
        void MoveDiceSteps(int steps)
        {
            if(!Config.EnableBodyEconomy){MoveSteps(steps);return;}
            int reverse=BodyCatalog.IsSold(CurrentPlayer,BodyPart.Brain) && Next(0,100)<BrainReverseChancePercent?Next(1,3):0;
            reverse=Math.Min(reverse,steps-steps/2);
            LastMovementPath.Clear();LastMovementFallMask=0;LastBrainReverseSteps=reverse;LastMovementSeat=CurrentPlayerIndex;MovementSerial++;
            bool salaryPaid=false;
            for(int i=0;i<steps;i++)
            {
                int direction=i>=steps/2 && i<steps/2+reverse?-1:1;
                int previous=CurrentPlayer.Position;
                int position=(previous+direction+Tiles.Count)%Tiles.Count;
                if(direction>0 && position==0 && !salaryPaid){CurrentPlayer.Laps++;ReceiveSalary(Config.Salary);salaryPaid=true;}
                CurrentPlayer.Position=position;LastMovementPath.Add(position);
                if(BodyCatalog.MissingLegs(CurrentPlayer)>0 && Next(0,100)<BodyCatalog.MissingLegs(CurrentPlayer))LastMovementFallMask|=1u<<i;
                RecordMovement(previous,position,false,(LastMovementFallMask&(1u<<i))!=0);
            }
            if(reverse>0)Note("인공 뇌 오류 · "+reverse+"칸 역주행");
        }
        internal void TestAdvanceEconomyTurn(){EndTurn();}
        internal void TestCreditSync(){SyncCreditAccounts();}
        internal void TestBodyMovement(int steps){MoveDiceSteps(steps);}
    }
}
