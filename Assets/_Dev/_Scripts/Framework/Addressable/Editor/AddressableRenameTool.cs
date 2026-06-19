using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressableRenameTool
{
    // [MenuItem("Game/Addressable/Rename All To File Name")]
    public static void RenameAllAssets()
    {
        // 1. 현재 어드레서블 세팅 가져오기
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("어드레서블 세팅 파일이 없습니다!");
            return;
        }

        int count = 0;

        // 2. 모든 그룹 순회
        foreach (var group in settings.groups)
        {
            // 기본 데이터 그룹 등은 건너뛰기
            if (group == null || group.ReadOnly) continue;

            // 3. 그룹 내의 모든 엔트리(리소스) 순회
            foreach (var entry in group.entries)
            {
                // 현재 주소(Address)와 파일 경로(AssetPath) 가져오기
                string oldAddress = entry.address;
                string assetPath = entry.AssetPath;

                // 4. 파일 이름만 추출 (확장자 제외)
                // 예: Assets/UI/Lobby.prefab -> Lobby
                string newName = Path.GetFileNameWithoutExtension(assetPath);

                // 이미 이름이 같으면 패스
                if (oldAddress == newName) continue;

                // 5. 이름 변경 적용
                entry.SetAddress(newName);
                count++;
            }
        }

        Debug.Log($"총 {count}개의 어드레서블 이름을 깔끔하게 정리했습니다!");
    }
}