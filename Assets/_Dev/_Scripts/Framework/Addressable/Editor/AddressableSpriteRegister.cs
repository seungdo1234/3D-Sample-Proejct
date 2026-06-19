using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressableSpriteRegister
{
    private const string SpriteResourcePath   = "Assets/_Dev/_Resources/2D/Sprites";
    private const string SpriteAddresableLabel = "InGameResource";
    private const string SpriteAddresableGroup = "Global_Sprites";

    [MenuItem("Game/Addressable/Register Sprites To Group")]
    public static void RegisterSprites()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable 세팅 파일이 없습니다!");
            return;
        }

        // 그룹 찾기 (없으면 생성)
        var group = settings.FindGroup(SpriteAddresableGroup);
        if (group == null)
        {
            group = settings.CreateGroup(SpriteAddresableGroup, false, false, true, null);
            Debug.Log($"그룹 생성: {SpriteAddresableGroup}");
        }

        // 라벨 없으면 추가
        if (!settings.GetLabels().Contains(SpriteAddresableLabel))
            settings.AddLabel(SpriteAddresableLabel);

        // 경로 내 모든 스프라이트 GUID 탐색
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SpriteResourcePath });
        int count = 0;

        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName  = Path.GetFileNameWithoutExtension(assetPath);

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(fileName);
            entry.SetLabel(SpriteAddresableLabel, true);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"총 {count}개의 스프라이트를 [{SpriteAddresableGroup}] 그룹에 등록했습니다.");
    }
}
