//using UnityEngine;
//using System;
//using System.Collections.Generic;
//using UnityEngine.AddressableAssets;
//using UnityEngine.ResourceManagement.AsyncOperations;

//public class DataManager : Singleton<DataManager>
//{
//    [Header("1. Scriptable Objects")]
//    public StringDataSo stringDataSo;
//    public ResourceDataSo resourceDataSo;

//    public LevelRuleDataSo levelRuleDataSo;

//    [Header("2. Runtime Dictionaries")]
//    private Dictionary<string, string> stringMap = new Dictionary<string, string>();
//    private Dictionary<string, ResourceData> resourceMap = new Dictionary<string, ResourceData>();

//    private Dictionary<string, int> levelRuleMap = new Dictionary<string, int>();


//    [Header("3. Resource Management")]
//    private Dictionary<string, AsyncOperationHandle> loadedHandles = new Dictionary<string, AsyncOperationHandle>();
//    private bool isAddressableInitialized = false;
//    private SystemLanguage currentLanguage = SystemLanguage.Korean;

//    public bool IsReady { get; private set; } = false;

//    private void Awake()
//    {
//        if (Instance == null) Instance = this;
//        else Destroy(gameObject);

//        currentLanguage = Application.systemLanguage;
//        InitializeAddressables();
//        InitializeAllData();
//    }

//    public void InitializeAllData()
//    {
//        IsReady = false;
//        Debug.Log("[DataManager] 데이터 초기화 시작...");

//        // 1. 기본 데이터 초기화
//        stringMap.Clear();
//        if (stringDataSo != null)
//        {
//            bool isKor = (currentLanguage == SystemLanguage.Korean);
//            foreach (var d in stringDataSo.stringDataList)
//                if (!stringMap.ContainsKey(d.key)) stringMap.Add(d.key, isKor || string.IsNullOrEmpty(d.eng) ? d.kor : d.eng);
//        }

//        resourceMap.Clear();
//        if (resourceDataSo != null)
//            foreach (var d in resourceDataSo.resourceDataList) if (!resourceMap.ContainsKey(d.key)) resourceMap.Add(d.key, d);

//        scheduleMap.Clear();
//        if (scheduleDataSo != null)
//            foreach (var d in scheduleDataSo.list) if (!scheduleMap.ContainsKey(d.id)) scheduleMap.Add(d.id, d);

//        npcMap.Clear();
//        if (npcDataSo != null)
//            foreach (var d in npcDataSo.list) if (!npcMap.ContainsKey(d.id)) npcMap.Add(d.id, d);

//        if (scenarioDataSo != null) dialogueMap = scenarioDataSo.GetGroupDictionary();
//        else dialogueMap = new Dictionary<string, List<DialogueStep>>();

//        if (choiceDataSo != null) choiceMap = choiceDataSo.GetChoiceDictionary();
//        else choiceMap = new Dictionary<string, List<ChoiceOption>>();

//        // 2. 보상 데이터
//        rewardMap.Clear();
//        if (rewardDataSo != null)
//        {
//            foreach (var d in rewardDataSo.list)
//            {
//                if (!rewardMap.ContainsKey(d.rewardId)) rewardMap.Add(d.rewardId, new List<RewardData>());
//                rewardMap[d.rewardId].Add(d);
//            }
//        }

//        // 3. 조건 데이터
//        conditionMap.Clear();
//        if (conditionDataSo != null)
//        {
//            foreach (var d in conditionDataSo.list)
//            {
//                if (!conditionMap.ContainsKey(d.conditionID)) conditionMap.Add(d.conditionID, new List<ConditionData>());
//                conditionMap[d.conditionID].Add(d);
//            }
//        }

//        // 4. 레벨 규칙 데이터
//        levelRuleMap.Clear();
//        if (levelRuleDataSo != null)
//        {
//            foreach (var d in levelRuleDataSo.list)
//            {
//                if (!levelRuleMap.ContainsKey(d.ruleID))
//                    levelRuleMap.Add(d.ruleID, d.requiredCount);
//            }
//        }

