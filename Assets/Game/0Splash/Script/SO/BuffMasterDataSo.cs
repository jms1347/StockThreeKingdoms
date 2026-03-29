using System.Collections.Generic;
using UnityEngine;
public enum BuffType
{
    None = 0,

    // 1. 가치 및 시세 관련 (MTS 핵심)
    CastleValue = 1,       // 성 가치(시세) 배율 증가 (최종가 영향)
    PriceValue = 2,      // 성 액면가 조정 (시세 과열/할인 - 기초가액 영향)

    // 2. 심리 및 성장 관련
    SentimentRecovery = 3,     // 민심 직접 가감 (합연산: +10, -20 등)
    PopulationGrowth = 4,      // 백성 수 증가 속도 가속 (곱연산: 1.1f 등)

    // 3. 전쟁 및 방어 관련
    WarAttackLossReduction = 5,
    WarDefenseLossReduction = 6,

    // 4. 보상 관련
    DividendBonus = 7,         // 배당금 추가 보너스 (합연산)
    DividendMultiplier = 8,    // 배당 배율 조정 (곱연산: 1.5f, 0.5f)

            TradeLock = 10,            // 거래 정지 (1: 정지, 0: 정상)

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
