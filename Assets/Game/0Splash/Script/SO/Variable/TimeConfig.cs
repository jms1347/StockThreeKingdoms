using UnityEngine;

/// <summary>
/// 현실 시간 대비 게임 하루 길이·테스트 모드. 빌드 심볼 <c>STOCKTK_FORCE_LIVE_TIME</c>이면 항상 라이브 초/일을 씁니다.
/// </summary>
[CreateAssetMenu(fileName = "TimeConfig", menuName = "StockTK/Time Config", order = 10)]
public class TimeConfig : ScriptableObject
{
    [Header("모드")]
    [Tooltip("체크 시 testSecondsPerDay 사용. 끄면 liveSecondsPerDay(라이브).")]
    public bool isTestMode;

    [Header("현실 초 / 게임 1일")]
    [Tooltip("테스트: 기본 60 = 현실 1분에 게임 하루.")]
    [Min(1)] public int testSecondsPerDay = 60;

    [Tooltip("라이브: 기본 86400 = 현실 24시간에 게임 하루.")]
    [Min(1)] public int liveSecondsPerDay = 86400;

    /// <summary>빌드·런타임에서 강제 라이브 모드 여부.</summary>
    public bool EffectiveIsTestMode
    {
        get
        {
#if STOCKTK_FORCE_LIVE_TIME
            return false;
#else
            return isTestMode;
#endif
        }
    }

    /// <summary>가상 시계에 쓸 ‘현실 몇 초 = 게임 내 86400초(1일)’ 비율의 분모.</summary>
    public float ResolveSecondsPerDay()
    {
        int raw = EffectiveIsTestMode ? testSecondsPerDay : liveSecondsPerDay;
        return Mathf.Max(1, raw);
    }
}
