using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GeneralMasterData
{
    public string id;                // G001
    public string name;              // 조조
    public Grade grade;              // SS, S, A...
    public int power;                // 무력
    public int intel;                // 지력
    public int charm;                // 매력
    public string buffId;            // S급 이상 버프 ID

    [Header("초기 배치 데이터")]
    public string initialNationId;   // 초기 소속 국가 (WEI, SHU, WU, OTHERS)
    public string initialCastleId;   // 초기 배치 성 ID (C01 ~ C50)

    [Header("UI")]
    [Tooltip("성 상세 태수 초상화(없으면 이니셜 표시).")]
    public Sprite governorPortrait;

    public bool HasBuff => !string.IsNullOrEmpty(buffId);
}

[CreateAssetMenu(fileName = "GeneralMasterDataSo", menuName = "ScriptableObject/GeneralMasterDataSo")]
public class GeneralMasterDataSo : ScriptableObject
{
    public List<GeneralMasterData> list = new List<GeneralMasterData>();

}
