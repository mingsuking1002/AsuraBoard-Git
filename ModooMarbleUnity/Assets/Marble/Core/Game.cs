using System;
using System.Collections.Generic;
using System.Linq;

namespace Marble.Core
{
    public enum TileKind { Start, City, Resort, Chance, Island, Olympics, Travel, Tax, Bonus }
    public enum GamePhase { AwaitRoll, Decision, Travel, Olympics, Debt, Finished, Bonus, Augment, Repayment }
    public static class GameLabels
    {
        public const string EventCard="돌발 카드";
        public const string Bonus="한탕 찬스";
        public const string Festival="도시 축제";
    }

    [Serializable]
    public sealed class GameConfig
    {
        public int Seed = 20261025;
        public int HumanCount = 1;
        public int PlayerCount = 4;
        public int MaxTurns = 240;
        public long StartingCash = 2000;
        public long Salary = 300;
        public long IslandFee = 100;
        public long TravelFee = 100;
        public long Tax = 150;
        public bool ColorSetWins = false;
        public bool EnableAugments = false;
        public int MapId;
        public bool EndOnFirstBankruptcy;
        public bool EnableBodyEconomy;
        public long CreditLimit=1000;
        public int CreditTermRounds=3,CreditInterestPercent=5,MaxEconomyRounds=60;
        public int[] AugmentRounds = new[] { 1, 6, 11, 16 };
    }

    [Serializable]
    public sealed class TileDefinition
    {
        public string Name;
        public TileKind Kind;
        public int Group;
        public long Price;
        public long[] BuildCosts;
        public long[] BaseTolls;
        public int ResortType;
        public TileDefinition(string name, TileKind kind, int group = -1, long price = 0,
            long[] buildCosts = null, long[] baseTolls = null, int resortType = 0)
        {
            Name = name; Kind = kind; Group = group; Price = price;
            BuildCosts = buildCosts == null ? new long[4] : (long[])buildCosts.Clone();
            BaseTolls = baseTolls == null ? new long[] { 0, 0, 0, 0, 0 } : (long[])baseTolls.Clone();
            ResortType=resortType;
        }
    }

    [Serializable]
    public sealed class TileState
    {
        public int Index;
        public TileDefinition Definition;
        public int OwnerIndex = -1;
        public int Buildings;
        public bool Landmark;
        public int Level
        {
            get=>Landmark?4:(Buildings&4)!=0?3:(Buildings&2)!=0?2:(Buildings&1)!=0?1:0;
            // Compatibility for test fixtures and explicit whole-property initialization.
            set{Landmark=value>=4;Buildings=value>=4?7:value<=0?0:(1<<value)-1;}
        }
        public bool HasBuilding(int type)=>type>=1 && type<=3 && (Buildings & (1<<(type-1)))!=0;
        public int FestivalMultiplier = 1;
        public int ResortArrivals;
        public bool IsPurchasable { get { return Definition.Kind == TileKind.City || Definition.Kind == TileKind.Resort; } }
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int Index;
        public int Avatar,ColorIndex,DiceSkin;
        public int Outfit=WardrobeCatalog.DefaultCode;
        public string Name;
        public long Cash;
        public int Position;
        public int Laps;
        public int IslandTurns;
        public int EscapeCards;
        public int ShieldCards;
        public bool PendingTravel;
        public bool LoanUsed;
        public long LoanBalance;
        public bool Bankrupt;
        public bool IsHuman;
        public int SoldBodyMask,LastBodySaleTurn=-1,CreditDueRound;
        public readonly List<AugmentSelection> Augments = new List<AugmentSelection>();
    }

    public sealed class GameAction
    {
        public string Id;
        public string Label;
        public int TileIndex;
        public long Cost;
        public GameAction(string id, string label, int tileIndex = -1, long cost = 0)
        { Id = id; Label = label; TileIndex = tileIndex; Cost = cost; }
    }

    // Pure deterministic game model; the view animates state changes without changing rules.
    public sealed partial class Game
    {
        public readonly GameConfig Config;
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<TileState> Tiles = new List<TileState>();
        public readonly List<string> History = new List<string>();
        public readonly List<string> ReplayActions = new List<string>();
        public int CurrentPlayerIndex { get; private set; }
        public GamePhase Phase { get; private set; }
        public int DiceA { get; private set; }
        public int DiceB { get; private set; }
        public int WinnerIndex { get; private set; } = -1;
        public string WinnerReason { get; private set; } = "";
        public string Message { get; private set; }
        public int TurnNumber { get; private set; } = 1;
        public long DebtAmount { get; private set; }
        public int DebtCreditorIndex { get; private set; } = -1;
        public PlayerState CurrentPlayer { get { return Players[CurrentPlayerIndex]; } }
        public bool ExtraRollPending { get { return _extraRoll; } }
        private uint _rng;
        private int _consecutiveDoubles;
        private bool _extraRoll;
        private bool _builtThisLanding;
        private bool _canTakeover;
        private Action _afterDebt;

        public Game(int seed = 20261025, int humanCount = 1, int playerCount = 4,
            int maxTurns = 240, TileDefinition[] definitions = null)
            : this(new GameConfig { Seed = seed, HumanCount = humanCount, PlayerCount = playerCount, MaxTurns = maxTurns }, definitions) { }

