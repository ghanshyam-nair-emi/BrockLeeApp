using System.Diagnostics;

namespace BrockLee.Middleware;

/// <summary>
/// Singleton tracker — updated on every request by PerformanceMiddleware.
/// Read by PerformanceController for the /performance endpoint.
/// </summary>
public class PerformanceTracker
{
    public double LastResponseMs { get; private set; }
    public double MemoryMb { get; private set; }
    public int ThreadCount { get; private set; }
    public string LastUpdated { get; private set; } = string.Empty;

    public void Update(double ms)
    {
        LastResponseMs = ms;
        MemoryMb = Math.Round(Process.GetCurrentProcess().WorkingSet64 / 1_048_576.0, 2);
        ThreadCount = Process.GetCurrentProcess().Threads.Count;
        LastUpdated = DateTime.UtcNow.ToString("HH:mm:ss.fff");
    }
}

/// <summary>
/// Middleware that measures wall-clock time of every request
/// and updates PerformanceTracker.
/// </summary>
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PerformanceTracker _tracker;

    public PerformanceMiddleware(RequestDelegate next, PerformanceTracker tracker)
    {
        _next = next;
        _tracker = tracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();
        _tracker.Update(Math.Round(sw.Elapsed.TotalMilliseconds, 2));
    }
}