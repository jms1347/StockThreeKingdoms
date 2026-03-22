#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using PedometerU;
#endif
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using System;
using UnityEngine;

/// <summary>
/// Kinkelin Pedometer 플러그인과 OS 만보기 센서 연동. 오늘 걸음 = (OS 누적 걸음) − baselineSteps.
/// DontDestroyOnLoad 단일 인스턴스를 권장합니다.
/// </summary>
public class PedometerManager : MonoBehaviour
{
    public static PedometerManager InstanceOrNull { get; private set; }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    Pedometer _pedometer;
#endif

    /// <summary>네이티브 콜백이 워커 스레드일 수 있어 메인 스레드에서 처리할 최신 raw 걸음.</summary>
    volatile int _pendingRawSteps = -1;

    void Awake()
    {
        if (InstanceOrNull != null && InstanceOrNull != this)
        {
            Destroy(gameObject);
            return;
        }
        InstanceOrNull = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
#if UNITY_EDITOR
        return;
#elif UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
            Permission.RequestUserPermission("android.permission.ACTIVITY_RECOGNITION");
#elif UNITY_IOS
        TryStartPedometer();
#endif
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_EDITOR
        return;
#elif UNITY_ANDROID
        if (hasFocus)
            TryStartPedometer();
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
            EditorAddFakeSteps(100);
#else
        FlushPendingStepsIfAny();
#if UNITY_ANDROID
        if (_pedometer == null &&
            Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
            TryStartPedometer();
#endif
#endif
        CheckCalendarDayRollover();
    }

#if UNITY_EDITOR
    static void EditorAddFakeSteps(int delta)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        gm.currentUser.stepsToday = Mathf.Max(0, gm.currentUser.stepsToday + delta);
        gm.currentUser.dailyStepCount = gm.currentUser.stepsToday;
        gm.SaveUserData();
        gm.OnStepsChanged?.Invoke(gm.currentUser.stepsToday);
    }
#endif

    void FlushPendingStepsIfAny()
    {
        int s = _pendingRawSteps;
        if (s < 0) return;
        _pendingRawSteps = -1;
        ApplyRawStepCount(s);
    }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    void TryStartPedometer()
    {
        if (_pedometer != null) return;
        try
        {
            _pedometer = new Pedometer(OnStepNative);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PedometerManager] Pedometer 시작 실패: {e.Message}");
        }
    }

    void OnStepNative(int steps, double distance)
    {
        _pendingRawSteps = steps;
    }
#endif

    void OnDisable()
    {
        DisposePedometer();
    }

    void OnDestroy()
    {
        DisposePedometer();
        if (InstanceOrNull == this)
            InstanceOrNull = null;
    }

    void DisposePedometer()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (_pedometer == null) return;
        try
        {
            _pedometer.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PedometerManager] Dispose: {e.Message}");
        }
        _pedometer = null;
#endif
    }

    static string GetLocalCalendarKey() => DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>OS에서 온 누적 걸음 수로 오늘 걸음·저장·이벤트 반영.</summary>
    void ApplyRawStepCount(int rawSteps)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        var u = gm.currentUser;
        string today = GetLocalCalendarKey();

        if (string.IsNullOrEmpty(u.stepCalendarDate))
            u.stepCalendarDate = today;

        if (u.stepCalendarDate != today)
        {
            ResetForNewCalendarDay(gm, u, rawSteps, today);
            return;
        }

        if (!u.pedometerBaselineInitialized)
        {
            u.baselineSteps = rawSteps;
            u.pedometerBaselineInitialized = true;
            u.stepsToday = 0;
            u.dailyStepCount = 0;
            gm.SaveUserData();
            gm.OnStepsChanged?.Invoke(0);
            return;
        }

        if (rawSteps < u.baselineSteps)
        {
            u.baselineSteps = rawSteps;
            u.stepsToday = 0;
            u.dailyStepCount = 0;
            gm.SaveUserData();
            gm.OnStepsChanged?.Invoke(0);
            return;
        }

        int todaySteps = rawSteps - u.baselineSteps;
        if (todaySteps == u.stepsToday)
            return;

        u.stepsToday = todaySteps;
        u.dailyStepCount = todaySteps;
        gm.SaveUserData();
        gm.OnStepsChanged?.Invoke(u.stepsToday);
    }

    void ResetForNewCalendarDay(GameManager gm, UserData u, int currentRawSteps, string today)
    {
        u.stepCalendarDate = today;
        u.baselineSteps = currentRawSteps;
        u.pedometerBaselineInitialized = true;
        u.stepsToday = 0;
        u.dailyStepCount = 0;
        if (u.stepRewardsClaimed != null)
        {
            for (int i = 0; i < u.stepRewardsClaimed.Length; i++)
                u.stepRewardsClaimed[i] = false;
        }
        gm.SaveUserData();
        gm.OnStepsChanged?.Invoke(0);
    }

    /// <summary>앱이 켜진 채 자정이 지났거나, 재실행 시 날짜만 먼저 바뀐 경우. 다음 OnStep 전까지 baseline은 미설정.</summary>
    void CheckCalendarDayRollover()
    {
#if UNITY_EDITOR
        return;
#endif
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        var u = gm.currentUser;
        string today = GetLocalCalendarKey();

        if (string.IsNullOrEmpty(u.stepCalendarDate))
            return;

        if (u.stepCalendarDate == today)
            return;

        u.stepCalendarDate = today;
        u.pedometerBaselineInitialized = false;
        u.baselineSteps = 0;
        u.stepsToday = 0;
        u.dailyStepCount = 0;
        if (u.stepRewardsClaimed != null)
        {
            for (int i = 0; i < u.stepRewardsClaimed.Length; i++)
                u.stepRewardsClaimed[i] = false;
        }
        gm.SaveUserData();
        gm.OnStepsChanged?.Invoke(0);
    }
}
