using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Addressable Manager 의 "폴더별 기본 정책" 을 담는 프로젝트 파일(공유용).
/// 개별 프리팹의 등록/라벨 진실은 AddressableAssetSettings 에 있고(이미 공유),
/// 이 SO 는 "이 폴더에 새로 들어온 프리팹의 기본 등록/라벨" 만 기억한다.
/// </summary>
public class AddressableManagerConfig : ScriptableObject
{
    [System.Serializable]
    public class FolderPolicy
    {
        public string folderKey;               // 예: "InGame/System"
        public bool autoRegister = true;       // 신규 프리팹 기본 등록 여부
        public List<string> defaultLabels = new(); // 신규 프리팹에 기본 적용할 라벨
    }

    public List<FolderPolicy> folderPolicies = new();

    public FolderPolicy Find(string folderKey) => folderPolicies.Find(p => p.folderKey == folderKey);

    public FolderPolicy GetOrCreate(string folderKey)
    {
        var p = Find(folderKey);
        if (p == null)
        {
            p = new FolderPolicy { folderKey = folderKey };
            folderPolicies.Add(p);
        }
        return p;
    }
}
