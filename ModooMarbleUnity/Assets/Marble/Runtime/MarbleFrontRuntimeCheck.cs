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
        [Serializable] sealed class FrontRuntimeReport
        {
            public string status="running",gameVersion,unityVersion;
            public bool framesRendered=true,createdLobbyBeforeGame,settingsReturnToLobby,mapApplied,augmentCountApplied,voiceModesAvailable,settingsReturnToMatch,returnLobby,leaveRoom,smallViewportRendered,aiAppearanceMatches,aiColorsUnique;
            public int capturedFrames;
            public bool steamAccountsVerified;
            public string[] errors;
        }
        IEnumerator FrontRuntimeCheck(string folder)
        {
            Application.runInBackground=true;folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);
            var report=new FrontRuntimeReport{gameVersion=Application.version,unityVersion=Application.unityVersion};
            var errors=new List<string>();Application.LogCallback collect=(message,trace,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=collect;
            string savedPreferences=JsonUtility.ToJson(preferences);
            preferences.Width=1440;preferences.Height=900;preferences.WindowMode=0;ApplyClientPreferences(true);
            yield return new WaitForSeconds(.7f);yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"01-main",report);
            flow.Navigate(PlayScreen.CreateRoom);steamRoomMode=false;
            yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"02-create-room",report);
            flow.Navigate(PlayScreen.FindRoom);
            yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"03-find-room",report);
            flow.Navigate(PlayScreen.CreateRoom);CreatePlayRoom();SetLobbyMap(1);SetLobbyAugments(8);
            report.createdLobbyBeforeGame=flow.Screen==PlayScreen.Lobby && Model==null;
            yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"04-lobby",report);
            OpenFrontPreferences();
            for(int tab=0;tab<4;tab++)
            {
                preferencesTab=tab;
                if(tab==1)preferences.Microphone=MicrophoneMode.AlwaysOpen;
                yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"05-settings-"+tab,report);
            }
            report.voiceModesAvailable=Enum.GetValues(typeof(MicrophoneMode)).Length==3 && preferences.Microphone==MicrophoneMode.AlwaysOpen;
            preferencesTab=1;bodyAudioSettings=true;
            yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"06-participant-audio",report);
            bodyAudioSettings=false;ClosePreferences(false);
            report.settingsReturnToLobby=flow.Screen==PlayScreen.Lobby && flow.InRoom && !preferencesOpen;
            frontLeaveConfirm=true;
            yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"07-leave-confirmation",report);
            frontLeaveConfirm=false;
            BeginLobbyMatch();paused=true;
            report.aiAppearanceMatches=true;for(int i=1;i<Model.Players.Count;i++)report.aiAppearanceMatches &= Model.Players[i].Outfit==LocalAiOutfit(i) && Model.Players[i].ColorIndex==LocalAiColor(i);
            report.aiColorsUnique=true;for(int color=0;color<4;color++)for(int first=1;first<=3;first++)
            {report.aiColorsUnique &= LocalAiColorFor(color,first)!=color;for(int second=first+1;second<=3;second++)report.aiColorsUnique &= LocalAiColorFor(color,first)!=LocalAiColorFor(color,second);}
            report.mapApplied=Model!=null && Model.Config.MapId==1;
            report.augmentCountApplied=Model!=null && Model.Config.AugmentRounds.Length==8;
            OpenInGamePreferences();preferencesTab=0;
            yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"08-match-settings",report);
            ClosePreferences(true);report.settingsReturnToMatch=flow.Screen==PlayScreen.Game && settings && !preferencesOpen;
            ReturnMatchToLobby();report.returnLobby=flow.Screen==PlayScreen.Lobby && flow.InRoom;
            preferences.Width=1280;preferences.Height=720;ApplyClientPreferences(true);
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();CaptureFrontFrame(folder,"08-lobby-1280",report);
            report.smallViewportRendered=Screen.width==1280 && Screen.height==720;
            LeavePlayRoom();report.leaveRoom=!flow.InRoom && flow.Screen==PlayScreen.Main;
            preferences=JsonUtility.FromJson<ClientPreferences>(savedPreferences);
            Application.logMessageReceived-=collect;report.errors=errors.ToArray();
            report.status=report.framesRendered && report.createdLobbyBeforeGame && report.settingsReturnToLobby && report.mapApplied && report.augmentCountApplied && report.voiceModesAvailable && report.settingsReturnToMatch && report.returnLobby && report.leaveRoom && report.smallViewportRendered && report.aiAppearanceMatches && report.aiColorsUnique && errors.Count==0?"passed":"failed";
            File.WriteAllText(Path.Combine(folder,"runtime-validation.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.status=="passed"?0:1);
        }
        void CaptureFrontFrame(string folder,string name,FrontRuntimeReport report)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());report.framesRendered &= HasRenderedPixels(texture);report.capturedFrames++;}
            finally{Destroy(texture);}
        }
    }
}
