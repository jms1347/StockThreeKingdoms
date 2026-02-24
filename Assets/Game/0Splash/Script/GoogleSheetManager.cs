//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Cysharp.Threading.Tasks; // UniTask
//using UnityEngine;
//using UnityEngine.Networking;
//using UniRx;

//public class GoogleSheetManager : Singleton<GoogleSheetManager>
//{
//    // =================================================================================
//    // ★ 구글 시트 URL (GID 확인 필수)
//    // =================================================================================
//    const string stringDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=1002398526&range=A2:C";
//    const string resourceDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=461760828&range=A2:C";

//    // 시나리오(대화) 데이터 (URL GID가 맞는지 확인해주세요)
//    const string dialogueDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=263033034&range=A2:K";
//    const string choiceDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=1850813939&range=A2:C";

//    const string npcDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=633677696&range=A2:L";
//    const string rewardDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=1771151604&range=A2:C";
//    const string scheduleDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=1771151604&range=A2:C";
//    const string conditionDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=1201483308&range=A2:D";
//    const string levelRuleDataURL = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=1929538017&range=A2:B";
//    const string eventDataUrl = "https://docs.google.com/spreadsheets/d/1bldtT0EEMpHaGgwdurWnKaNUlNkN-RSybSJvoFrP32c/export?format=tsv&gid=121265814&range=A2:C";

//    private BoolReactiveProperty isSetData = new BoolReactiveProperty(false);
//    public BoolReactiveProperty IsSetData { get => isSetData; set => isSetData = value; }

//    void Awake()
//    {
//        // 게임 시작 시 데이터 로드 시작
//        CheckGetAllGSData();
//    }

//    [ContextMenu("SetData")]
//    async void CheckGetAllGSData()
//    {
//        IsSetData.Value = false;
//        Debug.Log("[GoogleSheetManager] 데이터 다운로드 시작...");

//        // 1. 기본 데이터
//        string result = await GetGSDataToURL(stringDataURL);
//        await Task.Run(() => SetStringData(result));

//        result = await GetGSDataToURL(resourceDataURL);
//        await Task.Run(() => SetResourceData(result));

//        // 2. 시스템 데이터
//        result = await GetGSDataToURL(rewardDataURL);
//        await Task.Run(() => SetRewardData(result));

//        result = await GetGSDataToURL(conditionDataURL);
//        await Task.Run(() => SetConditionData(result));

//        result = await GetGSDataToURL(levelRuleDataURL);
//        await Task.Run(() => SetLevelRuleData(result));

//        result = await GetGSDataToURL(scheduleDataURL);
//        await Task.Run(() => SetScheduleData(result));

//        result = await GetGSDataToURL(npcDataURL);
//        await Task.Run(() => SetNpcData(result));

//        // 3. 시나리오 & 이벤트 데이터
//        // ★ DialogueDataURL을 SetScenarioData로 연결
//        result = await GetGSDataToURL(dialogueDataURL);
//        await Task.Run(() => SetScenarioData(result));

//        result = await GetGSDataToURL(choiceDataURL);
//        await Task.Run(() => SetChoiceData(result));

//        result = await GetGSDataToURL(eventDataUrl);
//        await Task.Run(() => SetEventData(result));

//        // ★ [중요] 모든 파싱이 끝난 후, 메인 스레드에서 DataManager 초기화
//        DataManager.Instance.InitializeAllData();

//        IsSetData.Value = true;
//        Debug.Log("[GoogleSheetManager] 모든 데이터 다운로드 및 적용 완료!");
//    }

//    async UniTask<string> GetGSDataToURL(string url)
//    {
//        try
//        {
//            if (string.IsNullOrEmpty(url) || !url.StartsWith("http")) return "";

//            UnityWebRequest www = UnityWebRequest.Get(url);
//            await www.SendWebRequest();

//            if (www.result != UnityWebRequest.Result.Success)
//            {
//                Debug.LogError($"[GoogleSheet Error] {www.error} / URL: {url}");
//                return "";
//            }

//            return www.downloadHandler.text;
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"[GoogleSheet Exception] {e.Message}");
//            return "";
//        }
//    }

//    // ========================================================================
//    // ★ [수정] 1. 시나리오 데이터 파싱 (SetScenarioData)
//    // ========================================================================
//    void SetScenarioData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;

