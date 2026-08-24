namespace EncoBudsTray.Models;

public sealed record BatteryState(int? Left, int? Right, int? Case, bool LeftCharging = false, bool RightCharging = false, bool CaseCharging = false)
{
    public static BatteryState Unknown => new(null, null, null);

    public BatteryState Merge(BatteryState update)
        => new(
            update.Left ?? Left,
            update.Right ?? Right,
            update.Case ?? Case,
            update.Left.HasValue ? update.LeftCharging : LeftCharging,
            update.Right.HasValue ? update.RightCharging : RightCharging,
            update.Case.HasValue ? update.CaseCharging : CaseCharging);
}
