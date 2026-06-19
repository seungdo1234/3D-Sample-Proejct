#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// [Editor 전용] Assets/_Dev/_Resources/GameResource 폴더 안의 모든 에셋을
/// Addressable 그룹 "InGame_Resources" / 라벨 "InGameResources" 로 일괄 등록합니다.
/// </summary>
public static class AddressableInGameResourceRegister
{
    private const string RESOURCE_ROOT     = "Assets/_Dev/_Resources/GameResources";
    private const string GROUP_NAME        = "InGame_Resources";
    private const string LABEL_NAME        = "InGameResource";

    // [MenuItem("Game/Addressable/Register In Game Resource")]
    public static void RegisterInGameResources()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableRegister] Addressable Settings를 찾을 수 없습니다. " +
                           "Window > Asset Management > Addressables > Groups 에서 초기화해주세요.");
            return;
        }

        // ── 1. 그룹 확보 (없으면 생성) ────────────────────────────────
        AddressableAssetGroup group = GetOrCreateGroup(settings, GROUP_NAME);

        // ── 2. 라벨 확보 ──────────────────────────────────────────────
        settings.AddLabel(LABEL_NAME, false);

        // ── 3. 폴더 내 모든 에셋 수집 ─────────────────────────────────
        if (!Directory.Exists(RESOURCE_ROOT))
        {
            Debug.LogError($"[AddressableRegister] 리소스 폴더가 없습니다: {RESOURCE_ROOT}");
            return;
        }

        // .meta 파일 제외하고 모든 파일의 GUID를 수집
        string[] guids = AssetDatabase.FindAssets("", new[] { RESOURCE_ROOT });

        int added   = 0;
        int skipped = 0;
        var processedGuids = new HashSet<string>();

        foreach (string guid in guids)
        {
            // 중복 방지 (폴더 자체가 발견되는 경우 등)
            if (!processedGuids.Add(guid)) continue;

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // 폴더 자체는 건너뜀
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                skipped++;
                continue;
            }

            // ── 4. 그룹에 엔트리 추가 ─────────────────────────────────
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
            {
                Debug.LogWarning($"[AddressableRegister] 엔트리 생성 실패: {assetPath}");
                continue;
            }

            // ── 5. 어드레스 = 파일 이름(확장자 제외) ──────────────────
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            entry.address = fileName;

            // ── 6. 라벨 부착 ──────────────────────────────────────────
            entry.SetLabel(LABEL_NAME, true, true, false);

            added++;
        }

        // ── 7. 변경사항 저장 ──────────────────────────────────────────
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AddressableRegister] 완료! 등록: {added}개 / 건너뜀(폴더 등): {skipped}개 " +
                  $"→ 그룹: {GROUP_NAME} / 라벨: {LABEL_NAME}");
    }

    /// <summary>
    /// 이름이 일치하는 그룹을 반환하거나, 없으면 BundledAssetGroupSchema를 가진 새 그룹을 만들어 반환합니다.
    /// </summary>
    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        // 기존 그룹 탐색
        foreach (var g in settings.groups)
        {
            if (g != null && g.name == groupName)
                return g;
        }

        // 없으면 새 그룹 생성 (기본 스키마 포함)
        var newGroup = settings.CreateGroup(
            groupName,
            setAsDefaultGroup: false,
            readOnly: false,
            postEvent: false,
            schemasToCopy: null,
            typeof(ContentUpdateGroupSchema),
            typeof(BundledAssetGroupSchema));

        Debug.Log($"[AddressableRegister] 그룹 생성: {groupName}");
        return newGroup;
    }
}
#endif
