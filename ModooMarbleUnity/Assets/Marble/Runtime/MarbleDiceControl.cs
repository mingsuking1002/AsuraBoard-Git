using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        bool diceCharging;float diceChargeAt;int diceChargeSeat=-1;
        float DiceGauge=>diceCharging?Mathf.PingPong((Time.unscaledTime-diceChargeAt)*.72f,1):.5f;
        bool CanControlDice=>Model!=null && Model.Phase==GamePhase.AwaitRoll && LocalTurn && !autoHuman && Board!=null && !Board.IsAnimating && !bodyMarketOpen && !bodyAudioSettings && !preferencesOpen && !settings && !help && !onlineOpen && !paused && (!onlineGameAttached || onlineSession.CanAct);
        void BeginDiceCharge(){if(!CanControlDice)return;diceCharging=true;diceChargeAt=Time.unscaledTime;diceChargeSeat=Model.CurrentPlayerIndex;}
        void ReleaseDiceCharge()
        {
            if(!diceCharging)return;int power=Mathf.RoundToInt(DiceGauge*10);bool valid=CanControlDice && diceChargeSeat==Model.CurrentPlayerIndex;diceCharging=false;
            if(valid)Act(Game.DiceControlAction(power,0));
        }
        void TickDiceControl()
        {
            if(!CanControlDice){diceCharging=false;return;}
            if(Input.GetKeyDown(KeyCode.Space))BeginDiceCharge();
            if(Input.GetKeyUp(KeyCode.Space))ReleaseDiceCharge();
        }
        void OnApplicationFocus(bool focused){if(!focused){diceCharging=false;if(preferences!=null && preferences.MuteMicWhenUnfocused){try{roomVoice?.Stop();}catch{}}}}
        void DrawDiceControl(bool ready)
        {
            ready=ready && CanControlDice;float gauge=DiceGauge;int target=Game.DiceControlTarget(Mathf.RoundToInt(gauge*10),0);
            if(diceCharging)Board.DrawTileOutline((Model.CurrentPlayer.Position+target)%Model.Tiles.Count,MarbleBoardView.PlayerColors[3],3);
            var bar=new Rect(520,510,400,22);PaperTheme.Card(bar,PaperTheme.Paper,false,1.5f);
            CircuitArt.Fill(new Rect(bar.x+5,bar.y+5,bar.width-10,10),PaperTheme.Wash(PaperTheme.Table,.18f));
            CircuitArt.Fill(new Rect(bar.x+5,bar.y+5,(bar.width-10)*gauge,10),PaperTheme.Forest);
            for(int i=1;i<4;i++)CircuitArt.Fill(new Rect(bar.x+5+(bar.width-10)*i/4f,bar.y+5,1,10),new Color(1,1,1,.55f));
            var gaugeLabel=new GUIStyle(tiny){alignment=TextAnchor.MiddleCenter,wordWrap=false};
            GUI.Label(new Rect(492,509,24,23),"2",gaugeLabel);GUI.Label(new Rect(924,509,28,23),"12",gaugeLabel);
            var r=new Rect(660,549,120,70);int control=GUIUtility.GetControlID(0x57434443,FocusType.Passive,r);var e=Event.current;
            if(e.type==EventType.MouseDown && e.button==0 && ready && r.Contains(e.mousePosition)){GUIUtility.hotControl=control;BeginDiceCharge();e.Use();}
            if(e.rawType==EventType.MouseUp && GUIUtility.hotControl==control){GUIUtility.hotControl=0;ReleaseDiceCharge();e.Use();}
            bool enabled=GUI.enabled;GUI.enabled=enabled && ready;
            CircuitArt.Circle(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(.13f,.16f,.10f,.3f));
            CircuitArt.Circle(r,ink);CircuitArt.Circle(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),PaperTheme.Paper);
            CircuitArt.Circle(new Rect(r.x+6,r.y+6,r.width-12,r.height-12),PaperTheme.Wash(MarbleBoardView.PlayerColors[3],diceCharging?.85f:.58f));
            GUI.Label(new Rect(r.x+3,r.y+6,r.width-6,r.height-12),diceCharging?"놓기 · "+target:"ROLL",center);GUI.enabled=enabled;
            var hint=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12,wordWrap=false};
            string hintText=ready?"SPACE 길게 → 놓기 · 목표는 확률 보정":Board.IsAnimating?"이동 중…":paused?"일시정지 중":autoHuman?"자동 진행 중":!LocalTurn?"상대 차례 · 진행 중":onlineGameAttached && !onlineSession.CanAct?"요청 처리 중…":"잠시 기다려 주세요.";
            GUI.Label(new Rect(590,625,260,25),hintText,hint);
        }
    }
}
