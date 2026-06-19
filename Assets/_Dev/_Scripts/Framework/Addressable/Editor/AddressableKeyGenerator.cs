using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public class AddressableKeyGenerator
{
    private const string KeyFilePath = "Assets/_Addressable/Generated/ADR_KEY.cs";
    private const string UIKeyFilePath = "Assets/_Addressable/Generated/UI_KEY.cs";
    private const string UIRegistryFilePath = "Assets/_Addressable/Generated/UI_REGISTRY.cs";
    private const string GenLabel = "Lazy";

    // UI 프리팹에서 수확한 행 (Generate 한 번에 KEY + UIKeys + UI_REGISTRY 모두 출력)
    private struct UIRow
    {
        public string key;
        public string owner; // CanvasGroupType
        public bool alwaysSpawn; // 프리로드(생성+Initialize)
        public bool openOnStart; // 프리로드 후 시작 시 오픈
        public string requireContent; // ContentType (플래그 조합 가능)
    }

    // [MenuItem("Game/Addressable/Generate Addressable Keys")]
    public static void Generate()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        // 에셋 이름 정리 (필요에 따라 주석 처리 가능)
        AddressableRenameTool.RenameAllAssets();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// 자동 생성된 파일입니다.");
        sb.AppendLine("public static class KEY");
        sb.AppendLine("{");

        // 중복 변수명 방지용 Set
        HashSet<string> generatedKeys = new HashSet<string>();
        // UI 프리팹(UIMetaTag 보유)에서 수확한 행
        List<UIRow> uiRows = new List<UIRow>();

        foreach (var group in settings.groups)
        {
            if (group == null || group.ReadOnly) continue;

            foreach (var entry in group.entries)
            {
                string entryPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                bool hasLazy = entry.labels.Contains(GenLabel);

                // 폴더 엔트리: Lazy 라벨일 때만 내부 파일명을 KEY 로 (UI 수확 대상 아님)
                if (AssetDatabase.IsValidFolder(entryPath))
                {
                    if (!hasLazy) continue;

                    string[] files = Directory.GetFiles(entryPath, "*.*", SearchOption.AllDirectories);
                    foreach (var filePath in files)
                    {
                        if (filePath.EndsWith(".meta")) continue;
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        AppendKey(sb, fileName, fileName, generatedKeys);
                    }
                    continue;
                }

                // 일반 파일: UIMetaTag 가 곧 UI 마커 (Lazy 불필요) / 그 외는 Lazy 라벨 필요
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entryPath);
                var meta = prefab != null ? prefab.GetComponent<UIMetaTag>() : null;

                if (meta != null)
                {
                    // UI → UIKeys + UI_REGISTRY (UIMetaTag 가 마커라 Lazy 라벨 요구하지 않음)
                    uiRows.Add(new UIRow {
                        key = entry.address,
                        owner = meta.owner.ToString(),
                        alwaysSpawn = meta.isAlwaysSpawn,
                        openOnStart = meta.openOnStart,
                        requireContent = ContentToCode(meta.requireContent),
                    });
                }
                else if (hasLazy)
                {
                    // 일반 리소스 → KEY (Lazy 라벨 필요)
                    AppendKey(sb, entry.address, entry.address, generatedKeys);
                }
            }
        }

        sb.AppendLine("}");

        WriteFile(KeyFilePath, sb.ToString());
        WriteFile(UIKeyFilePath, BuildUIKeys(uiRows));
        WriteFile(UIRegistryFilePath, BuildUIRegistry(uiRows));

        AssetDatabase.Refresh();

        Debug.Log($"[AddressableKeyGenerator] '{GenLabel}' 키 생성 완료! (UI {uiRows.Count}개 → UIKeys / UI_REGISTRY)");
    }

    // ── UIKeys 생성 ──────────────────────────────────────────────────────────
    private static string BuildUIKeys(List<UIRow> uiRows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// 자동 생성된 파일입니다. (Game/Addressable/Generate Addressable Keys)");
        sb.AppendLine("public static class UIKeys");
        sb.AppendLine("{");

        var used = new HashSet<string>();
        foreach (var row in uiRows)
        {
            string varName = SanitizeVariableName(row.key);
            if (!used.Add(varName)) continue;
            sb.AppendLine($"    public static readonly UIKey {varName} = new UIKey(\"{row.key}\");");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── UI_REGISTRY 생성 ─────────────────────────────────────────────────────
    private static string BuildUIRegistry(List<UIRow> uiRows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// 자동 생성된 파일입니다. (Game/Addressable/Generate Addressable Keys)");
        sb.AppendLine("public static class UI_REGISTRY");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly UIRegRow[] Rows =");
        sb.AppendLine("    {");

        var used = new HashSet<string>();
        foreach (var row in uiRows)
        {
            string varName = SanitizeVariableName(row.key);
            if (!used.Add(varName)) continue;
            string always = row.alwaysSpawn ? "true" : "false";
            string openStart = row.openOnStart ? "true" : "false";
            sb.AppendLine($"        new UIRegRow(UIKeys.{varName}, CanvasGroupType.{row.owner}, {always}, {openStart}, {row.requireContent}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ContentType 플래그 값을 코드 문자열로
    private static string ContentToCode(ContentType content)
    {
        if (content == ContentType.None) return "ContentType.None";

        string[] parts = content.ToString().Split(new[] {
            ", "
        }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++) parts[i] = "ContentType." + parts[i];
        return string.Join(" | ", parts);
    }

    private static void WriteFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 키를 StringBuilder에 추가하는 헬퍼 함수 (중복 체크 포함)
    /// </summary>
    private static void AppendKey(StringBuilder sb, string rawName, string value, HashSet<string> keys)
    {
        string varName = SanitizeVariableName(rawName);

        // 이미 만들어진 변수명이면 건너뜀 (또는 숫자를 붙이는 로직 추가 가능)
        if (keys.Contains(varName)) return;

        keys.Add(varName);
        sb.AppendLine($"    public const string {varName} = \"{value}\";");
    }

    private static string SanitizeVariableName(string name)
    {
        // 공백이나 특수문자를 언더바(_)로 변경
        string cleanName = Regex.Replace(name, "[^a-zA-Z0-9_]", "_");

        // 숫자로 시작하면 앞에 언더바 추가 (C# 변수명 규칙)
        if (char.IsDigit(cleanName[0])) cleanName = "_" + cleanName;

        return cleanName;
    }
}