        public Game(GameConfig config, TileDefinition[] definitions = null)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (config.PlayerCount < 2 || config.PlayerCount > 4 || config.HumanCount < 0 || config.HumanCount > config.PlayerCount)
                throw new ArgumentOutOfRangeException("config", "2–4 players and valid human count required.");
            if(config.EnableBodyEconomy && (config.MaxEconomyRounds<1 || config.MaxEconomyRounds>10000))throw new ArgumentOutOfRangeException("economy round limit");
            if(config.EnableBodyEconomy && (config.CreditLimit<0 || config.CreditLimit>1000000000 || config.CreditTermRounds<1 || config.CreditTermRounds>100 || config.CreditInterestPercent<0 || config.CreditInterestPercent>100))throw new ArgumentOutOfRangeException("credit config");
            if (config.StartingCash <= 0 || config.Salary < 0 || config.MaxTurns < 1 || config.IslandFee < 0 || config.TravelFee < 0 || config.Tax < 0)
                throw new ArgumentOutOfRangeException("config");
            Config = new GameConfig { Seed=config.Seed, HumanCount=config.HumanCount, PlayerCount=config.PlayerCount,
                MaxTurns=config.MaxTurns, StartingCash=config.StartingCash, Salary=config.Salary, IslandFee=config.IslandFee,
                TravelFee=config.TravelFee, Tax=config.Tax, ColorSetWins=config.ColorSetWins, EnableAugments=config.EnableAugments,
                AugmentRounds=ValidateAugmentRounds(config.AugmentRounds),MapId=config.MapId,EndOnFirstBankruptcy=config.EndOnFirstBankruptcy,EnableBodyEconomy=config.EnableBodyEconomy,CreditLimit=config.CreditLimit,CreditTermRounds=config.CreditTermRounds,CreditInterestPercent=config.CreditInterestPercent,MaxEconomyRounds=config.MaxEconomyRounds };
            _rng = unchecked((uint)Config.Seed) ^ 0x9E3779B9u;
            if (_rng == 0) _rng = 1;
            var board = definitions ?? BoardCatalog.Create(Config.MapId);
            if (board.Length != 32 || board.Any(d => d == null || d.Price < 0 || d.BuildCosts == null || d.BaseTolls == null || d.BuildCosts.Length != 4 || d.BaseTolls.Length != 5 ||
                d.BuildCosts.Any(v=>v<0) || d.BaseTolls.Any(v=>v<0))) throw new ArgumentException("32 valid board definitions required.");
            if (board.Count(d=>d.Kind==TileKind.Start)!=1 || board[0].Kind!=TileKind.Start || board.Count(d=>d.Kind==TileKind.Island)!=1 || board.Count(d=>d.Kind==TileKind.Travel)!=1)
                throw new ArgumentException("Board requires start at zero and exactly one island/travel tile.");
            for (int i = 0; i < board.Length; i++) Tiles.Add(new TileState { Index=i,
                Definition=new TileDefinition(board[i].Name,board[i].Kind,board[i].Group,board[i].Price,board[i].BuildCosts,board[i].BaseTolls,board[i].ResortType) });
            for (int i = 0; i < Config.PlayerCount; i++) Players.Add(new PlayerState { Index=i,
                Avatar=i,ColorIndex=i,Name = i < Config.HumanCount ? "플레이어 " + (i+1) : "CPU " + (i-Config.HumanCount+1), Cash=Config.StartingCash, IsHuman=i<Config.HumanCount });
            Phase = GamePhase.AwaitRoll;
            Note("기본판 시작 · " + CurrentPlayer.Name + " 차례");
            EnterAugmentIfDue();
        }

