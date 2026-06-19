using System;
using UnityEngine;
using UnityEngine.U2D; 
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SpriteAtlasLoader : MonoBehaviour
{
    void OnEnable()
    {
        SpriteAtlasManager.atlasRequested += RequestAtlas;
    }

    void OnDisable()
    {
        SpriteAtlasManager.atlasRequested -= RequestAtlas;
    }

    private void RequestAtlas(string tag, Action<SpriteAtlas> callback)
    {
        Debug.Log($"[AtlasLoader] 아틀라스 요청됨: {tag}");

        // Addressables로 아틀라스 비동기 로드 시작
        Addressables.LoadAssetAsync<SpriteAtlas>(tag).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // [수정 포인트]
                // 로딩이 완료된 시점에, 이미지를 요청했던 UI 객체가 이미 파괴되었을 수 있습니다.
                // callback(handle.Result)를 실행하면 Unity 내부는 대기 중인 Image들의 Rebuild를 시도합니다.
                // 이때 파괴된 객체에 접근하면 MissingReferenceException이 발생하므로
                // 이를 try-catch로 감싸서 게임이 멈추지 않도록 방어합니다.
                try
                {
                    Debug.Log($"[AtlasLoader] 아틀라스 로드 성공 및 등록 시도: {tag}");
                    
                    // Unity 내부(SpriteAtlasManager)로 아틀라스를 전달
                    callback(handle.Result); 
                }
                catch (Exception e)
                {
                    // 이 예외는 보통 "이미지가 파괴된 후 아틀라스가 도착했을 때" 발생합니다.
                    // 게임 로직상 치명적인 오류가 아니므로 경고만 띄우고 넘어갑니다.
                    Debug.LogWarning($"[AtlasLoader] 아틀라스 등록 중 예외 발생 (무시됨): {tag}\n" +
                                     $"원인: 요청한 UI 객체가 로딩 중 파괴되었을 가능성이 높습니다.\n{e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[AtlasLoader] 아틀라스 로드 실패: {tag}. Addressable 설정을 확인하세요.");
            }
        };
    }
}