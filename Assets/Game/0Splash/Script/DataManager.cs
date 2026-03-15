using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector; // Odin 네임스페이스 추가

public class DataManager : Singleton<DataManager>
{
    public Action OnDataReady;

    [Header("Runtime Dictionaries")]

    // ★ Odin Inspector의 마법! 이 속성 하나로 딕셔너리가 인스펙터에 예쁘게 그려집니다.
    // [DictionaryDrawerSettings]를 추가하면 UI를 더 깔끔하게 다듬을 수 있습니다.
    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "레벨", ValueLabel = "밸런스 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<int, LevelRuleData> levelRuleMap = new Dictionary<int, LevelRuleData>();

    public bool IsReady { get; private set; } = false;

    protected override void Awake()
    {
        base.Awake();
    }

    public void InitializeAllData()
    {
        IsReady = true;
        OnDataReady?.Invoke(); // 구독 중인 UI(UpgradeButton 등)가 초기화되도록 호출
        Debug.Log($"[DataManager] 데이터 세팅 완료! 총 {levelRuleMap.Count}개의 데이터를 로드했습니다.");
    }

    public LevelRuleData GetLevelData(int level)
    {
        if (levelRuleMap.TryGetValue(level, out LevelRuleData data)) return data;
        return null;
    }
}