        public List<GameAction> GetActions()=>GetActionsForPlayer(Phase==GamePhase.Augment?AugmentActor:CurrentPlayerIndex);
        public List<GameAction> GetActionsForPlayer(int seat)
        {
            var a = new List<GameAction>();
            if (!CanPlayerAct(seat)) return a;
            var p = CurrentPlayer;
            if (Phase == GamePhase.Augment)
            {
                for(int i=0;i<3;i++)a.Add(new GameAction(AugmentAction(seat,i),"선택지 "+(i+1)));
                for(int i=0;i<3;i++)if(CanRerollAugment(seat,i))a.Add(new GameAction(AugmentRerollAction(seat,i),"카드 "+(i+1)+" 리롤"));
            }
            else if (Phase == GamePhase.AwaitRoll)
            {
                for(int power=0;power<=10;power++)for(int parity=0;parity<3;parity++)a.Add(new GameAction(DiceControlAction(power,parity),"주사위 컨트롤"));
                a.Add(new GameAction("roll", p.IslandTurns > 0 ? "주사위로 무인도 탈출" : "주사위 굴리기"));
                if (p.IslandTurns > 0 && p.Cash >= Config.IslandFee) a.Add(new GameAction("island_pay", "탈출 비용 지불 후 굴리기",-1,Config.IslandFee));
                if (p.IslandTurns > 0 && p.EscapeCards > 0) a.Add(new GameAction("island_card", "무인도 탈출 카드 사용"));
            }
            else if (Phase == GamePhase.Debt)
            {
                foreach (var t in Tiles.Where(t=>t.OwnerIndex==p.Index && t.Level<4))
                    a.Add(new GameAction("sell:"+t.Index, t.Definition.Name+" 매각 (+"+GetLiquidationValue(t.Index)+")",t.Index,-GetLiquidationValue(t.Index)));
                if(!p.LoanUsed) a.Add(new GameAction("loan","긴급 대출 · 이번 지불금 부족분 충당",-1,0));
                a.Add(new GameAction("bankrupt", "파산 선언"));
            }
            else if (Phase == GamePhase.Travel)
            {
                foreach (var t in Tiles.Where(t=>t.Index!=p.Position))
                    a.Add(new GameAction("travel:"+t.Index,t.Definition.Name+" 이동",t.Index,Config.TravelFee));
                a.Add(new GameAction("travel_cancel","세계여행 건너뛰기"));
            }
            else if (Phase == GamePhase.Olympics)
            {
                foreach (var t in Tiles.Where(t=>t.OwnerIndex==p.Index && t.IsPurchasable))
                    a.Add(new GameAction("festival:"+t.Index,t.Definition.Name+" 도시 축제 개최",t.Index));
                a.Add(new GameAction("end","개최 건너뛰기"));
            }
            else if(Phase == GamePhase.Bonus)
            {
                if(p.Cash>=100)
                {
                    a.Add(new GameAction("bonus:heads",GameLabels.Bonus+" · 앞면",p.Position,100));
                    a.Add(new GameAction("bonus:tails",GameLabels.Bonus+" · 뒷면",p.Position,100));
                }
                a.Add(new GameAction("end",GameLabels.Bonus+" 건너뛰기"));
            }
            else if (Phase == GamePhase.Decision)
            {
                var t = Tiles[p.Position];
                if(t.IsPurchasable && t.OwnerIndex<0 && CanInvest(seat,t.Definition.Price))
                {
                    a.Add(new GameAction("buy","땅 구매 · "+t.Definition.Name,t.Index,t.Definition.Price));
                    if(t.Definition.Kind==TileKind.City)
                        foreach(int mask in LegalBuildingMasks(t,p))
                        {
                            long cost=t.Definition.Price+BuildingPrice(t,mask);
                            if(CanInvest(seat,cost))a.Add(new GameAction("purchase:"+mask,"땅 + "+BuildingNames(mask),t.Index,cost));
                        }
                }
                else if(t.OwnerIndex==p.Index && t.Definition.Kind==TileKind.City && !_builtThisLanding && !t.Landmark)
                {
                    foreach(int mask in LegalBuildingMasks(t,p))
                    {
                        long cost=BuildingPrice(t,mask);
                        if(CanInvest(seat,cost))
                        {
                            a.Add(new GameAction("construct:"+mask,BuildingNames(mask)+" 건설",t.Index,cost));
                            if((mask & (mask-1))==0)
                            {int type=mask==1?1:mask==2?2:3;a.Add(new GameAction("build:"+type,BuildName(type)+" 건설",t.Index,cost));}
                        }
                    }
                    if(t.Buildings==7 && CanInvest(seat,t.Definition.BuildCosts[3]))a.Add(new GameAction("build:4","랜드마크 건설",t.Index,t.Definition.BuildCosts[3]));
                }
                else if(_canTakeover && t.OwnerIndex>=0 && t.OwnerIndex!=p.Index && !t.Landmark && CanInvest(seat,GetTakeoverPrice(t.Index)))
                    a.Add(new GameAction("takeover","인수 · "+t.Definition.Name,t.Index,GetTakeoverPrice(t.Index)));
                if(t.Definition.Kind==TileKind.Start && !_builtThisLanding)
                    foreach(var owned in Tiles.Where(x=>x.OwnerIndex==p.Index && x.Definition.Kind==TileKind.City && !x.Landmark))
                    {
                        foreach(int mask in LegalBuildingMasks(owned,p))
                        {
                            long cost=BuildingPrice(owned,mask);
                            if(CanInvest(seat,cost))a.Add(new GameAction("remote:"+owned.Index+":"+mask,owned.Definition.Name+" · "+BuildingNames(mask)+" 원격 건설",owned.Index,cost));
                        }
                        if(owned.Buildings==7 && CanInvest(seat,owned.Definition.BuildCosts[3]))a.Add(new GameAction("remote:"+owned.Index+":8",owned.Definition.Name+" · 랜드마크 원격 건설",owned.Index,owned.Definition.BuildCosts[3]));
                    }
                a.Add(new GameAction("end",_extraRoll ? "계속 · 더블 추가 굴림" : "차례 종료"));
            }
            AddEconomyActions(a,seat);return a;
        }

