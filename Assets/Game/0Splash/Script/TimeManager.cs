using System;
using UnityEngine;

/// <summary>
/// 게임 내 UTC Unix 초. <see cref="GameManager.RealMinutesPerGameDay"/>로 실시간 대비 게임 일 속도 조절.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TimeManager : Singleton<TimeManager>
{
    long _anchorRealUnix;
    long _anchorGameUnix;
    float _appliedMinutesPerGameDay = -1f;

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
        _appliedMinutesPerGameDay = -1f;
    }

    public static long GetUnixNow()
    {
        EnsureCreated();
        return InstanceOrNull != null
            ? InstanceOrNull.GetCurrentUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public long GetCurrentUnixTimeSeconds()
    {
        long realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        float minutesPerDay = GameManager.InstanceOrNull != null
            ? GameManager.InstanceOrNull.RealMinutesPerGameDay
            : 1440;

        if (_appliedMinutesPerGameDay < 0f)
        {
            _anchorRealUnix = realNow;
            _anchorGameUnix = realNow;
            _appliedMinutesPerGameDay = minutesPerDay;
        }
        else if (Mathf.Abs(minutesPerDay - _appliedMinutesPerGameDay) > 0.001f)
        {
            double spdOld = Mathf.Max(1f, _appliedMinutesPerGameDay * 60f);
            long prevGame = _anchorGameUnix + (long)((realNow - _anchorRealUnix) * (86400.0 / spdOld));
            _anchorGameUnix = prevGame;
            _anchorRealUnix = realNow;
            _appliedMinutesPerGameDay = minutesPerDay;
        }

        float spd = Mathf.Max(1f, minutesPerDay * 60f);
        return (long)(_anchorGameUnix + (realNow - _anchorRealUnix) * (86400.0 / spd));
    }
}
