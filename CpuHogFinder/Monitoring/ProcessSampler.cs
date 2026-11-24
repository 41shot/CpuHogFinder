using System.ComponentModel;
using System.Diagnostics;

namespace CpuHogFinder.Monitoring;

public sealed record ProcessSample(double Timestamp, IReadOnlyDictionary<string, int> Active, IReadOnlyDictionary<string, double> CpuSeconds, int Unavailable);

public sealed class ProcessSampler
{
    private sealed record PreviousProcess(long StartTicks, TimeSpan CpuTime);
    private Dictionary<int, PreviousProcess> _previous = new();

    public ProcessSample Sample()
    {
        var next = new Dictionary<int, PreviousProcess>();
        var active = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cpu = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        int unavailable = 0;
        var processes = Process.GetProcesses();
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    int id = process.Id;
                    string name = process.ProcessName;
                    long start = process.StartTime.ToUniversalTime().Ticks;
                    TimeSpan time = process.TotalProcessorTime;
                    active[name] = active.GetValueOrDefault(name) + 1;
                    next[id] = new PreviousProcess(start, time);
                    if (_previous.TryGetValue(id, out var previous) && previous.StartTicks == start)
                    {
                        double delta = (time - previous.CpuTime).TotalSeconds;
                        if (delta > 0)
                            cpu[name] = cpu.GetValueOrDefault(name) + delta;
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
                {
                    unavailable++;
                }
            }
        }
        _previous = next;
        return new ProcessSample((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency, active, cpu, unavailable);
    }
}
