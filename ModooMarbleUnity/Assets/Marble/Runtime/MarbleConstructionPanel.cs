using System;
using System.Collections;
using System.IO;
using System.Linq;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        static readonly Rect ConstructionWindow=new Rect(348,270,744,466);
        Game constructionModel;
        int constructionSeat=-1,constructionTile=-1,constructionSequence=-1,constructionMask;
        bool CityConstructionContext=>Model!=null && Model.Phase==GamePhase.Decision &&
            Model.Tiles[Model.CurrentPlayer.Position].Definition.Kind==TileKind.City &&
            (Model.Tiles[Model.CurrentPlayer.Position].OwnerIndex<0 || Model.Tiles[Model.CurrentPlayer.Position].OwnerIndex==Model.CurrentPlayerIndex);
        void SyncConstructionChoice()
        {
            int tile=Model.CurrentPlayer.Position;
            if(constructionModel!=Model || constructionSeat!=Model.CurrentPlayerIndex || constructionTile!=tile || constructionSequence!=Model.ReplayActions.Count)
            {
                constructionModel=Model;constructionSeat=Model.CurrentPlayerIndex;constructionTile=tile;constructionSequence=Model.ReplayActions.Count;
                constructionMask=Model.ConstructionSpent?0:Model.AvailableBuildings(tile,Model.CurrentPlayerIndex);
                for(int bit=4;bit>=1;bit>>=1)if(!Model.CanInvest(Model.CurrentPlayerIndex,Model.ConstructionPrice(tile,constructionMask)))constructionMask &= ~bit;
            }
            constructionMask &= Model.AvailableBuildings(tile,Model.CurrentPlayerIndex);
        }
        void ToggleConstruction(int type)
        {
            SyncConstructionChoice();int bit=1<<(type-1);
            if(type<1 || type>3 || Model.ConstructionSpent || (Model.AvailableBuildings(Model.CurrentPlayer.Position,Model.CurrentPlayerIndex)&bit)==0)return;
            constructionMask ^= bit;
        }
        bool ConfirmConstruction()
        {
            SyncConstructionChoice();var t=Model.Tiles[Model.CurrentPlayer.Position];
            string action=t.OwnerIndex<0?(constructionMask==0?"buy":"purchase:"+constructionMask):constructionMask!=0?"construct:"+constructionMask:"build:4";
            return Act(action);
        }
        void DrawConstructionPanel(bool ready)
        {
            SyncConstructionChoice();var t=Model.Tiles[Model.CurrentPlayer.Position];var player=Model.CurrentPlayer;
            if(t.OwnerIndex==player.Index && t.Buildings==7){DrawLandmarkPanel(t,ready);return;}
            Color accent=t.Definition.Group>=0?MarbleBoardView.GroupColors[t.Definition.Group%8]:dark;
            Panel(ConstructionWindow,cream);
            Box(new Rect(348,270,744,7),accent,false);
            GUI.Label(new Rect(376,288,550,38),t.Definition.Name+"  ·  "+(t.OwnerIndex<0?"도시 구매":"추가 건설"),modalTitle);
            var lapStyle=new GUIStyle(small){alignment=TextAnchor.MiddleRight};
            GUI.Label(new Rect(865,294,200,27),"현재 "+(player.Laps+1)+"번째 바퀴",lapStyle);
            GUI.Label(new Rect(376,336,680,33),"첫 바퀴: 별장  →  1바퀴 완주: 빌딩  →  2바퀴 완주: 호텔",small);
            for(int i=0;i<4;i++)
            {
                var card=new Rect(376+i*174,381,166,192);bool land=i==0;
                Color category=MarbleBoardView.Hex(new[]{"CCAA74","6CB887","71A9DB","B18AD0"}[i]);
                bool built=land?t.OwnerIndex>=0:t.HasBuilding(i)||t.Landmark;
                bool locked=!land && i>Model.ConstructionLimit(player.Index);
                bool picked=land || (constructionMask&(1<<(i-1)))!=0;
                
                Color cardPaper=locked?PaperTheme.Wash(PaperTheme.Muted,.12f):PaperTheme.Wash(category,built?.24f:picked?.49f:.28f);
                PaperTheme.Card(card,cardPaper,true,picked && !built?3.4f:2.2f,i);
                PaperTheme.Surface(new Rect(card.x+9,card.y+8,card.width-18,32),PaperTheme.Wash(category,locked?.20f:.78f));
                if(picked && !built)PaperTheme.Underline(new Rect(card.x+15,card.y+43,card.width-30,1),ink);
                CircuitArt.Label(new Rect(card.x+9,card.y+11,148,27),land?"땅":Game.BuildName(i),font,19,locked?muted:ink,true);
                DrawConstructionArtwork(new Rect(card.x+43,card.y+51,80,67),i,locked?MarbleBoardView.Hex("AAB6AC"):category);
                long price=land?t.Definition.Price:t.Definition.BuildCosts[i-1];
                CircuitArt.Label(new Rect(card.x+5,card.y+123,156,25),Money(price),font,17,locked?muted:ink,true);
                string status=built?"보유 중 · 중복 불가":locked?(i-1)+"바퀴 완주 후 해금":land?"필수 구매":Model.ConstructionSpent?"이번 방문 건설 완료":picked?"✓ 선택됨":"눌러서 선택";
                CircuitArt.Label(new Rect(card.x+3,card.y+157,160,22),status,font,12,locked?muted:dark,false);
                if(!land && !built && !locked && !Model.ConstructionSpent && ready && GUI.Button(card,GUIContent.none,GUIStyle.none))ToggleConstruction(i);
            }
            long cost=Model.ConstructionPrice(t.Index,constructionMask);
            bool landmark=t.OwnerIndex==player.Index && t.Buildings==7 && !t.Landmark && !Model.ConstructionSpent;
            if(landmark && constructionMask==0)cost=t.Definition.BuildCosts[3];
            bool possible=t.OwnerIndex<0 || (!Model.ConstructionSpent && (constructionMask!=0 || landmark));
            string action=t.OwnerIndex<0?(constructionMask==0?"buy":"purchase:"+constructionMask):constructionMask!=0?"construct:"+constructionMask:"build:4";
            bool legal=Model.GetActions().Any(a=>a.Id==action);
            GUI.Label(new Rect(378,593,355,30),(possible?"총 결제 금액  "+Money(cost):t.Landmark?"랜드마크 보유 중":"현재 선택한 건물 없음"),modalTitle);
            long projected=landmark && constructionMask==0?Model.ProjectedLandmarkToll(t.Index):Model.ProjectedConstructionToll(t.Index,constructionMask);
            GUI.Label(new Rect(378,632,415,25),"건설 후 통행료  "+Money(projected),small);
            GUI.Label(new Rect(831,598,225,26),"보유금  "+Money(player.Cash),lapStyle);
            GUI.Label(new Rect(831,633,225,24),possible && !Model.CanInvest(player.Index,cost)?"신용한도를 초과합니다.":landmark?"세 건물 보유 · 랜드마크 가능":Model.ConstructionSpent?"이번 방문 건설 완료":"같은 건물은 한 번만 건설",lapStyle);
            string confirm=landmark && constructionMask==0?"랜드마크 건설":t.OwnerIndex<0?"구매 · 건설 확정":"선택 건물 건설";
            if(Button(new Rect(378,678,432,42),confirm,dark,ready && legal && possible))ConfirmConstruction();
            if(Button(new Rect(832,678,230,42),possible?"건너뛰기":"차례 종료",muted,ready))Act("end");
        }
        static void DrawConstructionArtwork(Rect r,int type,Color color)
        {
            float cx=r.center.x,ground=r.yMax-5;
            CircuitArt.Fill(new Rect(r.x+2,ground,r.width-4,5),Color.Lerp(color,Color.black,.18f));
            if(type==0)
            {
                CircuitArt.Fill(new Rect(r.x+7,ground-11,r.width-14,10),Color.Lerp(color,Color.white,.48f));
                CircuitArt.Line(new Vector2(cx,ground-11),new Vector2(cx,ground-45),color,3);
                CircuitArt.Fill(new Rect(cx+2,ground-45,26,16),color);return;
            }
            float height=type==1?24:type==2?47:type==4?72:55;
            CircuitArt.Fill(new Rect(cx-24,ground-height,48,height),PaperTheme.Wash(color,.55f));
            PaperTheme.Frame(new Rect(cx-25,ground-height-1,50,height+2),PaperTheme.Ink,1.8f);
            CircuitArt.Fill(new Rect(cx+24,ground-height+5,7,height-5),Color.Lerp(color,Color.black,.22f));
            CircuitArt.Fill(new Rect(cx-28,ground-height-5,56,6),Color.Lerp(color,Color.white,.32f));
            for(int row=0;row<(type==1?1:type==2?3:type==4?5:4);row++)for(int col=0;col<3;col++)
                CircuitArt.Fill(new Rect(cx-17+col*13,ground-height+7+row*11,7,7),MarbleBoardView.Hex("FFF7CE"));
            if(type==1)
            {
                CircuitArt.Line(new Vector2(cx-29,ground-height-2),new Vector2(cx,ground-height-17),color,7);
                CircuitArt.Line(new Vector2(cx,ground-height-17),new Vector2(cx+29,ground-height-2),color,7);
            }
            if(type==3)CircuitArt.Label(new Rect(cx-14,ground-height-3,28,13),"H",GUI.skin.font,10,PaperTheme.Ink,true);
            if(type==4)
            {
                var gold=MarbleBoardView.Hex("D8AE57");
                CircuitArt.Fill(new Rect(cx-17,ground-height-13,34,8),gold);
                CircuitArt.Line(new Vector2(cx,ground-height-13),new Vector2(cx,ground-height-27),gold,3);
                CircuitArt.Fill(new Rect(cx+2,ground-height-27,19,10),gold);
            }
        }
        void DrawLandmarkPanel(TileState tile,bool ready)
        {
            Panel(ConstructionWindow,cream);GUI.Label(new Rect(378,292,675,39),tile.Definition.Name+" · "+(tile.Landmark?"랜드마크":"랜드마크 건설"),modalTitle);
            PaperTheme.Card(new Rect(378,351,252,290),PaperTheme.Wash(MarbleBoardView.PlayerColors[Model.CurrentPlayer.ColorIndex],.22f),true,2.5f);
            DrawConstructionArtwork(new Rect(425,385,158,139),4,MarbleBoardView.PlayerColors[Model.CurrentPlayer.ColorIndex]);
            CircuitArt.Label(new Rect(393,544,220,32),"랜드마크",font,23,ink,true);
            CircuitArt.Label(new Rect(393,589,220,27),"인수 불가",font,17,MarbleBoardView.Hex("84642B"),true);
            GUI.Label(new Rect(659,357,398,33),"별장 · 빌딩 · 호텔 보유 완료",label);
            GUI.Label(new Rect(659,407,398,63),"기존 세 건물을 랜드마크로 전환합니다.\n다른 플레이어가 인수할 수 없습니다.",small);
            GUI.Label(new Rect(659,493,398,30),tile.Landmark?"랜드마크 보유 중":"건설 비용  "+Money(tile.Definition.BuildCosts[3]),modalTitle);
            GUI.Label(new Rect(659,539,398,29),"통행료  "+Money(tile.Landmark?Model.GetToll(tile.Index):Model.ProjectedLandmarkToll(tile.Index)),small);
            GUI.Label(new Rect(659,588,398,43),tile.Landmark?"최종 건설 단계입니다.":Model.ConstructionSpent?"이번 방문 건설 완료 · 다음 방문부터 전환 가능":"다시 건설하면 최종 단계로 전환됩니다.",small);
            bool legal=Model.GetActions().Any(a=>a.Id=="build:4");
            if(Button(new Rect(378,678,432,42),tile.Landmark?"랜드마크 완성":"랜드마크 건설",dark,ready && legal))ConfirmConstruction();
            if(Button(new Rect(832,678,230,42),"차례 종료",muted,ready))Act("end");
        }
        [Serializable] sealed class ConstructionPreviewReport
        {public string status;public bool atomicPurchase,duplicatesBlocked,lapLocks,ownedBuildingsSeparate,landmarkAfterAll;public int ownedMask;}
        IEnumerator ConstructionPreview(string path)
        {
            autoHuman=false;StartGame(new GameConfig{EnableAugments=true,HumanCount=humans,PlayerCount=players});
            settings=true;
            while(Model.Phase==GamePhase.Augment)Model.Execute(Model.ChooseAiAction());
            Model.CurrentPlayer.Position=1;Model.TestLand(0,1);Board.Sync(false);
            yield return new WaitForSeconds(.8f);settings=false;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-first-lap.png");
            long before=Model.CurrentPlayer.Cash;
            var report=new ConstructionPreviewReport{status="failed"};
            report.lapLocks=!Model.Execute("purchase:3") && !Model.Execute("purchase:7");
            report.atomicPurchase=ConfirmConstruction() && Model.Tiles[1].Buildings==1 && Model.CurrentPlayer.Cash==before-Model.Tiles[1].Definition.Price-Model.Tiles[1].Definition.BuildCosts[0];
            report.duplicatesBlocked=!Model.Execute("construct:1");
            Model.CurrentPlayer.Laps=1;Model.TestLand(0,1);Board.Sync(false);
            yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-second-lap.png");
            ConfirmConstruction();
            Model.CurrentPlayer.Laps=2;Model.TestLand(0,1);Board.Sync(false);
            yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-third-lap.png");
            ConfirmConstruction();report.ownedMask=Model.Tiles[1].Buildings;report.ownedBuildingsSeparate=report.ownedMask==7;
            Model.TestLand(0,1);report.landmarkAfterAll=Model.GetActions().Any(a=>a.Id=="build:4");
            if(report.atomicPurchase && report.duplicatesBlocked && report.lapLocks && report.ownedBuildingsSeparate && report.landmarkAfterAll)report.status="passed";
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
    }
}
