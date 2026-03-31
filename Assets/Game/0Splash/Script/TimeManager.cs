using System;
using UnityEngine;

/// <summary>
/// 게임 내 UTC Unix 초. <see cref="GameManager.RealSecondsPerGameDay"/> = 현실 몇 초에 게임 하루가 지나는지.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TimeManager : Singleton<TimeManager>
{
    long _anchorRealUnix;
    long _anchorGameUnix;
    float _appliedRealSecondsPerGameDay = -1f;

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
        float secondsPerGameDay = GameManager.InstanceOrNull != null
            ? GameManager.InstanceOrNull.RealSecondsPerGameDay
            : 86400f;

        if (_appliedRealSecondsPerGameDay < 0f)
        {
            _anchorRealUnix = realNow;
            _anchorGameUnix = realNow;
            _appliedRealSecondsPerGameDay = secondsPerGameDay;
        }
        else if (Mathf.Abs(secondsPerGameDay - _appliedRealSecondsPerGameDay) > 0.001f)
        {
            double spdOld = Mathf.Max(0.001f, _appliedRealSecondsPerGameDay);
            long prevGame = _anchorGameUnix + (long)((realNow - _anchorRealUnix) * (86400.0 / spdOld));
            _anchorGameUnix = prevGame;
            _anchorRealUnix = realNow;
            _appliedRealSecondsPerGameDay = secondsPerGameDay;
        }

        float spd = Mathf.Max(0.001f, secondsPerGameDay);
        return (long)(_anchorGameUnix + (realNow - _anchorRealUnix) * (86400.0 / spd));
    }
}
