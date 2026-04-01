/// <summary>
/// 조건형 이벤트(임계·상태)와 정기/샘플링 뉴스 트리거를 구분하기 위한 계약.
/// 실제 롤·가중치는 <see cref="EventMasterData"/>·시트와 병행해 확장합니다.
/// </summary>
public interface IEventTrigger
{
    /// <summary>테이블·로그용 고유 ID.</summary>
    string TriggerId { get; }

    /// <summary>
    /// true: 성 상태·조건을 검사해 발동(조건형).
    /// false: 일일 틱·샘플링 등 정해진 타이밍에만 후보로 올림(정기 뉴스).
    /// </summary>
    bool IsConditional { get; }

    /// <summary>현재 게임 일(UTC 일 버킷 등)과 성 상태로 발동 여부.</summary>
    bool ShouldEvaluateToday(DataManager dataManager, CastleStateData castle, int utcDayBucket);
}
