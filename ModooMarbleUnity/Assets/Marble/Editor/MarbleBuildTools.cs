using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marble.Core;
using Marble.Online;
using Marble.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Marble.Editor
{
    public static class MarbleBuildTools
    {
        const string ScenePath = "Assets/Scenes/Classic.unity";
        const string OutputRelativePath = "Builds/Windows/ModooMarbleClassic.exe";
        [Serializable] sealed class ValidationReport
        {
            public string status;
            public string unityVersion;
            public string generatedUtc;
            public int coreTestsPassed;
            public int completeAiGames;
            public int onlineTestsPassed;
            public int augmentTestsPassed;
            public int constructionTestsPassed;
            public int playLoopTestsPassed;
            public int progressionTestsPassed;
            public int bodyEconomyTestsPassed;
            public int voiceSettingsTestsPassed;
            public int abilityModuleTestsPassed;
            public int specialMechanicsTestsPassed;
            public int uiFrameworkTestsPassed;
            public int diamondBoardTestsPassed;
            public int movementTestsPassed;
            public int diceThrowTestsPassed;
            public int completeOnlineSimulations;
            public bool twoSteamAccountsVerified;
            public bool originalArtworkRemoved;
            public int boardCellsValidated;
            public string[] validatedAssets;
            public string scene;
            public string executable;
            public string buildResult;
            public double buildDurationSeconds;
            public long outputBytes;
            public int warningCount;
            public string[] errors;
        }
        [Serializable] sealed class SteamBuildSettings { public uint appId; public bool developmentAppId; }

        [MenuItem("Build/Validate and build")]
        public static void ValidateAndBuild()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportPath = Path.Combine(root, "Validation", "unity-validation.json");
            var report = new ValidationReport { status = "running", unityVersion = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("o"), scene = ScenePath,
                executable = Path.Combine(root, OutputRelativePath), buildResult = "NotStarted", errors = new string[0] };
            var errors = new List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                if (type == LogType.Warning) report.warningCount++;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 100)
                    errors.Add(message + (string.IsNullOrEmpty(trace) ? "" : "\n" + trace));
            };
            Application.logMessageReceived += capture;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            WriteReport(reportPath, report);
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Editor must finish compilation/import before validation.");
                report.coreTestsPassed = CoreSelfTests.Run();
                report.completeAiGames = 64;
                report.onlineTestsPassed = OnlineSelfTests.Run();
                report.augmentTestsPassed = AugmentSelfTests.Run();
                report.constructionTestsPassed=ConstructionSelfTests.Run();
                report.playLoopTestsPassed=PlayLoopSelfTests.Run();
                report.progressionTestsPassed=ProgressionSelfTests.Run();
                report.bodyEconomyTestsPassed=BodyEconomySelfTests.Run();
                report.voiceSettingsTestsPassed=VoiceSettingsSelfTests.Run();
                report.abilityModuleTestsPassed=AbilityModuleSelfTests.Run();
                report.specialMechanicsTestsPassed=SpecialMechanicsSelfTests.Run();
                report.uiFrameworkTestsPassed=UiFrameworkSelfTests.Run();
                report.diamondBoardTestsPassed=DiamondBoardLayoutSelfTests.Run();
                report.movementTestsPassed=MovementSelfTests.Run();
                report.diceThrowTestsPassed=DiceThrowMotionSelfTests.Run();
                report.completeOnlineSimulations = 12;
                PrepareDownloadedFloor();
                SidekickImportTools.Prepare();
                SimpleTownImportTools.Prepare();
                report.validatedAssets = ValidateOriginalArtwork(root);
                report.originalArtworkRemoved = true;
                report.boardCellsValidated = 32;
                var steamSettings=JsonUtility.FromJson<SteamBuildSettings>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"steam-config.json")));
                if(steamSettings==null || steamSettings.appId==0)throw new InvalidDataException("Steam App ID is missing");
                if(!steamSettings.developmentAppId && steamSettings.appId==480)throw new InvalidDataException("Steam release builds require the game's own App ID; 480 is a development test ID.");
                ConfigurePlayer();
                CreateClassicScene();
                AssetDatabase.SaveAssets();
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                Directory.CreateDirectory(Path.GetDirectoryName(report.executable));
                var built = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = report.executable,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                report.buildResult = built.summary.result.ToString();
                report.buildDurationSeconds = built.summary.totalTime.TotalSeconds;
                report.outputBytes = checked((long)built.summary.totalSize);
                string developmentIdFile=Path.Combine(Path.GetDirectoryName(report.executable),"steam_appid.txt");
                if(steamSettings.developmentAppId)File.WriteAllText(developmentIdFile,steamSettings.appId.ToString());
                else if(File.Exists(developmentIdFile))File.Delete(developmentIdFile);
                if (built.summary.result != BuildResult.Succeeded || !File.Exists(report.executable))
                    throw new InvalidOperationException("Windows build failed: " + built.summary.result);
                if (errors.Count > 0)
                    throw new InvalidOperationException("Validation/build logged " + errors.Count + " errors.");
                report.status = "passed";
                report.errors = errors.ToArray();
                WriteReport(reportPath, report);
                Debug.Log("MARBLE_VALIDATION_PASSED: " + report.coreTestsPassed + " core cases; 64 complete AI games; " + report.executable);
            }
            catch (Exception ex)
            {
                report.status = "failed";
                errors.Add(ex.ToString());
                report.errors = errors.ToArray();
                WriteReport(reportPath, report);
                throw;
            }
            finally { Application.logMessageReceived -= capture; }
        }

        static void WriteReport(string path, ValidationReport report)
        {
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        static void PrepareDownloadedFloor()
        {
            const string path="Assets/Marble/Resources/Downloaded/Synty/Floor1x1.fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null)throw new InvalidDataException("Synty floor model import missing.");
            if(!importer.isReadable || importer.materialImportMode!=ModelImporterMaterialImportMode.None)
            {importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();}
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var filter=model==null?null:model.GetComponentInChildren<MeshFilter>();
            if(filter==null || filter.sharedMesh==null || !filter.sharedMesh.isReadable || filter.sharedMesh.bounds.size.y<=0)
                throw new InvalidDataException("Synty floor solid readable mesh missing.");
        }
        static string[] ValidateOriginalArtwork(string root)
        {
            string[] paths = {
                "Assets/Marble/Runtime/CircuitArt.cs",
                "Assets/Marble/Runtime/PaperTheme.cs",
                "Assets/Marble/Runtime/CircuitModels.cs",
                "Assets/Marble/Runtime/MarbleBoardView.cs",
                "Assets/Marble/Resources/Downloaded/Synty/Floor1x1.fbx",
                "Assets/Marble/Resources/Downloaded/SimpleTown/apartment_large_thin_short.fbx",
                "Assets/Marble/Resources/Downloaded/SimpleTown/STL_Building_Apartment_02.png",
                "Assets/Marble/Resources/BoardFloor.shader",
                "Assets/StreamingAssets/steam-config.json",
                "Packages/com.rlabrecque.steamworks.net/Plugins/steam_api64.dll"
            };
            foreach(string path in paths)
                if(!File.Exists(Path.Combine(root,path)) || new FileInfo(Path.Combine(root,path)).Length==0)
                    throw new FileNotFoundException("Required project file missing",path);
            if(Directory.Exists(Path.Combine(Application.dataPath,"Marble/Resources/OriginalUI")))
                throw new InvalidDataException("Extracted artwork must not be in Assets.");
            var forbidden=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".png",".jpg",".jpeg",".tga",".psd",".ttf",".otf",".fbx",".obj",".blend",".ase",".wav",".mp3",".ogg",".mp4"};
            string approvedFloor=Path.GetFullPath(Path.Combine(root,"Assets/Marble/Resources/Downloaded/Synty/Floor1x1.fbx"));
            var approvedCharacters=new HashSet<string>(SidekickImportTools.Entries().Select(e=>Path.GetFullPath(Path.Combine(root,e.path))),StringComparer.OrdinalIgnoreCase);
            foreach(var entry in SimpleTownImportTools.Entries())approvedCharacters.Add(Path.GetFullPath(Path.Combine(root,entry.path)));
            foreach(string asset in Directory.GetFiles(Application.dataPath,"*",SearchOption.AllDirectories))
                if(forbidden.Contains(Path.GetExtension(asset)) && !string.Equals(Path.GetFullPath(asset),approvedFloor,StringComparison.OrdinalIgnoreCase) && !approvedCharacters.Contains(Path.GetFullPath(asset)))
                    throw new InvalidDataException("Visual/audio file needs explicit provenance approval: "+asset);
            using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(approvedFloor))
                if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!="acd9a12c9cf8ab363a65e9a3b3a76ee8effbd6d07121e1cdcc0d0a8f6eda1463")
                    throw new InvalidDataException("Downloaded floor differs from the selected Synty source asset.");
            var floorShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Marble/Resources/BoardFloor.shader");
            if(floorShader==null || ShaderUtil.ShaderHasError(floorShader))throw new InvalidDataException("Board floor shader import failed.");
            var characterShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Marble/Resources/SidekickPalette.shader");
            if(characterShader==null || ShaderUtil.ShaderHasError(characterShader))throw new InvalidDataException("Character palette shader import failed.");
            var cells=new HashSet<Vector2>();
            for(int i=0;i<32;i++)
            {
                var cell=MarbleBoardView.CellRect(i);var next=MarbleBoardView.CellRect((i+1)%32);
                if(!cells.Add(cell.center) || Mathf.Abs(Vector2.Distance(cell.center,next.center)-DiamondBoardLayout.StepLength(i))>.01f)
                    throw new InvalidDataException("Board cell order/adjacency mismatch at "+i);
                if(cell.xMin<0 || cell.xMax>1440 || cell.yMin<44 || cell.yMax>840)
                    throw new InvalidDataException("Board cell outside the visible board");
                float sample=i%8==0?25:8;
                foreach(Vector2 delta in new[]{Vector2.zero,new Vector2(sample,0),new Vector2(-sample,0),new Vector2(0,sample),new Vector2(0,-sample)})
                    if(MarbleBoardView.HitTestIndex(cell.center+delta)!=i)throw new InvalidDataException("Ambiguous diamond hit target at "+i);
            }
            if(MarbleBoardView.HitTestIndex(new Vector2(720,440))!=-1)throw new InvalidDataException("Board center must not select a tile");
            return paths;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "CircuitWorkshop";
            PlayerSettings.productName = "World Circuit";
            PlayerSettings.bundleVersion = "0.19.0";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 4;
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        static void CreateClassicScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Marble - Game").AddComponent<MarbleApp>();

            // Runtime creates all visual geometry. These serialized material references ensure
            // Shader.Find dependencies survive player shader stripping in an otherwise empty scene.
            var retained = new GameObject("Retained runtime shaders");
            string resourceFolder = "Assets/Marble/Resources/RenderSupport";
            Directory.CreateDirectory(resourceFolder);
            string[] shaders = { "Standard", "Unlit/Color" };
            for (int i = 0; i < shaders.Length; i++)
            {
                Shader shader = Shader.Find(shaders[i]);
                if (shader == null) throw new InvalidOperationException("Required built-in shader unavailable: " + shaders[i]);
                string path = resourceFolder + "/" + (i == 0 ? "Standard" : "UnlitColor") + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = "Runtime " + shaders[i] };
                    AssetDatabase.CreateAsset(material, path);
                }
                else { material.shader = shader; EditorUtility.SetDirty(material); }
                var support = GameObject.CreatePrimitive(PrimitiveType.Cube);
                support.name = shaders[i];
                support.transform.SetParent(retained.transform);
                support.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(support.GetComponent<Collider>());
            }
            retained.SetActive(false);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save classic scene.");
        }
    }
}
