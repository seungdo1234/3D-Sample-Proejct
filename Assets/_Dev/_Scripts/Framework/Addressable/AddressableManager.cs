using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public class AddressableManager : Singleton<AddressableManager>
{
    // [Scene용] 씬이 넘어가면 자동으로 정리될 리소스들
    private Dictionary<string, AsyncOperationHandle> _sceneHandles = new Dictionary<string, AsyncOperationHandle>();
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    
    // [Global용] 게임이 꺼질 때까지 (혹은 명시적으로 지울 때까지) 유지될 리소스들
    private Dictionary<string, AsyncOperationHandle> _globalHandles = new Dictionary<string, AsyncOperationHandle>();
    private List<GameObject> _globalSpawnedObjects = new List<GameObject>();
    
    private Transform _inactiveLoaderRoot;

    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();

        CreateTemporaryRoot();
    }

    private void CreateTemporaryRoot()
    {
        // 혹시라도 이미 있으면 만들지 않음
        if (_inactiveLoaderRoot != null) return;

        GameObject rootObj = new GameObject("Addressable_Inactive_Loader");
        
        rootObj.SetActive(false); 
        _inactiveLoaderRoot = rootObj.transform;
    }

    private void Start()
    {
        // 씬 이동 시 'Scene용' 리소스(핸들 + 스폰 오브젝트) 자동 정리
        if (LoadSceneManager.Instance != null)
            LoadSceneManager.Instance.OnBeforeMoveSceneEvent += OnSceneMoved;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (LoadSceneManager.HasInstance)
            LoadSceneManager.Instance.OnBeforeMoveSceneEvent -= OnSceneMoved;
    }

    /// <summary>
    /// 씬 이동 시 호출되어 'Scene용' 리소스만 정리
    /// </summary>
    private void OnSceneMoved()
    {
       DebugUtil.DevelopmentLog($"[Addressable] 씬 이동 감지! \n" +
                                               $"씬 전용 리소스{ _sceneHandles.Count}개 + 오브젝트 {_spawnedObjects.Count}개 \n" +
                                               $"=> 총 {_spawnedObjects.Count + _sceneHandles.Count}개 정리합니다.");
        ClearHandleResources(_sceneHandles, "Scene Resource");
        
        foreach(var obj in _spawnedObjects)
        {
            if(obj != null) Addressables.ReleaseInstance(obj);
        }
        _spawnedObjects.Clear();
    }
    
    // 1. [리소스 로드] LoadAssetAsync
    public async Task<T> LoadAssetAsync<T>(string key, bool isGlobal = false) where T : Object
    {
        if (CheckAndGetHandle(_globalHandles, key, out var globalResult)) return globalResult as T;
        if (CheckAndGetHandle(_sceneHandles, key, out var sceneResult)) return sceneResult as T;

        // 카탈로그에 키가 존재하는지 먼저 확인 (InvalidKeyException 방지)
        var locHandle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
        await locHandle.Task;
        if (locHandle.Status != AsyncOperationStatus.Succeeded || locHandle.Result.Count == 0)
        {
            DebugUtil.DevelopmentLog($"[Addressable] {key} 등록되어 있지 않습니다. " , DebugUtil.LogType.Warning);
            Addressables.Release(locHandle);
            return null;
        }
        Addressables.Release(locHandle);

        DebugUtil.DevelopmentLog($"[Addressable] 로드 시작: {key} (Global: {isGlobal})");

        // 2. 비동기 로드
        var handle = Addressables.LoadAssetAsync<T>(key);
        T result = await handle.Task;

        // 3. 실패 체크
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            DebugUtil.DevelopmentLog($"[Addressable] '{key}' 로드 실패!");
            return null;
        }

        // 4. [분기 저장] isGlobal 플래그에 따라 다른 주머니에 넣습니다.
        if (isGlobal)
        {
            _globalHandles.TryAdd(key, handle);
        }
        else
        {
            _sceneHandles.TryAdd(key, handle);
        }

        return result;
    }

    // 중복 체크용 헬퍼 함수
    private bool CheckAndGetHandle(Dictionary<string, AsyncOperationHandle> repo, string key, out Object result)
    {
        if (repo.TryGetValue(key, out AsyncOperationHandle handle))
        {
            if (handle.IsValid())
            {
                // 이미 있으면 결과 반환
                result = handle.Result as Object;
                return true;
            }
            else
            {
                // 핸들이 만료됐으면(유니티가 내부적으로 날렸으면) 리스트에서 제거
                repo.Remove(key);
            }
        }
        result = null;
        return false;
    }
    
    // 2. [리소스 로드 및 인스턴스] InstantiateAsync 
    public async Task<T> InstantiateAsync<T>(string key, Transform parent = null, bool isGlobal = false) where T : Object
    {
        // 부모가 지정되지 않았다면 _inactiveLoaderRoot를 사용해야 함
        if (parent == null)
        {
            // 씬이 바뀌어서 _inactiveLoaderRoot가 파괴되었을 수 있으므로 null 체크
            if (_inactiveLoaderRoot == null)
            {
                CreateTemporaryRoot();
            }
            parent = _inactiveLoaderRoot;
        }

        var handle = Addressables.InstantiateAsync(key, parent);
        GameObject instantiatedObject = await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
           DebugUtil.DevelopmentLog($"[AddressableManager] '{key}' 프리팹 로드 실패!");
            return null;
        }

        if (isGlobal)
            _globalSpawnedObjects.Add(instantiatedObject);
        else
            _spawnedObjects.Add(instantiatedObject);
        
        if (typeof(T) == typeof(GameObject))
        {
            return instantiatedObject as T; 
        }

        T component = instantiatedObject.GetComponent<T>();
    
        if (component != null)
        {
            return component;
        }
        else
        { 
           DebugUtil.DevelopmentLog($"[AddressableManager] '{key}'에 컴포넌트 '{typeof(T).Name}'가 없습니다.");
            Addressables.ReleaseInstance(instantiatedObject);
            return null;
        }
    }
    
    /// <summary>
    /// 특정 라벨(Label)이 붙은 모든 에셋을 로드하여 리스트로 반환합니다.
    /// 예: "Icon" 라벨이 붙은 모든 스프라이트 가져오기
    /// </summary>
    /// <typeparam name="T">리소스 타입 (Sprite, AudioClip, ScriptableObject 등)</typeparam>
    /// <param name="label">Addressable Label 이름</param>
    /// <param name="isGlobal">true면 Global(영구), false면 Scene(휘발) 저장소에 보관</param>
    /// <returns>로드된 리소스들의 리스트 (IList<T>)</returns>
    public async Task<IList<T>> LoadAssetsByLabelAsync<T>(string label, bool isGlobal = false) 
    {
        // 1. [중복 체크] 이미 해당 라벨로 로드한 핸들이
        // 있는지 확인
        // 주의: LoadAssetsAsync의 핸들 결과물은 T가 아니라 IList<T>입니다.
        
        if (_globalHandles.TryGetValue(label, out AsyncOperationHandle globalHandle))
        {
            if (globalHandle.IsValid())
            {
               DebugUtil.DevelopmentLog($"[AddressableManager] 라벨 '{label}'는 이미(Global) 로드되어 있습니다.");
                return globalHandle.Result as IList<T>;
            }
            _globalHandles.Remove(label);
        }

        if (_sceneHandles.TryGetValue(label, out AsyncOperationHandle sceneHandle))
        {
            if (sceneHandle.IsValid())
            {
               DebugUtil.DevelopmentLog($"[AddressableManager] 라벨 '{label}'는 이미(Scene) 로드되어 있습니다.");
                return sceneHandle.Result as IList<T>;
            }
            _sceneHandles.Remove(label);
        }

       DebugUtil.DevelopmentLog($"[AddressableManager] 라벨 그룹 로드 시작: {label} (Global: {isGlobal})");

        // 2. 비동기 로드 시작
        // 두 번째 인자(callback)는 null로 두어도 됩니다 (await로 기다릴 것이므로)
        var handle = Addressables.LoadAssetsAsync<T>(label, null);
        
        // 3. 대기
        IList<T> resultList = await handle.Task;

        // 4. 실패 체크
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
           DebugUtil.DevelopmentLog($"[AddressableManager] 라벨 '{label}' 로드 실패! 라벨 이름을 확인하세요.");
            return null;
        }

        // 5. [분기 저장] 핸들 보관 (나중에 한 번에 해제하기 위해)
        // 이 핸들 하나만 Release 하면, 리스트에 들어있던 모든 에셋의 카운트가 내려갑니다.
        if (isGlobal)
        {
            _globalHandles.TryAdd(label, handle);
        }
        else
        {
            _sceneHandles.TryAdd(label, handle);
        }

       DebugUtil.DevelopmentLog($"[AddressableManager] 라벨 '{label}' 로드 성공! (총 {resultList.Count}개)");
        return resultList;
    }

    /// <summary>
    /// GameObject(인스턴스) 해제용
    /// </summary>
    public void ReleaseObject(GameObject obj)
    {
        if (obj == null) return;

        // 리스트에 있다면 제거하고 ReleaseInstance 수행
        if (_spawnedObjects.Remove(obj) || _globalSpawnedObjects.Remove(obj))
        {
            bool success = Addressables.ReleaseInstance(obj);
            if (!success) Destroy(obj);
        }
        else
        {
            Destroy(obj);
        }
    }

    /// <summary>
    /// 일반 Asset(Material, Texture 등) 해제용
    /// 객체를 넣으면 딕셔너리를 뒤져서 해당 핸들을 찾아 해제합니다.
    /// </summary>
    public void ReleaseObject(Object asset)
    {
        if (asset == null) return;
        
        // GameObject가 들어오면 위쪽 오버로딩 함수로 토스
        if (asset is GameObject go)
        {
            ReleaseObject(go);
            return;
        }

        // 1. Scene 핸들에서 찾기 (역참조 검색)
        // 딕셔너리의 값(Handle)의 결과(Result)가 내가 지우려는 asset과 같은지 확인
        var sceneKey = _sceneHandles.FirstOrDefault(x => x.Value.IsValid() && x.Value.Result as Object == asset).Key;
        if (!string.IsNullOrEmpty(sceneKey))
        {
            UnloadAsset(sceneKey); // 키를 찾았으니 키로 해제
            return;
        }

        // 2. Global 핸들에서 찾기
        var globalKey = _globalHandles.FirstOrDefault(x => x.Value.IsValid() && x.Value.Result as Object == asset).Key;
        if (!string.IsNullOrEmpty(globalKey))
        {
            UnloadAsset(globalKey);
            return;
        }

        // 못 찾음 (Addressables로 로드한 게 아니거나 이미 해제됨)
        //DebugUtil.DevelopmentLog($"[AddressableManager] 해제할 리소스를 찾지 못했습니다: {asset.name}");
    }

    /// <summary>
    /// 키(Address)를 이용한 직접 해제
    /// </summary>
    public void UnloadAsset(string key)
    {
        if (_globalHandles.TryGetValue(key, out var handle))
        {
            Addressables.Release(handle);
            _globalHandles.Remove(key);
            //DebugUtil.DevelopmentLog($"[AddressableManager] Global 리소스 '{key}' 해제 완료.");
            return;
        }

        if (_sceneHandles.TryGetValue(key, out var handle2))
        {
            Addressables.Release(handle2);
            _sceneHandles.Remove(key);
            //DebugUtil.DevelopmentLog($"[AddressableManager] Scene 리소스 '{key}' 해제 완료.");
            return;
        }
    }

    public void ClearHandleResources(Dictionary<string, AsyncOperationHandle> dict, string categoryName = "Unknown")
    {
        if (dict.Count == 0) return;
        int count = dict.Count;

        foreach (var item in dict)
        {
            if (item.Value.IsValid())
            {
                Addressables.Release(item.Value);
            }
        }
        dict.Clear();
       DebugUtil.DevelopmentLog($"[AddressableManager] '{categoryName}' 리소스 {count}개 메모리 클린업 완료!");
    }
}