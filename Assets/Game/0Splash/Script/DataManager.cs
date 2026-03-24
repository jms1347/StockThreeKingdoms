using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector; // Odin 네임스페이스 추가
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataManager : Singleton<DataManager>
{
    public Action OnDataReady;

    [Header("Master Data SO References")]
    [SerializeField] LevelRuleDataSo levelRuleDataSo;
    [SerializeField] CastleMasterDataSo castleMasterDataSo;
    [SerializeField] GeneralMasterDataSo generalMasterDataSo;
    [SerializeField] BuffMasterDataSo buffMasterDataSo;

    [Header("Runtime Dictionaries")]

    // ★ Odin Inspector의 마법! 이 속성 하나로 딕셔너리가 인스펙터에 예쁘게 그려집니다.
    // [DictionaryDrawerSettings]를 추가하면 UI를 더 깔끔하게 다듬을 수 있습니다.
    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "레벨", ValueLabel = "밸런스 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<int, LevelRuleData> levelRuleMap = new Dictionary<int, LevelRuleData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "성 ID", ValueLabel = "성 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, CastleMasterData> castleMasterDataMap = new Dictionary<string, CastleMasterData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "장수 ID", ValueLabel = "장수 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, GeneralMasterData> generalMasterDataMap = new Dictionary<string, GeneralMasterData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "버프 ID", ValueLabel = "버프 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, BuffMasterData> buffMasterDataMap = new Dictionary<string, BuffMasterData>();

    public bool IsReady { get; private set; } = false;

    protected override void Awake()
    {
        base.Awake();
    }

    public void InitializeAllData()
    {
        // 시트 파싱 결과가 비어있을 때를 대비해 SO에서 딕셔너리를 보강합니다.
        SyncRuntimeMapsFromSo();
        IsReady = true;
        OnDataReady?.Invoke(); // 구독 중인 UI(UpgradeButton 등)가 초기화되도록 호출
        Debug.Log($"[DataManager] 데이터 세팅 완료! 레벨룰 {levelRuleMap.Count}개, 성 {castleMasterDataMap.Count}개, 장수 {generalMasterDataMap.Count}개, 버프 {buffMasterDataMap.Count}개를 로드했습니다.");
    }

    public LevelRuleData GetLevelData(int level)
    {
        if (levelRuleMap.TryGetValue(level, out LevelRuleData data)) return data;
        return null;
    }

    public CastleMasterData GetCastleMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (castleMasterDataMap.TryGetValue(id.Trim(), out CastleMasterData data)) return data;
        return null;
    }

    public GeneralMasterData GetGeneralMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (generalMasterDataMap.TryGetValue(id.Trim(), out GeneralMasterData data)) return data;
        return null;
    }

    public BuffMasterData GetBuffMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (buffMasterDataMap.TryGetValue(id.Trim(), out BuffMasterData data)) return data;
        return null;
    }

    public void SyncRuntimeMapsFromSo()
    {
        SyncLevelRuleFromSoIfNeeded();
        SyncCastleFromSoIfNeeded();
        SyncGeneralFromSoIfNeeded();
        SyncBuffFromSoIfNeeded();
    }

    public void SyncSoFromRuntimeMaps()
    {
        if (levelRuleDataSo != null)
            levelRuleDataSo.list = levelRuleMap.Values.OrderBy(x => x.level).ToList();

        if (castleMasterDataSo != null)
            castleMasterDataSo.list = castleMasterDataMap.Values.OrderBy(x => x.id).ToList();

        if (generalMasterDataSo != null)
            generalMasterDataSo.list = generalMasterDataMap.Values.OrderBy(x => x.id).ToList();

        if (buffMasterDataSo != null)
            buffMasterDataSo.list = buffMasterDataMap.Values.OrderBy(x => x.id).ToList();

#if UNITY_EDITOR
        if (levelRuleDataSo != null) EditorUtility.SetDirty(levelRuleDataSo);
        if (castleMasterDataSo != null) EditorUtility.SetDirty(castleMasterDataSo);
        if (generalMasterDataSo != null) EditorUtility.SetDirty(generalMasterDataSo);
        if (buffMasterDataSo != null) EditorUtility.SetDirty(buffMasterDataSo);
        AssetDatabase.SaveAssets();
#endif
    }

    void SyncLevelRuleFromSoIfNeeded()
    {
        if (levelRuleMap.Count > 0 || levelRuleDataSo == null || levelRuleDataSo.list == null)
            return;

        for (int i = 0; i < levelRuleDataSo.list.Count; i++)
        {
            var item = levelRuleDataSo.list[i];
            if (item == null)
                continue;
            levelRuleMap[item.level] = item;
        }
    }

    void SyncCastleFromSoIfNeeded()
    {
        if (castleMasterDataMap.Count > 0 || castleMasterDataSo == null || castleMasterDataSo.list == null)
            return;

        for (int i = 0; i < castleMasterDataSo.list.Count; i++)
        {
            var item = castleMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            castleMasterDataMap[item.id.Trim()] = item;
        }
    }

    void SyncGeneralFromSoIfNeeded()
    {
        if (generalMasterDataMap.Count > 0 || generalMasterDataSo == null || generalMasterDataSo.list == null)
            return;

        for (int i = 0; i < generalMasterDataSo.list.Count; i++)
        {
            var item = generalMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            generalMasterDataMap[item.id.Trim()] = item;
        }
    }

    void SyncBuffFromSoIfNeeded()
    {
        if (buffMasterDataMap.Count > 0 || buffMasterDataSo == null || buffMasterDataSo.list == null)
            return;

        for (int i = 0; i < buffMasterDataSo.list.Count; i++)
        {
            var item = buffMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            buffMasterDataMap[item.id.Trim()] = item;
        }
    }
}