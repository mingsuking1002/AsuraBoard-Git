using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marble.Core;
using Marble.Online;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        bool wardrobeRuntimeChecking;
        [Serializable] sealed class WardrobeRuntimeReport
        {
            public string status="running",gameVersion;
            public bool framesRendered=true,downloadedCharacterLoaded,genderChanged,skinChanged,outfitsChanged,appearanceWireRoundTrip,invalidOutfitRejected,signaturePanelsInward=true,landmarkOffered,landmarkBuilt,landmarkBlocksTakeover;
            public bool allWardrobeOptionsLoaded=true,profileCancelRestored,saveDataRoundTrip,landmarkMiniatureVisible;
            public int characterParts,capturedFrames,wardrobeOptionsChecked;
            public float genderDifference,skinDifference,outfitDifference;
            public string[] errors;
        }
        IEnumerator WardrobeRuntimeCheck(string folder)
        {
            wardrobeRuntimeChecking=true;Directory.CreateDirectory(folder);Application.runInBackground=true;
            var report=new WardrobeRuntimeReport{gameVersion=Application.version};var errors=new List<string>();
            Application.LogCallback collect=(message,trace,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);};Application.logMessageReceived+=collect;
            preferences.Width=1440;preferences.Height=900;preferences.WindowMode=0;ApplyClientPreferences(true);
            OpenWardrobe();profileOutfit=WardrobeCatalog.DefaultCode;RefreshMenuModel();
            yield return new WaitForSeconds(.7f);yield return new WaitForEndOfFrame();
            CaptureWardrobeFrame(folder,"01-wardrobe",report);var male=CaptureWardrobePortrait(folder,"01-character",report);
            report.downloadedCharacterLoaded=menuModels.UsesSidekick;report.characterParts=menuModels.VisibleCharacterParts(0);
            PickWardrobe(1,3);yield return null;yield return new WaitForEndOfFrame();
            var skin=CaptureWardrobePortrait(folder,"02-skin",report);report.skinDifference=PortraitDifference(male,skin);report.skinChanged=report.skinDifference>.001f;Destroy(skin);
            PickWardrobe(1,0);PickWardrobe(0,1);yield return null;yield return new WaitForEndOfFrame();
            var female=CaptureWardrobePortrait(folder,"03-female",report);report.genderDifference=PortraitDifference(male,female);report.genderChanged=report.genderDifference>.001f;
            CaptureWardrobeFrame(folder,"03-female-wardrobe",report);
            PickWardrobe(3,2);PickWardrobe(4,2);PickWardrobe(5,2);PickWardrobe(6,2);PickWardrobe(7,3);wardrobeTab=3;
            yield return null;yield return new WaitForEndOfFrame();var knight=CaptureWardrobePortrait(folder,"04-outfit",report);
            report.outfitDifference=PortraitDifference(female,knight);report.outfitsChanged=report.outfitDifference>.001f;Destroy(knight);Destroy(male);Destroy(female);
            CaptureWardrobeFrame(folder,"04-clothing",report);
            int chosen=profileOutfit;var packet=new OnlinePacket{Kind=PacketKind.Appearance,Outfit=chosen,Players=new[]{new RoomMember{Id=1,Name="Wardrobe",Outfit=chosen}}};
            var decoded=OnlineProtocol.Decode(OnlineProtocol.Encode(packet));report.appearanceWireRoundTrip=decoded.Outfit==chosen && decoded.Players[0].Outfit==chosen;
            try{OnlineProtocol.Encode(new OnlinePacket{Outfit=1<<25});}catch(InvalidDataException){report.invalidOutfitRejected=true;}
            var saved=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData{Outfits=new[]{chosen,WardrobeCatalog.DefaultCode}}));
            report.saveDataRoundTrip=saved.Version==6 && saved.Outfits.SequenceEqual(new[]{chosen,WardrobeCatalog.DefaultCode});
            for(int field=0;field<WardrobeCatalog.Counts.Length;field++)for(int option=0;option<WardrobeCatalog.Counts[field];option++)
            {
                profileOutfit=WardrobeCatalog.DefaultCode;PickWardrobe(field,option);
                yield return null;yield return new WaitForEndOfFrame();
                report.allWardrobeOptionsLoaded &= menuModels.UsesSidekick && menuModels.VisibleCharacterParts(0)>18;
                report.wardrobeOptionsChecked++;
            }
            CancelWardrobe();report.profileCancelRestored=profileOutfit==wardrobeSavedOutfit && profileColor==wardrobeSavedColor && profileDiceSkin==wardrobeSavedDice && profileAvatar==wardrobeSavedAvatar && localName==wardrobeSavedName;
            localRoom=true;flow.EnterRoom();flow.StartMatch();
            var game=new Game(new GameConfig{Seed=7,HumanCount=1,PlayerCount=2,StartingCash=100000,EnableAugments=false,EnableBodyEconomy=true});
            game.Players[0].Outfit=chosen;game.Players[0].Laps=2;
            foreach(int tile in new[]{1,10,17,26}){game.Tiles[tile].OwnerIndex=0;game.Tiles[tile].Buildings=7;}
            game.TestLand(0,1);InstallModel(game);paused=true;mutedSound=true;
            yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();CaptureWardrobeFrame(folder,"05-landmark-offer",report);
            report.landmarkOffered=Model.GetActions().Any(a=>a.Id=="build:4");
            report.landmarkBuilt=ConfirmConstruction() && Model.Tiles[1].Landmark;
            Model.TestLand(1,1);report.landmarkBlocksTakeover=!Model.GetActions().Any(a=>a.Id=="takeover");
            Model.TestLand(0,0);InstallModel(Model);paused=true;
            yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();CaptureWardrobeFrame(folder,"06-board",report);
            report.landmarkMiniatureVisible=Board.UsesMiniatureBuildings && Board.MiniatureVisible(1,4) && !Board.MiniatureVisible(1,1) && !Board.MiniatureVisible(1,2) && !Board.MiniatureVisible(1,3) && Board.VisibleMiniatureBuildings==10;
            for(int i=0;i<32;i++)if(i%8!=0)
            {var center=Board.TileGuiPoint(i);var inward=new Vector2(DiamondBoardLayout.CenterX,DiamondBoardLayout.CenterY)-center;report.signaturePanelsInward &= Vector2.Dot(Board.SignatureColorPoint(i)-center,inward)>0;}
            report.errors=errors.ToArray();Application.logMessageReceived-=collect;
            report.status=report.framesRendered && report.downloadedCharacterLoaded && report.characterParts>18 && report.genderChanged && report.skinChanged && report.outfitsChanged && report.appearanceWireRoundTrip && report.invalidOutfitRejected && report.allWardrobeOptionsLoaded && report.profileCancelRestored && report.saveDataRoundTrip && report.signaturePanelsInward && report.landmarkOffered && report.landmarkBuilt && report.landmarkBlocksTakeover && report.landmarkMiniatureVisible && errors.Count==0?"passed":"failed";
            File.WriteAllText(Path.Combine(folder,"runtime-validation.json"),JsonUtility.ToJson(report,true));Application.Quit(report.status=="passed"?0:1);
        }
        void CaptureWardrobeFrame(string folder,string name,WardrobeRuntimeReport report)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();report.framesRendered &= HasRenderedPixels(texture);report.capturedFrames++;
            File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());Destroy(texture);
        }
        Texture2D CaptureWardrobePortrait(string folder,string name,WardrobeRuntimeReport report)
        {
            var source=(RenderTexture)menuModels.Portrait(0);var previous=RenderTexture.active;RenderTexture.active=source;
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply();RenderTexture.active=previous;
            report.framesRendered &= HasRenderedPixels(texture);File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());return texture;
        }
        static float PortraitDifference(Texture2D a,Texture2D b)
        {var first=a.GetPixels32();var second=b.GetPixels32();float total=0;for(int i=0;i<first.Length;i++)total+=Mathf.Abs(first[i].r-second[i].r)+Mathf.Abs(first[i].g-second[i].g)+Mathf.Abs(first[i].b-second[i].b);return total/(first.Length*765);}
    }
}
