using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        [Serializable] sealed class DiceRuntimeReport
        {
            public string status="running",unityVersion,gameVersion;
            public bool flightObserved,movementWaitedForDice,resultFacesCorrect,arrivedAtModelPosition,reducedMotionSkippedThrow,groundClearanceValid=true,impactsOrdered,visualFirstStepForward=true;
            public float maximumHeight,minimumGroundClearance=float.MaxValue;
            public bool screenFramesRendered=true,diceFramesRendered=true;
            public int guiRepaints;
            public int diceA,diceB,destination;
            public int diceImpactCount;
            public HighRollCase[] highRollCases;
            public bool highRollsExact=true,eventArrivalPauseObserved,eventReasonVisible;
            public string[] errors;
        }
        [Serializable] sealed class HighRollCase
        {
            public int start,sum,seed,diceHops,eventHops,expectedFootsteps,renderedFootsteps,finalPosition;
            public bool bodyEconomy,arrived,exactDiceCount;
        }
        int runtimeGuiRepaints;
        IEnumerator DiceRuntimeCheck(string folder)
        {
            Application.runInBackground=true;folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);
            var report=new DiceRuntimeReport{unityVersion=Application.unityVersion,gameVersion=Application.version,movementWaitedForDice=true};
            var errors=new List<string>();Application.LogCallback collect=(message,trace,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=collect;
            InstallModel(new Game(new GameConfig{Seed=7,HumanCount=1,PlayerCount=4,EnableAugments=false}));
            paused=true;autoHuman=false;mutedSound=true;if(preferences!=null)preferences.ReducedMotion=false;Board.ReducedMotion=false;
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();CaptureRuntimeFrame(folder,"01-ready",report);
            var impactSequence=new List<int>();Board.DiceImpact+=impact=>{report.diceImpactCount++;impactSequence.Add(impact);};
            Vector2 start=Board.DisplayedPawnPosition(0);float previousVisibleX=Board.VisiblePawnPosition(0).x;
            if(!Act(Game.DiceControlAction(5,0)))errors.Add("Roll command was rejected.");
            report.diceA=Model.DiceA;report.diceB=Model.DiceB;report.destination=Model.Players[0].Position;
            float elapsed=0;int capture=0;float[] moments={.25f,.75f,1.25f,DiceThrowMotion.Duration+.05f};string[] names={"02-flight","03-bounce","03b-ground-roll","04-result"};
            while(Board.IsAnimating && elapsed<20)
            {
                report.maximumHeight=Mathf.Max(report.maximumHeight,Board.DiceMaximumHeight);
                report.minimumGroundClearance=Mathf.Min(report.minimumGroundClearance,Board.DiceMinimumClearance);
                if(Board.DiceMinimumClearance<-.001f)report.groundClearanceValid=false;
                if(Board.DiceInFlight){report.flightObserved=true;if(Vector2.Distance(start,Board.DisplayedPawnPosition(0))>.01f)report.movementWaitedForDice=false;}
                else if(Board.CompletedDiceSteps(0)==0)
                {
                    float visibleX=Board.VisiblePawnPosition(0).x;
                    if(visibleX>previousVisibleX+.75f)report.visualFirstStepForward=false;
                    previousVisibleX=visibleX;
                }
                if(capture<moments.Length && elapsed>=moments[capture])
                {yield return new WaitForEndOfFrame();CaptureRuntimeFrame(folder,names[capture],report);capture++;}
                yield return null;elapsed+=Time.deltaTime;
            }
            if(elapsed>=20)errors.Add("Animation did not finish within 20 seconds.");
            report.resultFacesCorrect=Board.DiceFacesMatchResult;
            report.impactsOrdered=impactSequence.SequenceEqual(new[]{0,1,2,3,4,5});
            report.arrivedAtModelPosition=Vector2.Distance(Board.DisplayedPawnPosition(0),Board.TileGuiPoint(Model.Players[0].Position)+new Vector2(-14,6))<.1f;
            yield return new WaitForEndOfFrame();CaptureRuntimeFrame(folder,"05-arrived",report);
            InstallModel(new Game(new GameConfig{Seed=7,HumanCount=1,PlayerCount=4,EnableAugments=false}));
            paused=true;if(preferences!=null)preferences.ReducedMotion=true;Board.ReducedMotion=true;
            Act(Game.DiceControlAction(5,0));
            report.reducedMotionSkippedThrow=!Board.DiceInFlight;
            elapsed=0;
            while(Board.IsAnimating && elapsed<10)
            {if(Board.DiceMaximumHeight>.001f)report.reducedMotionSkippedThrow=false;yield return null;elapsed+=Time.deltaTime;}
            yield return new WaitForEndOfFrame();CaptureRuntimeFrame(folder,"06-reduced-motion",report);
            var highCases=new List<HighRollCase>();
            int[,] cases={{0,4,6,7,0},{0,5,5,7,1},{0,6,4,7,0},{0,5,6,7,1},{0,6,5,7,0},{0,6,6,4,1},{0,6,6,7,0},{31,6,6,7,1},{24,6,6,7,0}};
            for(int c=0;c<cases.GetLength(0);c++)
            {
                int from=cases[c,0],a=cases[c,1],b=cases[c,2],seed=cases[c,3];
                var game=new Game(new GameConfig{Seed=seed,HumanCount=1,PlayerCount=2,EnableAugments=false,EnableBodyEconomy=cases[c,4]==1});
                game.Players[0].Position=from;InstallModel(game);paused=true;autoHuman=false;
                preferences.ReducedMotion=false;Board.ReducedMotion=false;
                yield return null;yield return new WaitForEndOfFrame();
                int stepsRendered=0;Board.StepSound+=()=>stepsRendered++;
                game.TestRoll(a,b);Board.Sync(true);
                var row=new HighRollCase{start=from,sum=a+b,seed=seed,bodyEconomy=game.Config.EnableBodyEconomy,diceHops=game.ActionMovement.Count(s=>s.Dice),eventHops=game.ActionMovement.Count(s=>!s.Dice),expectedFootsteps=game.ActionMovement.Count(s=>!s.Transfer),finalPosition=game.Players[0].Position};
                elapsed=0;bool capturedPause=false;
                while(Board.IsAnimating && elapsed<25)
                {
                    if(Board.EventMovementPaused)
                    {
                        report.eventArrivalPauseObserved=true;
                        if(c==6 && !capturedPause){yield return new WaitForEndOfFrame();CaptureRuntimeFrame(folder,"07-twelve-card-arrival",report);capturedPause=true;}
                    }
                    if(!Board.EventMovementPaused && Board.MovementCaption.StartsWith(GameLabels.EventCard,StringComparison.Ordinal))report.eventReasonVisible=true;
                    yield return null;elapsed+=Time.deltaTime;
                }
                row.renderedFootsteps=stepsRendered;
                row.arrived=!Board.IsAnimating && Vector2.Distance(Board.DisplayedPawnPosition(0),Board.TileGuiPoint(row.finalPosition)+new Vector2(-14,6))<.1f;
                row.exactDiceCount=row.diceHops==row.sum && Board.TotalDiceSteps(0)==row.sum && Board.CompletedDiceSteps(0)==row.sum;
                report.highRollsExact &= row.arrived && row.exactDiceCount && row.renderedFootsteps==row.expectedFootsteps && Board.DiceFacesMatchResult;
                highCases.Add(row);
                if(c==6){yield return new WaitForEndOfFrame();CaptureRuntimeFrame(folder,"08-twelve-card-finished",report);}
            }
            report.highRollCases=highCases.ToArray();
            Application.logMessageReceived-=collect;report.errors=errors.ToArray();report.guiRepaints=runtimeGuiRepaints;
            report.status=report.visualFirstStepForward && report.diceImpactCount==6 && report.impactsOrdered && report.groundClearanceValid && report.highRollsExact && report.eventArrivalPauseObserved && report.eventReasonVisible && report.flightObserved && report.maximumHeight>.5f && report.movementWaitedForDice && report.resultFacesCorrect && report.arrivedAtModelPosition && report.reducedMotionSkippedThrow && report.diceFramesRendered && report.screenFramesRendered && report.guiRepaints>0 && errors.Count==0?"passed":"failed";
            File.WriteAllText(Path.Combine(folder,"runtime-validation.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.status=="passed"?0:1);
        }
        void CaptureRuntimeFrame(string folder,string name,DiceRuntimeReport report)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());report.screenFramesRendered &= HasRenderedPixels(texture);}
            finally{Destroy(texture);}
            var previous=RenderTexture.active;var dice=Board.DiceTexture;
            var sample=new Texture2D(dice.width,dice.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=dice;sample.ReadPixels(new Rect(0,0,dice.width,dice.height),0,0);sample.Apply();
                File.WriteAllBytes(Path.Combine(folder,name+"-dice.png"),sample.EncodeToPNG());report.diceFramesRendered &= HasRenderedPixels(sample);
            }
            finally{RenderTexture.active=previous;Destroy(sample);}
        }
        static bool HasRenderedPixels(Texture2D texture)
        {
            int visible=0;foreach(var pixel in texture.GetPixels32())if(pixel.a>20 && pixel.r+pixel.g+pixel.b>60 && ++visible>100)return true;
            return false;
        }
    }
}
