using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

public class FolderNavigationWindow : EditorWindow
{
    private List<string> favoritePaths = new List<string>();
    private string newFolderPath = "Assets/";
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/SD/Folder Navigation")]
    public static void ShowWindow()
    {
        GetWindow<FolderNavigationWindow>("Folder Navigation Window");
    }
    
    private void OnEnable()
    {
        // 저장된 즐겨찾기 폴더 로드
        LoadFavoriteFolders();
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Favorites Folder", EditorStyles.boldLabel);
        
        // 새 폴더 추가 섹션
        EditorGUILayout.BeginHorizontal();
        newFolderPath = EditorGUILayout.TextField(newFolderPath);
        
        if (GUILayout.Button("Find Folder", GUILayout.Width(80)))
        {
            string folder = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                // 전체 경로에서 "Assets" 부분부터의 상대 경로 추출
                int index = folder.IndexOf("Assets");
                if (index >= 0)
                    newFolderPath = folder.Substring(index);
            }
        }
        
        if (GUILayout.Button("Add", GUILayout.Width(60)))
        {
            if (!string.IsNullOrEmpty(newFolderPath) && !favoritePaths.Contains(newFolderPath))
            {
                favoritePaths.Add(newFolderPath);
                SaveFavoriteFolders();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 저장된 폴더 목록 표시
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        for (int i = 0; i < favoritePaths.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            // 폴더 경로 표시
            EditorGUILayout.LabelField(favoritePaths[i]);
            
            // 이동 버튼
            if (GUILayout.Button("Move", GUILayout.Width(100)))
            {
                NavigateToFolder(favoritePaths[i]);
            }
            
            // 삭제 버튼
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                favoritePaths.RemoveAt(i);
                SaveFavoriteFolders();
                GUIUtility.ExitGUI(); // 목록이 변경되었으므로 GUI 갱신
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
private void NavigateToFolder(string folderPath)
{
    // 폴더 경로 정규화
    if (folderPath.EndsWith("/"))
        folderPath = folderPath.Substring(0, folderPath.Length - 1);
        
    Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
    
    if (folderObj == null)
    {
        Debug.LogError("폴더가 존재하지 않습니다: " + folderPath);
        return;
    }
    
    int folderInstanceID = folderObj.GetInstanceID();
    
    try
    {
        // 에디터 어셈블리 및 ProjectBrowser 타입 가져오기
        Assembly editorAssembly = typeof(Editor).Assembly;
        Type projectBrowserType = editorAssembly.GetType("UnityEditor.ProjectBrowser");
        
        // ProjectBrowser 인스턴스 찾기
        Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
        EditorWindow projectBrowser = null;
        
        if (projectBrowsers.Length > 0)
        {
            // 기존 브라우저 사용
            projectBrowser = projectBrowsers[0] as EditorWindow;
        }
        else
        {
            // 새 브라우저 열기
            projectBrowser = EditorWindow.GetWindow(projectBrowserType);
            projectBrowser.Show();
            
            // 초기화 메서드 호출 (필수)
            MethodInfo initMethod = projectBrowserType.GetMethod("Init", 
                BindingFlags.Instance | BindingFlags.Public);
            initMethod.Invoke(projectBrowser, null);
        }
        
        // 2열 모드로 설정 (ShowFolderContents에 필요)
        SerializedObject serializedObject = new SerializedObject(projectBrowser);
        bool inTwoColumnMode = serializedObject.FindProperty("m_ViewMode").enumValueIndex == 1;
        
        if (!inTwoColumnMode)
        {
            MethodInfo setTwoColumns = projectBrowserType.GetMethod("SetTwoColumns", 
                BindingFlags.Instance | BindingFlags.NonPublic);
            setTwoColumns.Invoke(projectBrowser, null);
        }
        
        // 폴더 내용 표시
        MethodInfo showFolderContents = projectBrowserType.GetMethod("ShowFolderContents", 
            BindingFlags.Instance | BindingFlags.NonPublic);
        showFolderContents.Invoke(projectBrowser, new object[] { folderInstanceID, true });
        
        // Project 창에 포커스
        projectBrowser.Focus();
    }
    catch (Exception ex)
    {
        Debug.LogError($"폴더 탐색 오류: {ex.Message}\n{ex.StackTrace}");
        
        // 대체 방법으로 Selection API 사용
        Selection.activeObject = folderObj;
        EditorGUIUtility.PingObject(folderObj);
    }
}

    
    private void SaveFavoriteFolders()
    {
        // 폴더 경로 목록을 EditorPrefs에 저장
        string pathsJson = JsonUtility.ToJson(new StringListWrapper { paths = favoritePaths });
        EditorPrefs.SetString("FavoriteProjectFolders", pathsJson);
    }
    
    private void LoadFavoriteFolders()
    {
        // EditorPrefs에서 폴더 경로 목록 로드
        if (EditorPrefs.HasKey("FavoriteProjectFolders"))
        {
            string pathsJson = EditorPrefs.GetString("FavoriteProjectFolders");
            StringListWrapper wrapper = JsonUtility.FromJson<StringListWrapper>(pathsJson);
            if (wrapper != null && wrapper.paths != null)
                favoritePaths = wrapper.paths;
        }
    }
    
    // JsonUtility는 List를 직접 직렬화할 수 없기 때문에 래퍼 클래스 사용
    [System.Serializable]
    private class StringListWrapper
    {
        public List<string> paths = new List<string>();
    }
}
