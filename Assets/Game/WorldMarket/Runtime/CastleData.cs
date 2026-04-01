using System;
using UnityEngine;

/// <summary>
/// <see cref="CastleStateData"/>의 관찰 가능 스탯·임계 돌파 이벤트(옵저버 패턴).
/// 세이브 페이로드는 동일 타입이므로 필드는 이 partial에 두어도 <see cref="UnityEngine.JsonUtility"/>에 포함됩니다.
/// </summary>
public partial class CastleStateData
{
    /// <summary>0~100. 치안·안정도(기획 확장용). 민심과 별도로 임계 이벤트에 사용 가능.</summary>
    public float stabilityScore = 100f;

    /// <summary>기획 스펙상 민심(0~200, 100 기준). <see cref="currentSentiment"/>와 동일 의미의 별칭 프로퍼티.</summary>
    public float PublicSentiment
    {
        get => currentSentiment;
        set => SetPublicSentiment(value);
    }

    /// <summary>안정도 0~100.</summary>
    public float Stability
    {
        get => stabilityScore;
        set => SetStabilityScore(value);
    }

    /// <summary>스탯 변경 알림(인스턴스 단위).</summary>
    public event Action<CastleStateData, CastleStatChangedEventArgs> OnStatChanged;

    /// <summary>임계치 돌파(한 번 구간을 넘을 때).</summary>
    public event Action<CastleStateData, CriticalWorldEventArgs> OnCriticalBreach;

    public void SetPublicSentiment(float value, bool notify = true)
    {
        float old = currentSentiment;
        float n = Mathf.Clamp(value, 0f, 200f);
        if (Mathf.Approximately(old, n)) return;
        currentSentiment = n;
        if (!notify) return;
        DispatchStatChanged(CastleStatField.PublicSentiment, old, n);
        EvaluateSentimentCriticalThresholds(old, n);
    }

    public void SetStabilityScore(float value, bool notify = true)
    {
        float old = stabilityScore;
        float n = Mathf.Clamp(value, 0f, 100f);
        if (Mathf.Approximately(old, n)) return;
        stabilityScore = n;
        if (!notify) return;
        DispatchStatChanged(CastleStatField.Stability, old, n);
        EvaluateStabilityCriticalThresholds(old, n);
    }

    /// <summary>버프·월드 이벤트 등에서 호출(누적 변화).</summary>
    public void ApplySentimentDelta(float delta, bool notify = true)
    {
        SetPublicSentiment(currentSentiment + delta, notify);
    }

    public void ApplyPopulationDelta(int delta, bool notify = true)
    {
        int old = currentPopulation;
        int n = Mathf.Max(1, old + delta);
        if (old == n) return;
        currentPopulation = n;
        if (!notify) return;
        DispatchStatChanged(CastleStatField.Population, old, n);
    }

    void DispatchStatChanged(CastleStatField field, float previousValue, float newValue)
    {
        var args = new CastleStatChangedEventArgs(field, previousValue, newValue);
        OnStatChanged?.Invoke(this, args);
        CastleWorldEventManager.RaiseGlobalStatChanged(this, args);
    }

    void EvaluateSentimentCriticalThresholds(float previous, float current)
    {
        float thr = CastleWorldEventManager.SentimentRiotThreshold;
        if (previous >= thr && current < thr)
        {
            var cargs = new CriticalWorldEventArgs(CriticalWorldEventKind.PopularRiot, CastleStatField.PublicSentiment,
                previous, current, thr);
            OnCriticalBreach?.Invoke(this, cargs);
            CastleWorldEventManager.RaiseGlobalCritical(this, cargs);
        }
    }

    void EvaluateStabilityCriticalThresholds(float previous, float current)
    {
        float thr = CastleWorldEventManager.StabilityCollapseThreshold;
        if (previous >= thr && current < thr)
        {
            var cargs = new CriticalWorldEventArgs(CriticalWorldEventKind.StabilityCollapse, CastleStatField.Stability,
                previous, current, thr);
            OnCriticalBreach?.Invoke(this, cargs);
            CastleWorldEventManager.RaiseGlobalCritical(this, cargs);
        }
    }

    /// <summary>구 세이브 등 stability 필드가 0으로 온 경우 기본값 보정.</summary>
    internal void NormalizeStabilityIfUnset()
    {
        if (stabilityScore <= 0f)
            stabilityScore = 100f;
    }
}

/// <summary>옵저버에 전달되는 스탯 종류.</summary>
public enum CastleStatField
{
    PublicSentiment,
    Stability,
    Population
}

public readonly struct CastleStatChangedEventArgs
{
    public CastleStatField Field { get; }
    public float PreviousValue { get; }
    public float NewValue { get; }

    public CastleStatChangedEventArgs(CastleStatField field, float previousValue, float newValue)
    {
        Field = field;
        PreviousValue = previousValue;
        NewValue = newValue;
    }
}

public enum CriticalWorldEventKind
{
    PopularRiot,
    StabilityCollapse
}

public readonly struct CriticalWorldEventArgs
{
    public CriticalWorldEventKind Kind { get; }
    public CastleStatField SourceField { get; }
    public float PreviousValue { get; }
    public float NewValue { get; }
    public float Threshold { get; }

    public CriticalWorldEventArgs(CriticalWorldEventKind kind, CastleStatField sourceField, float previousValue,
        float newValue, float threshold)
    {
        Kind = kind;
        SourceField = sourceField;
        PreviousValue = previousValue;
        NewValue = newValue;
        Threshold = threshold;
    }
}
