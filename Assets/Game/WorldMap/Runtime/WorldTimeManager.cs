using System;
using System.Collections;
using UnityEngine;

/// <summary>현실 시간 기준 시뮬레이션 일 틱. 기본 10초 = 1일.</summary>
[DefaultExecutionOrder(-100)]
public class WorldTimeManager : WorldMapSingleton<WorldTimeManager>
{
    [SerializeField] float realSecondsPerGameDay = 10f;
    [SerializeField] int startingSimulatedDay = 0;

    [Tooltip("플레이 직후 한 프레임 뒤 첫 일 틱을 보냅니다(GovernorAI 등 구독 이후). 이후는 realSecondsPerGameDay 간격으로.")]
    [SerializeField] bool runFirstSimulationDayOnPlay = true;

    float _accumulatedSeconds;
    int _simulatedDay;

    /// <summary>마지막으로 발행된 시뮬레이션 일 번호(첫 틱 후 1부터 증가).</summary>
    public int SimulatedDay => _simulatedDay;

    public float RealSecondsPerGameDay => Mathf.Max(1f, realSecondsPerGameDay);

    /// <summary>시뮬레이션 하루를 24시간으로 볼 때, 게임 내 1시간에 해당하는 현실 초(= 하루 길이 / 24).</summary>
    public float RealSecondsPerGameHour => RealSecondsPerGameDay / 24f;

    /// <summary>인자: 새로 시작된 시뮬레이션 일 인덱스(1, 2, 3…).</summary>
    public event Action<int> OnNewDayTick;

    protected override void Awake()
    {
        base.Awake();
        _simulatedDay = startingSimulatedDay;
    }

    void Start()
    {
        if (runFirstSimulationDayOnPlay)
            StartCoroutine(BootstrapFirstDayAfterFrame());
    }

    IEnumerator BootstrapFirstDayAfterFrame()
    {
        yield return null;
        _simulatedDay++;
        OnNewDayTick?.Invoke(_simulatedDay);
    }

    void Update()
    {
        float step = RealSecondsPerGameDay;
        _accumulatedSeconds += Time.deltaTime;
        while (_accumulatedSeconds >= step)
        {
            _accumulatedSeconds -= step;
            _simulatedDay++;
            OnNewDayTick?.Invoke(_simulatedDay);
        }
    }
}
