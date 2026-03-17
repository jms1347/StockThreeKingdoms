using System;
using System.Collections.Generic;

[Serializable]
public class UserData
{
    // [1] 기본 정보
    public string UserID;
    public string Nickname;
    
    // [2] 핵심 자본 (방치형 특성상 double 사용)
    public double Gold;    // 금화 (투자 및 업그레이드 재화)
    public double Grain;   // 식량 (실시간 병사 유지비) [cite: 7]
    
    // [3] M2E 요소
    public int DailySteps; // 당일 누적 걸음 수 
    public DateTime LastStepUpdatedTime; // 걸음수 마지막 갱신 시간 (어뷰징 방지 및 초기화용)
    
    public int LaborLevel;   // 노동력 레벨 (터치당 금화 획득량 증가)
    public int MarketLevel;  // 시장 레벨 (초당 자동 금화 획득량 증가)
    public int FarmLevel;    // 농장 레벨 (초당 식량 자동 생산 - 기획안 확장) [cite: 13]
    public int SoldierTier;  // 병사 등급 (성에 투자 시 지분율/전투력 가중치 증가) 

    // [5] 투자 현황 (주식)
    // Key: 성의 고유 ID(ex: "CITY_LUOYANG"), Value: 투입한 병사 수
    public Dictionary<string, double> InvestedSoldiers = new Dictionary<string, double>();

    // [6] 메타 스탯
    public int HonorPoint; // 명예 스탯 (작위 승급용) [cite: 21]
    public string CurrentTitle; // 현재 작위 (예: 평민, 영주, 황제) [cite: 96]
}