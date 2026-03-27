using System.Collections.Generic;
using UnityEngine;
public enum BuffType
{
    None = 0,

    // 1. 가치 관련
    ValueMultiplier = 1, // 성 가치(시세) 배율 증가
    ParValueModifier = 2,      // 성 액면가(가입/탈퇴비 기준) 조정

    // 2. 성장 관련
    SentimentRecovery = 3,     // 민심 회복량 증가
    PopulationGrowth = 4,      // 백성 수 증가 속도 가속

    // 3. 전쟁 관련 (손실 방어)
    WarAttackLossReduction = 5,  // 전쟁 공격 시 병사 손실율 감소
    WarDefenseLossReduction = 6, // 전쟁 수비 시 병사 손실율 감소

    // 4. 보상 관련
    DividendBonus = 7          // 배당금(보상) 추가 보너스
}
[System.Serializable]
public class BuffMasterData
{
    public string id;          // B01, B02...
    public string name;        // 버프 이름 (예: "황금 사과")
    public BuffType type;      // 위의 Enum 사용
    public float value;        // 적용 수치 (예: 0.2f 또는 1.1f)
    public string description; // "성 가치 배율을 20% 증가시킵니다."
}

[CreateAssetMenu(fileName = "BuffMasterDataSo", menuName = "ScriptableObject/BuffMasterDataSo")]
public class BuffMasterDataSo : ScriptableObject
{
    public List<BuffMasterData> list = new List<BuffMasterData>();

}
