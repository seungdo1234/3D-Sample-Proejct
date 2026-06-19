using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class SceneSelectorWindow : EditorWindow
{
    // EditorPrefs keys (project-scoped via Application.dataPath hash)
    private static readonly string PrefKeyPrefix = "SD.SceneSelector." + PathHash();
    private static string ProdPrefKey => PrefKeyPrefix + ".ProdPaths";
    private static string TestPrefKey => PrefKeyPrefix + ".TestPaths";

    // Default search folders used when no custom config exists yet
    private static readonly string[] DefaultProdPaths = { "Assets/_Dev/_Scenes" };
    private static readonly string[] DefaultTestPaths = { "Assets/_Dev/_Test", "Assets/_3D" };

    private Vector2 scrollPosition;
    private List<string> gameScenes = new List<string>();
    private List<string> testScenes = new List<string>();

    // Configurable search folders
    private List<string> prodPaths = new List<string>();
    private List<string> testPaths = new List<string>();

    private bool showSettings;

    [MenuItem("Tools/SD/Scene Select  #%Q")]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneSelectorWindow>();
        window.titleContent = new GUIContent("Scene Select Tool");
        window.minSize = new Vector2(320, 220);
        window.Show();
    }

    private void OnEnable()
    {
        LoadPaths();
        RefreshSceneList();
    }

    // ---------------------------------------------------------------------
    // Persistence
    // ---------------------------------------------------------------------

    private static string PathHash()
    {
        return Mathf.Abs(Application.dataPath.GetHashCode()).ToString();
    }

    private void LoadPaths()
    {
        prodPaths = LoadPathList(ProdPrefKey, DefaultProdPaths);
        testPaths = LoadPathList(TestPrefKey, DefaultTestPaths);
    }

    private static List<string> LoadPathList(string key, string[] fallback)
    {
        string raw = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return new List<string>(fallback);

        return raw.Split('\n')
                  .Select(p => p.Trim())
                  .Where(p => !string.IsNullOrEmpty(p))
                  .ToList();
    }

    private void SavePaths()
    {
        EditorPrefs.SetString(ProdPrefKey, string.Join("\n", prodPaths));
        EditorPrefs.SetString(TestPrefKey, string.Join("\n", testPaths));
    }

    // ---------------------------------------------------------------------
    // Scene gathering
    // ---------------------------------------------------------------------

    private void RefreshSceneList()
    {
        gameScenes = FindScenes(prodPaths);
        testScenes = FindScenes(testPaths);
    }

    private static List<string> FindScenes(List<string> searchPaths)
    {
        var result = new List<string>();
        if (searchPaths == null || searchPaths.Count == 0)
            return result;

        // Keep only folders that actually exist to avoid FindAssets warnings
        string[] validPaths = searchPaths
            .Where(p => !string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p))
            .ToArray();

        if (validPaths.Length == 0)
            return result;

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", validPaths))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!result.Contains(path))
                result.Add(path);
        }

        return result;
    }

    // ---------------------------------------------------------------------
    // GUI
    // ---------------------------------------------------------------------

    private void OnGUI()
    {
        DrawToolbar();
        DrawSettingsPanel();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawSceneSection("Prod Scene", gameScenes);
        EditorGUILayout.Space(10);
        DrawSceneSection("Test Scene", testScenes);

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            RefreshSceneList();
        }

        GUILayout.FlexibleSpace();

        // Collapsible settings toggle - keeps the path editor out of the way
        showSettings = GUILayout.Toggle(
            showSettings,
            new GUIContent(" Settings", EditorGUIUtility.IconContent("_Popup").image),
            EditorStyles.toolbarButton,
            GUILayout.Width(90));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSettingsPanel()
    {
        if (!showSettings)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Search Folders", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        bool changed = false;
        changed |= DrawPathGroup("Prod", prodPaths);
        EditorGUILayout.Space(4);
        changed |= DrawPathGroup("Test", testPaths);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset to Default", GUILayout.Width(120)))
        {
            prodPaths = new List<string>(DefaultProdPaths);
            testPaths = new List<string>(DefaultTestPaths);
            changed = true;
        }
        EditorGUILayout.EndHorizontal();

        if (changed)
        {
            SavePaths();
            RefreshSceneList();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);
    }

    // Returns true when the path list was modified
    private bool DrawPathGroup(string label, List<string> paths)
    {
        bool changed = false;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(40));
        if (GUILayout.Button(new GUIContent("+", "Add folder"), EditorStyles.miniButton, GUILayout.Width(24)))
        {
            paths.Add(string.Empty);
            changed = true;
        }
        EditorGUILayout.EndHorizontal();

        int removeIndex = -1;
        for (int i = 0; i < paths.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            // Invalid folders are highlighted so misconfiguration is obvious
            bool valid = !string.IsNullOrEmpty(paths[i]) && AssetDatabase.IsValidFolder(paths[i]);
            Color prev = GUI.color;
            if (!valid) GUI.color = new Color(1f, 0.6f, 0.6f);

            string newValue = EditorGUILayout.TextField(paths[i]);
            GUI.color = prev;

            if (newValue != paths[i])
            {
                paths[i] = newValue;
                changed = true;
            }

            // Folder picker - converts absolute path back to project-relative
            if (GUILayout.Button(new GUIContent("…", "Pick folder"), EditorStyles.miniButton, GUILayout.Width(26)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select Scene Folder", Application.dataPath, "");
                string relative = ToAssetRelative(picked);
                if (!string.IsNullOrEmpty(relative))
                {
                    paths[i] = relative;
                    changed = true;
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button(new GUIContent("×", "Remove"), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                removeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            paths.RemoveAt(removeIndex);
            changed = true;
        }

        return changed;
    }

    // Converts an absolute OS path to a project-relative "Assets/..." path
    private static string ToAssetRelative(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return null;

        absolutePath = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');

        if (absolutePath == dataPath)
            return "Assets";
        if (absolutePath.StartsWith(dataPath + "/"))
            return "Assets" + absolutePath.Substring(dataPath.Length);

        return null; // outside the project
    }

    private void DrawSceneSection(string title, List<string> scenes)
    {
        GUILayout.Label(title, EditorStyles.boldLabel);

        if (scenes.Count == 0)
        {
            EditorGUILayout.HelpBox($"{title} 목록이 비어있습니다.", MessageType.Info);
            return;
        }

        foreach (string scenePath in scenes)
        {
            EditorGUILayout.BeginHorizontal();

            // Show only the file name, not the full path
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            EditorGUILayout.LabelField(sceneName, GUILayout.Width(150));

            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
            }

            // Open then enter play mode
            if (GUILayout.Button("Play", GUILayout.Width(60)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                    EditorApplication.EnterPlaymode();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
