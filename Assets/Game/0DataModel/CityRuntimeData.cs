using System;
using System.Collections.Generic;

[Serializable]
public class CityRuntimeData
{
    public string CityID;
    
    // [1] 실시간 성내 자원
    public double CurrentGold;    // 현재 성에 누적된 배당용 금화
    public double CurrentGrain;   // 현재 성에 비축된 식량 (수비군 유지용) 
    
    [cite_start]// [2] 주주 및 지분 현황 (투자 심리전의 핵심) [cite: 2]
    public double TotalDeployedSoldiers; // 전체 투입된 수비 병사 총합 (지분율 분모)
    
    [cite_start]// 해당 성에 투자한 유저 리스트 (상위 5대 주주 파악용) [cite: 15]
    // Key: UserID, Value: 투입 병사 수
    public Dictionary<string, double> Shareholders = new Dictionary<string, double>();
    
    [cite_start]// [3] 태수(대주주) 정보 [cite: 10]
    public string CurrentGovernorUserID; // 현재 태수의 UserID
    public float CurrentTaxRate;         // 태수가 설정한 세금 징수율
    
    [cite_start]// [4] 이벤트 및 루머 상태 [cite: 15, 16]
    public bool IsUnderAttackWarning; // AI 공격 예고 상태 [cite: 20]
    public string ActiveRumor;        // 현재 적용 중인 루머 (예: "풍년", "전염병") [cite: 16]
}