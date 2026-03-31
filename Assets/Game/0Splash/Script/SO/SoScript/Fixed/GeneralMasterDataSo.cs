using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GeneralMasterData
{
    public string id;                // G001
    public string name;              // 이름
    public Grade grade;              // SS, S, A...
    public int power;                // 무력
    public int intel;                // 지력
    public int charm;                // 매력

    [Header("악명")]
    [Tooltip("구글 시트 G열(7번째 열). 0~100 정수.")]
    [Range(0, 100)]
    public int infamy;

    [Header("초기 배치 데이터")]
    public string initialNationId;   // 초기 소속 국가 (WEI, SHU, WU, OTHERS)
    public string initialCastleId;   // 초기 배치 성 ID (C01 ~ C50)

    [Header("UI")]
    [Tooltip("해당 성 태수 초상화(월드맵 등 표시).")]
    public Sprite governorPortrait;
}

[CreateAssetMenu(fileName = "GeneralMasterDataSo", menuName = "ScriptableObject/GeneralMasterDataSo")]
public class GeneralMasterDataSo : ScriptableObject
{
    public List<GeneralMasterData> list = new List<GeneralMasterData>();

}
