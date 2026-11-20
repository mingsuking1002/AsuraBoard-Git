using System;
namespace Marble.Core
{
    public static class UiFrameworkSelfTests
    {
        static void Check(bool value,string label){if(!value)throw new InvalidOperationException("UI framework: "+label);}
        public static int Run()
        {
            int n=0;var ui=new UiLayerController();Check(ui.Screen==PlayScreen.Main&&!ui.Allows(UiInput.Dice)&&ui.Allows(UiInput.Navigation),"main input scope");n++;
            ui.SyncScreen(PlayScreen.Game);Check(ui.Allows(UiInput.Dice)&&ui.Allows(UiInput.Board),"match input scope");n++;
            Check(ui.Open(UiSurface.MatchMenu)&&ui.Open(UiSurface.Preferences,"sound-tab")&&ui.Open(UiSurface.PlayerAudio,"voice-mixer")&&!ui.Allows(UiInput.Dice),"nested modal blocks dice");n++;
            Check(ui.Back()&&ui.Top==UiSurface.Preferences&&ui.RestoredFocus=="voice-mixer"&&ui.Back()&&ui.Top==UiSurface.MatchMenu,"back closes only top and restores focus");n++;
            ui=new UiLayerController();ui.SyncScreen(PlayScreen.Game);ui.SetRequired(UiSurface.AugmentDraft);Check(ui.Allows(UiInput.Augment)&&!ui.Allows(UiInput.Board)&&!ui.Open(UiSurface.BodyMarket),"draft is exclusive");n++;
            Check(ui.Back()&&ui.Top==UiSurface.MatchMenu&&ui.Required==UiSurface.AugmentDraft&&ui.Back()&&ui.Top==UiSurface.AugmentDraft,"escape preserves mandatory draft");n++;
            ui.SetRequired(UiSurface.DebtSettlement);Check(ui.Allows(UiInput.Trade)&&!ui.Allows(UiInput.Augment)&&!ui.Open(UiSurface.TileDetails),"repayment scope");n++;
            ui.Open(UiSurface.Preferences);ui.SetConnectionUnavailable(true);Check(ui.Top==UiSurface.ConnectionLost&&!ui.Back()&&!ui.Open(UiSurface.Help),"connection overrides existing modal");ui.SetConnectionUnavailable(false);Check(ui.Top==UiSurface.Preferences&&ui.Required==UiSurface.DebtSettlement,"connection recovery restores context");n++;
            ui.EditingText=true;Check(!ui.Allows(UiInput.VoiceShortcut)&&ui.Allows(UiInput.Text),"typing suppresses letter shortcuts");n++;
            ui.SyncScreen(PlayScreen.Lobby);Check(ui.Top==UiSurface.None&&ui.PanelCount==0&&ui.Required==UiSurface.None&&!ui.EditingText,"room transition clears stale decisions");n++;
            Check(ui.Open(UiSurface.Preferences)&&!ui.Open(UiSurface.Preferences)&&ui.PanelCount==1&&!ui.Open(UiSurface.Construction),"duplicate and system-only panels rejected");n++;
            var latch=new UiCommandLatch();long ticket=0;Check(!latch.TryBegin("purchase:1",false,true,1,out _)&&latch.TryBegin("purchase:1",true,true,1,out ticket)&&!latch.TryBegin("purchase:1",true,true,1,out _),"single pending purchase and authority gate");n++;
            Check(!latch.Complete(ticket+1)&&latch.IsPending&&latch.Complete(ticket)&&!latch.IsPending,"only matching reply completes command");n++;
            Check(latch.TryBegin("roll",true,true,2,out long newer)&&newer!=ticket&&!latch.Complete(ticket),"old reply cannot unlock new request");latch.Clear();Check(!latch.IsPending,"disconnect clears pending action");n++;
            return n;
        }
    }
}