//        // DataManager에서 ScenarioDataSo 가져오기
//        var targetSo = DataManager.Instance.scenarioDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<DialogueStep>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 2) continue;

//            DialogueStep step = new DialogueStep();

//            step.groupKey = cells[0].Trim();
//            step.stepID = cells[1].Trim();

//            if (Enum.TryParse(cells[2].Trim(), out DialogueType type)) step.type = type;
//            else step.type = DialogueType.Dialogue;

//            step.charNameKey = (cells.Length > 3) ? cells[3].Trim() : "";
//            step.charImgKey = (cells.Length > 4) ? cells[4].Trim() : "";

//            if (cells.Length > 5 && Enum.TryParse(cells[5].Trim(), out DialoguePos pos)) step.charPos = pos;
//            else step.charPos = DialoguePos.None;

//            // ★ [핵심] 줄바꿈 처리 (\n -> 실제 엔터)
//            string rawText = (cells.Length > 6) ? cells[6].Trim() : "";
//            step.textKey = rawText.Replace("\\n", "\n");

//            step.bgKey = (cells.Length > 7) ? cells[7].Trim() : "";
//            step.soundKey = (cells.Length > 8) ? cells[8].Trim() : "";
//            step.eventID = (cells.Length > 9) ? cells[9].Trim() : "";
//            step.nextStepID = (cells.Length > 10) ? cells[10].Trim() : "";

//            targetSo.list.Add(step);
//        }
//    }

//    // ========================================================================
//    // 2. 선택지 데이터 파싱 (SetChoiceData)
//    // ========================================================================
//    void SetChoiceData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.choiceDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<ChoiceOption>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 3) continue;

//            ChoiceOption option = new ChoiceOption();

//            option.choiceID = cells[0].Trim();

//            // ★ [핵심] 줄바꿈 처리
//            string rawText = cells[1].Trim();
//            option.textKey = rawText.Replace("\\n", "\n");

//            option.nextStepID = cells[2].Trim();

//            targetSo.list.Add(option);
//        }
//    }

//    // ========================================================================
//    // 3. 이벤트 데이터 파싱 (SetEventData)
//    // ========================================================================
//    public void SetEventData(string tsvData)
//    {
//        if (string.IsNullOrEmpty(tsvData)) return;
//        var targetSo = DataManager.Instance.eventDataSo;
//        if (targetSo == null) return;

//        targetSo.list.Clear();

//        string[] rows = tsvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 2) continue;

//            EventDefineData data = new EventDefineData();

//            data.eventID = cells[0].Trim();
//            data.prefabKey = cells[1].Trim();
//            data.nextEventID = (cells.Length > 2) ? cells[2].Trim() : "";
//            //data.param1 = (cells.Length > 3) ? cells[3].Trim() : "";
//            //data.param2 = (cells.Length > 4) ? cells[4].Trim() : "";

//            if (!string.IsNullOrEmpty(data.eventID))
//            {
//                targetSo.list.Add(data);
//            }
//        }
//    }

//    // ========================================================================
//    // 4. 스케줄 데이터 파싱
//    // ========================================================================
//    void SetScheduleData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.scheduleDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<ScheduleData>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 11) continue;

//            ScheduleData sch = new ScheduleData();

//            int.TryParse(cells[0].Trim(), out sch.id);
//            sch.title = cells[1].Trim();
//            sch.type = cells[2].Trim();
//            sch.description = cells[3].Trim();
//            sch.unlockConditionID = cells[4].Trim();
//            sch.relatedNpcID = cells[5].Trim();

//            sch.successFactors = new List<StatType>();
//            foreach (var f in cells[6].Split(','))
//            {
//                if (Enum.TryParse(f.Trim(), out StatType sType)) sch.successFactors.Add(sType);
//            }

//            sch.levelRuleGroup = cells[7].Trim();

//            sch.costs = new List<int>();
//            foreach (var c in cells[8].Split(','))
//            {
//                if (int.TryParse(c.Trim(), out int val)) sch.costs.Add(val);
//            }

//            sch.successRewardIDs = new List<string>();
//            foreach (var s in cells[9].Split(',')) if (!string.IsNullOrWhiteSpace(s)) sch.successRewardIDs.Add(s.Trim());

