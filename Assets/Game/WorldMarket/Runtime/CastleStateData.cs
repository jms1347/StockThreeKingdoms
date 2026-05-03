using System;
using System.Collections.Generic;

public enum Faction
{
    NONE = 0,
    WEI = 1,
    SHU = 2,
    WU = 3,
    OTHERS = 4
}

/// <summary>CastleStateData 전수 기준 세력별 성 점유 비율 (합계 1.0 근사).</summary>
[Serializable]
public struct FactionCastleShare
{
    public float wei;
    public float shu;
    public float wu;
    public float others;
}

/// <summary>
/// 태수·주둔 장수 기준 성의 합산 4능 (태수 100%, 소속 장수 10%, 방랑 장수 제외).
/// AI 성향·전략 틱에서 사용합니다.
/// </summary>
[Serializable]
public struct CastleActiveStats
{
    public float Power;
    public float Intel;
    public float Charm;
    public float Infamy;
}

[Serializable]
public partial class CastleStateData
{
    public string id; // CastleMasterData.id

    // 실시간 수치
    public float currentSentiment = 100f; // 0~200, 100=기본
    public int currentPopulation;

    // 점령/인사 상태
    public Faction currentLord;
    public string currentGovernorId;
    /// <summary>성 소속 장수 목록(태수 포함 가능). 비어 있으면 장수 마스터 initialCastleId 기준으로 추론.</summary>
    public List<string> residentGeneralIds = new List<string>();
    /// <summary>태수 일일 버프 마지막 적용 시각(Unix 초). <see cref="TimeManager.GetUnixNow"/> 기준.</summary>
    public long lastDailyBuffTime;
    /// <summary>일일 버프 쿨다운이 묶인 태수 id. <see cref="currentGovernorId"/>와 다르면 쿨다운을 새 태수 기준으로 리셋.</summary>
    public string lastDailyBuffGovernorId;

    // 상태/히스토리
    public bool isWar;
    /// <summary>AI 전략 틱에서 설정한 교전 상대 성 ID. 비어 있으면 단독 플래그만 유지.</summary>
    public string aiWarOpponentCastleId;
    /// <summary>교전 시작 Unix 시각. 자동 종료 판단용.</summary>
    public long aiWarStartUnix;
    /// <summary>AI 폭정 뉴스 스팸 방지용.</summary>
    public long lastAiTyrannyNewsUnix;
    /// <summary>AI 행사 뉴스 스팸 방지용.</summary>
    public long lastAiFestivalNewsUnix;
    /// <summary>AI 배당 삭감(탐욕) 뉴스 스팸 방지용.</summary>
    public long lastAiDividendCutNewsUnix;
    /// <summary>재해·특수 이벤트 등 (리스트 정렬 상단용 플래그).</summary>
    public bool isDisaster;
    /// <summary>호재(풍년 등) — 이벤트 탭 필터용. 데이터·연출 붙이면 갱신.</summary>
    public bool isFavorableEvent;
    public List<float> sentimentHistory = new List<float>(); // 최근 7~10개
    /// <summary>미니 스파크라인용 인구 이력 (최근 7~10개).</summary>
    public List<int> populationHistory = new List<int>();

    /// <summary>7일 일간 스냅샷(인구). 차트 X는 과거→현재.</summary>
    public List<float> historyPopulation7Day = new List<float>();
    /// <summary>7일 일간 스냅샷(민심 0~200, 100 기준).</summary>
    public List<float> historySentiment7Day = new List<float>();
    /// <summary>전일 종가에 가까운 성채 호가 앵커 — <see cref="DataManager.CalculateChangeRate24h"/>용.</summary>
    public float buyPricePrevDayClose;

    /// <summary>성채 호가(병 1단위 금화). 매도·수익률 기준도 동일 호가(관부 없음).</summary>
    public float currentBuyPrice;
    /// <summary>해 성 입성 관부율(%). 마스터 초기값에서 시드 후 성별로 유지.</summary>
    public float castleTaxRatePercent;

    // 천하 탭: 성별 유저 주둔(지분). AMM 매매로 AI 수비군과 금화 풀에서 이동.
    public int userDeployedTroops;
    /// <summary>가중 평균 매수 단가(금화, UI·수익률용). AMM 총액/인원으로 갱신.</summary>
    public float averagePurchasePrice;

