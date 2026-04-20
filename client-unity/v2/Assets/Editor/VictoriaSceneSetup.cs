using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VictoriaLike.Client;
using VictoriaLike.Client.Api;
using VictoriaLike.Client.UI;

/// Creates a UI Toolkit dashboard scene.
/// Safe to rerun: the generated legacy uGUI Canvas is removed, and stable
/// top-level objects/components are reused.
public static class VictoriaSceneSetup
{
    private const string DashboardUxmlPath = "Assets/UI/VictoriaDashboard/VictoriaDashboard.uxml";
    private const string PanelSettingsPath = "Assets/UI/VictoriaDashboard/VictoriaDashboardPanelSettings.asset";

    [MenuItem("Victoria/Setup UI Toolkit Dashboard")]
    public static void SetupScene()
    {
        ConfigureApiCompatibility();
        RemoveLegacyGeneratedCanvas();

        var bootstrapGo = FindOrCreate("Bootstrap");
        var bootstrap = EnsureComponent<Bootstrap>(bootstrapGo);

        var wsGo = FindOrCreate("WebSocketClient");
        var wsClient = EnsureComponent<WorldWebSocketClient>(wsGo);

        var dashboardGo = FindOrCreate("VictoriaDashboard");
        var document = EnsureComponent<UIDocument>(dashboardGo);
        var uiManager = EnsureComponent<WorldUIManager>(dashboardGo);

        document.visualTreeAsset = LoadRequiredAsset<VisualTreeAsset>(DashboardUxmlPath);
        document.panelSettings = LoadOrCreatePanelSettings();

        Wire(bootstrap, so =>
        {
            so.FindProperty("wsClient").objectReferenceValue = wsClient;
            so.FindProperty("worldUIManager").objectReferenceValue = uiManager;
        });

        Wire(uiManager, so =>
        {
            so.FindProperty("wsClient").objectReferenceValue = wsClient;
            so.FindProperty("serverUrl").stringValue = "http://localhost:5001";
        });

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("[Victoria Setup] UI Toolkit dashboard configured.");
        EditorUtility.DisplayDialog(
            "Victoria Setup",
            "UI Toolkit dashboard configured.\n\nDefault credentials:\n  england-player / eng123\n\nPress Play to test.",
            "OK");
    }

    // Keep the previous menu working, but route it to the new UI setup.
    [MenuItem("Victoria/Setup Scene for Day 79")]
    public static void SetupLegacyMenuAlias() => SetupScene();

    private static void ConfigureApiCompatibility()
    {
        var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_2021_2_OR_NEWER
        if (PlayerSettings.GetApiCompatibilityLevel(buildTarget) != ApiCompatibilityLevel.NET_Unity_4_8)
            PlayerSettings.SetApiCompatibilityLevel(buildTarget, ApiCompatibilityLevel.NET_Unity_4_8);
#else
        if (PlayerSettings.GetApiCompatibilityLevel(buildTarget) != ApiCompatibilityLevel.NET_4_6)
            PlayerSettings.SetApiCompatibilityLevel(buildTarget, ApiCompatibilityLevel.NET_4_6);
#endif
    }

    private static void RemoveLegacyGeneratedCanvas()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
            Undo.DestroyObjectImmediate(canvas);

        var debug = GameObject.Find("ConnectionDebug");
        if (debug != null)
            Undo.DestroyObjectImmediate(debug);
    }

    private static PanelSettings LoadOrCreatePanelSettings()
    {
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings != null)
            return panelSettings;

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1600, 900);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;

        AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        AssetDatabase.SaveAssets();
        return panelSettings;
    }

    private static T LoadRequiredAsset<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new System.InvalidOperationException($"Required asset missing: {path}");
        return asset;
    }

    private static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(go);
        return component;
    }

    private static void Wire<T>(T target, System.Action<SerializedObject> configure) where T : Object
    {
        var so = new SerializedObject(target);
        configure(so);
        so.ApplyModifiedProperties();
    }
}