        public bool Execute(string actionId)
        {
            if(actionId=="system:augment")return ExecuteSpecialAugment();
            if(Phase==GamePhase.Augment)return TryExecuteAugment(actionId);
            var action = GetActions().FirstOrDefault(a=>a.Id==actionId);
            if (action==null) return false;
            BeginActionMovement();
            ReplayActions.Add(actionId);
            var p = CurrentPlayer;
            if(IsEconomyAction(actionId))ExecuteEconomyAction(actionId,action);
            else if (actionId=="roll") Roll();
            else if(actionId.StartsWith("roll_control:",StringComparison.Ordinal)){var parts=actionId.Split(':');RollControlled(int.Parse(parts[1]),int.Parse(parts[2]));}
            else if (actionId=="island_pay") { p.Cash-=Config.IslandFee; p.IslandTurns=0; Note("탈출 비용 "+Config.IslandFee+" 지불"); }
            else if (actionId=="island_card") { p.EscapeCards--; p.IslandTurns=0; Note("무인도 탈출 카드 사용"); }
            else if(actionId.StartsWith("purchase:",StringComparison.Ordinal))
            {
                var t=Tiles[p.Position];int mask=int.Parse(actionId.Substring(9));
                p.Cash-=action.Cost;t.OwnerIndex=p.Index;t.Buildings=mask;t.Landmark=false;_builtThisLanding=true;
                Note(t.Definition.Name+" · 땅 구매 및 "+BuildingNames(mask)+" 건설");CheckVictory(p.Index);
            }
            else if(actionId.StartsWith("construct:",StringComparison.Ordinal))
            {
                var t=Tiles[p.Position];int mask=int.Parse(actionId.Substring(10));
                p.Cash-=action.Cost;t.Buildings|=mask;_builtThisLanding=true;
                Note(t.Definition.Name+" · "+BuildingNames(mask)+" 건설");CheckVictory(p.Index);
            }
            else if (actionId=="buy")
            {
                var t=Tiles[p.Position]; p.Cash-=t.Definition.Price; t.OwnerIndex=p.Index; t.Level=0;
                Note(p.Name+" · "+t.Definition.Name+" 구매"); CheckVictory(p.Index);
            }
            else if (actionId.StartsWith("build:",StringComparison.Ordinal))
            {
                var t=Tiles[p.Position]; int level=int.Parse(actionId.Substring(6));
                p.Cash-=action.Cost;if(level==4)t.Landmark=true;else t.Buildings|=1<<(level-1);_builtThisLanding=true;
                Note(t.Definition.Name+" · "+BuildName(level)+" 건설"); CheckVictory(p.Index);
            }
            else if (actionId=="takeover")
            {
                var t=Tiles[p.Position]; long cost=GetTakeoverPrice(t.Index); p.Cash-=cost; Players[t.OwnerIndex].Cash+=cost;
                t.OwnerIndex=p.Index; _canTakeover=false; Note(p.Name+" · "+t.Definition.Name+" 인수"); CheckVictory(p.Index);
            }
            else if (actionId.StartsWith("sell:",StringComparison.Ordinal))
            {
                var t=Tiles[action.TileIndex]; p.Cash+=GetLiquidationValue(t.Index); t.OwnerIndex=-1; t.Level=0; t.FestivalMultiplier=1; t.ResortArrivals=0;
                Note(t.Definition.Name+" 매각");
                if (p.Cash>=DebtAmount) CompleteDebt();
            }
            else if(actionId=="loan")
            {
                long amount=DebtAmount-p.Cash;
                p.LoanUsed=true; p.LoanBalance+=amount+amount/5; p.Cash+=amount;
                Note("긴급 대출 "+amount+" · 상환금 "+p.LoanBalance); CompleteDebt();
            }
            else if (actionId=="bankrupt") DeclareBankruptcy();
            else if (actionId.StartsWith("travel:",StringComparison.Ordinal))
            {
                int target=action.TileIndex;
                p.PendingTravel=false;
                BeginPayment(Config.TravelFee,-1,()=> { MoveTo(target,true); ResolveLanding(0); });
            }
            else if (actionId=="travel_cancel") { p.PendingTravel=false; FinishLanding(); }
            else if (actionId.StartsWith("festival:",StringComparison.Ordinal))
            {
                var t=Tiles[action.TileIndex]; t.FestivalMultiplier=Math.Min(5,t.FestivalMultiplier+1);
                Note(t.Definition.Name+" 도시 축제 · 통행료 ×"+t.FestivalMultiplier); FinishLanding();
            }
            else if(actionId.StartsWith("remote:",StringComparison.Ordinal))
            {
                var t=Tiles[action.TileIndex]; int mask=int.Parse(actionId.Split(':')[2]);
                p.Cash-=action.Cost;if(mask==8)t.Landmark=true;else t.Buildings|=mask;_builtThisLanding=true;
                Note(t.Definition.Name+" · "+(mask==8?"랜드마크":BuildingNames(mask))+" 원격 건설"); CheckVictory(p.Index);
            }
            else if(actionId.StartsWith("bonus:",StringComparison.Ordinal))
            {
                p.Cash-=100; bool heads=Next(0,2)==0; bool won=(actionId=="bonus:heads")==heads;
                if(won) p.Cash+=200;
                Note(GameLabels.Bonus+" · "+(heads ? "앞면" : "뒷면")+" · "+(won ? "성공 +100" : "실패 -100"));
                FinishLanding();
            }
            else if (actionId=="end") FinishLanding();
            SyncCreditAccounts();return true;
        }

