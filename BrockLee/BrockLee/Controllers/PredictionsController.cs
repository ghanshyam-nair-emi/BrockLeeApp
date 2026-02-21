using BrockLee.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrockLee.Controllers;

/// <summary>
/// Exposes ML prediction endpoints to the Angular dashboard.
/// Delegates entirely to the Python sidecar.
///
/// Endpoints:
///   POST /blackrock/challenge/v1/predictions        — run all 5 models
///   GET  /blackrock/challenge/v1/predictions/models — model metadata
/// </summary>
[ApiController]
[Route("blackrock/challenge/v1/predictions")]
public class PredictionsController : ControllerBase
{
    private readonly PythonBridgeService            _python;
    private readonly ILogger<PredictionsController> _logger;

    public PredictionsController(
        PythonBridgeService python,
        ILogger<PredictionsController> logger)
    {
        _python = python;
        _logger = logger;
    }

    /// <summary>
    /// POST /blackrock/challenge/v1/predictions
    ///
    /// Accepts user profile + savings summary.
    /// Returns predictions from 5 ML models (LR, PR, Ridge, RF, GB)
    /// plus consensus and feature importance for the dashboard chart.
    ///
    /// Called after /returns:compute to populate the prediction panel.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Predict([FromBody] PredictRequest request)
    {
        if (request.Age is < 1 or > 59)
            return BadRequest(new { message = "Age must be between 1 and 59." });

        if (request.MonthlyWage <= 0)
            return BadRequest(new { message = "Monthly wage must be greater than 0." });

        if (request.TotalRemanent < 0)
            return BadRequest(new { message = "Total remanent cannot be negative." });

        if (request.ExpenseCount < 1)
            return BadRequest(new { message = "Expense count must be at least 1." });

        try
        {
            var result = await _python.GetPredictionsAsync(request);
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Python ML sidecar unavailable for predictions.");
            return StatusCode(503, new
            {
                message = "ML prediction service temporarily unavailable.",
                detail  = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prediction failed.");
            return StatusCode(500, new { message = "Prediction failed.", detail = ex.Message });
        }
    }

    /// <summary>
    /// GET /blackrock/challenge/v1/predictions/models
    ///
    /// Returns accuracy metadata for all trained models.
    /// Used by Angular to render the model comparison table.
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        try
        {
            var metadata = await _python.GetModelMetadataAsync();
            return Ok(metadata);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Python sidecar unavailable for model metadata.");
            return StatusCode(503, new { message = "ML service unavailable.", detail = ex.Message });
        }
    }
}