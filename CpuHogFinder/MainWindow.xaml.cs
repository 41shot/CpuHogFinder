using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CpuHogFinder.Monitoring;

namespace CpuHogFinder;

public partial class MainWindow : Window
{
    private readonly ProcessSampler _sampler = new();
    private readonly UsageHistory _history = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private ProcessSample? _lastSample;
    private string? _selectedName;
    private bool _sampling;
    private bool _closed;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        await SampleAsync();
    }

    private async void Timer_Tick(object? sender, EventArgs e) => await SampleAsync();

    private async Task SampleAsync()
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            var sample = await Task.Run(_sampler.Sample);
            if (_closed) return;
            double start = _lastSample?.Timestamp ?? sample.Timestamp;
            _history.Add(new UsageInterval(start, sample.Timestamp, sample.CpuSeconds));
            _lastSample = sample;
            RefreshView();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            if (!_closed)
                StatusText.Text = $"Sampling failed: {ex.Message} Retrying automatically.";
        }
        finally
        {
            _sampling = false;
        }
    }

    private void RefreshView()
    {
        if (_lastSample is null || WindowSelector.SelectedItem is not ComboBoxItem item) return;
        double seconds = double.Parse((string)item.Tag, System.Globalization.CultureInfo.InvariantCulture);
        var summary = _history.Summarize(_lastSample.Timestamp, seconds, _lastSample.Active, _selectedName);
        ProcessGrid.ItemsSource = summary.Rows;
        if (_selectedName is not null)
            ProcessGrid.SelectedItem = summary.Rows.FirstOrDefault(row => string.Equals(row.Name, _selectedName, StringComparison.OrdinalIgnoreCase));
        HistoryChart.Values = _selectedName is null ? null : summary.Chart;
        ChartTitle.Text = _selectedName is null ? "Select a process to see its history" : $"{_selectedName} · CPU history";
        StatusText.Text = $"Observed {TimeSpan.FromSeconds(summary.ObservedSeconds):h\\:mm\\:ss} of this window. " +
            $"{_lastSample.Active.Values.Sum():N0} readable processes; {_lastSample.Unavailable:N0} unavailable or exited during sampling. " +
            "History is in memory only; short-lived processes may be missed.";
    }

    private void WindowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshView();
    }

    private void ProcessGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is UsageRow row && !string.Equals(_selectedName, row.Name, StringComparison.OrdinalIgnoreCase))
        {
            _selectedName = row.Name;
            RefreshView();
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _closed = true;
        _timer.Stop();
    }
}