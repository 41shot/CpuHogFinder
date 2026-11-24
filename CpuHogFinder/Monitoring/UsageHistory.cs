namespace CpuHogFinder.Monitoring;

public sealed record UsageInterval(double Start, double End, IReadOnlyDictionary<string, double> CpuSeconds);

public sealed record UsageRow(string Name, int Instances, double CpuSeconds, double AverageCpuPercent, double CurrentCpuPercent);

public sealed record UsageSummary(IReadOnlyList<UsageRow> Rows, IReadOnlyList<double?> Chart, double ObservedSeconds);

public sealed class UsageHistory
{
    public const double RetentionSeconds = 3600;
    private readonly Queue<UsageInterval> _intervals = new();
    private double? _startedAt;

    public void Add(UsageInterval interval)
    {
        if (interval.End < interval.Start)
            throw new ArgumentException("The interval cannot end before it starts.", nameof(interval));

        _startedAt ??= interval.Start;
        if (interval.End > interval.Start)
            _intervals.Enqueue(interval);
        while (_intervals.TryPeek(out var oldest) && oldest.End <= interval.End - RetentionSeconds)
            _intervals.Dequeue();
    }

    public UsageSummary Summarize(double now, double windowSeconds, IReadOnlyDictionary<string, int> active, string? selectedName, int chartPoints = 60)
    {
        if (windowSeconds <= 0 || windowSeconds > RetentionSeconds)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        if (chartPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(chartPoints));

        double windowStart = now - windowSeconds;
        double observed = _startedAt is null ? 0 : Math.Clamp(now - Math.Max(windowStart, _startedAt.Value), 0, windowSeconds);
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var chartCpu = new double[chartPoints];
        double binWidth = windowSeconds / chartPoints;
        var latest = _intervals.LastOrDefault();

        foreach (var interval in _intervals)
        {
            double duration = interval.End - interval.Start;
            double overlap = Math.Max(0, Math.Min(now, interval.End) - Math.Max(windowStart, interval.Start));
            if (overlap <= 0)
                continue;

            foreach (var (name, seconds) in interval.CpuSeconds)
            {
                totals.TryGetValue(name, out double total);
                totals[name] = total + seconds * overlap / duration;
            }

            if (ReferenceEquals(interval, latest))
            {
                foreach (var (name, seconds) in interval.CpuSeconds)
                    current[name] = seconds / duration * 100;
            }

            if (selectedName is not null && interval.CpuSeconds.TryGetValue(selectedName, out double selectedCpu))
            {
                int first = Math.Clamp((int)((Math.Max(windowStart, interval.Start) - windowStart) / binWidth), 0, chartPoints - 1);
                int last = Math.Clamp((int)((Math.Min(now, interval.End) - windowStart) / binWidth), 0, chartPoints - 1);
                for (int i = first; i <= last; i++)
                {
                    double binStart = windowStart + i * binWidth;
                    double binOverlap = Math.Max(0, Math.Min(interval.End, binStart + binWidth) - Math.Max(interval.Start, binStart));
                    chartCpu[i] += selectedCpu * binOverlap / duration;
                }
            }
        }

        var chart = new double?[chartPoints];
        for (int i = 0; i < chartPoints; i++)
        {
            double binStart = windowStart + i * binWidth;
            double observedInBin = _startedAt is null ? 0 : Math.Max(0, Math.Min(now, binStart + binWidth) - Math.Max(_startedAt.Value, binStart));
            if (observedInBin > 0)
                chart[i] = chartCpu[i] / observedInBin * 100;
        }

        foreach (var name in active.Keys)
            totals.TryAdd(name, 0);

        var rows = totals.Select(pair => new UsageRow(pair.Key, active.GetValueOrDefault(pair.Key), pair.Value,
                observed > 0 ? pair.Value / observed * 100 : 0, current.GetValueOrDefault(pair.Key)))
            .OrderByDescending(row => row.CpuSeconds)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new UsageSummary(rows, chart, observed);
    }
}