        public string ChooseAiAction()
        {
            var actions=GetActions(); if(actions.Count==0) return null;
            if(Phase==GamePhase.Augment)return ChooseAugmentAction(AugmentActor);
            string rescue=ChooseEconomyRescue(actions);if(rescue!=null)return rescue;
            if(Phase==GamePhase.Debt)
            {
                if(actions.Any(a=>a.Id=="loan")) return "loan";
                var sell=actions.Where(a=>a.Id.StartsWith("sell:",StringComparison.Ordinal)).OrderBy(a=>GetToll(a.TileIndex)).ThenByDescending(a=>-a.Cost).FirstOrDefault();
                return sell==null ? "bankrupt" : sell.Id;
            }
            if(Phase==GamePhase.Travel)
            {
                var safe=actions.Where(a=>a.TileIndex>=0 && Tiles[a.TileIndex].IsPurchasable)
                    .OrderByDescending(a=>Tiles[a.TileIndex].OwnerIndex==CurrentPlayerIndex ? 3 : Tiles[a.TileIndex].OwnerIndex<0 ? 2 : 0)
                    .ThenByDescending(a=>Tiles[a.TileIndex].Definition.Price).FirstOrDefault();
                return safe==null ? "travel_cancel" : safe.Id;
            }
            if(Phase==GamePhase.Olympics)
                return actions.Where(a=>a.TileIndex>=0).OrderByDescending(a=>GetToll(a.TileIndex)).Select(a=>a.Id).FirstOrDefault() ?? "end";
            if(Phase==GamePhase.Bonus) return CurrentPlayer.Cash>500 ? "bonus:heads" : "end";
            var card=actions.FirstOrDefault(a=>a.Id=="island_card"); if(card!=null) return card.Id;
            var escape=actions.FirstOrDefault(a=>a.Id=="island_pay" && CurrentPlayer.Cash>Config.IslandFee+400); if(escape!=null) return escape.Id;
            var purchase=actions.Where(a=>a.Id.StartsWith("purchase:",StringComparison.Ordinal) && CurrentPlayer.Cash-a.Cost>=100).OrderByDescending(a=>a.Cost).FirstOrDefault();
            if(purchase!=null)return purchase.Id;
            var construction=actions.Where(a=>a.Id.StartsWith("construct:",StringComparison.Ordinal) && CurrentPlayer.Cash-a.Cost>=250).OrderByDescending(a=>a.Cost).FirstOrDefault();
            if(construction!=null)return construction.Id;
            var buy=actions.FirstOrDefault(a=>a.Id=="buy"); if(buy!=null && CurrentPlayer.Cash-buy.Cost>=100) return buy.Id;
            var build=actions.Where(a=>a.Id.StartsWith("build:",StringComparison.Ordinal) && CurrentPlayer.Cash-a.Cost>=250).OrderByDescending(a=>a.Cost).FirstOrDefault();
            if(build!=null) return build.Id;
            var remote=actions.Where(a=>a.Id.StartsWith("remote:",StringComparison.Ordinal) && CurrentPlayer.Cash-a.Cost>=250).OrderByDescending(a=>a.Cost).FirstOrDefault();
            if(remote!=null) return remote.Id;
            var takeover=actions.FirstOrDefault(a=>a.Id=="takeover" && CurrentPlayer.Cash-a.Cost>=400);
            if(takeover!=null) return takeover.Id;
            return actions.FirstOrDefault(a=>a.Id=="roll")!=null ? "roll" : "end";
        }

        public long GetNetWorth(int playerIndex)
        { return Players[playerIndex].Cash - Players[playerIndex].LoanBalance + Tiles.Where(t=>t.OwnerIndex==playerIndex).Sum(t=>Invested(t)); }
        public long GetLiquidationValue(int tileIndex) { return Invested(Tiles[tileIndex])/2; }
        public long GetTakeoverPrice(int tileIndex) { return Invested(Tiles[tileIndex])*2; }
        public long GetToll(int tileIndex)
        {
            var t=Tiles[tileIndex]; if(t.OwnerIndex<0 || !t.IsPurchasable) return 0;
            long toll=t.Definition.Kind==TileKind.City?RawCityToll(t,t.Buildings,t.Landmark):t.Definition.BaseTolls[t.Level];
            if(t.Definition.Kind==TileKind.City && OwnsGroup(t.OwnerIndex,t.Definition.Group)) toll*=2;
            if(t.Definition.Kind==TileKind.Resort)
            {
                if(t.Definition.ResortType==2) toll*=1L<<Math.Min(3,t.ResortArrivals);
                else
                {
                    int resorts=Tiles.Count(x=>x.Definition.Kind==TileKind.Resort && x.Definition.ResortType!=2 && x.OwnerIndex==t.OwnerIndex);
                    toll*=1L << Math.Max(0,resorts-1);
                }
            }
            return toll*t.FestivalMultiplier;
        }
        public bool OwnsGroup(int playerIndex,int group)
        {
            var members=Tiles.Where(t=>t.Definition.Kind==TileKind.City && t.Definition.Group==group).ToList();
            return group>=0 && members.Count>0 && members.All(t=>t.OwnerIndex==playerIndex && t.Level>=1);
        }
        public int CompleteGroupCount(int playerIndex)
        { return Tiles.Where(t=>t.Definition.Kind==TileKind.City && t.Definition.Group>=0).Select(t=>t.Definition.Group).Distinct().Count(g=>OwnsGroup(playerIndex,g)); }

