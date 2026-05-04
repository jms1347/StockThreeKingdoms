using UnityEngine;

public partial class DataManager
{
    /// <summary>성별 <see cref="CastleStateData.recruitmentFee"/>·사유를 갱신(전략 틱·초기화·수동).</summary>
    public void RefreshRecruitmentFeesForAllCastles()
    {
        if (!IsStateReady || castleStateDataMap == null) return;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            s.recruitmentFee = RecruitmentDutyCalculator.CalculateRecruitmentFee(this, s.id, out var r);
            s.recruitmentFeeReason = r ?? "";
        }
    }
}
