using System;
using System.Collections.Generic;
namespace Marble.Core
{
    // Presentation-only state: authoritative game/room state remains in Game and OnlineSession.
    public enum UiSurface { None,TileDetails,BodyMarket,MatchMenu,Preferences,PlayerAudio,Help,ConfirmLeaveRoom,ConfirmEndMatch,Construction,AugmentDraft,DebtSettlement,ConnectionLost }
    [Flags] public enum UiInput { None=0,Navigation=1,VoiceShortcut=2,Text=4,Inspect=8,Board=16,Dice=32,Decision=64,Augment=128,Trade=256 }
    public static class PaperUiMetrics
    {
        public const int ReferenceWidth=1440,ReferenceHeight=900,PageInset=24,PanelGap=16,ControlGap=8;
        public const int MinimumControlHeight=44,BodyFont=18,SmallFont=14,HeadingFont=30;
        public const int TooltipDelayMs=350,TooltipMaxWidth=360;
    }
    public sealed class UiLayerController
    {
        sealed class Panel {public UiSurface Surface;public string ReturnFocus;}
        readonly List<Panel> panels=new List<Panel>();
        UiSurface required;
        public PlayScreen Screen {get;private set;}=PlayScreen.Main;
        public bool ConnectionUnavailable {get;private set;}
        public bool EditingText {get;set;}
        public string RestoredFocus {get;private set;}="";
        public int PanelCount=>panels.Count;
        public UiSurface Required=>required;
        public UiSurface Top=>ConnectionUnavailable?UiSurface.ConnectionLost:panels.Count>0?panels[panels.Count-1].Surface:required;
        public void SyncScreen(PlayScreen screen)
        {
            if(!Enum.IsDefined(typeof(PlayScreen),screen))throw new ArgumentOutOfRangeException(nameof(screen));
            if(Screen==screen)return;
            Screen=screen;panels.Clear();required=UiSurface.None;EditingText=false;RestoredFocus="";
        }
        public void SetRequired(UiSurface surface)
        {
            if(surface!=UiSurface.None&&surface!=UiSurface.Construction&&surface!=UiSurface.AugmentDraft&&surface!=UiSurface.DebtSettlement)throw new ArgumentException("Not a required decision surface.");
            if(surface!=UiSurface.None&&Screen!=PlayScreen.Game)throw new InvalidOperationException("Decisions require a match.");
            required=surface;
            // A new authoritative phase invalidates an old local trade/details panel.
            if(surface!=UiSurface.None)panels.RemoveAll(p=>p.Surface==UiSurface.BodyMarket||p.Surface==UiSurface.TileDetails);
        }
        public void SetConnectionUnavailable(bool unavailable){ConnectionUnavailable=unavailable;EditingText=false;}
        public bool Open(UiSurface surface,string returnFocus="")
        {
            if(ConnectionUnavailable||!CanOpen(surface))return false;
            if(panels.Exists(p=>p.Surface==surface))return false;
            panels.Add(new Panel{Surface=surface,ReturnFocus=returnFocus??""});EditingText=false;RestoredFocus="";return true;
        }
        bool CanOpen(UiSurface surface)
        {
            switch(surface)
            {
                case UiSurface.Preferences:case UiSurface.Help:return true;
                case UiSurface.PlayerAudio:return Screen==PlayScreen.Lobby||Screen==PlayScreen.Game||Screen==PlayScreen.Results;
                case UiSurface.MatchMenu:case UiSurface.ConfirmEndMatch:return Screen==PlayScreen.Game;
                case UiSurface.BodyMarket:case UiSurface.TileDetails:return Screen==PlayScreen.Game&&required==UiSurface.None;
                case UiSurface.ConfirmLeaveRoom:return Screen==PlayScreen.Lobby||Screen==PlayScreen.Results;
                default:return false;
            }
        }
        public bool Back()
        {
            if(ConnectionUnavailable)return false;
            if(panels.Count>0)
            {
                var top=panels[panels.Count-1];panels.RemoveAt(panels.Count-1);RestoredFocus=top.ReturnFocus;EditingText=false;return true;
            }
            // Opening the menu never cancels a construction/draft/debt choice underneath it.
            return Screen==PlayScreen.Game&&Open(UiSurface.MatchMenu);
        }
        public UiInput AllowedInputs
        {
            get
            {
                const UiInput common=UiInput.Navigation|UiInput.VoiceShortcut;
                UiInput inputs;
                switch(Top)
                {
                    case UiSurface.None:inputs=common|(Screen==PlayScreen.Game?UiInput.Board|UiInput.Dice|UiInput.Inspect|UiInput.Trade:UiInput.Text);break;
                    case UiSurface.TileDetails:inputs=common|UiInput.Inspect;break;
                    case UiSurface.Construction:inputs=common|UiInput.Decision;break;
                    case UiSurface.AugmentDraft:inputs=common|UiInput.Augment;break;
                    case UiSurface.DebtSettlement:case UiSurface.BodyMarket:inputs=common|UiInput.Trade;break;
                    case UiSurface.ConnectionLost:inputs=UiInput.Navigation|UiInput.VoiceShortcut;break;
                    default:inputs=common|UiInput.Text;break;
                }
                // Letter shortcuts (including mic mute) must not fire while typing a name/code.
                return EditingText?inputs&(UiInput.Navigation|UiInput.Text):inputs;
            }
        }
        public bool Allows(UiInput input)=>input!=UiInput.None&&(AllowedInputs&input)==input;
    }
    public sealed class UiCommandLatch
    {
        long serial,pending;
        public bool IsPending=>pending!=0;
        public string Action {get;private set;}="";
        public long Revision {get;private set;}
        public bool TryBegin(string action,bool actorCanAct,bool legalAction,long revision,out long ticket)
        {
            ticket=0;if(IsPending||!actorCanAct||!legalAction||string.IsNullOrWhiteSpace(action)||revision<0||serial==long.MaxValue)return false;
            pending=++serial;Action=action;Revision=revision;ticket=pending;return true;
        }
        public bool Complete(long ticket)
        {if(ticket==0||ticket!=pending)return false;Clear();return true;}
        public void Clear(){pending=0;Action="";Revision=0;}
    }
}
