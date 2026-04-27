using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks; // UniTask
using UnityEngine;
using UnityEngine.Networking;
using UniRx;
using static System.Net.WebRequestMethods;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GoogleSheetManager : Singleton<GoogleSheetManager>
{
    // ★ 구글 시트 URL (웹에 게시 -> TSV 형식으로 추출한 URL을 넣으세요)
    const string levelRuleDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=0&range=A2:I";
    /// <summary>A:id, B:name, C:regionId, D:grade, E:initialNationId, F:initialTaxRate%, G:baseValue, H:maxTroops, I:initPopulation, J:posX, K:posY, L:adjacentIdsRaw</summary>
    const string castleMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=661929505&range=A2:L";
    /// <summary>A:id, B:name, C:grade, D:power, E:intel, F:charm, G:infamy(0~100), H:initialNationId, I:initialCastleId</summary>
    const string generalMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1008843975&range=A2:I";
    /// <summary>A:id, B:name, C:CastleStatType, D:CurveType, E:value, F:description, G:durationDays(일, 비우면 1)</summary>
    const string buffMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1241447495&range=A2:G";
    /// <summary>세력(Nation) 마스터 TSV URL. A:id, B:name, C:colorCode, D:capitalId, E:description (예: range=A2:E)</summary>
    const string nationMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1621681501&range=A2:E";
    /// <summary>지역(섹터) 마스터 TSV URL. A:지역코드, B:섹터명, C:특징, D:배정 성 예시 (낙양(C01) 형식, range=A2:D 등)</summary>
    const string regionMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1716545491&range=A2:D";
    /// <summary>조건 라이브러리 TSV. A:condId, B:targetAttr, C:op, D:thresholdValue, E:Description (헤더 행은 자동 스킵)</summary>
    const string conditionLibraryDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=537776857&range=A2:E";
    /// <summary>이벤트 통합 TSV. A~F: EventMaster, G:rumorNewsCodes, H:breakingNewsCodes(콤마), M: affinity, N: ConditionIDs, O~S: 레거시/미사용 열.</summary>
    const string eventMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=902917272&range=A2:S";
    /// <summary>NewsMaster TSV. A:newsCode, B:headline, C:script. 비우면 다운로드 생략.</summary>
    const string newsMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=301270709&range=A2:D";
    /// <summary>EventStatModifier TSV. A:eventId, B:flatProbBonus, C:perMight, D:perIntel, E:perCharm, F:perInfamy. 비우면 다운로드 생략(SO·기존 맵 유지).</summary>
    const string eventStatModifierDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1830339338&range=A2:F";
    /// <summary>무작위 방문객 이벤트. A:id, B:visitorType, C:probability(예: 10%), D:effectReward</summary>
    const string randomVisitorDataURL =
        "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=814956030&range=A2:D";
    /// <summary>만보기 미션. A:step, B:targetSteps, C:mpReward, D:remarks</summary>
    const string stepMissionDataURL =
        "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1272332026&range=A2:D";

    public BoolReactiveProperty IsSetData = new BoolReactiveProperty(false);

    protected override void Awake()
    {
        base.Awake();
        CheckGetAllGSData();
    }

    [ContextMenu("SetData (수동 다운로드 테스트)")]
    async void CheckGetAllGSData()
    {
        IsSetData.Value = false;
        Debug.Log("[GoogleSheetManager] 밸런스 데이터 다운로드 시작...");

        // 1. 구글 시트 긁어오기
        string levelRuleResult = await GetGSDataToURL(levelRuleDataURL);
        string castleMasterResult = await GetGSDataToURL(castleMasterDataURL);
        string generalMasterResult = await GetGSDataToURL(generalMasterDataURL);
        string buffMasterResult = await GetGSDataToURL(buffMasterDataURL);
        string nationMasterResult = await GetGSDataToURL(nationMasterDataURL);
        string regionMasterResult = await GetGSDataToURL(regionMasterDataURL);
        string conditionLibraryResult = string.IsNullOrWhiteSpace(conditionLibraryDataURL)
            ? ""
            : await GetGSDataToURL(conditionLibraryDataURL);
        string eventMasterResult = string.IsNullOrWhiteSpace(eventMasterDataURL)
            ? ""
            : await GetGSDataToURL(eventMasterDataURL);
        string eventStatModifierResult = string.IsNullOrWhiteSpace(eventStatModifierDataURL)
            ? ""
            : await GetGSDataToURL(eventStatModifierDataURL);
        string newsMasterResult = string.IsNullOrWhiteSpace(newsMasterDataURL)
            ? ""
            : await GetGSDataToURL(newsMasterDataURL);
        string randomVisitorResult = string.IsNullOrWhiteSpace(randomVisitorDataURL)
            ? ""
            : await GetGSDataToURL(randomVisitorDataURL);
        string stepMissionResult = string.IsNullOrWhiteSpace(stepMissionDataURL)
            ? ""
            : await GetGSDataToURL(stepMissionDataURL);

#if UNITY_EDITOR
        // 에디터에서 수동 실행(비플레이) 시 DataManager가 없더라도 SO에 직접 반영
        if (!Application.isPlaying && DataManager.InstanceOrNull == null)
        {
            bool saved = SaveToSoWithoutDataManager(levelRuleResult, castleMasterResult, generalMasterResult, buffMasterResult, nationMasterResult, regionMasterResult, eventMasterResult, conditionLibraryResult, eventStatModifierResult, newsMasterResult, randomVisitorResult, stepMissionResult);
            IsSetData.Value = saved;
            if (saved)
                Debug.Log("[GoogleSheetManager] DataManager 없이 SO 저장 완료.");
            return;
        }
#endif

        // SingletonLoader에서 GoogleSheetManager가 DataManager보다 먼저 로드될 수 있음
        await UniTask.WaitUntil(() => DataManager.InstanceOrNull != null);
        var dm = DataManager.InstanceOrNull;
        if (dm == null)
        {
            Debug.LogError("[GoogleSheetManager] DataManager를 찾지 못해 시트 파싱을 중단합니다.");
            return;
        }

        if (dm.FixedSo == null)
        {
            Debug.LogError(
                "[GoogleSheetManager] FixedSoDataManager가 없습니다. DataManager와 같은 오브젝트에 FixedSoDataManager를 붙이고 마스터 SO를 할당하세요.");
            return;
        }

        // 2. 메인 스레드에서 파싱/반영 (DataManager/Unity 오브젝트 안전)
        SetLevelRuleData(dm, levelRuleResult);
        SetCastleMasterData(dm, castleMasterResult);
        SetGeneralMasterData(dm, generalMasterResult);
        SetBuffMasterData(dm, buffMasterResult);
        dm.MergeBuffMasterFromSoMissingKeys();
        SetNationMasterData(dm, nationMasterResult);
        SetRegionMasterData(dm, regionMasterResult);
        SetConditionLibraryData(dm, conditionLibraryResult);
        SetEventMasterData(dm, eventMasterResult);
        SetEventStatModifierData(dm, eventStatModifierResult);
        dm.MergeEventStatModifierFromSoMissingKeys();
        SetNewsMasterData(dm, newsMasterResult);
        dm.MergeNewsMasterFromSoMissingKeys();
        SetRandomVisitorData(dm, randomVisitorResult);
        SetStepMissionData(dm, stepMissionResult);

        // 3. 런타임 맵 내용을 SO 리스트에도 반영 (인스펙터에서 즉시 확인 가능)
        dm.SyncSoFromRuntimeMaps();

        // 4. DataManager 레디 상태로 변경
        dm.InitializeAllData();

        IsSetData.Value = true;
        Debug.Log("[GoogleSheetManager] 패치 완료! 게임을 시작해도 좋습니다.");
    }

    async UniTask<string> GetGSDataToURL(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return "";
            UnityWebRequest www = UnityWebRequest.Get(url);
            await www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GoogleSheet Error] {www.error}");
                return "";
            }
            return www.downloadHandler.text;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleSheet Exception] {e.Message}");
            return "";
        }
    }

    // ========================================================================
    // ★ [Odin 적용] 딕셔너리에 직접 파싱
    // ========================================================================

    /// <summary>시트 C열 등: <c>10%</c> → 0.1f, 숫자만이면 그대로(0~1로 클램프).</summary>
    public static bool TryParseSheetProbability(string raw, out float probability)
    {
        probability = 0f;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string t = raw.Trim();
        if (t.Length > 0 && t[t.Length - 1] == '%')
        {
            t = t.Substring(0, t.Length - 1).Trim();
            if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return false;
            probability = Mathf.Clamp01(pct / 100f);
            return true;
        }

        if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out probability))
            return false;
        probability = Mathf.Clamp01(probability);
        return true;
    }

    /// <summary>TSV A:id, B:visitorType, C:probability, D:effectReward. <see cref="DataManager"/>가 없으면 무시.</summary>
    public void SetRandomVisitorData(string tsv) => SetRandomVisitorData(DataManager.InstanceOrNull, tsv);

    public void SetRandomVisitorData(DataManager dm, string tsv)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(tsv)) return;
        if (tsv.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] RandomVisitor TSV가 아닌 HTML이 반환되었습니다.");
            return;
        }

        dm.randomVisitorMap.Clear();
        string[] rows = tsv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var row = new RandomVisitorData
            {
                id = id,
                visitorType = cells.Length > 1 ? cells[1].Trim() : "",
                effectReward = cells.Length > 3 ? cells[3].Trim() : ""
            };
            if (!TryParseSheetProbability(cells.Length > 2 ? cells[2] : "", out row.probability))
                row.probability = 0f;

            dm.randomVisitorMap[id] = row;
        }
    }

    /// <summary>TSV A:step, B:targetSteps, C:mpReward, D:remarks</summary>
    public void SetStepMissionData(string tsv) => SetStepMissionData(DataManager.InstanceOrNull, tsv);

    public void SetStepMissionData(DataManager dm, string tsv)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(tsv)) return;
        if (tsv.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] StepMission TSV가 아닌 HTML이 반환되었습니다.");
            return;
        }

        dm.stepMissionMap.Clear();
        string[] rows = tsv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;

            if (!int.TryParse(cells[0].Trim(), out int step)) continue;

            var row = new StepMissionData
            {
                step = step,
                remarks = cells.Length > 3 ? cells[3].Trim() : ""
            };
            int.TryParse(cells[1].Trim(), out row.targetSteps);
            int.TryParse(cells[2].Trim(), out row.mpReward);
            dm.stepMissionMap[step] = row;
        }
    }

    void SetLevelRuleData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        // 🛡️ 젬스(Gems)의 방어 로직: 구글 시트 에러로 HTML 페이지가 반환되었을 경우 파싱 중지
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.levelRuleMap.Clear(); // 딕셔너리 초기화

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 7) continue;

            LevelRuleData rule = new LevelRuleData();

            int.TryParse(cells[0].Trim(), out rule.level);                    // A: 레벨
            double.TryParse(cells[1].Trim(), out rule.laborCost);              // B: 노동력 비용
            double.TryParse(cells[2].Trim(), out rule.laborValue);             // C: 노동력 효과
            double.TryParse(cells[3].Trim(), out rule.marketCost);              // D: 시장 비용
            double.TryParse(cells[4].Trim(), out rule.marketValuePerSec);      // E: 시장 초당 생산
            double.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", out rule.warehouseCost);       // F: 창고 비용
            double.TryParse(cells.Length > 6 ? cells[6].Trim() : "0", out rule.warehouseMaxCapacity); // G: 창고 금화 최대
            double.TryParse(cells.Length > 7 ? cells[7].Trim() : "0", out rule.logisticsCost);       // H: 병참 비용
            double.TryParse(cells.Length > 8 ? cells[8].Trim() : "0", out rule.logisticsDiscountRate); // I: 유지비 할인율 %

            if (rule.warehouseMaxCapacity <= 0 && rule.marketValuePerSec > 0)
                rule.warehouseMaxCapacity = rule.marketValuePerSec * 28800;

            dm.levelRuleMap[rule.level] = rule;
        }
    }

    void SetCastleMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] CastleMaster TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.castleMasterDataMap.Clear();

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 9) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var castleData = new CastleMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                regionId = cells.Length > 2 ? cells[2].Trim() : "",
                initialNationId = cells.Length > 4 ? cells[4].Trim() : ""
            };

            string gradeRaw = cells.Length > 3 ? cells[3].Trim() : "";
            if (int.TryParse(gradeRaw, out int gradeInt) && Enum.IsDefined(typeof(Grade), gradeInt))
                castleData.grade = (Grade)gradeInt;
            else if (!Enum.TryParse(gradeRaw, true, out castleData.grade))
                castleData.grade = Grade.D;

            float.TryParse(cells[5].Trim(), out castleData.initialTaxRatePercent);
            float.TryParse(cells[6].Trim(), out castleData.baseValue);
            int.TryParse(cells[7].Trim(), out castleData.maxTroops);
            int.TryParse(cells[8].Trim(), out castleData.initPopulation);
            float.TryParse(cells.Length > 9 ? cells[9].Trim() : "0", out castleData.posX);
            float.TryParse(cells.Length > 10 ? cells[10].Trim() : "0", out castleData.posY);
            castleData.adjacentIdsRaw = cells.Length > 11 ? cells[11].Trim() : "";
            castleData.EnsureDerivedDefaults();

            dm.castleMasterDataMap[castleData.id] = castleData;
        }
    }

    static Faction ParseFaction(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Faction.OTHERS;
        raw = raw.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(Faction), n))
            return (Faction)n == Faction.NONE ? Faction.OTHERS : (Faction)n;
        if (Enum.TryParse(raw, true, out Faction f))
            return f == Faction.NONE ? Faction.OTHERS : f;
        return Faction.OTHERS;
    }

    /// <summary>장수 마스터 시트 G열(7번째, 0-based index 6). 0~100으로 클램프. 레거시 "B12" 형태는 숫자 부분만 사용.</summary>
    static int ParseGeneralMasterInfamyCell(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        raw = raw.Trim();
        if (int.TryParse(raw, out int n))
            return Mathf.Clamp(n, 0, 100);
        if (raw.Length > 1 && (raw[0] == 'B' || raw[0] == 'b') && int.TryParse(raw.Substring(1), out int legacy))
            return Mathf.Clamp(legacy, 0, 100);
        return 0;
    }

    void SetGeneralMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] GeneralMaster TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.generalMasterDataMap.Clear();

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 6) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var general = new GeneralMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                infamy = ParseGeneralMasterInfamyCell(cells.Length > 6 ? cells[6] : ""),
                initialNationId = cells.Length > 7 ? cells[7].Trim() : "",
                initialCastleId = cells.Length > 8 ? cells[8].Trim() : ""
            };

            string gradeRaw = cells.Length > 2 ? cells[2].Trim() : "";
            if (int.TryParse(gradeRaw, out int gradeInt) && Enum.IsDefined(typeof(Grade), gradeInt))
                general.grade = (Grade)gradeInt;
            else if (!Enum.TryParse(gradeRaw, true, out general.grade))
                general.grade = Grade.D;

            int.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out general.power);
            int.TryParse(cells.Length > 4 ? cells[4].Trim() : "0", out general.intel);
            int.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", out general.charm);

            dm.generalMasterDataMap[general.id] = general;
        }
    }

    void SetBuffMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] BuffMaster TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.buffMasterDataMap.Clear();

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 5) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var buff = new BuffMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                description = cells.Length > 5 ? cells[5].Trim() : "",
                durationDays = 1
            };

            if (!TryParseCastleStatTypeCell(cells.Length > 2 ? cells[2] : "", out buff.statType))
                buff.statType = CastleStatType.None;
            if (!TryParseCurveTypeCell(cells.Length > 3 ? cells[3] : "", out buff.curveType))
                buff.curveType = CurveType.None;

            float.TryParse(cells.Length > 4 ? cells[4].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out buff.value);

            if (cells.Length > 6 && int.TryParse(cells[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int dur) && dur >= 1)
                buff.durationDays = dur;

            dm.buffMasterDataMap[buff.id] = buff;
        }
    }

    /// <summary>시트 C열 <see cref="CastleStatType"/> — 정수·영문·또는 enum 주석과 동일한 한글(예: 성 가치). <see cref="CastleStatTypeSheetParser"/>.</summary>
    public static bool TryParseCastleStatTypeCell(string raw, out CastleStatType statType) =>
        CastleStatTypeSheetParser.TryParse(raw, out statType);

    /// <summary>시트 D열 <see cref="CurveType"/> (이름 또는 정수).</summary>
    public static bool TryParseCurveTypeCell(string raw, out CurveType curveType)
    {
        curveType = CurveType.None;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string t = raw.Trim();
        if (Enum.TryParse(t, true, out curveType))
            return true;
        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) &&
            Enum.IsDefined(typeof(CurveType), n))
        {
            curveType = (CurveType)n;
            return true;
        }

        return false;
    }

    void SetNationMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] NationMaster TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.nationMasterDataMap.Clear();

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 1) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var nation = new NationMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                colorCode = cells.Length > 2 ? cells[2].Trim() : "",
                capitalId = cells.Length > 3 ? cells[3].Trim() : "",
                description = cells.Length > 4 ? cells[4].Trim() : ""
            };

            dm.nationMasterDataMap[nation.id] = nation;
        }
    }

    void SetRegionMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] RegionMaster TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.regionMasterDataMap.Clear();

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            var region = ParseRegionMasterRow(cells);
            if (region != null)
                dm.regionMasterDataMap[region.id] = region;
        }

        dm.RebuildRegionCastleLookup();
    }

    static EventScope ParseEventScopeCell(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return EventScope.Castle;
        raw = raw.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(EventScope), n))
            return (EventScope)n;
        if (Enum.TryParse(raw, true, out EventScope s))
            return s;
        return EventScope.Castle;
    }

    static List<string> ParseEventBuffCodesCell(string cell)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(cell)) return list;
        string[] parts = cell.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string t = parts[i].Trim();
            if (!string.IsNullOrEmpty(t))
                list.Add(t);
        }

        return list;
    }

    /// <summary>Condition 탭 TSV → <see cref="ConditionData"/> 목록. <see cref="ConditionDataSo"/>.list에 넣기 위함.</summary>
    public static List<ConditionData> ParseConditionLibraryDataFromTsv(string data)
    {
        var list = new List<ConditionData>();
        if (string.IsNullOrWhiteSpace(data)) return list;
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase)) return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;
            string cid = cells[0].Trim();
            if (string.IsNullOrEmpty(cid)) continue;
            if (IsConditionLibraryHeaderRow(cid, cells))
                continue;

            string attr, op;
            float tv;
            string desc = "";
            // 레거시: A condId, B eventId(EV_*), C targetAttr, D op, E threshold [, F Description…]
            if (cells.Length >= 5 && cells[1].Trim().StartsWith("EV_", StringComparison.OrdinalIgnoreCase))
            {
                attr = cells[2].Trim();
                op = cells[3].Trim();
                if (!float.TryParse(cells[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tv))
                    tv = 0f;
                if (cells.Length > 5)
                    desc = cells[5].Trim();
            }
            else
            {
                // 표준: A condId, B targetAttr, C op, D thresholdValue, E Description
                attr = cells[1].Trim();
                op = cells[2].Trim();
                if (!float.TryParse(cells[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tv))
                    tv = 0f;
                if (cells.Length > 4)
                    desc = cells[4].Trim();
            }

            if (!ConditionTypeSheetParser.TryParse(attr, out ConditionType ct))
            {
                Debug.LogWarning($"[GoogleSheetManager] Condition 라이브러리: 알 수 없는 targetAttr (condId={cid}, B={attr})");
                continue;
            }

            if (!ConditionOperatorSheetParser.TryParse(op, out ConditionOperator cop))
            {
                Debug.LogWarning($"[GoogleSheetManager] Condition 라이브러리: 알 수 없는 op (condId={cid}, C={op})");
                continue;
            }

            list.Add(new ConditionData
            {
                conditionId = cid,
                conditionType = ct,
                conditionOperator = cop,
                targetValue = tv,
                description = desc
            });
        }

        return list;
    }

    void SetConditionLibraryData(DataManager dm, string data)
    {
        if (dm == null) return;
        dm.ClearConditionLibrary();
        if (string.IsNullOrWhiteSpace(data)) return;
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] Condition 라이브러리 TSV가 아닌 HTML이 반환되었습니다.");
            return;
        }

        var list = ParseConditionLibraryDataFromTsv(data);
        dm.ApplyParsedConditionLibrary(list);
    }

    static bool IsConditionLibraryHeaderRow(string colA, string[] cells)
    {
        if (string.Equals(colA, "condId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(colA, "conditionId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(colA, "conditionid", StringComparison.OrdinalIgnoreCase))
            return true;
        if (cells.Length > 1)
        {
            string b = cells[1].Trim();
            if (string.Equals(b, "targetAttr", StringComparison.OrdinalIgnoreCase)
                || string.Equals(b, "targetattr", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    void SetEventMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrEmpty(data)) return;

        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] EventMaster TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        dm.eventMasterDataMap.Clear();
        dm.ClearNewsTemplateSheetRows();

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (!TryParseUnifiedEventNewsRow(cells, out var ev, out _, out _))
                continue;

            dm.AddEventMasterDataRow(ev);
        }
    }

    void SetEventStatModifierData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrWhiteSpace(data)) return;
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] EventStatModifier TSV가 아닌 HTML이 반환되었습니다. URL·gid·웹 게시를 확인해 주세요.");
            return;
        }

        var list = ParseEventStatModifierTsv(data);
        dm.ApplyParsedEventStatModifier(list);
    }

    void SetNewsMasterData(DataManager dm, string data)
    {
        if (dm == null) return;
        if (string.IsNullOrWhiteSpace(data)) return;
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] NewsMaster TSV가 아닌 HTML이 반환되었습니다. URL·gid·웹 게시를 확인해 주세요.");
            return;
        }

        var list = ParseNewsMasterTsv(data);
        dm.ApplyParsedNewsMaster(list);
    }

    /// <summary>
    /// NewsMaster TSV. 표준: A:newsCode, B:newsType, C:headline, D:script.
    /// B가 타입으로 파싱되지 않으면 레거시: B=headline, C=script (추가 열은 무시).
    /// </summary>
    public static List<NewsMasterData> ParseNewsMasterTsv(string data)
    {
        var list = new List<NewsMasterData>();
        if (string.IsNullOrWhiteSpace(data)) return list;
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase)) return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 2) continue;
            string code = cells[0].Trim();
            if (string.IsNullOrEmpty(code)) continue;
            if (string.Equals(code, "newsCode", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(code, "code", StringComparison.OrdinalIgnoreCase) && cells.Length > 1
                    && (string.Equals(cells[1].Trim(), "headline", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(cells[1].Trim(), "newsType", StringComparison.OrdinalIgnoreCase))))
                continue;

            string colB = cells.Length > 1 ? cells[1].Trim() : "";
            var row = new NewsMasterData { newsCode = code };
            if (TryParseNewsTypeCell(colB, out var ntype))
            {
                row.newsType = ntype;
                row.headline = cells.Length > 2 ? cells[2].Trim() : "";
                row.script = cells.Length > 3 ? cells[3].Trim() : "";
            }
            else
            {
                row.newsType = NewsType.None;
                row.headline = colB;
                row.script = cells.Length > 2 ? cells[2].Trim() : "";
            }

            list.Add(row);
        }

        return list;
    }

    static bool TryParseNewsTypeCell(string raw, out NewsType newsType)
    {
        newsType = NewsType.None;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        raw = raw.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(NewsType), n))
        {
            newsType = (NewsType)n;
            return true;
        }

        if (!Enum.TryParse(raw, true, out newsType))
            return false;
        return Enum.IsDefined(typeof(NewsType), newsType);
    }

    /// <summary>EventStatModifier 탭 TSV → 리스트. A:eventId, B:flatProbBonus, C:perMight, D:perIntel, E:perCharm, F:perInfamy.</summary>
    public static List<EventStatModifierData> ParseEventStatModifierTsv(string data)
    {
        var list = new List<EventStatModifierData>();
        if (string.IsNullOrWhiteSpace(data)) return list;
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase)) return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 2) continue;
            string eid = cells[0].Trim();
            if (string.IsNullOrEmpty(eid)) continue;
            if (IsEventStatModifierHeaderRow(eid, cells)) continue;

            var row = new EventStatModifierData { id = eid };
            float.TryParse(cells.Length > 1 ? cells[1].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out row.flatProbBonus);
            float.TryParse(cells.Length > 2 ? cells[2].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out row.perMight);
            float.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out row.perIntel);
            float.TryParse(cells.Length > 4 ? cells[4].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out row.perCharm);
            float.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out row.perInfamy);
            list.Add(row);
        }

        return list;
    }

    static bool IsEventStatModifierHeaderRow(string colA, string[] cells)
    {
        if (string.Equals(colA, "eventId", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(colA, "id", StringComparison.OrdinalIgnoreCase) && cells.Length > 1
            && string.Equals(cells[1].Trim(), "flatProbBonus", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>A~F 이벤트, G:rumorNewsCodes, H:breakingNewsCodes(콤마), M affinity, N ConditionIDs. 확률 보정은 EventStatModifier 탭.</summary>
    internal static bool TryParseUnifiedEventNewsRow(string[] cells, out EventMasterData ev,
        out NewsTemplateSheetRow sheetRow, out NewsTemplateEntry soEntry)
    {
        ev = null;
        sheetRow = null;
        soEntry = null;
        if (cells == null || cells.Length < 5) return false;

        string id = cells[0].Trim();
        if (string.IsNullOrEmpty(id)) return false;

        ev = new EventMasterData
        {
            id = id,
            name = cells.Length > 1 ? cells[1].Trim() : "",
            scope = ParseEventScopeCell(cells.Length > 2 ? cells[2] : ""),
            buffCodes = ParseEventBuffCodesCell(cells.Length > 5 ? cells[5] : ""),
            affinityTagsRaw = cells.Length > 12 ? cells[12].Trim() : "",
            conditionIds = ParseConditionIdsCell(cells.Length > 13 ? cells[13] : "")
        };

        int.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out ev.minDays);
        int.TryParse(cells.Length > 4 ? cells[4].Trim() : "0", out ev.maxDays);
        if (ev.maxDays < ev.minDays)
            ev.maxDays = ev.minDays;

        if (cells.Length > 6)
            ev.rumorNewsCodes = ParseCommaSeparatedCodes(cells[6]);
        if (cells.Length > 7)
            ev.breakingNewsCodes = ParseCommaSeparatedCodes(cells[7]);

        return true;
    }

    static List<string> ParseCommaSeparatedCodes(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string t = parts[i].Trim();
            if (!string.IsNullOrEmpty(t))
                list.Add(t);
        }

        return list;
    }

    public static List<string> ParseConditionIdsCell(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string t = parts[i].Trim();
            if (!string.IsNullOrEmpty(t))
                list.Add(t);
        }

        return list;
    }

    public static bool TryParseEventConditionOp(string token, out EventConditionOp op)
    {
        op = EventConditionOp.Eq;
        if (!ConditionOperatorSheetParser.TryParse(token, out ConditionOperator co))
            return false;
        op = EventConditionOpFrom(co);
        return true;
    }

    static EventConditionOp EventConditionOpFrom(ConditionOperator co)
    {
        switch (co)
        {
            case ConditionOperator.LessThan: return EventConditionOp.Lt;
            case ConditionOperator.LessOrEqual: return EventConditionOp.Le;
            case ConditionOperator.GreaterThan: return EventConditionOp.Gt;
            case ConditionOperator.GreaterOrEqual: return EventConditionOp.Ge;
            case ConditionOperator.Equal: return EventConditionOp.Eq;
            case ConditionOperator.NotEqual: return EventConditionOp.Ne;
            default: return EventConditionOp.Eq;
        }
    }

    static List<NewsTemplateEntry> MergeNewsTemplateSoPreserveSprites(NewsTemplateSo prev, List<NewsTemplateEntry> fromSheet)
    {
        var oldById = new Dictionary<string, NewsTemplateEntry>();
        if (prev != null && prev.entries != null)
        {
            for (int i = 0; i < prev.entries.Count; i++)
            {
                var e = prev.entries[i];
                if (e != null && !string.IsNullOrWhiteSpace(e.id))
                    oldById[e.id.Trim()] = e;
            }
        }

        var merged = new List<NewsTemplateEntry>();
        var seen = new HashSet<string>();
        if (fromSheet != null)
        {
            for (int i = 0; i < fromSheet.Count; i++)
            {
                var n = fromSheet[i];
                if (n == null || string.IsNullOrWhiteSpace(n.id)) continue;
                string nid = n.id.Trim();
                seen.Add(nid);
                if (oldById.TryGetValue(nid, out var o) && n.reporterIcon == null)
                    n.reporterIcon = o.reporterIcon;
                merged.Add(n);
            }
        }

        foreach (var kv in oldById)
        {
            if (!seen.Contains(kv.Key))
                merged.Add(kv.Value);
        }

        return merged;
    }

    /// <summary>"낙양(C01), 호로관(C21)" 등 괄호 안의 성 ID를 순서대로 추출.</summary>
    static List<string> ParseCastleIdsFromAssignedExamplesCell(string cell)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(cell)) return list;
        foreach (Match m in Regex.Matches(cell, @"\(([A-Za-z0-9_]+)\)"))
        {
            if (m.Groups.Count > 1 && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                list.Add(m.Groups[1].Value.Trim());
        }
        return list;
    }

    static RegionMasterData ParseRegionMasterRow(string[] cells)
    {
        if (cells == null || cells.Length < 1) return null;
        string id = cells[0].Trim();
        if (string.IsNullOrEmpty(id)) return null;
        return new RegionMasterData
        {
            id = id,
            sectorName = cells.Length > 1 ? cells[1].Trim() : "",
            features = cells.Length > 2 ? cells[2].Trim() : "",
            castleIds = ParseCastleIdsFromAssignedExamplesCell(cells.Length > 3 ? cells[3] : "")
        };
    }

