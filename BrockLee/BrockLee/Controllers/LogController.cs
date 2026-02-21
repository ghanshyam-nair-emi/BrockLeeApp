using BrockLee.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrockLee.Controllers;

[ApiController]
[Route("blackrock/challenge/v1")]
public class LogController : ControllerBase
{
    private readonly LogService _logService;

    public LogController(LogService logService)
    {
        _logService = logService;
    }

    /// <summary>Returns latest 50 log entries for the dashboard table.</summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int count = 50)
    {
        var logs = await _logService.GetLatestAsync(Math.Clamp(count, 1, 200));
        return Ok(logs);
    }

    /// <summary>Returns aggregate metrics for the Metric Update panel.</summary>
    [HttpGet("logs/metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _logService.GetMetricsAsync();
        return Ok(metrics);
    }
}