using UnityEngine;

[CreateAssetMenu(fileName = "New City", menuName = "CapitalWar/CityStaticData")]
public class CityStaticData : ScriptableObject
{
    public string CityID;         // 고유 ID (ex: "CITY_001")
    public string CityName;       // 성 이름 (ex: 낙양, 장안) [cite: 14]
    public int BasePopulation;    // 기본 인구수 (금화/식량 자동 생성의 베이스 수치) 
    public double MaxGrainVault;  // 성의 최대 식량 보관량 (초과 시 수비군 이탈 위험) 
    
    // 삼국지 장수 버프 (태수가 없을 때 기본 적용되거나 NPC 태수 데이터)
    public string DefaultGovernorName; 
    public float EconomyBuffRate; // 경제 활성 버프 계수 
}