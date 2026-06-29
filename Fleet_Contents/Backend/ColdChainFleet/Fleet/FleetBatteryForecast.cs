namespace FleetCore.Fleet;

/// <summary>
/// Estimates remaining battery life using ordinary least-squares linear regression
/// over recent battery_pct readings. No external dependencies — pure math.
///
/// Call Forecast() with a sequence of (timestamp, batteryPct) pairs.
/// Returns null when fewer than 3 usable readings are available.
/// </summary>
public static class FleetBatteryForecast
{
    public record BatteryForecastResult(
        double  CurrentPct,
        double  SlopePerHour,
        double? HoursUntilThreshold,
        double  ThresholdPct,
        int     DataPoints,
        string  Status
    );

    public static BatteryForecastResult? Forecast(
        IEnumerable<(DateTime ts, double batteryPct)> points,
        double thresholdPct = 20.0)
    {
        var pts = points
            .Where(p => p.batteryPct is >= 0 and <= 100)
            .OrderBy(p => p.ts)
            .ToList();

        if (pts.Count < 3) return null;

        var current = pts[^1].batteryPct;
        var origin  = pts[0].ts;

        var xs = pts.Select(p => (p.ts - origin).TotalHours).ToArray();
        var ys = pts.Select(p => p.batteryPct).ToArray();

        var (slope, _) = Ols(xs, ys);

        string status;
        if      (slope > 0.5)                           status = "Charging";
        else if (slope > -0.1)                          status = "Stable";
        else if (current < thresholdPct + 5.0)          status = "Critical";
        else                                             status = "Discharging";

        double? hoursUntil = null;
        if (slope < 0 && current > thresholdPct)
            hoursUntil = Math.Round((thresholdPct - current) / slope, 1);

        return new BatteryForecastResult(
            CurrentPct:          Math.Round(current, 1),
            SlopePerHour:        Math.Round(slope,   3),
            HoursUntilThreshold: hoursUntil,
            ThresholdPct:        thresholdPct,
            DataPoints:          pts.Count,
            Status:              status
        );
    }

    private static (double slope, double intercept) Ols(double[] xs, double[] ys)
    {
        var xBar = xs.Average();
        var yBar = ys.Average();
        var ssXX = xs.Sum(x => (x - xBar) * (x - xBar));
        var ssXY = xs.Zip(ys, (x, y) => (x - xBar) * (y - yBar)).Sum();

        if (ssXX == 0) return (0, yBar);

        var slope = ssXY / ssXX;
        return (slope, yBar - slope * xBar);
    }
}
