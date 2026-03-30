using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일간 이벤트 요약·뉴스 연동. 마스터는 <see cref="DataManager.eventMasterDataMap"/> 우선,
/// 비어 있으면 인스펙터의 <see cref="eventMasterDataSo"/> 목록을 사용합니다.
/// </summary>
public class EventManager : Singleton<EventManager>
{
    [Header("데이터 (선택)")]
    [Tooltip("DataManager 이벤트 맵이 비어 있을 때만 사용합니다. 보통 DataManager와 동일 에셋을 연결합니다.")]
    [SerializeField] EventMasterDataSo eventMasterDataSo;

    readonly List<EventMasterData> _masters = new List<EventMasterData>();

    public IReadOnlyList<EventMasterData> Masters => _masters;

    public event Action<CastleStateData, EventMasterData> OnEventSample;

    protected override void Awake()
    {
        base.Awake();
        RebuildFromSources();
    }

    public void RebuildFromSources()
    {
        _masters.Clear();
        var dm = DataManager.InstanceOrNull;
        if (dm != null && dm.eventMasterDataMap != null && dm.eventMasterDataMap.Count > 0)
        {
            foreach (var kv in dm.eventMasterDataMap)
            {
                if (kv.Value != null && !string.IsNullOrWhiteSpace(kv.Value.id))
                    _masters.Add(kv.Value);
            }
        }
        else if (eventMasterDataSo != null && eventMasterDataSo.list != null)
        {
            for (int i = 0; i < eventMasterDataSo.list.Count; i++)
            {
                var e = eventMasterDataSo.list[i];
                if (e != null && !string.IsNullOrWhiteSpace(e.id))
                    _masters.Add(e);
            }
        }
    }

    /// <summary>자정 틱에서 <see cref="DataManager"/>가 호출합니다.</summary>
    public void CheckDailyEventsFromDataManager()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null)
            return;

        NewsManager.EnsureCreated();
        RebuildFromSources();
        if (_masters.Count == 0)
            return;

        var nm = NewsManager.InstanceOrNull;
        if (nm == null)
            return;

        foreach (var kv in dm.castleStateDataMap)
        {
            var state = kv.Value;
            if (state == null || string.IsNullOrWhiteSpace(state.id))
                continue;

            if (UnityEngine.Random.value > 0.07f)
                continue;

            var ev = _masters[UnityEngine.Random.Range(0, _masters.Count)];
            if (ev == null) continue;

            string msg = string.IsNullOrWhiteSpace(ev.name) ? ev.id : ev.name;
            msg = $"{msg} · {state.id.Trim()}";
            nm.AddNews(NewsType.Breaking, msg);
            OnEventSample?.Invoke(state, ev);
        }
    }

    public static void EnsureCreated()
    {
        if (InstanceOrNull != null) return;
        var go = new GameObject(nameof(EventManager));
        go.AddComponent<EventManager>();
    }
}
