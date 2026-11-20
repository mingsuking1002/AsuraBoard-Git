using System.Linq;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        bool bodyMarketOpen;Vector2 bodyAssetsScroll;
        int BodyViewSeat=>onlineGameAttached?onlineSession.LocalSeat:Model.Players.FindIndex(p=>p.IsHuman && !p.Bankrupt)>=0?Model.Players.FindIndex(p=>p.IsHuman && !p.Bankrupt):0;
        bool BodyCanAct=>LocalTurn && !autoHuman && !Board.IsAnimating && !bodyAudioSettings && !preferencesOpen && !settings && !help && !onlineOpen && (!onlineGameAttached || onlineSession.CanAct);
        void DrawRepaymentSummary()
        {
            Panel(new Rect(465,320,510,176),cream);
            GUI.Label(new Rect(488,337,464,39),Model.CurrentPlayer.Name+" 최종 상환",modalTitle);
            GUI.Label(new Rect(488,389,464,66),"만기 미상환액 "+Money(-Model.CurrentPlayer.Cash)+"\n신체 여러 부위와 부동산을 매각할 수 있습니다.",center);
        }
        void DrawBodyMarket()
        {
            bool final=Model.Phase==GamePhase.Repayment;int seat=final?Model.CurrentPlayerIndex:BodyViewSeat;var p=Model.Players[seat];
            Shade();PaperTheme.Card(new Rect(110,97,1220,708),cream,true,3);
            GUI.Label(new Rect(140,117,1030,39),p.Name+(final?" · 최종 상환 정산":" · 신체와 부동산 거래"),title);
            if(Button(new Rect(1104,121,190,34),"기계 소음 설정",muted))bodyAudioSettings=true;
            GUI.Label(new Rect(141,166,1140,31),"잔액 "+Money(p.Cash)+"   투자 가능 "+Money(Model.InvestmentCapacity(seat))+"   "+(p.CreditDueRound>0?p.CreditDueRound+"R 종료 만기":"부채 없음")+"   현재 "+Model.EconomyRound+"R",label);
            GUI.Label(new Rect(142,207,1135,25),final?"긴급 매각은 여러 부위 가능. 미상환 상태에서 정산을 확정하면 탈락합니다.":"내 턴에 1회 매각 · 빚이 없어도 판매 가능 · 복구 비용은 판매가의 150%",small);
            for(int i=0;i<BodyCatalog.Parts.Length;i++)
            {
                var part=BodyCatalog.Parts[i];float x=142+(i%2)*414,y=248+(i/2)*85;bool sold=BodyCatalog.IsSold(p,part.Part);
                PaperTheme.Card(new Rect(x,y,398,74),PaperTheme.Wash(sold?MarbleBoardView.PlayerColors[1]:MarbleBoardView.PlayerColors[2],sold?.23f:.1f),false,1.8f,i);
                GUI.Label(new Rect(x+10,y+7,265,26),part.Name+" · "+(sold?"인공 대체 / 복구 "+Money(part.RestorePrice):"판매 "+Money(part.Price)),small);
                GUI.Label(new Rect(x+10,y+37,270,27),part.Effect,tiny);
                bool allowed=BodyCanAct && (sold?Model.CanRestoreBody(seat,part.Part):Model.CanSellBody(seat,part.Part));
                if(Button(new Rect(x+288,y+18,99,38),sold?"복구":"매각",sold?muted:dark,allowed))Act((sold?"body_restore:":"body_sell:")+i);
            }
            GUI.Label(new Rect(992,249,300,29),"부동산 자발적 매각",label);
            var owned=Model.Tiles.Where(t=>t.OwnerIndex==seat).ToArray();
            bodyAssetsScroll=GUI.BeginScrollView(new Rect(991,292,298,373),bodyAssetsScroll,new Rect(0,0,272,Mathf.Max(373,owned.Length*58)));
            for(int i=0;i<owned.Length;i++)
            {var t=owned[i];if(Button(new Rect(2,i*58+2,263,48),t.Definition.Name+" +"+Money(Model.GetLiquidationValue(t.Index)),muted,BodyCanAct))Act("asset_sell:"+t.Index);}
            GUI.EndScrollView();
            if(owned.Length==0)GUI.Label(new Rect(1003,309,270,34),"매각할 부동산 없음",small);
            if(final)
            {if(Button(new Rect(143,728,1143,44),p.Cash>=0?"상환 완료 · 정산 확정":"미상환 탈락 확정",p.Cash>=0?dark:MarbleBoardView.PlayerColors[1],BodyCanAct))Act("repayment_confirm");}
            else if(Button(new Rect(143,728,1143,44),"거래창 닫기",dark))bodyMarketOpen=false;
            GUI.Label(new Rect(143,686,1143,26),!LocalTurn?"내 턴에 거래할 수 있습니다.":p.LastBodySaleTurn==Model.TurnNumber && !final?"이번 턴 신체 매각 사용 완료. 복구와 부동산 매각은 가능합니다.":"모든 부위를 판매해도 게임을 계속할 수 있습니다.",small);
        }
        void DrawBodyWorld()
        {
            Board.DrawBoard();
        }
        Vector2 BodyVisionCenter
        {
            get
            {
                var pawn=Board.VisiblePawnPosition(BodyViewSeat)+new Vector2(0,-28);
                var point=Board.ViewTransform.MultiplyPoint3x4(new Vector3(pawn.x,pawn.y,0));
                return new Vector2(point.x,point.y);
            }
        }
        void DrawBodyVision()
        {
            int eyes=Model.Config.EnableBodyEconomy?Model.Players[BodyViewSeat].SoldBodyMask&3:0;
            BodyWorldLayer.DrawVignette(eyes,BodyVisionCenter,BodyWorldLayer.NearVisionRadius*Board.ViewTransform.m00,preferences!=null && preferences.ComfortBlur?.5f:1);
        }
    }
}
