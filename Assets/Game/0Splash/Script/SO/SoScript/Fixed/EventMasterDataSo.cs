using System;

using System.Collections.Generic;

using UnityEngine;



public enum EventScope

{

    Region = 0,

    Castle = 1,

}



/// <summary>이벤트 발생 조건 비교 연산자. 시트 문자열 == != &gt; &lt; &gt;= &lt;= 와 매핑.</summary>

public enum EventConditionOp

{

    Eq,

    Ne,

    Gt,

    Lt,

    Ge,

    Le

}
public enum EventCategory
{
    None = 0,
    War = 1,          // 전쟁, 군사, 침공 관련 (무력 영향)
    Economy = 2,      // 경제, 상업, 금화 관련 (지력 영향, MTS 핵심)
    Politics = 3,     // 정치, 내정, 외교 관련 (지력/매력 영향)
    Social = 4,       // 민심, 축제, 인구 관련 (매력 영향)
    Disaster = 5,     // 재해, 역병, 기근 관련 (운/지력 영향)
    Resource = 6,     // 자원, 광산, 식량 생산 관련 (지력 영향)
    Technology = 7,   // 기술 발전, 신무기, 발명 관련 (지력 영향)
    Crime = 8,        // 비리, 사기, 도적, 횡령 관련 (악명 영향)
    Noble = 9,        // 황실, 귀족, 권위 관련 (매력 영향)
    Chaos = 10        // 혼란, 민란, 폭동 관련 (민심/악명 영향)
}


/// <summary>

/// 월드 이벤트 마스터 <b>한 행</b>. 동일 <see cref="id"/>(eventId)로 여러 행이 있을 수 있으며,

/// 동일 id의 여러 행은 <b>행 간 OR</b>(하나라도 조건 만족 시 이벤트 후보), 한 행의 <see cref="conditionIds"/>는 <b>AND</b>입니다.

/// <see cref="conditionIds"/>는 <see cref="ConditionDataSo"/> condId와 동일 키입니다. 버프·기사는 <see cref="buffCodes"/>, <see cref="rumorNewsCodes"/>, <see cref="breakingNewsCodes"/>로 <see cref="BuffMasterDataSo"/>·<see cref="NewsMasterDataSo"/>와 연결됩니다.

/// 확률 보정은 태수 스탯 + <see cref="EventStatModifierData"/> / eventStatModifierMap(eventId)입니다. 파이프라인 요약은 <see cref="WorldEventCenter"/>를 참고하세요.

/// </summary>

[Serializable]

public class EventMasterData

{

    /// <summary>이벤트 ID (eventId). 여러 행이 같은 값을 공유할 수 있습니다.</summary>

    public string id;



    public string name;

    public EventScope scope;

    public int minDays;

    public int maxDays;

    public List<string> buffCodes = new List<string>();

    /// <summary>통합 시트 M열 등. 콤마 구분 태그(EventCategory·디버그 등).</summary>

    public string affinityTagsRaw;



    /// <summary>ConditionMaster(condId) 목록. 한 행 내 전부 만족 시 AND.</summary>

    public List<string> conditionIds = new List<string>();



    /// <summary>시트 G열(콤마) — <see cref="NewsMasterData.newsCode"/>. 소문 단계에서 무작위 1개 추첨.</summary>

    public List<string> rumorNewsCodes = new List<string>();

    /// <summary>시트 H열(콤마) — 속보·<see cref="WorldEventCenter.TriggerDirectBreakingEvent"/>에서 무작위 1개 추첨.</summary>

    public List<string> breakingNewsCodes = new List<string>();

}



[CreateAssetMenu(fileName = "EventMasterDataSo", menuName = "ScriptableObject/EventMasterDataSo")]

public class EventMasterDataSo : ScriptableObject

{

    public List<EventMasterData> list = new List<EventMasterData>();

}


