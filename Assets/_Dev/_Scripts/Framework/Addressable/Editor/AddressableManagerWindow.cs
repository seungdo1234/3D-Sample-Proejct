#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Shows prefabs under _Resources/{Global,InGame,Lobby} grouped by tab/folder.
/// You set registration (group) + labels (Lazy, etc.) via toggles, then Register
/// performs group registration + label apply + AddressableKeyGenerator.Generate() in one step.
/// </summary>
public class AddressableManagerWindow : EditorWindow
{
    private const string ResourceRoot = "Assets/_Dev/_Resources";
    private const string LazyLabel = "Lazy";

    // tab = folder under _Resources -> group name
    private static readonly (string tab, string group)[] Tabs =
    {
        ("Global", "Global_Assets"),
        ("InGame", "InGame_Assets"),
        ("Lobby",  "Lobby_Assets"),
    };

    private enum ViewMode { ByFolder, All, ByLabel }

    private int _tabIndex;
    private ViewMode _viewMode;
    private Vector2 _scroll;
    private string _newLabel = "";

    private readonly Dictionary<string, PrefabRow> _rows = new();      // guid -> row
    private readonly Dictionary<string, bool> _foldouts = new();       // group foldout state

    // Per-folder default policy (project asset -> shared via VCS)
    private const string ConfigPath = "Assets/_Dev/_Settings/AddressableManagerConfig.asset";
    private AddressableManagerConfig _config;
    private AddressableManagerConfig Config
    {
        get
        {
            if (_config != null) return _config;
            _config = AssetDatabase.LoadAssetAtPath<AddressableManagerConfig>(ConfigPath);
            if (_config == null)
            {
                _config = CreateInstance<AddressableManagerConfig>();
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                AssetDatabase.CreateAsset(_config, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            return _config;
        }
    }

    private void SaveConfig()
    {
        EditorUtility.SetDirty(Config);
        AssetDatabase.SaveAssetIfDirty(Config);
    }

    private class PrefabRow
    {
        public string guid, fileName, tab, subFolder;
        public bool isUI;
        public string uiOwner;
        public bool register = true;                     // default ON
        public readonly HashSet<string> labels = new();  // default empty (Lazy off)
        public bool wasRegistered;                        // actual registered state when opened
    }

    [MenuItem("Game/Addressable/Addressable Manager")]
    private static void Open()
    {
        var w = GetWindow<AddressableManagerWindow>("Addressable Manager");
        w.minSize = new Vector2(560, 400);
    }

    private void OnEnable()
    {
        _tabIndex = EditorPrefs.GetInt("ADRMgr.Tab", 0);
        _viewMode = (ViewMode)EditorPrefs.GetInt("ADRMgr.View", 0);
        Refresh();
    }

    // ─────────────────────────────────────────────── Scan
    private void Refresh()
    {
        _rows.Clear();
        var settings = AddressableAssetSettingsDefaultObject.Settings;

        foreach (var (tab, _) in Tabs)
        {
            string root = $"{ResourceRoot}/{tab}";
            if (!AssetDatabase.IsValidFolder(root)) continue;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var row = new PrefabRow
                {
                    guid = guid,
                    fileName = Path.GetFileNameWithoutExtension(path),
                    tab = tab,
                    subFolder = GetSubFolder(root, path),
                };

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var meta = go ? go.GetComponent<UIMetaTag>() : null;
                if (meta != null) { row.isUI = true; row.uiOwner = meta.owner.ToString(); }

                // Read current addressable state
                var entry = settings != null ? settings.FindAssetEntry(guid) : null;
                if (entry != null)
                {
                    row.wasRegistered = true;
                    row.register = true;
                    foreach (var l in entry.labels) row.labels.Add(l);
                }
                else
                {
                    // New (unregistered) -> apply folder policy defaults
                    var policy = Config.Find($"{tab}/{row.subFolder}");
                    if (policy != null)
                    {
                        row.register = policy.autoRegister;
                        foreach (var l in policy.defaultLabels) row.labels.Add(l);
                    }
                }

                _rows[guid] = row;
            }
        }
    }

    private static string GetSubFolder(string root, string assetPath)
    {
        string rel = assetPath.Substring(root.Length).TrimStart('/');
        int slash = rel.IndexOf('/');
        return slash < 0 ? "(root)" : rel.Substring(0, slash);
    }

    // ─────────────────────────────────────────────── GUI
    private void OnGUI()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorGUILayout.HelpBox(
                "No Addressable Settings. Initialize via Window > Asset Management > Addressables > Groups.",
                MessageType.Error);
            if (GUILayout.Button("Refresh")) Refresh();
            return;
        }

