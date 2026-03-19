using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks; // UniTask
using UnityEngine;
using UnityEngine.Networking;
using UniRx;

public class GoogleSheetManager : Singleton<GoogleSheetManager>
{
    // ★ 구글 시트 URL (웹에 게시 -> TSV 형식으로 추출한 URL을 넣으세요)
    const string levelRuleDataURL = "https://docs.google.com/spreadsheets/d/1lKO3bQFraPLt6cu-SsOGGH2-qQLxzOaEWHnMXOcgEMU/export?format=tsv&gid=0&range=A2:I";

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
        string result = await GetGSDataToURL(levelRuleDataURL);

        // 2. 백그라운드 스레드에서 파싱 (게임 렉 방지)
        await Task.Run(() => SetLevelRuleData(result));

        // 3. 메인 스레드에서 DataManager 레디 상태로 변경
        DataManager.Instance.InitializeAllData();

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
    void SetLevelRuleData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        // 🛡️ 젬스(Gems)의 방어 로직: 구글 시트 에러로 HTML 페이지가 반환되었을 경우 파싱 중지
        if (data.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[GoogleSheetManager] TSV가 아닌 HTML이 반환되었습니다. 시트가 '웹에 게시' 상태인지, URL이 정확한지 확인해 주세요.");
            return;
        }

        DataManager.Instance.levelRuleMap.Clear(); // 딕셔너리 초기화

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

            DataManager.Instance.levelRuleMap[rule.level] = rule;
        }
    }
}