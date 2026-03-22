#if UNITY_ANDROID
using UnityEngine.Android;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine.Scripting;
#endif
using System;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// 만보기: Android <see cref="Sensor.TYPE_STEP_COUNTER"/> (누적), iOS CoreMotion CMPedometer (당일 0시~현재).
/// Kinkelin 등 외부 패키지 없음. DontDestroyOnLoad 단일 인스턴스 권장.
/// </summary>
public class PedometerManager : MonoBehaviour
{
    public static PedometerManager InstanceOrNull { get; private set; }

    volatile int _pendingRawSteps = -1;

#if UNITY_ANDROID && !UNITY_EDITOR
    const string ActivityRecognitionPermission = "android.permission.ACTIVITY_RECOGNITION";
    AndroidJavaObject _androidSensorManager;
    StepCounterListener _androidListener;
    bool _androidRegistered;
#endif

#if UNITY_IOS && !UNITY_EDITOR
    const float IosPollIntervalSeconds = 0.5f;
    float _iosPollAccum;

    delegate void IosStepsNativeDelegate(int steps);

    [DllImport("__Internal")]
    static extern void NativePedometer_SetCallback(IosStepsNativeDelegate callback);

    [DllImport("__Internal")]
    static extern void NativePedometer_QueryTodaySteps();

    [DllImport("__Internal")]
    static extern void NativePedometer_Release();

    [MonoPInvokeCallback(typeof(IosStepsNativeDelegate))]
    static void IosStepsFromNative(int steps)
    {
        if (InstanceOrNull != null)
            InstanceOrNull._pendingRawSteps = steps;
    }
#endif

    void Awake()
    {
        if (InstanceOrNull != null && InstanceOrNull != this)
        {
            Destroy(gameObject);
            return;
        }
        InstanceOrNull = this;
        // 부모 GameManager(Singleton)가 이미 transform.root 에 DontDestroyOnLoad 적용.
        // 자식에 또 DDOL 하면 Unity가 자식을 DDOL 씬 루트로 분리해 계층이 깨질 수 있음.
    }

    void Start()
    {
#if UNITY_EDITOR
        return;
#elif UNITY_ANDROID
        StartCoroutine(AndroidInitWhenReady());
#elif UNITY_IOS
        try
        {
            NativePedometer_SetCallback(IosStepsFromNative);
            NativePedometer_QueryTodaySteps();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PedometerManager] iOS 네이티브 초기화 실패: {e.Message}");
        }
#endif
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_EDITOR
        return;
#elif UNITY_ANDROID
        if (hasFocus)
            TryStartAndroidSensor();
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
        TryStartAndroidSensor();
#elif UNITY_IOS
        _iosPollAccum += Time.unscaledDeltaTime;
        if (_iosPollAccum >= IosPollIntervalSeconds)
        {
            _iosPollAccum = 0f;
            try
            {
                NativePedometer_QueryTodaySteps();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PedometerManager] iOS Query: {e.Message}");
            }
        }
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

        // 스플래시 직후 등 GameManager/유저 로드 전에 센서가 먼저 오면 값이 날아가지 않게 대기
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null)
            return;

        _pendingRawSteps = -1;

#if UNITY_IOS && !UNITY_EDITOR
        ApplyTodayStepsFromIos(s);