        _labelCache = settings.GetLabels().ToArray(); // MaskField options (fixed for this frame)

        DrawTopBar();
        DrawLabelBar(settings);
        DrawList();
        DrawFooter();
    }

    private void DrawTopBar()
    {
        EditorGUILayout.BeginHorizontal();
        int newTab = GUILayout.Toolbar(_tabIndex, Tabs.Select(t => t.tab).ToArray(), GUILayout.Width(270));
        if (newTab != _tabIndex) { _tabIndex = newTab; EditorPrefs.SetInt("ADRMgr.Tab", _tabIndex); }

        GUILayout.FlexibleSpace();
        GUILayout.Label("View", GUILayout.Width(34));
        var newView = (ViewMode)EditorGUILayout.EnumPopup(_viewMode, GUILayout.Width(90));
        if (newView != _viewMode) { _viewMode = newView; EditorPrefs.SetInt("ADRMgr.View", (int)_viewMode); }
        var refresh = EditorGUIUtility.IconContent("Refresh");
        refresh.tooltip = "Refresh";
        if (GUILayout.Button(refresh, GUILayout.Width(30))) Refresh();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLabelBar(AddressableAssetSettings settings)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Labels:", GUILayout.Width(48));
        foreach (var l in settings.GetLabels())
            GUILayout.Label(l, EditorStyles.miniButton, GUILayout.MaxWidth(90));

        GUILayout.FlexibleSpace();
        _newLabel = EditorGUILayout.TextField(_newLabel, GUILayout.Width(120));
        if (GUILayout.Button("Add", GUILayout.Width(46)))
        {
            if (!string.IsNullOrWhiteSpace(_newLabel)) { settings.AddLabel(_newLabel); _newLabel = ""; }
        }
        if (GUILayout.Button("Rename…", GUILayout.Width(70))) ShowRenameLabelMenu();
        if (GUILayout.Button("Delete…", GUILayout.Width(66))) ShowDeleteLabelMenu();
        EditorGUILayout.EndHorizontal();
    }

    // Fixed-width columns (horizontal scroll when narrow) -> keeps alignment
    private const float W_NAME = 260f, W_REG = 50f, W_LABELS = 160f;
    private const float RowH = 18f;
    private static float RowWidth => W_NAME + W_REG + W_LABELS;
    private GUIStyle _colHeader;
    private GUIStyle _foldoutBold;
    private string[] _labelCache = System.Array.Empty<string>();
    private static Color FolderBg => EditorGUIUtility.isProSkin
        ? new Color(1f, 1f, 1f, 0.08f)
        : new Color(0f, 0f, 0f, 0.10f);

    private void DrawList()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawColumnHeader();

        switch (_viewMode)
        {
            case ViewMode.ByFolder:
            {
                string tab = Tabs[_tabIndex].tab;
                var rows = _rows.Values.Where(r => r.tab == tab);
                foreach (var grp in rows.GroupBy(r => r.subFolder).OrderBy(g => g.Key))
                    DrawGroup($"{tab}/{grp.Key}", grp.Key, grp.OrderBy(r => r.fileName).ToList(), true);
                break;
            }
            case ViewMode.All:
            {
                foreach (var grp in _rows.Values.GroupBy(r => $"{r.tab}/{r.subFolder}").OrderBy(g => g.Key))
                    DrawGroup(grp.Key, grp.Key, grp.OrderBy(r => r.fileName).ToList(), true);
                break;
            }
            case ViewMode.ByLabel:
            {
                var byLabel = new Dictionary<string, List<PrefabRow>>();
                foreach (var row in _rows.Values)
                {
                    if (row.labels.Count == 0) AddTo(byLabel, "(no label)", row);
                    else foreach (var l in row.labels) AddTo(byLabel, l, row);
                }
                foreach (var kv in byLabel.OrderBy(k => k.Key))
                    DrawGroup($"label/{kv.Key}", kv.Key, kv.Value.OrderBy(r => r.fileName).ToList(), false);
                break;
            }
        }

        GUILayout.Space(4);
        EditorGUILayout.EndScrollView();
    }

    private void DrawColumnHeader()
    {
        _colHeader ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };

        Rect r = GUILayoutUtility.GetRect(RowWidth, RowH);
        Columns(r, out var name, out var reg, out var labels);
        EditorGUI.LabelField(name, "Name", EditorStyles.miniBoldLabel);
        EditorGUI.LabelField(reg, "Reg", _colHeader);
        EditorGUI.LabelField(labels, "Labels (multi-select)", EditorStyles.miniBoldLabel);
    }

    private void DrawGroup(string foldKey, string title, List<PrefabRow> rows, bool folderToggles)
    {
        if (!_foldouts.ContainsKey(foldKey)) _foldouts[foldKey] = true;

        Rect r = GUILayoutUtility.GetRect(RowWidth, RowH);
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(r, FolderBg);          // folder header background

        Columns(r, out var nameR, out var regR, out var labelsR);

        _foldoutBold ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        _foldouts[foldKey] = EditorGUI.Foldout(nameR, _foldouts[foldKey], $"{title}  ({rows.Count})", true, _foldoutBold);

        if (folderToggles)
        {
            var policy = Config.GetOrCreate(foldKey);

            // Bulk Reg (+ save policy)
            bool regAll = rows.All(x => x.register);
            bool regAny = rows.Any(x => x.register);
            EditorGUI.showMixedValue = regAny && !regAll;
            EditorGUI.BeginChangeCheck();
            bool regV = ToggleAt(regR, regAll);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var x in rows) x.register = regV;
                policy.autoRegister = regV;
                SaveConfig();
            }
            EditorGUI.showMixedValue = false;

            // Bulk labels: common labels -> MaskField (multi-toggle, closes on outside click) + save policy
            int common = LabelsToMask(CommonLabelSet(rows));
            EditorGUI.BeginChangeCheck();
            int newMask = EditorGUI.MaskField(labelsR, common, _labelCache);
            if (EditorGUI.EndChangeCheck())
                ApplyFolderMask(rows, policy, common, newMask);
        }

        if (_foldouts[foldKey])
            foreach (var row in rows) DrawRow(row);

        GUILayout.Space(2);
    }

    private void DrawRow(PrefabRow row)
    {
        Rect r = GUILayoutUtility.GetRect(RowWidth, RowH);
        Columns(r, out var nameR, out var regR, out var labelsR);

        var nm = nameR; nm.x += 16f; nm.width -= 16f; // child indent
        string nameText = row.isUI ? $"{row.fileName}   (UI: {row.uiOwner})" : row.fileName;
        EditorGUI.LabelField(nm, nameText);

        row.register = ToggleAt(regR, row.register);

        // Label MaskField -- multi-toggle, closes on outside click
        int mask = LabelsToMask(row.labels);
        EditorGUI.BeginChangeCheck();
        int newMask = EditorGUI.MaskField(labelsR, mask, _labelCache);
        if (EditorGUI.EndChangeCheck())
            MaskToLabels(newMask, row.labels);
    }

    // ── Column / toggle helpers ──
    private static void Columns(Rect r, out Rect name, out Rect reg, out Rect labels)
    {
        float x = r.x;
        name = new Rect(x, r.y, W_NAME, r.height); x += W_NAME;
        reg  = new Rect(x, r.y, W_REG,  r.height); x += W_REG;
        labels = new Rect(x + 2f, r.y, W_LABELS - 4f, r.height);
    }

    private static bool ToggleAt(Rect cell, bool value)
    {
        var t = new Rect(cell.x + (cell.width - 16f) * 0.5f, cell.y, 16f, cell.height);
        return EditorGUI.Toggle(t, value);
    }

    // ── Label <-> bitmask conversion (for MaskField) ──
    private int LabelsToMask(HashSet<string> labels)
    {
        int m = 0;
        for (int i = 0; i < _labelCache.Length; i++)
            if (labels.Contains(_labelCache[i])) m |= 1 << i;
        return m;
    }

    private void MaskToLabels(int mask, HashSet<string> dest)
    {
        dest.Clear();
        for (int i = 0; i < _labelCache.Length; i++)
            if ((mask & (1 << i)) != 0) dest.Add(_labelCache[i]);
    }

    private static HashSet<string> CommonLabelSet(List<PrefabRow> rows)
    {
        HashSet<string> c = null;
        foreach (var r in rows)
        {
            if (c == null) c = new HashSet<string>(r.labels);
            else c.IntersectWith(r.labels);
            if (c.Count == 0) break;
        }
        return c ?? new HashSet<string>();
    }

    // Apply only the changed bits to all children + policy (preserves per-prefab labels)
    private void ApplyFolderMask(List<PrefabRow> rows, AddressableManagerConfig.FolderPolicy policy, int oldMask, int newMask)
    {
        int changed = oldMask ^ newMask;
        for (int i = 0; i < _labelCache.Length; i++)
        {
            int bit = 1 << i;
            if ((changed & bit) == 0) continue;

            string lbl = _labelCache[i];
            bool on = (newMask & bit) != 0;
            foreach (var r in rows) SetLabel(r, lbl, on);

            if (on) { if (!policy.defaultLabels.Contains(lbl)) policy.defaultLabels.Add(lbl); }
            else policy.defaultLabels.Remove(lbl);
        }
        SaveConfig();
    }

    private void DrawFooter()
    {
        int reg = _rows.Values.Count(r => r.register);
        int lazy = _rows.Values.Count(r => !r.isUI && r.labels.Contains(LazyLabel));
        int ui = _rows.Values.Count(r => r.isUI);
        int unreg = _rows.Values.Count(r => !r.register && r.wasRegistered);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label($"Registered {reg} · Lazy {lazy} · UI {ui} · To unregister {unreg}");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Register + Generate", GUILayout.Width(180), GUILayout.Height(22)))
            Register();
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────── Label edit (in-memory)
    private static void SetLabel(PrefabRow row, string label, bool on)
    {
        if (on) row.labels.Add(label); else row.labels.Remove(label);
    }

    // ─────────────────────────────────────────────── Label management (settings)
    private void ShowDeleteLabelMenu()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var menu = new GenericMenu();
        foreach (var l in settings.GetLabels())
        {
            if (l == LazyLabel) { menu.AddDisabledItem(new GUIContent($"{l} (protected)")); continue; }
            string from = l;
            menu.AddItem(new GUIContent(from), false, () =>
            {
                if (EditorUtility.DisplayDialog("Delete Label", $"Delete label '{from}'?", "Delete", "Cancel"))
                {
                    settings.RemoveLabel(from);
                    foreach (var row in _rows.Values) row.labels.Remove(from);
                }
            });
        }
        menu.ShowAsContext();
    }

    private void ShowRenameLabelMenu()
    {
        if (string.IsNullOrWhiteSpace(_newLabel))
        {
            EditorUtility.DisplayDialog("Rename", "Type the new name in the text field first.", "OK");
            return;
        }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        string to = _newLabel;
        var menu = new GenericMenu();
        foreach (var l in settings.GetLabels())
        {
            if (l == LazyLabel) { menu.AddDisabledItem(new GUIContent($"{l} (protected)")); continue; }
            string from = l;
            menu.AddItem(new GUIContent($"{from}  ->  {to}"), false, () => RenameLabel(from, to));
        }
        menu.ShowAsContext();
    }

    private void RenameLabel(string from, string to)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.AddLabel(to);

        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var entry in group.entries)
                if (entry.labels.Contains(from))
                {
                    entry.SetLabel(to, true, true, false);
                    entry.SetLabel(from, false, false, false);
                }
        }
        settings.RemoveLabel(from);

        foreach (var row in _rows.Values)
            if (row.labels.Remove(from)) row.labels.Add(to);

        _newLabel = "";
    }

    // ─────────────────────────────────────────────── Register (commit + Generate)
    private void Register()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        int unregisterCount = _rows.Values.Count(r => !r.register && r.wasRegistered);
        if (unregisterCount > 0 &&
            !EditorUtility.DisplayDialog("Addressable Manager",
                $"{unregisterCount} entries will be unregistered from Addressables.\nContinue?", "Continue", "Cancel"))
            return;

        foreach (var (tab, groupName) in Tabs)
        {
            AddressableAssetGroup group = null;
            foreach (var row in _rows.Values.Where(r => r.tab == tab))
            {
                if (row.register)
                {
                    group ??= GetOrCreateGroup(settings, groupName);
                    var entry = settings.CreateOrMoveEntry(row.guid, group, false, false);
                    entry.address = row.fileName;
                    ReconcileLabels(entry, row.labels);
                }
                else if (row.wasRegistered)
                {
                    settings.RemoveAssetEntry(row.guid, false);
                }
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        AssetDatabase.SaveAssets();

        // Generate KEY / UIKeys / UI_REGISTRY
        AddressableKeyGenerator.Generate();

        Refresh();
        Debug.Log("[AddressableManager] Register + Generate done");
    }

    private static void ReconcileLabels(AddressableAssetEntry entry, HashSet<string> desired)
    {
        // Add desired labels (force=true also ensures the label exists in settings)
        foreach (var l in desired)
            entry.SetLabel(l, true, true, false);

        // Remove labels that are no longer desired
        foreach (var l in entry.labels.ToList())
            if (!desired.Contains(l))
                entry.SetLabel(l, false, false, false);
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        foreach (var g in settings.groups)
            if (g != null && g.name == groupName) return g;

        return settings.CreateGroup(groupName, false, false, false, null,
            typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
    }

    private static void AddTo(Dictionary<string, List<PrefabRow>> d, string key, PrefabRow row)
    {
        if (!d.TryGetValue(key, out var list)) { list = new List<PrefabRow>(); d[key] = list; }
        list.Add(row);
    }
}
#endif
