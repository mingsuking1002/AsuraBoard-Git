using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Marble.Editor
{
    // Local file trigger allows a build in an already-open editor, without closing the user's window.
    [InitializeOnLoad]
    public static class MarbleLocalBuildRequest
    {
        static double nextCheck;
        static bool running;
        static MarbleLocalBuildRequest() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (running || EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/build-request.txt"));
            if (!File.Exists(path)) return;
            string command = File.ReadAllText(path).Trim();
            if (command != "ValidateAndBuild") return;
            if (!SessionState.GetBool("Marble.Build.Refreshed", false))
            {
                SessionState.SetBool("Marble.Build.Refreshed", true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return;
            }
            SessionState.SetBool("Marble.Build.Refreshed", false);
            File.Move(path, path + ".accepted-" + DateTime.UtcNow.Ticks);
            running = true;
            EditorApplication.delayCall += () =>
            {
                try { MarbleBuildTools.ValidateAndBuild(); }
                catch (Exception ex) { Debug.LogException(ex); }
                finally { running = false; }
            };
        }
    }
}
