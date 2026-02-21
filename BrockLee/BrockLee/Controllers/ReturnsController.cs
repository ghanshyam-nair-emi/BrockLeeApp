using BrockLee.DTOs;
using BrockLee.Messaging;
using BrockLee.Middleware;
using BrockLee.Services;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace BrockLee.Controllers;

[ApiController]
[Route("blackrock/challenge/v1")]
public class ReturnsController : ControllerBase
{
    private readonly ReturnsService _returnsService;
    private readonly PerformanceTracker _perf;
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<ReturnsController> _logger;

    public ReturnsController(
        ReturnsService returnsService,
        PerformanceTracker perf,
        IPublishEndpoint publish,
        ILogger<ReturnsController> logger)
    {
        _returnsService = returnsService;
        _perf = perf;
        _publish = publish;
        _logger = logger;
    }

    [HttpPost("returns:nps")]
    public async Task<IActionResult> Nps([FromBody] ReturnsRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _returnsService.CalculateAsync(request, isNps: true, 0);
        sw.Stop();
        return Ok(result);
    }

    [HttpPost("returns:index")]
    public async Task<IActionResult> Index([FromBody] ReturnsRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _returnsService.CalculateAsync(request, isNps: false, 0);
        sw.Stop();
        return Ok(result);
    }

    /// <summary>
    /// POST /returns:compute
    /// Main form submission endpoint.
    ///
    /// Synchronous work  : compute NPS + Index returns (Python)
    /// Asynchronous work : ML predictions via RabbitMQ queue
    ///                     Log write via Channel<T>
    ///                     Dashboard push via RabbitMQ → SignalR
    ///
    /// Returns immediately after compute — client receives predictions
    /// asynchronously via SignalR when the queue consumer finishes.
    /// </summary>
    [HttpPost("returns:compute")]
    public async Task<IActionResult> Compute([FromBody] ReturnsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var submissionId = Guid.NewGuid().ToString();

        // ── Synchronous: compute returns (fast — ~50ms) ───────────────────────
        var npsResult = await _returnsService.CalculateAsync(
            request, isNps: true, 0);
        var indexResult = await _returnsService.CalculateAsync(
            request, isNps: false, 0);

        sw.Stop();
        var ms = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

        // ── Async: queue ML prediction (CPU-bound Python — don't block) ───────
        await _publish.Publish(new PredictRequestMessage(
            SubmissionId: submissionId,
            Age: request.Age,
            MonthlyWage: request.Wage,
            Inflation: request.Inflation,
            TotalRemanent: npsResult.SavingsByDates.Sum(s => s.Amount),
            ExpenseCount: request.Transactions.Count
        ));

        // ── Async: notify dashboard (all connected clients refresh) ───────────
        await _publish.Publish(new DashboardRefreshEvent(
            TriggerType: "submission",
            OccurredAt: DateTime.UtcNow
        ));

        _logger.LogInformation(
            "Compute complete. SubmissionId={Id} ResponseTime={Ms}ms",
            submissionId, ms);

        return Ok(new
        {
            submissionId,           // Angular uses this to join SignalR group
            nps = npsResult,
            index = indexResult,
            responseTimeMs = ms,
            // Predictions will arrive via SignalR "PredictionResult" event
            predictionsAsync = true
        });
    }
}