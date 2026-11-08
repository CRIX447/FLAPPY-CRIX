// Sets the project up the first time it's opened (and again from the menu):
//   - the scene (Assets/FlappyCrix/Scenes/Main.unity) and the build list
//   - CRIX STUDIOS / Flappy Crix names, app id, icon, version
//   - fullscreen by default, resizable window, runs in the background (keeps the connection up)
//   - Unity's own boot screen off: the game shows CRIX STUDIOS itself
//   - Android: IL2CPP, 64-bit; any orientation
// Menu: Flappy Crix > Build > Windows / Mac / Linux / Android / iOS.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlappyCrix.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const int SetupVersion = 3;
        const string ScenePath = "Assets/FlappyCrix/Scenes/Main.unity";
        const string IconPath = "Assets/FlappyCrix/Resources/FlappyCrix/Images/icon.png";
        public const string AppId = "com.crixstudios.flappycrix";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                string key = "FlappyCrix.Setup." + Application.dataPath.GetHashCode();
                if (!File.Exists(ScenePath) || EditorPrefs.GetInt(key, 0) < SetupVersion)
                {
                    Run();
                    EditorPrefs.SetInt(key, SetupVersion);
                }
            };
        }

        [MenuItem("Flappy Crix/Set Up Project", false, 0)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // the scene: empty on purpose, the game builds itself when it starts (Boot.cs)
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(ScenePath);

            PlayerSettings.companyName = "CRIX STUDIOS";
            PlayerSettings.productName = "Flappy Crix";
            PlayerSettings.bundleVersion = "2.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, AppId);

            // fullscreen by default; a window works too
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;

            // our own boot screen (CRIX STUDIOS) instead of Unity's
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;

            // phones and tablets: any way up
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;      // online play

            // the browser build: small gzip files that work on any web host (no special server settings),
            // cached by the browser after the first visit, shown in our own full-window page
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:FlappyCrix";

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            AssetDatabase.SaveAssets();
            Debug.Log("[Flappy Crix] Project set up. Press Play to run the game.");
        }

        // ------------------------------------------------------------------ builds

        [MenuItem("Flappy Crix/Build/Windows", false, 20)] static void BuildWindows() { Build(BuildTarget.StandaloneWindows64, "Windows/Flappy Crix.exe"); }
        [MenuItem("Flappy Crix/Build/Mac", false, 21)] static void BuildMac() { Build(BuildTarget.StandaloneOSX, "Mac/Flappy Crix.app"); }
        [MenuItem("Flappy Crix/Build/Linux", false, 22)] static void BuildLinux() { Build(BuildTarget.StandaloneLinux64, "Linux/FlappyCrix.x86_64"); }
        [MenuItem("Flappy Crix/Build/Android (APK)", false, 23)] static void BuildAndroid() { Build(BuildTarget.Android, "Android/FlappyCrix.apk"); }
        [MenuItem("Flappy Crix/Build/iOS (Xcode project)", false, 24)] static void BuildIos() { Build(BuildTarget.iOS, "iOS"); }
        [MenuItem("Flappy Crix/Build/Web (WebGL)", false, 25)] static void BuildWeb() { Build(BuildTarget.WebGL, "Web"); }

        static void Build(BuildTarget target, string output)
        {
            Run();
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", output);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log("[Flappy Crix] Build " + target + ": " + report.summary.result + " -> " + path);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// For GitHub Actions (GameCI unity-builder, buildMethod): sets the project up, then builds the
        /// target in -buildTarget to the file in -customBuildPath. Exits with 1 if the build fails.
        /// </summary>
        public static void CiBuild()
        {
            Run();
            string[] args = System.Environment.GetCommandLineArgs();
            string path = Arg(args, "-customBuildPath"), targetName = Arg(args, "-buildTarget");
            BuildTarget target;
            try { target = (BuildTarget)System.Enum.Parse(typeof(BuildTarget), targetName ?? "StandaloneWindows64"); }
            catch (System.Exception) { Debug.LogError("[Flappy Crix] Unknown build target " + targetName); EditorApplication.Exit(1); return; }
            if (string.IsNullOrEmpty(path)) path = Path.Combine("Builds", target.ToString(), "FlappyCrix");
            if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log("[Flappy Crix] CI build " + target + ": " + report.summary.result + " -> " + path);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        [MenuItem("Flappy Crix/Open Save Folder", false, 40)]
        static void OpenSaves() { EditorUtility.RevealInFinder(Application.persistentDataPath); }
    }

    /// <summary>Music streams from disk; short sounds load into memory.</summary>
    public sealed class FlappyCrixImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("FlappyCrix/Resources")) return;
            var a = (AudioImporter)assetImporter;
            var s = a.defaultSampleSettings;
            s.loadType = assetPath.Contains("music") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            a.defaultSampleSettings = s;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("FlappyCrix/Resources/FlappyCrix/Images")) return;
            var t = (TextureImporter)assetImporter;
            t.mipmapEnabled = true;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
