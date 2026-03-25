using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks; // UniTask
using UnityEngine;
using UnityEngine.Networking;
using UniRx;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GoogleSheetManager : Singleton<GoogleSheetManager>
{
    // ★ 구글 시트 URL (웹에 게시 -> TSV 형식으로 추출한 URL을 넣으세요)
    const string levelRuleDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=0&range=A2:I";
    const string castleMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=661929505&range=A2:H";
    const string generalMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1008843975&range=A2:H";
    const string buffMasterDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=1241447495&range=A2:E";

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

#if UNITY_EDITOR
        // 에디터에서 수동 실행(비플레이) 시 DataManager가 없더라도 SO에 직접 반영
        if (!Application.isPlaying && DataManager.InstanceOrNull == null)
        {
            bool saved = SaveToSoWithoutDataManager(levelRuleResult, castleMasterResult, generalMasterResult, buffMasterResult);
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
            if (cells.Length < 7) continue; // H(initialLord)는 옵션

            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            CastleMasterData castleData = new CastleMasterData
            {
                id = id,
                region = cells[1].Trim(),
                name = cells[2].Trim()
            };

            string gradeRaw = cells[3].Trim();
            if (int.TryParse(gradeRaw, out int gradeInt) && Enum.IsDefined(typeof(Grade), gradeInt))
            {
                castleData.grade = (Grade)gradeInt;
            }
            else if (!Enum.TryParse(gradeRaw, true, out castleData.grade))
            {
                castleData.grade = Grade.D;
            }

            float.TryParse(cells[4].Trim(), out castleData.baseValue);
            int.TryParse(cells[5].Trim(), out castleData.maxGarrison);
            int.TryParse(cells[6].Trim(), out castleData.initPopulation);

            // H: initialLord (옵션) - 숫자(0~) 또는 문자열(WEI/SHU/WU/OTHERS/NONE)
            castleData.initialLord = cells.Length > 7 ? ParseFaction(cells[7]) : Faction.NONE;

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
                buffId = cells.Length > 6 ? cells[6].Trim() : ""
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
            if (int.TryParse(typeRaw, out int typeInt) && Enum.IsDefined(typeof(BuffType), typeInt))
                buff.type = (BuffType)typeInt;
            else if (!Enum.TryParse(typeRaw, true, out buff.type))
                buff.type = BuffType.None;

            float.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out buff.value);

            dm.buffMasterDataMap[buff.id] = buff;
        }
    }

#if UNITY_EDITOR
    bool SaveToSoWithoutDataManager(string levelRuleData, string castleData, string generalData, string buffData)
    {
        var levelSo = FindAsset<LevelRuleDataSo>();
        var castleSo = FindAsset<CastleMasterDataSo>();
        var generalSo = FindAsset<GeneralMasterDataSo>();
        var buffSo = FindAsset<BuffMasterDataSo>();

        if (levelSo == null || castleSo == null || generalSo == null || buffSo == null)
        {
            Debug.LogError("[GoogleSheetManager] SO를 찾지 못했습니다. Level/Castle/General/Buff SO가 모두 프로젝트에 있어야 합니다.");
            return false;
        }

        levelSo.list = ParseLevelRuleList(levelRuleData);
        castleSo.list = ParseCastleList(castleData);
        generalSo.list = ParseGeneralList(generalData);
        buffSo.list = ParseBuffList(buffData);

        EditorUtility.SetDirty(levelSo);
        EditorUtility.SetDirty(castleSo);
        EditorUtility.SetDirty(generalSo);
        EditorUtility.SetDirty(buffSo);
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
            if (cells.Length < 7) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            var item = new CastleMasterData { id = id, region = cells[1].Trim(), name = cells[2].Trim() };
            string gradeRaw = cells[3].Trim();
            if (int.TryParse(gradeRaw, out int gi) && Enum.IsDefined(typeof(Grade), gi)) item.grade = (Grade)gi;
            else if (!Enum.TryParse(gradeRaw, true, out item.grade)) item.grade = Grade.D;
            float.TryParse(cells[4].Trim(), out item.baseValue);
            int.TryParse(cells[5].Trim(), out item.maxGarrison);
            int.TryParse(cells[6].Trim(), out item.initPopulation);
            item.initialLord = cells.Length > 7 ? ParseFaction(cells[7]) : Faction.NONE;
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
            var item = new GeneralMasterData { id = id, name = cells.Length > 1 ? cells[1].Trim() : "", buffId = cells.Length > 6 ? cells[6].Trim() : "" };
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
        if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
            return list;

        string[] rows = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split('\t');
            if (cells.Length < 4) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            var item = new BuffMasterData { id = id, name = cells.Length > 1 ? cells[1].Trim() : "", description = cells.Length > 4 ? cells[4].Trim() : "" };
            string typeRaw = cells.Length > 2 ? cells[2].Trim() : "";
            if (int.TryParse(typeRaw, out int ti) && Enum.IsDefined(typeof(BuffType), ti)) item.type = (BuffType)ti;
            else if (!Enum.TryParse(typeRaw, true, out item.type)) item.type = BuffType.None;
            float.TryParse(cells.Length > 3 ? cells[3].Trim() : "0", out item.value);
            list.Add(item);
        }
        return list;
    }
#endif
}