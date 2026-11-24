# CPU Hog Finder

CPU Hog Finder is a Windows desktop monitor for finding processes responsible for recent CPU work. It samples process CPU time approximately once per second, groups instances by case-insensitive process name, and keeps up to one hour of history in memory. Select a 1, 5, 15, 30 or 60 minute look-back and select a row to see its CPU history. A row with zero running instances remains visible while it has CPU time in the selected window.

## Run

On Windows, install the .NET 11 SDK, then run `dotnet run --project CpuHogFinder\CpuHogFinder.csproj`. The application starts monitoring when opened; history is not persisted across restarts.

## Reading the results

Rows are ranked by **CPU seconds**: the sum of observed CPU time in the chosen window, with equal weight throughout the window. An interval crossing the window boundary is apportioned by its overlap, assuming work was uniform within that interval. **Average CPU** is CPU seconds divided by the time observed in the window, expressed as a percentage of one logical CPU; **Latest CPU** is the same rate for the latest sampling interval. Aggregated multi-core or multi-instance usage can exceed 100%. The graph shows average CPU for 60 equal time buckets across the selected window. Before the window fills, its earlier portion is empty, and averages use only the observed time.

This is a sampled approximation, not an accounting log. A process must be seen in consecutive samples to contribute a delta: processes that start and exit between samples, their first interval, and CPU used after their last observation are missed. Inaccessible processes and processes that exit during enumeration are counted in the status line and may be absent from totals. Access permissions can affect what is visible; running elevated may improve visibility. Processes with the same name are combined even if their executable paths differ. CPU time measures processor time, not energy, frequency, or cycles; sampling intervals and CPU work near window boundaries are approximate.