    /// <summary>주둔·AMM 정원 상한(내정·마스터). <c>userDeployedTroops + currentAiGarrison</c> ≤ 이 값.</summary>
    public int maxGarrison;
    /// <summary>성에 남은 AI 수비군. constant product 풀.</summary>
    public int currentAiGarrison;
    /// <summary>성 금화 창고. 거래 후 <c>round(constantK / currentAiGarrison)</c>로 정렬.</summary>
    public long goldReserve;
    /// <summary>AMM 상수 K (= R·G). 초기화 후 불변(double).</summary>
    public double constantK;

    /// <summary>거래세 등 성에 쌓인 주간 배당 재원. 월요일 정산 시 유저 지분만큼 지급 후 0으로 초기화.</summary>
    public long accumulatedDividendPool;
    /// <summary>최근 주간 정산 기준 예상 배당률(%, 1주 단위).</summary>
    public float expectedWeeklyYieldPercent;
    /// <summary>과부하 시 최종 배당 효율(0~100). 비과부하는 100.</summary>
    public float dividendEfficiencyPercent = 100f;
    /// <summary>주간 상대평가로 재산정되는 런타임 등급(미설정 시 마스터 등급 사용).</summary>
    public Grade runtimeGrade = Grade.D;
    /// <summary>등급 락 남은 주기 수(0이면 다음 정산에서 변경 가능).</summary>
    public int gradeLockRemainingSettlements;
    /// <summary>직전 등급 변동 방향(-1:하락, 0:없음, +1:상승).</summary>
    public int lastGradeChange;
    /// <summary>마지막 등급 변경 시각(Unix 초).</summary>
    public long lastGradeChangeUnix;

    public bool IsUserInvested => userDeployedTroops > 0;

    /// <summary>성별 월드 이벤트(eventId) 마지막 발생 UTC 일 버킷(30일 쿨다운).</summary>
    public List<WorldEventCooldownEntry> worldEventCooldowns = new List<WorldEventCooldownEntry>();

    /// <summary>월드 이벤트 등으로 부여된 버프 코드 목록(UI·후속 로직용).</summary>
    public List<ActiveBuffEntry> activeBuffs = new List<ActiveBuffEntry>();
}

/// <summary>동일 성에서 같은 eventId 재발 방지용.</summary>
[Serializable]
public class WorldEventCooldownEntry
{
    public string eventId;
    public int lastOccurredUtcDay;
}

/// <summary>성에 적용 중인 월드 이벤트 버프(코드 문자열).</summary>
[Serializable]
public class ActiveBuffEntry
{
    public string buffCode;
    public string sourceEventId;
    public int appliedUtcDay;
    /// <summary>버프 마스터의 <see cref="BuffMasterData.durationDays"/> 스냅샷. 멀티데이 분할용.</summary>
    public int totalDurationDays;
    /// <summary>이미 일차 샘플을 몇 번 적용했는지(확정 직후 1일차 포함).</summary>
    public int completedDayCount;
}

/// <summary>소문 → N일 뒤 속보 확정 파이프라인. <see cref="CastleStateSavePayload"/>에 저장.</summary>
[Serializable]
public class PendingRumorWorldEvent
{
    public string eventId;
    public string targetCastleId;
    public int confirmOnUtcDay;
    /// <summary>소문으로 생성된 <see cref="WorldNewsItem.unixTime"/> — 확정 시 같은 기사를 속보로 갱신.</summary>
    public long linkedNewsUnixTime;
    /// <summary>Region 스코프 시 영향 받는 성 ID 목록(콤마). 비어 있으면 <see cref="targetCastleId"/>만.</summary>
    public string affectedCastleIdsRaw;
    /// <summary>확정 시 한 번에 적용할 민심 변화(소문 단계의 미세 조정은 이미 반영됨).</summary>
    public float largeSentimentDelta;
    /// <summary>확정 시 인구 변화.</summary>
    public int largePopulationDelta;

    /// <summary>소문 당첨 행의 속보용 뉴스 코드 후보(확정 시 무작위 1개 추첨).</summary>
    public List<string> pendingBreakingNewsCodes = new List<string>();
    /// <summary>당첨 행의 버프 코드(확정 시 적용·<see cref="CastleStateData.activeBuffs"/> 등록).</summary>
    public List<string> buffCodesToApply = new List<string>();
    /// <summary>세이브 호환 필드. 현재는 항상 태수+EventStatModifier 기반 확정 주사위를 쓰며 이 값은 무시됩니다.</summary>
    public bool confirmUsesEventProbabilityRoll;
}