        private int Next(int min,int max)
        { _rng^=_rng<<13; _rng^=_rng>>17; _rng^=_rng<<5; return min+(int)(_rng%(uint)(max-min)); }
        private static long Invested(TileState t)=>t.Definition.Price+BuildingPrice(t,t.Buildings)+(t.Landmark?t.Definition.BuildCosts[3]:0);
        public static string BuildName(int level)
        { return new[] {"대지","별장","빌딩","호텔","랜드마크"}[Math.Max(0,Math.Min(4,level))]; }
        private void Note(string text)
        { Message=text; History.Add("["+TurnNumber+"] "+text); }

        private void Roll()
        {
            LastDiceControlled=false;LastDiceActor=CurrentPlayerIndex;
            DiceA=Next(1,7); DiceB=Next(1,7);
            ProcessRoll();
        }
        private void ProcessRoll()
        {
            LastDiceActor=CurrentPlayerIndex;
            Note(CurrentPlayer.Name+" · 주사위 "+DiceA+" + "+DiceB+(DiceA==DiceB ? " 더블" : ""));
            bool escaping=CurrentPlayer.IslandTurns>0;
            if(escaping)
            {
                _extraRoll=false;
                if(DiceA==DiceB) { CurrentPlayer.IslandTurns=0; MoveRolledSteps(DiceA+DiceB); ResolveLanding(0); }
                else
                {
                    CurrentPlayer.IslandTurns--;
                    if(CurrentPlayer.IslandTurns==0)
                        BeginPayment(Config.IslandFee,-1,()=> { MoveRolledSteps(DiceA+DiceB); ResolveLanding(0); });
                    else { Note("무인도 탈출 실패 · 남은 대기 "+CurrentPlayer.IslandTurns+"회"); EndTurn(); }
                }
                return;
            }
            _extraRoll=DiceA==DiceB;
            _consecutiveDoubles=_extraRoll ? _consecutiveDoubles+1 : 0;
            if(_consecutiveDoubles>=3) { SendToIsland(); EndTurn(); return; }
            MoveRolledSteps(DiceA+DiceB); ResolveLanding(0);
        }

