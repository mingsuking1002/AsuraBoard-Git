using System;
using System.Collections;
using System.IO;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        [Serializable] sealed class PaperPreviewReport
        {public string status;public string[] screens;public bool sameBoardLayout,sameRules,emptyAugments;}
        IEnumerator PaperPreview(string path)
        {
            autoHuman=false;
            StartGame(new GameConfig{EnableAugments=true,HumanCount=humans,PlayerCount=players});
            settings=true;yield return new WaitForSeconds(.3f);
            while(Model.Phase==GamePhase.Augment)Model.Execute(Model.ChooseAiAction());
            settings=false;
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-board.png");
            settings=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-menu.png");
            settings=false;help=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-help.png");
            help=false;onlineOpen=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-steam-connect.png");
            onlineOpen=false;selected=1;showHistory=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-details.png");
            selected=-1;showHistory=false;
            StartGame(new GameConfig{EnableAugments=true,HumanCount=humans,PlayerCount=players,MaxTurns=1});
            while(Model.Phase==GamePhase.Augment)Model.Execute(Model.ChooseAiAction());
            Model.TestLand(0,0);Model.Execute("end");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path+"-result.png");
            var start=MarbleBoardView.CellCenter(0);
            var report=new PaperPreviewReport{status="passed",screens=new[]{"board","menu","help","steam-connect","details","result"},sameBoardLayout=Mathf.Abs(start.x-720)<.01f && Mathf.Abs(start.y-770.088f)<.01f && MarbleBoardView.HitTestIndex(start)==0,sameRules=Model.Phase==GamePhase.Finished,emptyAugments=Array.TrueForAll(Game.BlankAugmentOptions,a=>a.Name=="" && a.Description=="" && a.Icon=="")};
            if(!report.sameBoardLayout || !report.sameRules || !report.emptyAugments)report.status="failed";
            File.WriteAllText(path+".json",JsonUtility.ToJson(report,true));
            yield return new WaitForSeconds(1);Application.Quit(report.status=="passed"?0:1);
        }
    }
}
