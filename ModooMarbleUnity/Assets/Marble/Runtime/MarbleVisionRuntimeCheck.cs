using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        [Serializable] sealed class VisionRuntimeReport
        {
            public string status="running",gameVersion;
            public float rightMissingBrightness,rightOpenBrightness,leftMissingBrightness,leftOpenBrightness,bothBrightness,comfortBrightness,restoredDifference;
            public float nearVisionDifference,distantBrightness,movingNearVisionDifference,movingDistantBrightness,visionCenterTravel;
            public float gradientNearVisibility,gradientMiddleVisibility,gradientFarVisibility,gradientEdgeVisibility,movingDistantVisibility;
            public float rightTransitionNear,rightTransitionMiddle,rightTransitionFar,leftTransitionNear,leftTransitionMiddle,leftTransitionFar;
            public bool framesRendered=true,uiUnchanged=true,sideSpecific,comfortReducedMask,restoredVision,otherSeatUnaffected,disabledEconomyUnaffected,movingVisionAnchored;
            public bool nearVisionVisible,distantWorldDimmed,nearVisionFollowsPawn,progressiveGradient;
            public bool singleEyeGradientExpanded;
            public bool downloadedFloorLoaded;
            public int floorTiles,floorSourceVertices;
            public int signatureColorPanels;
            public bool signatureColorsVisible;
            public bool downloadedBuildingLoaded,miniaturesFaceInward;
            public int visibleMiniatureBuildings,buildingSourceVertices;
            public bool fortuneTilesNeutral,resortsMatchLineColors,bonusColorPreserved,ownedResortMiniatureVisible;
            public bool fourPawnPortraitsLoaded,fourPawnsSeparated;
            public int fortuneTiles,bonusTiles,resortTiles;
            public float floorTopAlignmentError;
            public string[] errors, failedChecks;
        }
        IEnumerator VisionRuntimeCheck(string folder)
        {
            Application.runInBackground=true;Directory.CreateDirectory(folder);
            var report=new VisionRuntimeReport{gameVersion=Application.version};
            var errors=new List<string>();Application.LogCallback collect=(message,trace,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=collect;
            InstallModel(new Game(new GameConfig{Seed=7,HumanCount=1,PlayerCount=2,EnableBodyEconomy=true,EnableAugments=false}));
            paused=true;autoHuman=false;mutedSound=true;preferences.Width=1440;preferences.Height=900;preferences.WindowMode=0;ApplyClientPreferences(true);preferences.ComfortBlur=false;preferences.ReducedMotion=false;
            foreach(int index in new[]{1,10,17,26}){Model.Tiles[index].OwnerIndex=0;Model.Tiles[index].Buildings=7;}
            Model.Tiles[5].OwnerIndex=1;
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
            var normal=CaptureVisionFrame(folder,"01-normal",report);
            report.downloadedFloorLoaded=Board.UsesDownloadedFloor;report.floorTiles=Board.FloorTileCount;report.floorSourceVertices=Board.FloorSourceVertexCount;report.floorTopAlignmentError=Board.FloorTopAlignmentError;
            report.signatureColorPanels=Board.SignatureColorPanelCount;
            report.downloadedBuildingLoaded=Board.UsesMiniatureBuildings;report.visibleMiniatureBuildings=Board.VisibleMiniatureBuildings;report.buildingSourceVertices=Board.BuildingSourceVertices;report.miniaturesFaceInward=Board.MiniaturesFaceInward;
            report.fortuneTilesNeutral=true;report.resortsMatchLineColors=true;
            foreach(var tile in Model.Tiles)
            {
                if(tile.Definition.Kind==TileKind.Chance)
                {
                    report.fortuneTiles++;
                    report.fortuneTilesNeutral &= ColorDistance(Board.TileSurfaceColor(tile.Index),PaperTheme.Paper)<.001f && !Board.HasSignaturePanel(tile.Index);
                }
                else if(tile.Definition.Kind==TileKind.Bonus)report.bonusTiles++;
                else if(tile.Definition.Kind==TileKind.Resort)
                {
                    report.resortTiles++;
                    report.resortsMatchLineColors &= ColorDistance(Board.TileSurfaceColor(tile.Index),MarbleBoardView.LineColors[tile.Index/8])<.001f && !Board.HasSignaturePanel(tile.Index);
                }
            }
            report.bonusColorPreserved=ColorDistance(Board.TileSurfaceColor(2),MarbleBoardView.Hex("FFF0C9"))<.001f && Board.HasSignaturePanel(2);
            report.ownedResortMiniatureVisible=Board.MiniatureVisible(5,1);
            // Sample unbuilt tiles: buildings now intentionally cover the inward panels.
            var green=FramePixel(normal,Board.SignatureColorPoint(3));var blue=FramePixel(normal,Board.SignatureColorPoint(11));
            var purple=FramePixel(normal,Board.SignatureColorPoint(19));var orange=FramePixel(normal,Board.SignatureColorPoint(27));
            report.signatureColorsVisible=green.g>green.r*1.3f && green.g>green.b*1.15f && blue.b>blue.r*1.3f && blue.b>blue.g*1.1f && purple.r>purple.g*1.2f && purple.b>purple.g*1.2f && orange.r>orange.g*1.2f && orange.g>orange.b*1.3f;
            var startingVisionCenter=BodyVisionCenter;
            int[] masks={2,1,3};string[] names={"02-right-eye","03-left-eye","04-both-eyes"};
            for(int i=0;i<masks.Length;i++)
            {
                Model.Players[0].SoldBodyMask=masks[i];yield return null;yield return new WaitForEndOfFrame();
                var frame=CaptureVisionFrame(folder,names[i],report);
                if(i==0)
                {
                    report.rightMissingBrightness=SideBrightness(frame,true);report.rightOpenBrightness=SideBrightness(frame,false);
                    report.rightTransitionNear=VisionVisibility(normal,frame,new Vector2(900,450));
                    report.rightTransitionMiddle=VisionVisibility(normal,frame,new Vector2(1080,450));
                    report.rightTransitionFar=VisionVisibility(normal,frame,new Vector2(1260,450));
                }
                if(i==1)
                {
                    report.leftMissingBrightness=SideBrightness(frame,false);report.leftOpenBrightness=SideBrightness(frame,true);
                    report.leftTransitionNear=VisionVisibility(normal,frame,new Vector2(540,450));
                    report.leftTransitionMiddle=VisionVisibility(normal,frame,new Vector2(360,450));
                    report.leftTransitionFar=VisionVisibility(normal,frame,new Vector2(180,450));
                }
                if(i==2)
                {
                    report.bothBrightness=Mathf.Max(SideBrightness(frame,false),SideBrightness(frame,true),PatchBrightness(frame,720,340));
                    report.nearVisionDifference=VisionPatchDifference(normal,frame,BodyVisionCenter+new Vector2(40,20));
                    var distant=BodyVisionCenter+new Vector2(170,0);
                    report.distantBrightness=PatchBrightness(frame,Mathf.RoundToInt(distant.x),Mathf.RoundToInt(distant.y));
                    report.nearVisionVisible=report.nearVisionDifference<.005f;
                    report.gradientNearVisibility=VisionVisibility(normal,frame,BodyVisionCenter+new Vector2(100,0));
                    report.gradientMiddleVisibility=VisionVisibility(normal,frame,BodyVisionCenter+new Vector2(200,0));
                    report.gradientFarVisibility=VisionVisibility(normal,frame,BodyVisionCenter+new Vector2(400,0));
                    report.gradientEdgeVisibility=VisionVisibility(normal,frame,BodyVisionCenter+new Vector2(600,0));
                    report.progressiveGradient=report.gradientNearVisibility>.4f && report.gradientNearVisibility<.9f && report.gradientMiddleVisibility>.05f && report.gradientMiddleVisibility<report.gradientNearVisibility-.15f && report.gradientFarVisibility>.01f && report.gradientFarVisibility<.15f && report.gradientEdgeVisibility>.01f && report.gradientEdgeVisibility<=report.gradientFarVisibility+.01f;
                    report.distantWorldDimmed=report.gradientEdgeVisibility<.08f && report.gradientEdgeVisibility>.01f;
                }
                report.uiUnchanged &= VisionUiDifference(normal,frame)<.005f;Destroy(frame);
            }
            preferences.ComfortBlur=true;
            yield return null;yield return new WaitForEndOfFrame();var comfort=CaptureVisionFrame(folder,"05-comfort",report);
            report.comfortBrightness=SideBrightness(comfort,true);report.comfortReducedMask=report.comfortBrightness>report.bothBrightness+.1f && report.comfortBrightness<SideBrightness(normal,true)*.8f;
            report.uiUnchanged &= VisionUiDifference(normal,comfort)<.005f;Destroy(comfort);
            Model.Players[0].SoldBodyMask=0;preferences.ComfortBlur=false;
            yield return null;yield return new WaitForEndOfFrame();var restored=CaptureVisionFrame(folder,"06-restored",report);
            report.restoredDifference=RoadDifference(normal,restored,Matrix4x4.identity);report.restoredVision=report.restoredDifference<.005f;Destroy(restored);
            Model.Players[1].SoldBodyMask=3;
            yield return null;yield return new WaitForEndOfFrame();var other=CaptureVisionFrame(folder,"07-other-seat",report);
            report.otherSeatUnaffected=RoadDifference(normal,other,Matrix4x4.identity)<.005f;Destroy(other);Model.Players[1].SoldBodyMask=0;
            Model.Players[0].SoldBodyMask=3;Model.Config.EnableBodyEconomy=false;
            yield return null;yield return new WaitForEndOfFrame();var disabled=CaptureVisionFrame(folder,"08-disabled",report);
            // Disabling the economy also changes toll currency text (G -> 만).
            // Compare near/far world patches rather than those intentionally changed labels.
            report.disabledEconomyUnaffected=VisionPatchDifference(normal,disabled,new Vector2(80,450))<.005f && VisionPatchDifference(normal,disabled,new Vector2(1360,450))<.005f && VisionPatchDifference(normal,disabled,new Vector2(720,340))<.005f;Destroy(disabled);Model.Config.EnableBodyEconomy=true;
            Model.Players[0].SoldBodyMask=2;preferences.ComfortBlur=false;
            Act(Game.DiceControlAction(5,0));
            // Wait for actual pawn movement, including the full dice throw and result hold.
            float movementDeadline=Time.realtimeSinceStartup+6;
            while(Vector2.Distance(startingVisionCenter,BodyVisionCenter)<40 && Time.realtimeSinceStartup<movementDeadline)yield return null;
            yield return new WaitForEndOfFrame();
            var moving=CaptureVisionFrame(folder,"09-moving",report);
            report.movingVisionAnchored=Board.IsAnimating && SideBrightness(moving,true)<.02f && SideBrightness(moving,false)>.2f;Destroy(moving);
            // Freeze the visual pose to compare the same moving world with/without its mask.
            Time.timeScale=0;Model.Players[0].SoldBodyMask=0;
            yield return null;yield return new WaitForEndOfFrame();var movingClear=CaptureVisionFrame(folder,"10-moving-clear",report);
            Model.Players[0].SoldBodyMask=3;
            yield return null;yield return new WaitForEndOfFrame();var movingNear=CaptureVisionFrame(folder,"11-moving-near-vision",report);
            report.visionCenterTravel=Vector2.Distance(startingVisionCenter,BodyVisionCenter);
            report.movingNearVisionDifference=VisionPatchDifference(movingClear,movingNear,BodyVisionCenter+new Vector2(40,20));
            var movingDistant=BodyVisionCenter+new Vector2(170,0);
            report.movingDistantBrightness=PatchBrightness(movingNear,Mathf.RoundToInt(movingDistant.x),Mathf.RoundToInt(movingDistant.y));
            report.movingDistantVisibility=VisionVisibility(movingClear,movingNear,movingDistant);
            report.nearVisionFollowsPawn=report.visionCenterTravel>30 && report.movingNearVisionDifference<.005f && report.movingDistantVisibility>.05f && report.movingDistantVisibility<.6f;
            report.uiUnchanged &= VisionUiDifference(movingClear,movingNear)<.005f;
            Destroy(movingClear);Destroy(movingNear);Time.timeScale=1;
            report.sideSpecific=report.rightMissingBrightness<.02f && report.leftMissingBrightness<.02f && report.rightOpenBrightness>SideBrightness(normal,false)*.95f && report.leftOpenBrightness>SideBrightness(normal,true)*.95f;
            report.singleEyeGradientExpanded=ExpandedEyeTransition(report.rightTransitionNear,report.rightTransitionMiddle,report.rightTransitionFar) && ExpandedEyeTransition(report.leftTransitionNear,report.leftTransitionMiddle,report.leftTransitionFar) && Mathf.Abs(report.rightTransitionMiddle-report.leftTransitionMiddle)<.02f;
            Destroy(normal);
            // Capture the crowded start and several colored tiles with four real pawns.
            var pawnCheck=new Game(new GameConfig{Seed=7,HumanCount=1,PlayerCount=4,EnableBodyEconomy=true,EnableAugments=false});
            InstallModel(pawnCheck);paused=true;autoHuman=false;
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
            var crowded=CaptureVisionFrame(folder,"12-four-pawns-start",report);Destroy(crowded);
            report.fourPawnPortraitsLoaded=true;report.fourPawnsSeparated=true;
            for(int i=0;i<4;i++)
            {
                report.fourPawnPortraitsLoaded &= Board.CharacterPortrait(i)!=null;
                for(int j=0;j<i;j++)report.fourPawnsSeparated &= Vector2.Distance(Board.VisiblePawnPosition(i),Board.VisiblePawnPosition(j))>=24;
            }
            int[] pawnTiles={3,9,18,25};for(int i=0;i<4;i++)pawnCheck.Players[i].Position=pawnTiles[i];
            InstallModel(pawnCheck);paused=true;autoHuman=false;
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
            var spread=CaptureVisionFrame(folder,"13-four-pawns-spread",report);Destroy(spread);
            Application.logMessageReceived-=collect;report.errors=errors.ToArray();
            var failedChecks = new List<string>();
            if (!(report.fourPawnPortraitsLoaded && report.fourPawnsSeparated))
                failedChecks.Add("pawns");
            if (!(report.fortuneTilesNeutral && report.resortsMatchLineColors && report.bonusColorPreserved &&
                  report.fortuneTiles == 4 && report.bonusTiles == 1 && report.resortTiles == 5 &&
                  report.signatureColorPanels == 19 && report.signatureColorsVisible))
                failedChecks.Add("tile colors");
            if (!(report.ownedResortMiniatureVisible && report.downloadedBuildingLoaded && report.miniaturesFaceInward &&
                  report.visibleMiniatureBuildings == 13 && report.buildingSourceVertices > 0))
                failedChecks.Add("buildings");
            if (!(report.downloadedFloorLoaded && report.floorTiles == 32 && report.floorSourceVertices > 0 &&
                  report.floorTopAlignmentError < .05f))
                failedChecks.Add("board floor");
            if (!(report.framesRendered && report.uiUnchanged))
                failedChecks.Add("frames and HUD");
            if (!(report.sideSpecific && report.singleEyeGradientExpanded))
                failedChecks.Add("single-eye visibility");
            if (!(report.comfortReducedMask && report.restoredVision && report.otherSeatUnaffected &&
                  report.disabledEconomyUnaffected))
                failedChecks.Add("settings and restoration");
            if (!(report.movingVisionAnchored && report.nearVisionVisible && report.distantWorldDimmed &&
                  report.progressiveGradient && report.nearVisionFollowsPawn))
                failedChecks.Add("moving and radial vision");
            if (errors.Count > 0) failedChecks.Add("Unity errors");
            report.failedChecks = failedChecks.ToArray();
            report.status = failedChecks.Count == 0 ? "passed" : "failed";
            File.WriteAllText(Path.Combine(folder,"runtime-validation.json"),JsonUtility.ToJson(report,true));Application.Quit(report.status=="passed"?0:1);
        }
        Texture2D CaptureVisionFrame(string folder,string name,VisionRuntimeReport report)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();report.framesRendered &= HasRenderedPixels(texture);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());return texture;
        }
        static float ColorDistance(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
        static Color FramePixel(Texture2D frame,Vector2 point)=>frame.GetPixel(Mathf.Clamp(Mathf.RoundToInt(point.x),0,frame.width-1),Mathf.Clamp(frame.height-1-Mathf.RoundToInt(point.y),0,frame.height-1));
        static float RoadDifference(Texture2D normal,Texture2D frame,Matrix4x4 view)
        {
            float total=0;
            for(int i=0;i<32;i++)
            {
                var point=MarbleBoardView.CellCenter(i)+new Vector2(14,22);var moved=view.MultiplyPoint3x4(new Vector3(point.x,point.y,0));
                total+=ColorDistance(FramePixel(normal,point),FramePixel(frame,new Vector2(moved.x,moved.y)));
            }
            return total/32;
        }
        static float SideBrightness(Texture2D frame,bool right)=>PatchBrightness(frame,right?1360:80,450);
        static bool ExpandedEyeTransition(float near,float middle,float far)=>near>.6f && near<.95f && middle>.25f && middle<.65f && far>.03f && far<.3f && near>middle && middle>far;
        static float VisionVisibility(Texture2D normal,Texture2D frame,Vector2 point)
        {
            int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);
            return PatchBrightness(frame,x,y)/Mathf.Max(.001f,PatchBrightness(normal,x,y));
        }
        static float VisionPatchDifference(Texture2D normal,Texture2D frame,Vector2 point)
        {
            float total=0;for(int dy=-4;dy<=4;dy+=2)for(int dx=-4;dx<=4;dx+=2)total+=ColorDistance(FramePixel(normal,point+new Vector2(dx,dy)),FramePixel(frame,point+new Vector2(dx,dy)));return total/25;
        }
        static float PatchBrightness(Texture2D frame,int x,int y)
        {
            float total=0;for(int dy=-12;dy<=12;dy+=4)for(int dx=-12;dx<=12;dx+=4)total+=FramePixel(frame,new Vector2(x+dx,y+dy)).grayscale;return total/49;
        }
        static float VisionUiDifference(Texture2D normal,Texture2D frame)
        {
            float total=0;int count=0;
            // Header, player names/cash, and roll button stay above the mask.
            foreach(var rect in new[]{new Rect(30,5,870,33),new Rect(90,75,210,25),new Rect(155,120,140,45),new Rect(90,690,210,25),new Rect(155,740,140,40),new Rect(680,575,80,25)})
                for(float x=rect.x;x<rect.xMax;x+=13)for(float y=rect.y;y<rect.yMax;y+=7){total+=ColorDistance(FramePixel(normal,new Vector2(x,y)),FramePixel(frame,new Vector2(x,y)));count++;}
            return total/count;
        }
    }
}
