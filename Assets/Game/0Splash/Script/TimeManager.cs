using System;
using UnityEngine;

/// <summary>
/// 게임 내 UTC Unix 초(가상) 및 게임 일 버킷.
/// <list type="bullet">
/// <item><description><see cref="GetUnixNow"/> — 현실 경과에 비례해 하루당 86400초씩 진행하는 가상 Unix.</description></item>
/// <item><description><see cref="GetGameDayBucket"/> — 이벤트·일 틱·차트 날짜와 동일한 ‘게임 UTC 일’ 인덱스(<c>GetUnixNow()/86400</c>).</description></item>
/// <item><description><see cref="SessionElapsedWholeGameDays"/> — 앵커 이후 (현재Unix−시작Unix)/초당일길이 의 전체 일 수.</description></item>
/// </list>
/// <see cref="TimeConfig"/> 또는 <see cref="GameManager.RealSecondsPerGameDay"/> 로 SECONDS_PER_DAY(현실 초/게임일) 결정.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TimeManager : Singleton<TimeManager>
{
    public const long GameSecondsPerDay = 86400L;

    [Tooltip("씬/프리팹에 붙인 경우 사용. 비어 있으면 RegisterTimeConfig 또는 GameManager 순.")]
    [SerializeField] TimeConfig timeConfig;

    static TimeConfig s_registeredConfig;

    long _anchorRealUnix;
    long _anchorGameUnix;
    float _appliedRealSecondsPerGameDay = -1f;

    /// <summary>코드에서 주입(예: GameManager Awake). 씬에 할당한 <see cref="timeConfig"/>보다 우선합니다.</summary>
    public static void RegisterTimeConfig(TimeConfig cfg) => s_registeredConfig = cfg;

    public static void EnsureCreated()
    {
        if (InstanceOrNull != null) return;
        var go = new GameObject(nameof(TimeManager));
        go.AddComponent<TimeManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        ResetVirtualTimeAnchor();
    }

    /// <summary>가상 시각을 실제 UTC와 다시 맞춥니다(플레이 중 테스트용).</summary>
    public void ResetVirtualTimeAnchor()
    {
        long r = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _anchorRealUnix = r;
        _anchorGameUnix = r;
        _appliedRealSecondsPerGameDay = -1f;
    }

    TimeConfig ActiveConfig => s_registeredConfig != null ? s_registeredConfig : timeConfig;

    /// <summary>현실 몇 초에 게임 내 하루(86400초)가 지나는지. 최소 1초.</summary>
    public float GetRealSecondsPerGameDay()
    {
        if (ActiveConfig != null)
            return ActiveConfig.ResolveSecondsPerDay();
        if (GameManager.InstanceOrNull != null)
            return GameManager.InstanceOrNull.RealSecondsPerGameDay;
        return 86400f;
    }

    public static long GetUnixNow()
    {
        EnsureCreated();
        return InstanceOrNull != null
            ? InstanceOrNull.GetCurrentUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>가상 Unix 기준 UTC 일 버킷. 일일 이벤트·쿨다운·차트 축과 동일 기준.</summary>
    public static long GetGameDayBucket() => GetUnixNow() / GameSecondsPerDay;

    /// <summary>
    /// (현재 실제 UTC Unix − 세션 앵커) / SECONDS_PER_DAY 의 몫. 에디터/테스트에서 0,1,2… 일차 표기용.
    /// </summary>
    public long SessionElapsedWholeGameDays
    {
        get
        {
            long realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float spd = Mathf.Max(1f, GetRealSecondsPerGameDay());
            return (long)((realNow - _anchorRealUnix) / spd);
        }
    }

    public long GetCurrentUnixTimeSeconds()
    {
        long realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        float secondsPerGameDay = GetRealSecondsPerGameDay();

        if (_appliedRealSecondsPerGameDay < 0f)
        {
            _anchorRealUnix = realNow;
            _anchorGameUnix = realNow;
            _appliedRealSecondsPerGameDay = secondsPerGameDay;
        }
        else if (Mathf.Abs(secondsPerGameDay - _appliedRealSecondsPerGameDay) > 0.001f)
        {
            double spdOld = Mathf.Max(0.001f, _appliedRealSecondsPerGameDay);
            long prevGame = _anchorGameUnix + (long)((realNow - _anchorRealUnix) * (GameSecondsPerDay / spdOld));
            _anchorGameUnix = prevGame;
            _anchorRealUnix = realNow;
            _appliedRealSecondsPerGameDay = secondsPerGameDay;
        }

        float spd = Mathf.Max(0.001f, secondsPerGameDay);
        return (long)(_anchorGameUnix + (realNow - _anchorRealUnix) * (GameSecondsPerDay / spd));
    }
}