#else
        ApplyRawStepCount(s);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator AndroidInitWhenReady()
    {
        // Activity 완전 resume 이후 권한 다이얼로그가 더 안정적으로 뜸
        yield return null;
        yield return null;

        if (!Permission.HasUserAuthorizedPermission(ActivityRecognitionPermission))
        {
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += name =>
            {
                if (name == ActivityRecognitionPermission)
                    TryStartAndroidSensor();
            };
            cb.PermissionDenied += name =>
            {
                if (name == ActivityRecognitionPermission)
                    Debug.LogWarning("[PedometerManager] ACTIVITY_RECOGNITION 거부됨 — 설정에서 신체 활동 권한을 켜 주세요.");
            };
            cb.PermissionDeniedAndDontAskAgain += name =>
            {
                if (name == ActivityRecognitionPermission)
                    Debug.LogWarning("[PedometerManager] ACTIVITY_RECOGNITION '다시 묻지 않음' — 앱 설정에서 권한을 허용해 주세요.");
            };
            Permission.RequestUserPermission(ActivityRecognitionPermission, cb);
        }
        else
            TryStartAndroidSensor();

        // 권한 직후 한동안 재시도 (일부 기기에서 지연)
        for (int i = 0; i < 8; i++)
        {
            yield return new WaitForSeconds(0.5f);
            if (!_androidRegistered && Permission.HasUserAuthorizedPermission(ActivityRecognitionPermission))
                TryStartAndroidSensor();
        }
    }

    static float[] ReadSensorEventValues(AndroidJavaObject sensorEvent)
    {
        if (sensorEvent == null) return null;
        try
        {
            return sensorEvent.Get<float[]>("values");
        }
        catch (Exception) { /* fall through */ }

        // 일부 IL2CPP/기기에서 Get<float[]> 실패 시 JNI로 필드 values ([F) 직접 읽기
        try
        {
            IntPtr raw = sensorEvent.GetRawObject();
            IntPtr clazz = AndroidJNI.FindClass("android/hardware/SensorEvent");
            try
            {
                IntPtr fid = AndroidJNI.GetFieldID(clazz, "values", "[F");
                if (fid == IntPtr.Zero) return null;
                IntPtr arr = AndroidJNI.GetObjectField(raw, fid);
                if (arr == IntPtr.Zero) return null;
                try
                {
                    return AndroidJNI.FromFloatArray(arr);
                }
                finally
                {
                    AndroidJNI.DeleteLocalRef(arr);
                }
            }
            finally
            {
                if (clazz != IntPtr.Zero)
                    AndroidJNI.DeleteLocalRef(clazz);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Sensor.TYPE_STEP_COUNTER 콜백 (메인 스레드).</summary>
    [Preserve]
    class StepCounterListener : AndroidJavaProxy
    {
        readonly PedometerManager _owner;

        [Preserve]
        public StepCounterListener(PedometerManager owner) : base("android.hardware.SensorEventListener")
        {
            _owner = owner;
        }

        [Preserve]
        public void onSensorChanged(AndroidJavaObject sensorEvent)
        {
            if (sensorEvent == null || _owner == null) return;
            float[] vals = ReadSensorEventValues(sensorEvent);
            if (vals == null || vals.Length == 0) return;
            int steps = (int)vals[0];
            _owner._pendingRawSteps = steps;
        }

        [Preserve]
        public void onAccuracyChanged(AndroidJavaObject sensor, int accuracy) { }
    }

    void TryStartAndroidSensor()
    {
        if (_androidRegistered) return;
        if (!Permission.HasUserAuthorizedPermission(ActivityRecognitionPermission))
            return;

        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;

                _androidSensorManager = activity.Call<AndroidJavaObject>("getSystemService", "sensor");
                if (_androidSensorManager == null) return;

                using (var sensorClass = new AndroidJavaClass("android.hardware.Sensor"))
                {
                    int typeStepCounter = sensorClass.GetStatic<int>("TYPE_STEP_COUNTER");
                    AndroidJavaObject sensor = _androidSensorManager.Call<AndroidJavaObject>("getDefaultSensor", typeStepCounter);
                    if (sensor == null)
                    {
                        Debug.LogWarning("[PedometerManager] TYPE_STEP_COUNTER 기본 센서 없음");
                        return;
                    }

                    using (var smClass = new AndroidJavaClass("android.hardware.SensorManager"))
                    {
                        int rate = smClass.GetStatic<int>("SENSOR_DELAY_UI");
                        _androidListener = new StepCounterListener(this);
                        bool ok = _androidSensorManager.Call<bool>("registerListener", _androidListener, sensor, rate);
                        if (!ok)
                        {
                            Debug.LogError("[PedometerManager] registerListener(TYPE_STEP_COUNTER) 가 false 를 반환했습니다.");
                            return;
                        }
                        _androidRegistered = true;
                        Debug.Log("[PedometerManager] TYPE_STEP_COUNTER 리스너 등록 완료");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PedometerManager] Android 센서 등록 실패: {e.Message}");
        }
    }

    void StopAndroidSensor()
    {
        if (!_androidRegistered || _androidSensorManager == null || _androidListener == null)
        {
            _androidListener = null;
            _androidSensorManager = null;
            _androidRegistered = false;
            return;
        }
        try
        {
            _androidSensorManager.Call("unregisterListener", _androidListener);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PedometerManager] unregisterListener: {e.Message}");
        }
        _androidListener = null;
        _androidSensorManager = null;
        _androidRegistered = false;
    }
#endif

    void OnDisable()
    {
        DisposeNativePedometer();
    }

    void OnDestroy()
    {
        DisposeNativePedometer();
        if (InstanceOrNull == this)
            InstanceOrNull = null;
    }

    void DisposeNativePedometer()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StopAndroidSensor();
#elif UNITY_IOS && !UNITY_EDITOR
        try
        {
            NativePedometer_Release();
        }
        catch (Exception) { /* 에디터 스텁 없음 */ }
#endif
    }

    static string GetLocalCalendarKey() => DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>Android STEP_COUNTER: 부팅 이후 누적 → baseline 차감으로 오늘 걸음.</summary>
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

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>iOS CMPedometer: 이미 '오늘 0시~현재' 걸음 수.</summary>
    void ApplyTodayStepsFromIos(int osTodaySteps)
    {
        osTodaySteps = Mathf.Max(0, osTodaySteps);
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        var u = gm.currentUser;
        string today = GetLocalCalendarKey();

        if (string.IsNullOrEmpty(u.stepCalendarDate))
            u.stepCalendarDate = today;

        if (u.stepCalendarDate != today)
        {
            u.stepCalendarDate = today;
            if (u.stepRewardsClaimed != null)
            {
                for (int i = 0; i < u.stepRewardsClaimed.Length; i++)
                    u.stepRewardsClaimed[i] = false;
            }
            u.baselineSteps = 0;
            u.pedometerBaselineInitialized = true;
            u.stepsToday = osTodaySteps;
            u.dailyStepCount = osTodaySteps;
            gm.SaveUserData();
            gm.OnStepsChanged?.Invoke(u.stepsToday);
            return;
        }

        if (u.stepsToday == osTodaySteps)
            return;

        u.stepsToday = osTodaySteps;
        u.dailyStepCount = osTodaySteps;
        u.pedometerBaselineInitialized = true;
        u.baselineSteps = 0;
        gm.SaveUserData();
        gm.OnStepsChanged?.Invoke(u.stepsToday);
    }
#endif

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
