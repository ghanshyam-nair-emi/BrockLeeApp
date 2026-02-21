using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrockLee.Services;

/// <summary>
/// HTTP bridge to the Python FastAPI compute sidecar (port 8000).
/// Python is the PRIMARY compute engine (Option B).
/// All financial math and ML predictions are delegated here.
/// </summary>
public class PythonBridgeService
{
    private readonly HttpClient _http;
    private readonly ILogger<PythonBridgeService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public PythonBridgeService(HttpClient http, ILogger<PythonBridgeService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ── Core compute ──────────────────────────────────────────────────────────

    public async Task<FullProjectionResponse> GetFullProjectionAsync(
        double principal, int age, double annualIncome, double inflation = 0.055)
    {
        return await PostAsync<FullProjectionResponse>("/compute/full", new
        {
            principal,
            age,
            annual_income = annualIncome,
            inflation
        });
    }

    public async Task<CeilingResponse> GetCeilingAsync(double amount)
        => await PostAsync<CeilingResponse>("/compute/ceiling", new { amount });

    public async Task<TaxResponse> GetTaxAsync(double income)
        => await PostAsync<TaxResponse>("/compute/tax", new { income });

    // ── ML Predictions ────────────────────────────────────────────────────────

    /// <summary>
    /// POST /predict
    /// Runs all 5 ML models and returns per-model predictions + consensus.
    /// Called by PredictionsController.
    /// </summary>
    public async Task<PredictResponse> GetPredictionsAsync(PredictRequest request)
        => await PostAsync<PredictResponse>("/predict", new
        {
            age            = request.Age,
            monthly_wage   = request.MonthlyWage,
            inflation      = request.Inflation,
            total_remanent = request.TotalRemanent,
            expense_count  = request.ExpenseCount
        });

    /// <summary>
    /// GET /predict/models
    /// Returns accuracy metadata for all trained models.
    /// </summary>
    public async Task<List<ModelMetadata>> GetModelMetadataAsync()
    {
        var response = await _http.GetAsync("/predict/models");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ModelMetadata>>(JsonOpts)
               ?? new List<ModelMetadata>();
    }

    // ── Generic ───────────────────────────────────────────────────────────────

    public async Task<T> PostAsync<T>(string endpoint, object payload)
    {
        _logger.LogDebug("Python bridge → POST {Endpoint}", endpoint);

        var response = await _http.PostAsJsonAsync(endpoint, payload, JsonOpts);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOpts)
               ?? throw new InvalidOperationException(
                   $"Null response from Python endpoint: {endpoint}");
    }
}

// ── Response types ────────────────────────────────────────────────────────────

public record ProjectionResult(
    double FutureValue,
    double RealValue,
    double TaxBenefit,
    double Profits);

public record FullProjectionResponse(
    ProjectionResult Nps,
    ProjectionResult Index);

public record CeilingResponse(double Amount, double Ceiling, double Remanent);
public record TaxResponse(double Income, double Tax);

// ── ML types ─────────────────��───────────────────────────────────────────────

public class PredictRequest
{
    public int    Age           { get; set; }
    public double MonthlyWage   { get; set; }
    public double Inflation     { get; set; } = 0.055;
    public double TotalRemanent { get; set; }
    public int    ExpenseCount  { get; set; }
}

public class SingleModelScore
{
    public double PredictedValue { get; set; }
    public double R2Score        { get; set; }
    public double Mae            { get; set; }
    public double Confidence     { get; set; }
}

public class ModelPrediction
{
    public string          ModelName   { get; set; } = string.Empty;
    public string          ShortName   { get; set; } = string.Empty;
    public string          Description { get; set; } = string.Empty;
    public SingleModelScore Nps        { get; set; } = new();
    public SingleModelScore Index      { get; set; } = new();
}

public class ConsensusResult
{
    public string BestModel      { get; set; } = string.Empty;
    public string BestModelShort { get; set; } = string.Empty;
    public double ConsensusNps   { get; set; }
    public double ConsensusIndex { get; set; }
    public double NpsStdDev      { get; set; }
    public double IndexStdDev    { get; set; }
    public string ModelAgreement { get; set; } = string.Empty;
}

public class PredictResponse
{
    public List<ModelPrediction>  Models            { get; set; } = new();
    public ConsensusResult        Consensus         { get; set; } = new();
    public List<FeatureImportance>? FeatureImportance { get; set; }
}

public class FeatureImportance
{
    public string Feature    { get; set; } = string.Empty;
    public double Importance { get; set; }
}

public class ModelMetadata
{
    public string ModelName   { get; set; } = string.Empty;
    public string ShortName   { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double R2Nps       { get; set; }
    public double R2Index     { get; set; }
    public double MaeNps      { get; set; }
    public double MaeIndex    { get; set; }
}