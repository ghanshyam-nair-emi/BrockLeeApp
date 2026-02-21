using BrockLee.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace BrockLee.Controllers;

[ApiController]
[Route("blackrock/challenge/v1")]
public class PerformanceController : ControllerBase
{
    private readonly PerformanceTracker _tracker;

    public PerformanceController(PerformanceTracker tracker)
    {
        _tracker = tracker;
    }

    /// <summary>
    /// GET /blackrock/challenge/v1/performance
    /// Returns system execution metrics: response time, memory, thread count.
    /// </summary>
    [HttpGet("performance")]
    public IActionResult GetPerformance() => Ok(new
    {
        time = _tracker.LastUpdated,
        memory = $"{_tracker.MemoryMb:F2} MB",
        threads = _tracker.ThreadCount
    });
}