        private void MoveSteps(int steps)
        {
            int initial=CurrentPlayer.Position;
            for(int step=1;step<=steps;step++)RecordMovement((initial+step-1)%Tiles.Count,(initial+step)%Tiles.Count);
            int total=initial+steps;
            if(total>=Tiles.Count)
            {
                int laps=total/Tiles.Count; CurrentPlayer.Laps+=laps; ReceiveSalary(laps*Config.Salary);
                Note("출발지 통과 · 월급 +"+(laps*Config.Salary));
            }
            CurrentPlayer.Position=total%Tiles.Count;
        }
        private void MoveTo(int target,bool salary)
        {
            int steps=(target-CurrentPlayer.Position+Tiles.Count)%Tiles.Count;
            if(salary) MoveSteps(steps); else {if(CurrentPlayer.Position!=target)RecordMovement(CurrentPlayer.Position,target,true);CurrentPlayer.Position=target;}
        }
        private void ReceiveSalary(long amount)
        {
            long repayment=Math.Min(CurrentPlayer.LoanBalance,amount/2);
            CurrentPlayer.LoanBalance-=repayment; CurrentPlayer.Cash+=amount-repayment;
            if(repayment>0) Note("긴급 대출 상환 "+repayment);
        }
        private void ResolveLanding(int depth)
        {
            if(depth>8) { Note("이벤트 연쇄 제한"); FinishLanding(); return; }
            _builtThisLanding=false; _canTakeover=false;
            var t=Tiles[CurrentPlayer.Position];
            Note(CurrentPlayer.Name+" · "+t.Definition.Name+" 도착");
            if(t.IsPurchasable)
            {
                if(t.Definition.Kind==TileKind.Resort && t.Definition.ResortType==2 && t.OwnerIndex>=0)
                    t.ResortArrivals=Math.Min(3,t.ResortArrivals+1);
                Phase=GamePhase.Decision;
                if(t.OwnerIndex>=0 && t.OwnerIndex!=CurrentPlayerIndex)
                {
                    long toll=GetToll(t.Index);
                    if(CurrentPlayer.ShieldCards>0) { CurrentPlayer.ShieldCards--; Note("통행료 면제 카드 자동 사용"); _canTakeover=t.Level<4; }
                    else BeginPayment(toll,t.OwnerIndex,()=> { Phase=GamePhase.Decision; _canTakeover=t.Level<4; });
                }
                return;
            }
            switch(t.Definition.Kind)
            {
                case TileKind.Island: SendToIsland(); EndTurn(); break;
                case TileKind.Travel: CurrentPlayer.PendingTravel=true; _extraRoll=false; Note("다음 차례에 세계여행 목적지를 선택합니다"); EndTurn(); break;
                case TileKind.Olympics: Phase=GamePhase.Olympics; break;
                case TileKind.Bonus: Phase=GamePhase.Bonus; break;
                case TileKind.Tax: BeginPayment(Config.Tax,-1,FinishLanding); break;
                case TileKind.Chance: DrawChance(depth+1); break;
                case TileKind.Start: Note("출발지 · 소유 도시에 원격 건설 가능"); Phase=GamePhase.Decision; break;
                default: Phase=GamePhase.Decision; break;
            }
        }
        private void DrawChance(int depth)
        {
            int card=Next(0,10);
            switch(card)
            {
                case 0: CurrentPlayer.Cash+=200; Note(GameLabels.EventCard+" · 복권 당첨 +200"); Phase=GamePhase.Decision; break;
                case 1: Note(GameLabels.EventCard+" · 수리비 200"); BeginPayment(200,-1,FinishLanding); break;
                case 2: Note(GameLabels.EventCard+" · 출발지로 이동"); MoveTo(0,true); ResolveLanding(depth); break;
                case 3: Note(GameLabels.EventCard+" · 무인도행"); SendToIsland(); EndTurn(); break;
                case 4: CurrentPlayer.EscapeCards++; Note(GameLabels.EventCard+" · 무인도 탈출 카드 획득"); Phase=GamePhase.Decision; break;
                case 5: CurrentPlayer.ShieldCards++; Note(GameLabels.EventCard+" · 통행료 면제 카드 획득"); Phase=GamePhase.Decision; break;
                case 6: CurrentPlayer.Cash+=Config.Salary; Note(GameLabels.EventCard+" · 보너스 +"+Config.Salary); Phase=GamePhase.Decision; break;
                case 7: Note(GameLabels.EventCard+" · 세계여행"); MoveTo(Tiles.FindIndex(t=>t.Definition.Kind==TileKind.Travel),false); ResolveLanding(depth); break;
                case 8:
                    Note(GameLabels.EventCard+" · 가까운 관광지로 이동");
                    var resort=Tiles.Where(t=>t.Definition.Kind==TileKind.Resort).OrderBy(t=>(t.Index-CurrentPlayer.Position+Tiles.Count)%Tiles.Count).FirstOrDefault();
                    if(resort!=null) { MoveTo(resort.Index,true); ResolveLanding(depth); } else Phase=GamePhase.Decision;
                    break;
                default: Note(GameLabels.EventCard+" · 앞으로 3칸"); MoveSteps(3); ResolveLanding(depth); break;
            }
        }
        private void SendToIsland()
        {
            int island=Tiles.FindIndex(t=>t.Definition.Kind==TileKind.Island);
            if(CurrentPlayer.Position!=island)RecordMovement(CurrentPlayer.Position,island,true);
            CurrentPlayer.Position=island; CurrentPlayer.IslandTurns=3;
            _extraRoll=false; Note(CurrentPlayer.Name+" 무인도 · 최대 3회 대기");
        }
        private void BeginPayment(long amount,int creditor,Action after)
        {
            if(amount==0) { after(); return; }
            if(Config.EnableBodyEconomy){CurrentPlayer.Cash-=amount;if(creditor>=0)Players[creditor].Cash+=amount;SyncCreditAccounts();Note(amount+" 정상 지불 · 잔액 "+CurrentPlayer.Cash);after();return;}
            DebtAmount=amount; DebtCreditorIndex=creditor; _afterDebt=after;
            if(CurrentPlayer.Cash>=amount) CompleteDebt();
            else
            {
                Phase=GamePhase.Debt;
                Note("지불할 금액 "+amount+" · 부족 "+(amount-CurrentPlayer.Cash)+" · 부동산 매각 또는 파산");
            }
        }
        private void CompleteDebt()
        {
            long paid=DebtAmount; int creditor=DebtCreditorIndex;
            CurrentPlayer.Cash-=paid; if(creditor>=0) Players[creditor].Cash+=paid;
            DebtAmount=0; DebtCreditorIndex=-1; var next=_afterDebt; _afterDebt=null;
            Note(paid+" 지불"+(creditor>=0 ? " → "+Players[creditor].Name : " → 은행")); next();
        }
        private void DeclareBankruptcy()
        {
            var p=CurrentPlayer; int creditor=DebtCreditorIndex;
            if(creditor>=0) Players[creditor].Cash+=p.Cash;
            foreach(var t in Tiles.Where(t=>t.OwnerIndex==p.Index))
            { t.OwnerIndex=creditor; if(creditor<0) { t.Level=0; t.FestivalMultiplier=1; t.ResortArrivals=0; } }
            p.Cash=0; p.Bankrupt=true; p.IslandTurns=0; DebtAmount=0; DebtCreditorIndex=-1; _afterDebt=null;
            Note(p.Name+" 파산"+(creditor>=0 ? " · 잔여 재산을 "+Players[creditor].Name+"에게 이전" : ""));
            if(creditor>=0) CheckVictory(creditor);
            if(Phase==GamePhase.Finished) return;
            var alive=Players.Where(x=>!x.Bankrupt).ToList();
            if(Config.EndOnFirstBankruptcy)
            {int winner=alive.OrderByDescending(x=>GetNetWorth(x.Index)).ThenBy(x=>x.Index).First().Index;Finish(winner,p.Name+" 파산 · 경기 종료");}
            else if(alive.Count==1) Finish(alive[0].Index,"최후의 생존자"); else EndTurn();
        }
        private void FinishLanding()
        {
            if(Phase==GamePhase.Finished) return;
            if(_extraRoll && CurrentPlayer.IslandTurns==0)
            { Phase=GamePhase.AwaitRoll; Note(CurrentPlayer.Name+" · 더블 추가 굴림"); }
            else EndTurn();
        }
        private void EndTurn()
        {
            if(Phase==GamePhase.Finished) return;
            _extraRoll=false; _consecutiveDoubles=0; _canTakeover=false; _builtThisLanding=false;
            if(Config.EnableBodyEconomy){EndEconomyTurn();return;}
            if(TurnNumber>=Config.MaxTurns)
            {
                int winner=Players.Where(p=>!p.Bankrupt).OrderByDescending(p=>GetNetWorth(p.Index)).ThenBy(p=>p.Index).First().Index;
                Finish(winner,"제한 턴 종료 · 총자산 1위"); return;
            }
            do { CurrentPlayerIndex=(CurrentPlayerIndex+1)%Players.Count; } while(Players[CurrentPlayerIndex].Bankrupt);
            TurnNumber++; Phase=CurrentPlayer.PendingTravel ? GamePhase.Travel : GamePhase.AwaitRoll; Note(CurrentPlayer.Name+" 차례"+(CurrentPlayer.PendingTravel ? " · 세계여행 목적지 선택" : ""));
            EnterAugmentIfDue();
        }
        private void CheckVictory(int player)
        {
            if(Config.EnableBodyEconomy)return;
            int groups=CompleteGroupCount(player);
            if(Config.ColorSetWins && groups>=1) { Finish(player,"컬러 독점"); return; }
            if(groups>=3) { Finish(player,"트리플 독점"); return; }
            var resorts=Tiles.Where(t=>t.Definition.Kind==TileKind.Resort).ToList();
            if(resorts.Count>0 && resorts.All(t=>t.OwnerIndex==player)) { Finish(player,"관광지 독점"); return; }
            for(int side=0;side<4;side++)
            {
                int s=side;
                var line=Tiles.Where(t=>t.IsPurchasable && t.Index/8==s).ToList();
                if(line.Count>0 && line.All(t=>t.OwnerIndex==player)) { Finish(player,"라인 독점"); return; }
            }
        }
        private void Finish(int player,string reason)
        { WinnerIndex=player; WinnerReason=reason; Phase=GamePhase.Finished; Note(Players[player].Name+" 승리 · "+reason); }

