using System;
using System.Collections.Generic;
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
    /// <summary>A:id, B:name, C:regionId, D:grade, E:initialNationId, F:baseValue, G:maxTroops, H:initPopulation, I:posX, J:posY, K:adjacentIdsRaw</summary>
    const string castleMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=661929505&range=A2:K";
    /// <summary>A:id, B:name, C:grade, D:power, E:intel, F:charm, G:buffId, H:initialNationId, I:initialCastleId</summary>
    const string generalMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1008843975&range=A2:I";
    const string buffMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1241447495&range=A2:E";
    /// <summary>세력(Nation) 마스터 TSV URL. A:id, B:name, C:colorCode, D:capitalId, E:description (예: range=A2:E)</summary>
    const string nationMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1621681501&range=A2:E";
    /// <summary>지역(섹터) 마스터 TSV URL. A:지역코드, B:섹터명, C:특징, D:배정 성 예시 (낙양(C01) 형식, range=A2:D 등)</summary>
    const string regionMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1716545491&range=A2:D";

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

#if UNITY_EDITOR
        // 에디터에서 수동 실행(비플레이) 시 DataManager가 없더라도 SO에 직접 반영
        if (!Application.isPlaying && DataManager.InstanceOrNull == null)
        {
            bool saved = SaveToSoWithoutDataManager(levelRuleResult, castleMasterResult, generalMasterResult, buffMasterResult, nationMasterResult, regionMasterResult);
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

        // 2. 메인 스레드에서 파싱/반영 (DataManager/Unity 오브젝트 안전)
        SetLevelRuleData(dm, levelRuleResult);
        SetCastleMasterData(dm, castleMasterResult);
        SetGeneralMasterData(dm, generalMasterResult);
        SetBuffMasterData(dm, buffMasterResult);
        SetNationMasterData(dm, nationMasterResult);
        SetRegionMasterData(dm, regionMasterResult);

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
            if (cells.Length < 6) continue;

            LevelRuleData rule = new LevelRuleData();

            int.TryParse(cells[0].Trim(), out rule.level);                    // A: 레벨
            double.TryParse(cells[1].Trim(), out rule.laborCost);              // B: 노동력 비용
            double.TryParse(cells[2].Trim(), out rule.laborValue);             // C: 노동력 추가 금화
            double.TryParse(cells[3].Trim(), out rule.marketCost);              // D: 시장 비용
            double.TryParse(cells[4].Trim(), out rule.marketValuePerSec);      // E: 시장 금화/초
            double.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", out rule.marketMaxCapacity);  // F: 시장 창고 MAX
            double.TryParse(cells.Length > 6 ? cells[6].Trim() : "0", out rule.farmCost);            // G: 농장 비용
            double.TryParse(cells.Length > 7 ? cells[7].Trim() : "0", out rule.farmValuePerSec);    // H: 농장 식량/초
            double.TryParse(cells.Length > 8 ? cells[8].Trim() : "0", out rule.farmMaxCapacity);    // I: 농장 창고 MAX

            if (rule.marketMaxCapacity <= 0 && rule.marketValuePerSec > 0)
                rule.marketMaxCapacity = rule.marketValuePerSec * 28800;
            if (rule.farmMaxCapacity <= 0 && rule.farmValuePerSec > 0)
                rule.farmMaxCapacity = rule.farmValuePerSec * 28800;

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
            if (cells.Length < 8) continue;

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

            float.TryParse(cells[5].Trim(), out castleData.baseValue);
            int.TryParse(cells[6].Trim(), out castleData.maxTroops);
            int.TryParse(cells[7].Trim(), out castleData.initPopulation);
            float.TryParse(cells.Length > 8 ? cells[8].Trim() : "0", out castleData.posX);
            float.TryParse(cells.Length > 9 ? cells[9].Trim() : "0", out castleData.posY);
            castleData.adjacentIdsRaw = cells.Length > 10 ? cells[10].Trim() : "";

            dm.castleMasterDataMap[castleData.id] = castleData;
        }
    }

    static Faction ParseFaction(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Faction.NONE;
        raw = raw.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(Faction), n))
            return (Faction)n;
        if (Enum.TryParse(raw, true, out Faction f))
            return f;
        return Faction.NONE;
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
                buffId = cells.Length > 6 ? cells[6].Trim() : "",
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
            if (cells.Length < 4) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var buff = new BuffMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                description = cells.Length > 4 ? cells[4].Trim() : ""
            };

            string typeRaw = cells.Length > 2 ? cells[2].Trim() : "";
            if (Enum.TryParse<BuffType>(typeRaw, true, out BuffType parsedType))
                buff.type = parsedType;
            else if (int.TryParse(typeRaw, out int typeInt) && Enum.IsDefined(typeof(BuffType), typeInt))
                buff.type = (BuffType)typeInt;
            else
                buff.type = BuffType.None;

            float.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out buff.value);

            dm.buffMasterDataMap[buff.id] = buff;
        }
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
    bool SaveToSoWithoutDataManager(string levelRuleData, string castleData, string generalData, string buffData, string nationData, string regionData)
    {
        var levelSo = FindAsset<LevelRuleDataSo>();
        var castleSo = FindAsset<CastleMasterDataSo>();
        var generalSo = FindAsset<GeneralMasterDataSo>();
        var buffSo = FindAsset<BuffMasterDataSo>();
        var nationSo = FindAsset<NationMasterDataSo>();
        var regionSo = FindAsset<RegionMasterDataSo>();

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

        EditorUtility.SetDirty(levelSo);
        EditorUtility.SetDirty(castleSo);
        EditorUtility.SetDirty(generalSo);
        EditorUtility.SetDirty(buffSo);
        if (!string.IsNullOrWhiteSpace(nationData) && !nationData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(nationSo);
        if (!string.IsNullOrWhiteSpace(regionData) && !regionData.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            EditorUtility.SetDirty(regionSo);
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
            if (cells.Length < 6) continue;
            var rule = new LevelRuleData();
            int.TryParse(cells[0].Trim(), out rule.level);
            double.TryParse(cells[1].Trim(), out rule.laborCost);
            double.TryParse(cells[2].Trim(), out rule.laborValue);
            double.TryParse(cells[3].Trim(), out rule.marketCost);
            double.TryParse(cells[4].Trim(), out rule.marketValuePerSec);
            double.TryParse(cells.Length > 5 ? cells[5].Trim() : "0", out rule.marketMaxCapacity);
            double.TryParse(cells.Length > 6 ? cells[6].Trim() : "0", out rule.farmCost);
            double.TryParse(cells.Length > 7 ? cells[7].Trim() : "0", out rule.farmValuePerSec);
            double.TryParse(cells.Length > 8 ? cells[8].Trim() : "0", out rule.farmMaxCapacity);
            if (rule.marketMaxCapacity <= 0 && rule.marketValuePerSec > 0) rule.marketMaxCapacity = rule.marketValuePerSec * 28800;
            if (rule.farmMaxCapacity <= 0 && rule.farmValuePerSec > 0) rule.farmMaxCapacity = rule.farmValuePerSec * 28800;
            list.Add(rule);
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
            if (cells.Length < 8) continue;
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
            float.TryParse(cells[5].Trim(), out item.baseValue);
            int.TryParse(cells[6].Trim(), out item.maxTroops);
            int.TryParse(cells[7].Trim(), out item.initPopulation);
            float.TryParse(cells.Length > 8 ? cells[8].Trim() : "0", out item.posX);
            float.TryParse(cells.Length > 9 ? cells[9].Trim() : "0", out item.posY);
            item.adjacentIdsRaw = cells.Length > 10 ? cells[10].Trim() : "";
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
                buffId = cells.Length > 6 ? cells[6].Trim() : "",
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

        // i = 1부터 시작 (첫 줄이 컬럼 제목인 경우 스킵)
        for (int i = 1; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            var item = new BuffMasterData
            {
                id = id,
                name = cells.Length > 1 ? cells[1].Trim() : "",
                description = cells.Length > 4 ? cells[4].Trim() : ""
            };

            // --- 버프 타입(Enum) 파싱 로직 ---
            string typeRaw = cells.Length > 2 ? cells[2].Trim() : "";

            // 1. 문자열 이름으로 시도 (예: "ValueMultiplier", "ParValueModifier")
            if (Enum.TryParse<BuffType>(typeRaw, true, out BuffType parsedEnum))
            {
                item.type = parsedEnum;
            }
            // 2. 만약 숫자로 입력되었을 경우 대비 (예: "1", "2")
            else if (int.TryParse(typeRaw, out int ti) && Enum.IsDefined(typeof(BuffType), ti))
            {
                item.type = (BuffType)ti;
            }
            // 3. 둘 다 실패하면 기본값 None
            else
            {
                item.type = BuffType.None;
                Debug.LogWarning($"[DataManager] 알 수 없는 버프 타입 발견 (ID: {id}, Type: {typeRaw})");
            }

            // 수치 파싱
            float.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out item.value);

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
#endif
}