using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BrockLee.DTOs;

/// <summary>
/// Request for risk-based instrument recommendation.
/// Uses expense volatility and wage stability to recommend NPS vs Index.
/// </summary>
public class RiskProfileRequest
{
    [Required]
    [Range(0, double.MaxValue)]
    [JsonPropertyName("principal")]
    public double Principal { get; set; }

    [Required]
    [Range(1, 59)]
    [JsonPropertyName("age")]
    public int Age { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    [JsonPropertyName("annual_income")]
    public double AnnualIncome { get; set; }

    [Required]
    [Range(0, 1)]
    [JsonPropertyName("expense_volatility")]
    public double ExpenseVolatility { get; set; }

    [Range(0, 1)]
    [JsonPropertyName("wage_stability")]
    public double WageStability { get; set; } = 0.8;
}

/// <summary>
/// Response containing ML-based risk profiling and instrument recommendation.
/// </summary>
public class RiskProfileResponse
{
    [JsonPropertyName("riskProfile")]
    public string RiskProfile { get; set; } = string.Empty;

    [JsonPropertyName("recommendedInstrument")]
    public string RecommendedInstrument { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("expenseVolatility")]
    public double ExpenseVolatility { get; set; }

    [JsonPropertyName("wageStability")]
    public double WageStability { get; set; }

    [JsonPropertyName("stabilityScore")]
    public double StabilityScore { get; set; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    [JsonPropertyName("npsAdvantages")]
    public List<string> NpsAdvantages { get; set; } = new();

    [JsonPropertyName("indexAdvantages")]
    public List<string> IndexAdvantages { get; set; } = new();
}