        public static Game Replay(int seed,int humanCount,int playerCount,IEnumerable<string> actions,int maxTurns=240,TileDefinition[] definitions=null)
        {
            var game=new Game(seed,humanCount,playerCount,maxTurns,definitions);
            foreach(var action in actions) if(!game.Execute(action)) throw new ArgumentException("Invalid replay action: "+action);
            return game;
        }
        public static Game Replay(GameConfig config,IEnumerable<string> actions,TileDefinition[] definitions=null)
        {
            var game=new Game(config,definitions);
            foreach(var action in actions) if(!game.Execute(action)) throw new ArgumentException("Invalid replay action: "+action);
            return game;
        }

        // Narrow internal setup only for deterministic boundary tests, never used by the view.
        internal void TestLand(int player,int position)
        { CurrentPlayerIndex=player; CurrentPlayer.Position=position; _extraRoll=false; ResolveLanding(0); }
        internal void TestCheckVictory(int player) { CheckVictory(player); }
        internal void TestMove(int steps) { MoveSteps(steps); }
        internal void TestRoll(int a,int b) { BeginActionMovement();DiceA=a; DiceB=b; ProcessRoll(); }
    }

    public static class DefaultBoard
    {
        // Reconstructed classic layout; all numeric balancing values are explicit implementation choices.
        public static TileDefinition[] Create()
        {
            var board=new TileDefinition[32];
            board[0]=new TileDefinition("출발지",TileKind.Start);
            board[2]=new TileDefinition(GameLabels.Bonus,TileKind.Bonus);
            board[4]=new TileDefinition(GameLabels.EventCard,TileKind.Chance);
            board[8]=new TileDefinition("무인도",TileKind.Island);
            board[12]=new TileDefinition(GameLabels.EventCard,TileKind.Chance);
            board[16]=new TileDefinition(GameLabels.Festival,TileKind.Olympics);
            board[20]=new TileDefinition(GameLabels.EventCard,TileKind.Chance);
            board[24]=new TileDefinition("세계여행",TileKind.Travel);
            board[28]=new TileDefinition(GameLabels.EventCard,TileKind.Chance);
            int[] cities={1,3,6,7,10,11,13,15,17,19,21,22,23,26,27,29,30,31};
            string[] names={"방콕","베이징","두바이","카이로","도쿄","시드니","퀘백","상파울로","프라하","베를린","모스크바","제네바","로마","아테네","런던","파리","뉴욕","서울"};
            int[] groups={1,1,2,2,3,3,4,4,5,5,6,6,6,7,7,8,8,8};
            for(int i=0;i<cities.Length;i++)
            {
                long p=100+i*15;
                board[cities[i]]=new TileDefinition(names[i],TileKind.City,groups[i],p,
                    new[]{p/2,p*3/4,p,p*2},new[]{p/5,p/2,p,p*2,p*4});
            }
            int[] resortTiles={5,9,14,18,25}; string[] resortNames={"독도","발리","하와이","푸켓","타히티"};
            for(int i=0;i<resortTiles.Length;i++)
                board[resortTiles[i]]=new TileDefinition(resortNames[i],TileKind.Resort,-1,150+i*25,null,new long[]{75,75,75,75,75},i==1 || i==4 ? 2 : 1);
            return board;
        }
    }
}