//            sch.failRewardIDs = new List<string>();
//            foreach (var f in cells[10].Split(',')) if (!string.IsNullOrWhiteSpace(f)) sch.failRewardIDs.Add(f.Trim());

//            if (cells.Length > 11) int.TryParse(cells[11].Trim(), out sch.fatigue);

//            targetSo.list.Add(sch);
//        }
//    }

//    // ========================================================================
//    // 5. 보상 데이터 파싱
//    // ========================================================================
//    void SetRewardData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.rewardDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<RewardData>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i =0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 3) continue;

//            RewardData r = new RewardData();
//            r.rewardId = cells[0].Trim();
//            if (Enum.TryParse(cells[1].Trim(), out StatType type)) r.statType = type;
//            else r.statType = StatType.Hp;
//            int.TryParse(cells[2].Trim(), out r.amount);

//            targetSo.list.Add(r);
//        }
//    }

//    // ========================================================================
//    // 6. 조건 데이터 파싱
//    // ========================================================================
//    void SetConditionData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.conditionDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<ConditionData>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 4) continue;

//            ConditionData c = new ConditionData();
//            c.conditionID = cells[0].Trim();
//            if (Enum.TryParse(cells[1].Trim(), out ConditionType type)) c.type = type;
//            else c.type = ConditionType.None;
//            c.key = cells[2].Trim();
//            int.TryParse(cells[3].Trim(), out c.value);

//            targetSo.list.Add(c);
//        }
//    }

//    // ========================================================================
//    // 7. 레벨 규칙 데이터 파싱
//    // ========================================================================
//    void SetLevelRuleData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.levelRuleDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<LevelRuleData>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 2) continue;

//            LevelRuleData rule = new LevelRuleData();
//            rule.ruleID = cells[0].Trim();
//            if (int.TryParse(cells[1].Trim(), out int val))
//            {
//                rule.requiredCount = val;
//                targetSo.list.Add(rule);
//            }
//        }
//    }

//    // ========================================================================
//    // 8. NPC 데이터 파싱
//    // ========================================================================
//    void SetNpcData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.npcDataSo;
//        if (targetSo == null) return;

//        if (targetSo.list == null) targetSo.list = new List<NpcData>();
//        else targetSo.list.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 11) continue;

//            NpcData npc = new NpcData();
//            npc.id = cells[0].Trim();
//            npc.name = cells[2].Trim();
//            npc.jobTitle = cells[3].Trim();
//            npc.gender = cells[4].Trim();
//            npc.age = cells[5].Trim();
//            npc.personality = cells[6].Trim();
//            npc.goal = cells[7].Trim();
//            npc.description = cells[8].Trim();
//            npc.baseImageKey = cells[9].Trim();
//            int.TryParse(cells[10].Trim(), out npc.mainPlaceID);

//            targetSo.list.Add(npc);
//        }
//    }

//    // ========================================================================
//    // 9. 스트링 데이터 파싱
//    // ========================================================================
//    void SetStringData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.stringDataSo;
//        if (targetSo == null) return;

//        if (targetSo.stringDataList == null) targetSo.stringDataList = new List<StringData>();
//        else targetSo.stringDataList.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 2) continue;

//            StringData s = new StringData();
//            s.key = cells[0].Trim();
//            s.kor = cells[1].Trim();
//            if (cells.Length > 2) s.eng = cells[2].Trim();
//            targetSo.stringDataList.Add(s);
//        }
//    }

//    // ========================================================================
//    // 10. 리소스 데이터 파싱
//    // ========================================================================
//    void SetResourceData(string data)
//    {
//        if (string.IsNullOrEmpty(data)) return;
//        var targetSo = DataManager.Instance.resourceDataSo;
//        if (targetSo == null) return;

//        if (targetSo.resourceDataList == null) targetSo.resourceDataList = new List<ResourceData>();
//        else targetSo.resourceDataList.Clear();

//        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

//        for (int i = 0; i < rows.Length; i++)
//        {
//            string[] cells = rows[i].Split('\t');
//            if (cells.Length < 2) continue;

//            ResourceData r = new ResourceData();
//            r.key = cells[0].Trim();
//            r.description = (cells.Length > 2) ? cells[2].Trim() : "";
//            targetSo.resourceDataList.Add(r);
//        }
//    }
//}