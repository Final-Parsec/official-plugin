using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine.Networking;
using System.IO;
using System;

public class UploaderWindow : EditorWindow
{
    struct SceneUI
    {
        public string Text { get; set; }
        public string Priority { get; set; }
        public EditorBuildSettingsScene Scene { get; set; }
    }

    private List<SceneUI> sceneUIs = new List<SceneUI>();
    private SceneAsset sceneAsset = null;
    private string gameName = "Enter Game Name";

    private static string GAME_ID = null;
    public static readonly string[] DESIRED_ARTIFACTS = new string[] {
        "Application Data",
        "WebAssembly Framework",
        "Build Loader",
        "WebAssembly Code",
        "unityweb",
        "js",
    };

    Vector2 scrollPosition;

    [MenuItem("Final Parsec/Upload Game")]
    public static void CreateUploader()
    {
#if UNITY_2020_3_OR_NEWER
        
        PlayerSettings.WebGL.decompressionFallback = true;
        EditorWindow.GetWindow<UploaderWindow>(false, "Final Parsec");
        
#else

        Debug.Log("Final Parsec Uploader requires 2020.3 or newer. Please upgrade to use this asset.");

#endif
    }

    public void BuildForWeb()
    {
        var scenesToInclude = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenesToInclude.Add(scene.path);
            }
        }

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenesToInclude.ToArray(),
            locationPathName = "FinalParsecStaging/",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes.");

            var buildFiles = report.files;
            var desiredBuildFiles = new List<string>();
            foreach (var buildFile in buildFiles)
            {
                if (DESIRED_ARTIFACTS.Contains(buildFile.role))
                {
                    desiredBuildFiles.Add(buildFile.path);
                    Debug.Log(buildFile.path);
                }
            }

            Debug.Log("Beginning upload to Final Parsec.");
            Upload(desiredBuildFiles);
        }

        if (summary.result == BuildResult.Failed)
        {
            Debug.Log("Build failed.");
        }
    }

    public void Upload(List<string> filesToUpload)
    {
#if UNITY_2020_3_OR_NEWER

        var formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("gameName", gameName));
        foreach (var fileToUpload in filesToUpload)
        {
            var data = File.ReadAllBytes(fileToUpload);
            formData.Add(new MultipartFormFileSection("gameFiles", data, Path.GetFileName(fileToUpload), "application/octet-stream"));
        }

        var request = UnityWebRequest.Post("https://www.finalparsec.com/Game/SaveAnonymousGame", formData);
        request.SendWebRequest();
        
        while(request.result == UnityWebRequest.Result.InProgress)
        {
            // Block until request completes.
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            UploaderWindow.GAME_ID = request.downloadHandler.text;
            Debug.Log("Game ID: " + request.downloadHandler.text);
            Debug.Log("Upload response: " + request.responseCode);
            Debug.Log("Upload result: " + request.result);
        }
        else
        {
            Debug.Log("Upload response: " + request.responseCode);
            Debug.Log("Upload result: " + request.result);
            Debug.Log("Upload error: " + request.error);
        }

#endif
    }

    public void OnGUI()
    {
        this.minSize = new Vector2(475, 300);

        // SETUP
        sceneUIs.Clear();
        var priority = 0;
        foreach (var scene in EditorBuildSettings.scenes)
        {
            sceneUIs.Add(new SceneUI
            {
                Text = scene.path,
                Priority = scene.enabled ? (priority++).ToString() : "",
                Scene = scene
            });
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Space(16);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        var docsUrl = "https://www.finalparsec.com/Blog/ViewPost/final-parsec-upload";
        if (GUILayout.Button("Click here to learn how to use the uploader!"))
        {
            Application.OpenURL(docsUrl);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(16);

        GUILayout.BeginVertical("HelpBox");
        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Game Name");
        gameName = EditorGUILayout.TextField(gameName);
        EditorGUILayout.EndHorizontal();

        GUILayout.Label("Select Scenes to include in build:", EditorStyles.boldLabel);

        GUILayout.Space(8);
        EditorGUI.indentLevel++;
        foreach (var sceneUI in sceneUIs)
        {
            EditorGUILayout.BeginHorizontal();
            var value = EditorGUILayout.Toggle(sceneUI.Scene.enabled);
            if (sceneUI.Scene.enabled != value)
            {
                Debug.Log("PRESSED");
                sceneUI.Scene.enabled = value;
                SetEditorBuildSettingsScenes();
            }
            EditorGUILayout.LabelField(sceneUI.Text);
            EditorGUILayout.LabelField(sceneUI.Priority);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        EditorGUI.indentLevel--;
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Scene to add:", GUILayout.Width(90));
        sceneAsset = (SceneAsset)EditorGUILayout.ObjectField(sceneAsset, typeof(SceneAsset), false);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Scene Add/Manage Buttons
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Add", GUILayout.Width(80)) && sceneAsset != null)
        {
            AddSceneAsset(sceneAsset);
        }


        GUILayout.Space(8);

        EditorGUI.indentLevel++;
        if (GUILayout.Button("Manage Scenes In Build Settings", GUILayout.Width(200)))
        {
            EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.EndVertical();

        // Upload Game
        EditorGUI.indentLevel--;

        GUILayout.Space(8);
        if (GUILayout.Button("Upload Game"))
        {
            BuildForWeb();
        }

        if (!string.IsNullOrEmpty(UploaderWindow.GAME_ID))
        {
            GUILayout.Space(8);
            GUI.backgroundColor = Color.green;
            var anonPlayUrl = $"https://www.finalparsec.com/Game/AnonymousPlay/{UploaderWindow.GAME_ID}";
            if (GUILayout.Button("Play your game now", GUILayout.Height(30)))
            {
                Application.OpenURL(anonPlayUrl);
            }
        }


        EditorGUILayout.EndScrollView();
    }

    public void SetEditorBuildSettingsScenes()
    {
        var editorBuildSettingsScenes = sceneUIs
            .Select(sceneUI => sceneUI.Scene);
        EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
    }

    public void AddSceneAsset(SceneAsset sceneAsset)
    {
        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);

        if (!string.IsNullOrEmpty(scenePath) &&
            !sceneUIs.Any(sceneUI => sceneUI.Scene.path == scenePath))
        {
            var newScene = new EditorBuildSettingsScene(scenePath, true);
            sceneUIs.Add(new SceneUI
            {
                Text = newScene.path,
                Priority = "",
                Scene = newScene
            });

            SetEditorBuildSettingsScenes();
        }
    }

    public bool IsDesiredArtifact(BuildFile buildFile)
    {
        if (DESIRED_ARTIFACTS.Contains(buildFile.role))
        {
            return true;
        }

        if (buildFile.path.EndsWith("data.unityweb") ||
            buildFile.path.EndsWith("framework.js.unityweb") ||
            buildFile.path.EndsWith("loader.js") ||
            buildFile.path.EndsWith("wasm.unityweb") )
        {
            return true;
        }

        return false;
    }
}
