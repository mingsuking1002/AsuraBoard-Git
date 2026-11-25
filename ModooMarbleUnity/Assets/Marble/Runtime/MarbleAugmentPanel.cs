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
        int augmentCandidate=-1,augmentDraftSeen=-2,augmentPlayerSeen=-1,augmentRevisionSeen=-1;
        int augmentHoverSeat=-1,augmentHoverIndex=-1;
        Rect augmentHoverAnchor;
        Vector2? augmentDiagnosticPointer;
        Game augmentModelSeen;
        static string AugmentTierName(AugmentTier tier)=>new[]{"브론즈","실버","골드","프리즘"}[(int)tier];
        static Color AugmentTierColor(AugmentTier tier)=>MarbleBoardView.Hex(new[]{"CD8B54","C1CFDF","F0C96A","99E9EC"}[(int)tier]);
        static Color PrismSpectrum(float t)
        {
            Color[] colors={MarbleBoardView.Hex("86EBE6"),MarbleBoardView.Hex("B8A1FF"),MarbleBoardView.Hex("F3AAD9"),MarbleBoardView.Hex("ABDAFF"),MarbleBoardView.Hex("86EBE6")};
            float value=Mathf.Repeat(t,1)*4;int index=Mathf.Min(3,(int)value);return Color.Lerp(colors[index],colors[index+1],value-index);
        }
        bool AugmentOpen=>Model!=null && Model.Phase==GamePhase.Augment && (!OnlineRoom || onlineGameAttached);
        int AugmentViewSeat
        {
            get
            {
                if(onlineGameAttached)return onlineSession.LocalSeat;
                int human=Model.Players.FindIndex(p=>p.IsHuman && Model.HasPendingAugment(p.Index));
                return human>=0?human:Math.Max(0,Model.AugmentActor);
            }
        }
        bool CanChooseAugment=>AugmentOpen && Model.HasPendingAugment(AugmentViewSeat) && (onlineGameAttached || Model.Players[AugmentViewSeat].IsHuman) && !Board.IsAnimating && !autoHuman && !preferencesOpen && !settings && !help && !onlineOpen &&
            (!onlineGameAttached || onlineSession.CanAct);
        void ResetAugmentCandidate()
        {augmentCandidate=-1;augmentDraftSeen=-2;augmentPlayerSeen=-1;augmentRevisionSeen=-1;augmentModelSeen=null;}
        void SyncAugmentCandidate()
        {
            if(augmentModelSeen!=Model || augmentDraftSeen!=Model.AugmentDraftIndex || augmentPlayerSeen!=AugmentViewSeat || augmentRevisionSeen!=Model.AugmentRevision(AugmentViewSeat))
            {
                augmentCandidate=-1;augmentModelSeen=Model;augmentDraftSeen=Model.AugmentDraftIndex;augmentPlayerSeen=AugmentViewSeat;augmentRevisionSeen=Model.AugmentRevision(AugmentViewSeat);
            }
        }
        void SelectAugmentSlot(int index)
        {
            SyncAugmentCandidate();if(!CanChooseAugment || index<0 || index>=3)return;
            augmentCandidate=index;if(!mutedSound)audioSource.PlayOneShot(clickSound);
        }
        bool ConfirmAugment()
        {
            SyncAugmentCandidate();if(!CanChooseAugment || augmentCandidate<0)return false;
            int selectedSlot=augmentCandidate;
            bool accepted=Act(Model.AugmentAction(AugmentViewSeat,selectedSlot));
            if(accepted)augmentCandidate=-1;return accepted;
        }
        bool RerollAugment(int slot=-1)
        {
            SyncAugmentCandidate();if(slot<0)slot=augmentCandidate>=0?augmentCandidate:Enumerable.Range(0,3).FirstOrDefault(i=>Model.CanRerollAugment(AugmentViewSeat,i));if(!CanChooseAugment || !Model.CanRerollAugment(AugmentViewSeat,slot))return false;
            bool accepted=Act(Model.AugmentRerollAction(AugmentViewSeat,slot));
            if(accepted)augmentCandidate=-1;return accepted;
        }
        void AugmentKeyboard()
        {
            SyncAugmentCandidate();if(!CanChooseAugment)return;
            if(Input.GetKeyDown(KeyCode.R)){RerollAugment();return;}
            if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1))SelectAugmentSlot(0);
            if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2))SelectAugmentSlot(1);
            if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Keypad3))SelectAugmentSlot(2);
            if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))ConfirmAugment();
        }
        static Rect AugmentCard(int i)=>new Rect(288+i*294,236,276,390);
        void DrawAugmentOverlay()
        {
            SyncAugmentCandidate();Shade();
            var tier=Model.GetAugmentTier(Model.AugmentDraftIndex);Color accent=AugmentTierColor(tier);
            PaperTheme.Card(new Rect(486,77,468,125),PaperTheme.Wash(accent,.25f),true,3.5f);
            CircuitArt.Label(new Rect(500,89,440,24),Model.GoldenRerollAvailable(AugmentViewSeat)?"황금 리롤! 다음 카드 1장만 등급 상승":"카드마다 리롤 1회 · 세 장 중 하나 선택",font,14,PaperTheme.Muted,false);
            CircuitArt.Label(new Rect(500,117,440,48),"증강 선택",font,33,ink,true);
            CircuitArt.Label(new Rect(491,171,458,22),Model.Players[AugmentViewSeat].Name+"  ·  보유 "+Model.Players[AugmentViewSeat].Augments.Count+" / 8",font,14,ink,false);
            bool canChoose=CanChooseAugment;
            for(int i=0;i<3;i++)
            {
                var r=AugmentCard(i);bool chosen=i==augmentCandidate,hovered=canChoose && r.Contains(Event.current.mousePosition);
                if(hovered)r.y-=3;
                var cardTier=Model.GetAugmentOfferTier(AugmentViewSeat,i);Color cardAccent=AugmentTierColor(cardTier);
                DrawAugmentPaper(r,cardTier,true,chosen,i+1);
                TierRibbon(new Rect(r.x+18,r.y+19,r.width-36,30),cardTier,cardAccent);
                CircuitArt.Label(new Rect(r.x+20,r.y+20,r.width-40,27),AugmentTierName(cardTier),font,15,ink,true);
                PaperTheme.Frame(new Rect(r.center.x-31,r.y+76,62,70),new Color(.2f,.22f,.15f,.30f),1.6f,i);
                PaperTheme.Underline(new Rect(r.x+33,r.y+170,r.width-66,1),new Color(.2f,.22f,.15f,.22f));
                PaperTheme.Underline(new Rect(r.x+25,r.yMax-61,r.width-50,1),ink);
                CircuitArt.Label(new Rect(r.x+20,r.yMax-44,r.width-40,27),chosen?"✓ 이 카드 선택":new[]{"선택지 I","선택지 II","선택지 III"}[i],font,15,ink,chosen);
                if(chosen)
                {
                    var stamp=new Rect(r.xMax-55,r.yMax-111,36,36);
                    CircuitArt.Circle(stamp,PaperTheme.Wash(accent,.85f));PaperTheme.Frame(stamp,ink,2);
                    CircuitArt.Label(stamp,"✓",font,22,ink,true);
                }
                if(canChoose && GUI.Button(AugmentCard(i),GUIContent.none,GUIStyle.none))SelectAugmentSlot(i);
                bool available=Model.CanRerollAugment(AugmentViewSeat,i),golden=Model.GoldenRerollAvailable(AugmentViewSeat);
                string rerollText=available?(golden?"황금 리롤 · 등급 +1":"이 카드 리롤 · 1 / 1"):"리롤 사용 완료";
                if(Button(new Rect(r.x+8,642,r.width-16,42),rerollText,golden?AugmentTierColor(AugmentTier.Gold):cardAccent,canChoose && available))RerollAugment(i);
                if(Model.GoldenRerollSlot(AugmentViewSeat)==i)CircuitArt.Label(new Rect(r.x+18,r.y+151,r.width-36,22),"황금 리롤 적용",font,12,ink,true);
            }
            PaperTheme.Card(new Rect(474,701,492,98),PaperTheme.Paper,true,2.5f);
            CircuitArt.Label(new Rect(487,707,466,23),canChoose?"한 장을 고른 뒤 확정하세요.":onlineGameAttached && onlineSession.Pending?"요청을 전달하고 있습니다…":"다른 플레이어의 선택을 기다리는 중…",font,14,ink,false);
            int ready=Model.Players.Count(p=>!p.Bankrupt && !Model.HasPendingAugment(p.Index)),total=Model.Players.Count(p=>!p.Bankrupt);
            CircuitArt.Label(new Rect(487,731,466,21),"전원 동시 선택  ·  "+ready+" / "+total+"명 완료",font,12,PaperTheme.Muted,false);
            if(Button(new Rect(559,758,322,33),"선택 확정",dark,canChoose && augmentCandidate>=0))ConfirmAugment();
            CircuitArt.Label(new Rect(398,810,644,23),canChoose?"1 · 2 · 3 선택 / R 선택 카드 리롤 / ENTER 확정 · 황금은 페이즈당 1회":"",font,12,ink,false);
        }

        static void DrawAugmentPaper(Rect r,AugmentTier tier,bool stacked,bool chosen,int variation=0)
        {
            Color accent=AugmentTierColor(tier);
            float strength=chosen?.57f:.37f;
            PaperTheme.Card(r,PaperTheme.Wash(accent,strength),stacked,chosen?4.4f:r.height>80?3.1f:1.4f,variation);
            if(tier==AugmentTier.Prism)
            {
                var face=new Rect(r.x+7,r.y+7,r.width-14,r.height-14);
                for(int stripe=0;stripe<48;stripe++)
                {
                    float t=stripe/47f;
                    Color tint=PaperTheme.Wash(PrismSpectrum(t*.81f),chosen?.64f:.47f);
                    CircuitArt.Fill(new Rect(face.x+face.width*stripe/48f,face.y,face.width/48f+1,face.height),tint);
                }
            }
            else if(r.height>80)
                PaperTheme.Surface(new Rect(r.x+8,r.y+8,7,r.height-16),accent);
            PaperTheme.Frame(r,PaperTheme.Ink,chosen?4.4f:r.height>80?3.1f:1.4f,variation);
        }
        static void CardFill(Rect r,Color c)=>PaperTheme.Card(r,PaperTheme.Wash(c,.16f),false,2.5f);
        static void CardOutline(Rect r,Color c,float width)=>PaperTheme.Frame(r,PaperTheme.Ink,width);
        static void TierRibbon(Rect r,AugmentTier tier,Color color)
        {
            if(tier!=AugmentTier.Prism)PaperTheme.Surface(r,PaperTheme.Wash(color,.92f));
            else for(int i=0;i<32;i++)CircuitArt.Fill(new Rect(r.x+r.width*i/32,r.y,r.width/32+1,r.height),PaperTheme.Wash(PrismSpectrum(i/31f),.92f));
            PaperTheme.Underline(new Rect(r.x,r.yMax,r.width,1),PaperTheme.Ink);
        }
        static void TierCardOutline(Rect r,AugmentTier tier,Color color,float width,float opacity)
        {
            PaperTheme.Frame(r,PaperTheme.Ink,Mathf.Max(1.4f,width));
            TierRibbon(new Rect(r.x+3,r.y+3,r.width-6,Mathf.Min(5,r.height*.22f)),tier,color);
        }
        static void Diamond(Vector2 p,float size,Color c)
        {
            CircuitArt.Line(p+Vector2.up*size,p+Vector2.right*size,c,1);
            CircuitArt.Line(p+Vector2.right*size,p+Vector2.down*size,c,1);
            CircuitArt.Line(p+Vector2.down*size,p+Vector2.left*size,c,1);
            CircuitArt.Line(p+Vector2.left*size,p+Vector2.up*size,c,1);
        }
        void TickAugmentAutomation(float delta)
        {
            if(Board.IsAnimating)return;
            int seat=onlineGameAttached?onlineSession.LocalSeat:Model.Players.FindIndex(p=>Model.HasPendingAugment(p.Index) && (!p.IsHuman || autoHuman));
            if(seat<0 || !Model.HasPendingAugment(seat) || (onlineGameAttached && (!autoHuman || !onlineSession.CanAct)))return;
            aiTimer-=delta;if(aiTimer>0)return;
            Act(Model.ChooseAugmentAction(seat));aiTimer=fast?.08f:.65f;
        }
        Rect OwnedAugmentIconRect(int seat,int index)
        {
            var card=PlayerCardRect(seat);
            return new Rect(card.x+3+index*37,card.y-23,33,20);
        }
        void DrawOwnedAugments(int seat,float x,float y)
        {
            var owned=Model.Players[seat].Augments;
            for(int i=0;i<owned.Count && i<Game.MaxAugments;i++)
            {
                var slot=OwnedAugmentIconRect(seat,i);var tier=owned[i].Tier;Color color=AugmentTierColor(tier);
                DrawAugmentPaper(slot,tier,false,false,i);TierCardOutline(slot,tier,color,1,1);
                // Generic gem is a UI placeholder until actual augment icons are supplied. No text on this strip.
                Diamond(new Vector2(slot.center.x,slot.center.y+1),5,ink);
                CircuitArt.Circle(new Rect(slot.center.x-1.5f,slot.center.y-.5f,3,3),ink);
                Vector2 pointer=augmentDiagnosticPointer??Event.current.mousePosition;
                if(!settings && !help && !onlineOpen && !AugmentOpen && slot.Contains(pointer))
                {augmentHoverSeat=seat;augmentHoverIndex=i;augmentHoverAnchor=slot;}
            }
        }
        void DrawOwnedAugmentTooltip()
        {
            if(augmentHoverSeat<0 || augmentHoverIndex<0)return;
            var player=Model.Players[augmentHoverSeat];var selection=player.Augments[augmentHoverIndex];
            var content=Game.BlankAugmentOptions[selection.OptionIndex];Color accent=AugmentTierColor(selection.Tier);
            float x=Mathf.Clamp(augmentHoverAnchor.x,12,1114),y=augmentHoverAnchor.y<450?augmentHoverAnchor.yMax+12:augmentHoverAnchor.y-154;
            var r=new Rect(x,y,312,145);PaperTheme.Card(r,PaperTheme.Wash(accent,.17f),true,2.8f);
            TierRibbon(new Rect(x+12,y+10,288,25),selection.Tier,accent);
            CircuitArt.Label(new Rect(x+14,y+11,284,23),AugmentTierName(selection.Tier)+" 증강",font,14,ink,true);
            var nameStyle=new GUIStyle(small){fontStyle=FontStyle.Bold,wordWrap=true};
            GUI.Label(new Rect(x+17,y+44,278,25),string.IsNullOrEmpty(content.Name)?"미설정 증강":content.Name,nameStyle);
            GUI.Label(new Rect(x+17,y+74,278,38),string.IsNullOrEmpty(content.Description)?"증강 내용이 아직 비어 있습니다.":content.Description,small);
            GUI.Label(new Rect(x+17,y+118,278,20),player.Name+" · "+(selection.GoldenRerolled?"황금 리롤로 획득":selection.Rerolled?"리롤 후 획득":"최초 선택에서 획득"),tiny);
        }
        [Serializable] sealed class RerollIconPreviewReport
        {public string status;public bool rerollAvailableBefore,rerollSpent,secondBlocked,candidateCleared,staleCardBlocked,selectedRerolled,hoverDoesNotMutate;public int owned;}
        IEnumerator RerollIconPreview(string path)
        {
            autoHuman=false;StartGame(new GameConfig{Seed=20261025,EnableAugments=true,HumanCount=humans,PlayerCount=players});
            settings=true;yield return new WaitForSeconds(.3f);settings=false;
            var report=new RerollIconPreviewReport{status="failed"};
            report.rerollAvailableBefore=Model.CanRerollAugment(0);string oldCard=Model.AugmentAction(0,0);
            SelectAugmentSlot(0);report.rerollSpent=RerollAugment(0) && Model.AugmentRevision(0)==1;
            report.candidateCleared=augmentCandidate<0;report.secondBlocked=!RerollAugment(0);report.staleCardBlocked=!Act(oldCard);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-rerolled.png");
            SelectAugmentSlot(0);ConfirmAugment();
            float until=Time.realtimeSinceStartup+8;while(AugmentOpen && Time.realtimeSinceStartup<until)yield return null;
            report.selectedRerolled=Model.Players[0].Augments.Count==1 && Model.Players[0].Augments[0].Rerolled;
            settings=true;
            for(int i=1;i<8;i++){Model.RequestSpecialAugment();while(Model.Phase==GamePhase.Augment)Model.Execute(Model.ChooseAiAction());}
            Board.Sync(false);settings=false;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-icons.png");
            string before=Marble.Online.OnlineProtocol.StateHash(Model);augmentDiagnosticPointer=OwnedAugmentIconRect(0,0).center;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-hover.png");
            report.hoverDoesNotMutate=before==Marble.Online.OnlineProtocol.StateHash(Model);report.owned=Model.Players[0].Augments.Count;augmentDiagnosticPointer=null;
            if(report.rerollAvailableBefore && report.rerollSpent && report.secondBlocked && report.candidateCleared && report.staleCardBlocked && report.selectedRerolled && report.hoverDoesNotMutate && report.owned==8)report.status="passed";
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
        [Serializable] sealed class CollectionPreviewReport
        {public string status;public int defaultChoices,capacity,ownedPerPlayer;public bool ninthBlocked,replayMatches;}
        IEnumerator AugmentCollectionPreview(string path)
        {
            StartGame(new GameConfig{Seed=20261025,EnableAugments=true,HumanCount=humans,PlayerCount=players});
            settings=true;yield return null;
            for(int draft=0;draft<8;draft++)
            {
                if(draft>0 && !Model.RequestSpecialAugment())throw new InvalidOperationException("Special preview draft failed");
                while(Model.Phase==GamePhase.Augment)Model.Execute(Model.ChooseAiAction());
            }
            Board.Sync(false);settings=false;autoHuman=false;
            var restored=new Game(Model.Config);bool matches=true;foreach(var action in Model.ReplayActions)matches &= restored.Execute(action);
            var report=new CollectionPreviewReport{status="passed",defaultChoices=new GameConfig().AugmentRounds.Length,capacity=Game.MaxAugments,ownedPerPlayer=Model.Players[0].Augments.Count,ninthBlocked=!Model.RequestSpecialAugment(),replayMatches=matches && Marble.Online.OnlineProtocol.StateHash(Model)==Marble.Online.OnlineProtocol.StateHash(restored)};
            if(report.ownedPerPlayer!=8 || !report.ninthBlocked || !report.replayMatches)report.status="failed";
            yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+".png");
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
        [Serializable] sealed class AugmentPreviewReport
        {public string status;public int chosenSlot,selectionCount,replayActions;public string chosenTier;public bool emptyContent,rollBlockedBefore,confirmationRequired,resumed,duplicateBlocked;}
        [Serializable] sealed class TierPreviewReport
        {public string status;public string[] tiers;public bool allChoicesRecorded,emptyContent;}
        IEnumerator AugmentTierPreview(string path)
        {
            autoHuman=false;
            var report=new TierPreviewReport{status="passed",tiers=new[]{"Bronze","Silver","Gold","Prism"},allChoicesRecorded=true,emptyContent=true};
            for(int i=0;i<4;i++)
            {
                int seed=0;Game sample;
                do{sample=new Game(new GameConfig{Seed=seed++,EnableAugments=true});}while((int)sample.GetAugmentTier(0)!=i);
                StartGame(sample.Config);settings=true;
                yield return new WaitForSeconds(.15f);settings=false;
                SelectAugmentSlot(1);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-"+report.tiers[i].ToLowerInvariant()+".png");
                if(!ConfirmAugment() || Model.Players[0].Augments[0].Tier!=(AugmentTier)i)report.allChoicesRecorded=false;
            }
            report.emptyContent=Array.TrueForAll(Game.BlankAugmentOptions,o=>o.Name=="" && o.Description=="" && o.Icon=="");
            if(!report.allChoicesRecorded || !report.emptyContent)report.status="failed";
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));
            yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
        IEnumerator AugmentPreview(string path)
        {
            autoHuman=false;yield return new WaitForSeconds(.3f);
            var report=new AugmentPreviewReport{status="failed",chosenSlot=1};
            report.emptyContent=Array.TrueForAll(Game.BlankAugmentOptions,o=>o.Name=="" && o.Description=="" && o.Icon=="");
            report.rollBlockedBefore=!Model.Execute("roll");report.confirmationRequired=!ConfirmAugment();
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-empty.png");
            SelectAugmentSlot(1);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-selected.png");
            bool confirmed=ConfirmAugment();report.selectionCount=Model.Players[0].Augments.Count;
            report.chosenTier=Model.Players[0].Augments[0].Tier.ToString();
            report.duplicateBlocked=!Act("augment:0:0:1");
            float until=Time.realtimeSinceStartup+8;
            while(AugmentOpen && Time.realtimeSinceStartup<until)yield return null;
            report.resumed=confirmed && Model.Phase==GamePhase.AwaitRoll;report.replayActions=Model.ReplayActions.Count;
            report.status=report.emptyContent && report.rollBlockedBefore && report.confirmationRequired && report.resumed && report.selectionCount==1 && report.replayActions==Model.Players.Count && report.duplicateBlocked?"passed":"failed";
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-owned.png");
            yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
    }
}
