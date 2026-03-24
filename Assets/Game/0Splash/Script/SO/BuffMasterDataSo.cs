using System.Collections.Generic;
using UnityEngine;
public enum BuffType
{
    None = 0,

    // 1. 경제 (성 가치 배율 및 가격 관련)
    ValueMultiplier = 1,    // 성 가치 배율(Value Multiplier) 증가
    BuyDiscount = 2,       // 가입비(Buy Price) 할인

    // 2. 민심 및 인구 (성장성 관련)
    SentimentRecovery = 3, // 민심 자동 회복량 증가
    PopulationGrowth = 4,  // 백성 수 증가 속도 가속

    // 3. 군사 및 리스크 (안정성 관련)
    WarDefense = 5,        // 전쟁 시 함락 확률 감소 및 유저 병사 손실(Slipage) 방어

    // 4. 보상 (수익성 관련)
    DividendBonus = 6      // 유저에게 지급되는 배당금 추가 보너스
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
