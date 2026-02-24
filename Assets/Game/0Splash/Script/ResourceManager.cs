using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceManager : Singleton<ResourceManager>
{
    //[Header("Data Table")]
    //public ResourceDataSo resourceDataSo;

    //private Dictionary<string, AsyncOperationHandle> loadedHandles = new Dictionary<string, AsyncOperationHandle>();
    //private bool isInitialized = false;

    //// ★ Addressables 시스템 초기화 함수
    //public void Initialize()
    //{
    //    if (isInitialized) return;

    //    Addressables.InitializeAsync().Completed += (handle) =>
    //    {
    //        isInitialized = true;
    //        Debug.Log("[ResourceManager] Addressables 초기화 완료");
    //    };
    //}

    //public void LoadAsync<T>(string key, Action<T> callback = null) where T : UnityEngine.Object
    //{
    //    // 1. 초기화가 안 되어있다면 초기화부터 수행 후 다시 호출
    //    if (!isInitialized)
    //    {
    //        Addressables.InitializeAsync().Completed += (initHandle) =>
    //        {
    //            isInitialized = true;
    //            LoadAsync<T>(key, callback); // 초기화 완료 후 재시도
    //        };
    //        return;
    //    }

    //    ResourceData data = resourceDataSo.resourceDataList.Find(x => x.key == key);

    //    if (data == null)
    //    {
    //        Debug.LogError($"[ResourceManager] 테이블에 없는 키입니다: {key}");
    //        callback?.Invoke(null); // 실패 시에도 콜백은 호출해주는 것이 흐름상 안전합니다.
    //        return;
    //    }

    //    if (loadedHandles.ContainsKey(key))
    //    {
    //        if (loadedHandles[key].Status == AsyncOperationStatus.Succeeded)
    //        {
    //            callback?.Invoke(loadedHandles[key].Result as T);
    //            return;
    //        }
    //    }

    //    // 3. 어드레서블 로드 시작
    //    Addressables.LoadAssetAsync<T>(data.key).Completed += (handle) =>
    //    {
    //        if (handle.Status == AsyncOperationStatus.Succeeded)
    //        {
    //            if (!loadedHandles.ContainsKey(key))
    //            {
    //                loadedHandles.Add(key, handle);
    //            }
    //            callback?.Invoke(handle.Result);
    //        }
    //        else
    //        {
    //            // ★ 빌드 환경 디버깅을 위한 상세 로그
    //            Debug.LogError($"[ResourceManager] 로드 실패: {key} (에러: {handle.OperationException})");
    //            Addressables.Release(handle);
    //            callback?.Invoke(null);
    //        }
    //    };
    //}

    //public void Release(string key)
    //{
    //    if (loadedHandles.ContainsKey(key))
    //    {
    //        Addressables.Release(loadedHandles[key]);
    //        loadedHandles.Remove(key);
    //    }
    //}
}