//        // 5. ★ [New] 이벤트 데이터 초기화
//        InitializeEventData();

//        IsReady = true;
//        Debug.Log("[DataManager] 초기화 완료");
//    }

//    // ★ [New] 이벤트 데이터만 따로 갱신하는 함수 (GoogleSheetManager에서 호출 가능)
//    public void InitializeEventData()
//    {
//        eventMap.Clear();
//        if (eventDataSo != null)
//        {
//            foreach (var d in eventDataSo.list)
//            {
//                if (!eventMap.ContainsKey(d.eventID))
//                    eventMap.Add(d.eventID, d);
//            }
//        }
//    }

//    // --- Resource Loader ---
//    private void InitializeAddressables() { if (!isAddressableInitialized) Addressables.InitializeAsync().Completed += (h) => isAddressableInitialized = true; }

//    public void LoadAsync<T>(string key, Action<T> cb = null) where T : UnityEngine.Object
//    {
//        if (!isAddressableInitialized) { Addressables.InitializeAsync().Completed += (_) => { isAddressableInitialized = true; LoadAsync<T>(key, cb); }; return; }
//        if (!resourceMap.TryGetValue(key, out ResourceData d)) { cb?.Invoke(null); return; }
//        if (loadedHandles.ContainsKey(key) && loadedHandles[key].Status == AsyncOperationStatus.Succeeded) { cb?.Invoke(loadedHandles[key].Result as T); return; }
//        Addressables.LoadAssetAsync<T>(d.key).Completed += (h) => {
//            if (h.Status == AsyncOperationStatus.Succeeded) { if (!loadedHandles.ContainsKey(key)) loadedHandles.Add(key, h); cb?.Invoke(h.Result); }
//            else { Addressables.Release(h); cb?.Invoke(null); }
//        };
//    }
//    public void Release(string key) { if (loadedHandles.ContainsKey(key)) { Addressables.Release(loadedHandles[key]); loadedHandles.Remove(key); } }

//    // --- Getters ---
//    public string Text(string k) => stringMap.TryGetValue(k, out string v) ? v : k;
//    public ScheduleData GetSchedule(int id) => scheduleMap.TryGetValue(id, out var v) ? v : null;
//    public NpcData GetNPC(string id) => npcMap.TryGetValue(id, out var v) ? v : null;
//    public List<DialogueStep> GetScenario(string k) => dialogueMap.TryGetValue(k, out var v) ? v : null;
//    public List<ChoiceOption> GetChoice(string k) => choiceMap.TryGetValue(k, out var v) ? v : null;
//    public List<RewardData> GetRewardList(string k) => (string.IsNullOrEmpty(k) || !rewardMap.ContainsKey(k)) ? null : new List<RewardData>(rewardMap[k]);
//    public List<ConditionData> GetConditions(string k) => conditionMap.TryGetValue(k, out var v) ? v : null;
//    public int GetLevelRuleValue(string ruleID) => levelRuleMap.TryGetValue(ruleID, out int count) ? count : 9999;

//    // ★ [New] 이벤트 데이터 조회 함수
//    public EventDefineData GetEventData(string eventID)
//    {
//        if (string.IsNullOrEmpty(eventID)) return null;
//        if (eventMap.TryGetValue(eventID, out var data)) return data;

//        Debug.LogWarning($"[Data] 이벤트 ID를 찾을 수 없음: {eventID}");
//        return null;
//    }

//    // ★ [New] 테스트/런타임용 시나리오 데이터 주입 함수 (ScenarioManager 테스트용)
//    public void InjectDialogueData(string groupKey, List<DialogueStep> steps)
//    {
//        if (dialogueMap == null) dialogueMap = new Dictionary<string, List<DialogueStep>>();

//        if (dialogueMap.ContainsKey(groupKey))
//        {
//            dialogueMap[groupKey] = steps; // 이미 있으면 덮어쓰기
//        }
//        else
//        {
//            dialogueMap.Add(groupKey, steps); // 없으면 추가
//        }

//        Debug.Log($"[DataManager] 시나리오(대화) 데이터 주입 완료: {groupKey}");
//    }
//}