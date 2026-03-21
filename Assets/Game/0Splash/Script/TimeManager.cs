using System;
using UnityEngine;

/// <summary>
/// 게임 내 "현재 시각(UTC Unix 초)"의 단일 진입점. 서버 연동 시 본 클래스만 수정하면
/// 클라 전체가 보정된 서버 시간을 사용하도록 전환할 수 있습니다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TimeManager : Singleton<TimeManager>
{
    /// <summary>TimeManager가 없을 때 GameManager 등에서 선행 생성.</summary>
    public static void EnsureCreated()
    {
        if (InstanceOrNull != null) return;
        var go = new GameObject(nameof(TimeManager));
        go.AddComponent<TimeManager>();
    }

    /// <summary>
    /// 전역에서 사용할 UTC Unix 초. GameManager Awake 전·씬 단독 테스트에서도 EnsureCreated 후 반환.
    /// </summary>
    public static long GetUnixNow()
    {
        EnsureCreated();
        return InstanceOrNull != null
            ? InstanceOrNull.GetCurrentUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>UTC 기준 Unix 시간(초). 로컬 DateTime.Now 사용 금지 정책.</summary> 
    /// 나중에 여기를 서버 시간으로 변경하면됨. ******************************************************
    public long GetCurrentUnixTimeSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