[Serializable]
public class WorldNewsItem
{
    public long unixTime;
    /// <summary>리스트 요약·본문 폴백용 한 줄 또는 전체 문자열.</summary>
    public string text;

    /// <summary><see cref="WorldNewsFeedKind"/>와 동일한 분류 바이트. 0이면 구 데이터(문자열·태그로 추론).</summary>
    public byte newsKind;
    public string eventId;
    public string targetCastleId;
    /// <summary>표시용 제목(태그 제외 권장).</summary>
    public string headline;
    /// <summary>본문(상세).</summary>
    public string bodyContent;
    /// <summary>소문이 현실화(속보)로 확정되었는지.</summary>
    public bool isConfirmed;

    /// <summary>상세 팝업 헤드라인 (비어 있으면 <see cref="text"/> 첫 줄 사용).</summary>
    public string detailTitle;
    /// <summary>예: "2분 전 · 관련 성: 업성(C04)" (비어 있으면 런타임에서 시간·성 ID 추정).</summary>
    public string detailSubline;
    /// <summary>상세 본문 (비어 있으면 <see cref="text"/> 전체 또는 나머지 줄).</summary>
    public string detailBody;
    /// <summary>쉼표 구분 성 ID: C04,C06</summary>
    public string relatedCastleIdsRaw;
    public string impactRangeText;
    public string debuffIconsHint;
    public string statLine1;
    public string statLine2;
    public string durationText;

    /// <summary>검증된 사실(속보 탭). 태그 없는 구 데이터는 <see cref="NewsManager"/>·태그로 추론합니다.</summary>
    public bool isVerifiedFact;
    /// <summary>소문이 확정 시 허위로 판명된 경우(버프·대형 델타 미적용).</summary>
    public bool isDebunked;
    /// <summary>소문 성격의 기사(소문 탭 분류용).</summary>
    public bool isRumorContent;

    /// <summary>추첨된 <see cref="NewsMasterData.newsCode"/> — UI 추적용.</summary>
    public string newsMasterCode;
    /// <summary>레거시 필드(아이콘 마스터 제거 후 미사용). 세이브 호환용.</summary>
    public string newsIconResourcePath;

    public string GetEffectiveDetailTitle()
    {
        if (!string.IsNullOrWhiteSpace(headline)) return headline.Trim();
        if (!string.IsNullOrWhiteSpace(detailTitle)) return detailTitle.Trim();
        if (string.IsNullOrWhiteSpace(text)) return "소식";
        int nl = text.IndexOf('\n');
        return nl >= 0 ? text.Substring(0, nl).Trim() : text.Trim();
    }

    public string GetEffectiveSummaryForList()
    {
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            string b = bodyContent.Trim();
            int nl = b.IndexOf('\n');
            string line = nl >= 0 ? b.Substring(0, nl).Trim() : b;
            return line.Length > 120 ? line.Substring(0, 120) + "…" : line;
        }
        if (string.IsNullOrWhiteSpace(text)) return "";
        int nl2 = text.IndexOf('\n');
        if (nl2 < 0) return text.Trim();
        string rest = text.Substring(nl2 + 1).Trim();
        return string.IsNullOrEmpty(rest) ? text.Substring(0, nl2).Trim() : rest;
    }

    public string GetEffectiveDetailBody()
    {
        if (!string.IsNullOrWhiteSpace(bodyContent)) return bodyContent.Trim();
        if (!string.IsNullOrWhiteSpace(detailBody)) return detailBody.Trim();
        if (string.IsNullOrWhiteSpace(text)) return "";
        int nl = text.IndexOf('\n');
        return nl >= 0 && nl < text.Length - 1 ? text.Substring(nl + 1).Trim() : text.Trim();
    }
}

[Serializable]
public class CastleStateSavePayload
{
    public List<CastleStateData> castles = new List<CastleStateData>();
    public List<WorldNewsItem> news = new List<WorldNewsItem>();
    public List<PendingRumorWorldEvent> pendingRumorWorldEvents = new List<PendingRumorWorldEvent>();
    /// <summary>마지막으로 주간 배당을 지급한 로컬 월요일 06:00 앵커(Unix 초).</summary>
    public long lastWeeklyDividendPaidAnchorUnix;
}