#if UNITY_EDITOR
    bool SaveToSoWithoutDataManager(string levelRuleData, string castleData, string generalData, string buffData, string nationData, string regionData, string eventData, string conditionLibraryData = "", string eventStatModifierData = "", string newsMasterData = "", string randomVisitorData = "", string stepMissionData = "")
    {
        var levelSo = FindAsset<LevelRuleDataSo>();
        var castleSo = FindAsset<CastleMasterDataSo>();
        var generalSo = FindAsset<GeneralMasterDataSo>();
        var buffSo = FindAsset<BuffMasterDataSo>();
        var nationSo = FindAsset<NationMasterDataSo>();
        var regionSo = FindAsset<RegionMasterDataSo>();
        var eventSo = FindAsset<EventMasterDataSo>();
        var newsTemplateSo = FindAsset<NewsTemplateSo>();

        if (levelSo == null || castleSo == null || generalSo == null || buffSo == null || nationSo == null || regionSo == null)
        {
            Debug.LogError("[GoogleSheetManager] SO를 찾지 못했습니다. Level/Castle/General/Buff/Nation/Region SO가 모두 프로젝트에 있어야 합니다.");
            return false;
        }

        levelSo.list = ParseLevelRuleList(levelRuleData);
        castleSo.list = ParseCastleList(castleData);
        generalSo.list = ParseGeneralList(generalData);
        buffSo.list = ParseBuffList(buffData);
        if (!string.IsNullOrWhiteSpace(nationData) && !nationData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            nationSo.list = ParseNationList(nationData);
        if (!string.IsNullOrWhiteSpace(regionData) && !regionData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            regionSo.list = ParseRegionList(regionData);
        if (eventSo != null && !string.IsNullOrWhiteSpace(eventData) && !eventData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            ParseUnifiedEventSheetForEditor(eventData, out var evList, out var newsList);
            eventSo.list = evList;
            if (newsTemplateSo != null && newsList != null && newsList.Count > 0)
            {
                newsTemplateSo.entries = MergeNewsTemplateSoPreserveSprites(newsTemplateSo, newsList);
                EditorUtility.SetDirty(newsTemplateSo);
            }
        }

        var conditionSo = FindAsset<ConditionDataSo>();
        if (conditionSo != null && !string.IsNullOrWhiteSpace(conditionLibraryData)
                                && !conditionLibraryData.TrimStart()
                                    .StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            conditionSo.list = ParseConditionLibraryDataFromTsv(conditionLibraryData);
            EditorUtility.SetDirty(conditionSo);
        }

        var eventStatModifierSo = FindAsset<EventStatModifierSo>();
        if (eventStatModifierSo != null && !string.IsNullOrWhiteSpace(eventStatModifierData)
                                         && !eventStatModifierData.TrimStart()
                                             .StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            eventStatModifierSo.list = ParseEventStatModifierTsv(eventStatModifierData);
            EditorUtility.SetDirty(eventStatModifierSo);
        }

        var newsMasterSo = FindAsset<NewsMasterDataSo>();
        if (newsMasterSo != null && !string.IsNullOrWhiteSpace(newsMasterData)
                                  && !newsMasterData.TrimStart()
                                      .StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            newsMasterSo.list = ParseNewsMasterTsv(newsMasterData);
            EditorUtility.SetDirty(newsMasterSo);
        }

        var randomVisitorSo = FindAsset<RandomVisitorDataSo>();
        if (randomVisitorSo != null && !string.IsNullOrWhiteSpace(randomVisitorData)
                                     && !randomVisitorData.TrimStart()
                                         .StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            randomVisitorSo.list = ParseRandomVisitorList(randomVisitorData);
            EditorUtility.SetDirty(randomVisitorSo);
        }

        var stepMissionSo = FindAsset<StepMissionDataSo>();
        if (stepMissionSo != null && !string.IsNullOrWhiteSpace(stepMissionData)
                                   && !stepMissionData.TrimStart()
                                       .StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            stepMissionSo.list = ParseStepMissionList(stepMissionData);
            EditorUtility.SetDirty(stepMissionSo);
        }

        EditorUtility.SetDirty(levelSo);
        EditorUtility.SetDirty(castleSo);
        EditorUtility.SetDirty(generalSo);
        EditorUtility.SetDirty(buffSo);
        if (!string.IsNullOrWhiteSpace(nationData) && !nationData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(nationSo);
        if (!string.IsNullOrWhiteSpace(regionData) && !regionData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(regionSo);
        if (eventSo != null && !string.IsNullOrWhiteSpace(eventData) && !eventData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(eventSo);
        if (newsMasterSo != null && !string.IsNullOrWhiteSpace(newsMasterData)
                                && !newsMasterData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(newsMasterSo);
        if (randomVisitorSo != null && !string.IsNullOrWhiteSpace(randomVisitorData)
                                     && !randomVisitorData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(randomVisitorSo);
        if (stepMissionSo != null && !string.IsNullOrWhiteSpace(stepMissionData)
                                  && !stepMissionData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(stepMissionSo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return true;
    }

    static T FindAsset<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    List<LevelRuleData> ParseLevelRuleList(string data)
    {
        var list = new List<LevelRuleData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 7) continue;
            var rule = new LevelRuleData();
            int.TryParse(cells[0].Trim(), out rule.level);
            double.TryParse(cells[1].Trim(), out rule.laborCost);
            double.TryParse(cells[2].Trim(), out rule.laborValue);
            double.TryParse(cells[3].Trim(), out rule.marketCost);
            double.TryParse(cells[4].Trim(), out rule.marketValuePerSec);
            double.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", out rule.warehouseCost);
            double.TryParse(cells.Length > 6 ? cells[6].Trim() : "0", out rule.warehouseMaxCapacity);
            double.TryParse(cells.Length > 7 ? cells[7].Trim() : "0", out rule.logisticsCost);
            double.TryParse(cells.Length > 8 ? cells[8].Trim() : "0", out rule.logisticsDiscountRate);
            if (rule.warehouseMaxCapacity <= 0 && rule.marketValuePerSec > 0)
                rule.warehouseMaxCapacity = rule.marketValuePerSec * 28800;
            list.Add(rule);
        }
        return list;
    }

    List<RandomVisitorData> ParseRandomVisitorList(string data)
    {
        var list = new List<RandomVisitorData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var row = new RandomVisitorData
            {
                id = id,
                visitorType = cells.Length > 1 ? cells[1].Trim() : "",
                effectReward = cells.Length > 3 ? cells[3].Trim() : ""
            };
            if (!TryParseSheetProbability(cells.Length > 2 ? cells[2] : "", out row.probability))
                row.probability = 0f;
            list.Add(row);
        }

        return list;
    }

    List<StepMissionData> ParseStepMissionList(string data)
    {
        var list = new List<StepMissionData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;
            if (!int.TryParse(cells[0].Trim(), out int step)) continue;

            var row = new StepMissionData
            {
                step = step,
                remarks = cells.Length > 3 ? cells[3].Trim() : ""
            };
            int.TryParse(cells[1].Trim(), out row.targetSteps);
            int.TryParse(cells[2].Trim(), out row.mpReward);
            list.Add(row);
        }

        return list;
    }

    List<CastleMasterData> ParseCastleList(string data)
    {
        var list = new List<CastleMasterData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 9) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            var item = new CastleMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                regionId = cells.Length > 2 ? cells[2].Trim() : "",
                initialNationId = cells.Length > 4 ? cells[4].Trim() : ""
            };
            string gradeRaw = cells.Length > 3 ? cells[3].Trim() : "";
            if (int.TryParse(gradeRaw, out int gi) && Enum.IsDefined(typeof(Grade), gi)) item.grade = (Grade)gi;
            else if (!Enum.TryParse(gradeRaw, true, out item.grade)) item.grade = Grade.D;
            float.TryParse(cells[5].Trim(), out item.initialTaxRatePercent);
            float.TryParse(cells[6].Trim(), out item.baseValue);
            int.TryParse(cells[7].Trim(), out item.maxTroops);
            int.TryParse(cells[8].Trim(), out item.initPopulation);
            float.TryParse(cells.Length > 9 ? cells[9].Trim() : "0", out item.posX);
            float.TryParse(cells.Length > 10 ? cells[10].Trim() : "0", out item.posY);
            item.adjacentIdsRaw = cells.Length > 11 ? cells[11].Trim() : "";
            item.EnsureDerivedDefaults();
            list.Add(item);
        }
        return list;
    }

    List<GeneralMasterData> ParseGeneralList(string data)
    {
        var list = new List<GeneralMasterData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 6) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            var item = new GeneralMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                infamy = ParseGeneralMasterInfamyCell(cells.Length > 6 ? cells[6] : ""),
                initialNationId = cells.Length > 7 ? cells[7].Trim() : "",
                initialCastleId = cells.Length > 8 ? cells[8].Trim() : ""
            };
            string gradeRaw = cells.Length > 2 ? cells[2].Trim() : "";
            if (int.TryParse(gradeRaw, out int gi) && Enum.IsDefined(typeof(Grade), gi)) item.grade = (Grade)gi;
            else if (!Enum.TryParse(gradeRaw, true, out item.grade)) item.grade = Grade.D;
            int.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out item.power);
            int.TryParse(cells.Length > 4 ? cells[4].Trim() : "0", out item.intel);
            int.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", out item.charm);
            list.Add(item);
        }
        return list;
    }

    List<BuffMasterData> ParseBuffList(string data)
    {
        var list = new List<BuffMasterData>();
        // 구글 시트 오류(HTML 응답) 또는 데이터 없음 체크
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[DataManager] 구글 시트 접근 실패 혹은 잘못된 데이터 형식입니다.");
            return list;
        }

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 5) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var item = new BuffMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                description = cells.Length > 5 ? cells[5].Trim() : "",
                durationDays = 1
            };

            if (!TryParseCastleStatTypeCell(cells.Length > 2 ? cells[2] : "", out item.statType))
            {
                item.statType = CastleStatType.None;
                Debug.LogWarning($"[GoogleSheetManager] 알 수 없는 CastleStatType (ID: {id}, C열: {cells[2]})");
            }

            if (!TryParseCurveTypeCell(cells.Length > 3 ? cells[3] : "", out item.curveType))
                item.curveType = CurveType.None;

            float.TryParse(cells.Length > 4 ? cells[4].Trim() : "0", NumberStyles.Float, CultureInfo.InvariantCulture,
                out item.value);

            if (cells.Length > 6 && int.TryParse(cells[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int dur) && dur >= 1)
                item.durationDays = dur;

            list.Add(item);
        }
        return list;
    }

    List<NationMasterData> ParseNationList(string data)
    {
        var list = new List<NationMasterData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 1) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            list.Add(new NationMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                colorCode = cells.Length > 2 ? cells[2].Trim() : "",
                capitalId = cells.Length > 3 ? cells[3].Trim() : "",
                description = cells.Length > 4 ? cells[4].Trim() : ""
            });
        }
        return list;
    }

    List<RegionMasterData> ParseRegionList(string data)
    {
        var list = new List<RegionMasterData>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            var item = ParseRegionMasterRow(rows[i].Split('\t'));
            if (item != null)
                list.Add(item);
        }
        return list;
    }

    static void ParseUnifiedEventSheetForEditor(string data, out List<EventMasterData> events, out List<NewsTemplateEntry> newsEntries)
    {
        events = new List<EventMasterData>();
        newsEntries = new List<NewsTemplateEntry>();
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (!TryParseUnifiedEventNewsRow(cells, out var ev, out _, out var soEntry))
                continue;
            events.Add(ev);
            if (soEntry != null)
                newsEntries.Add(soEntry);
        }
    }

    List<EventMasterData> ParseEventList(string data)
    {
        ParseUnifiedEventSheetForEditor(data, out var events, out _);
        return events;
    }
#endif
}