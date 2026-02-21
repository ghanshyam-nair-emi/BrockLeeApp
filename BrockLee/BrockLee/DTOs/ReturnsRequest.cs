using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BrockLee.Models;

namespace BrockLee.DTOs;

/// <summary>
/// Input for:
///   POST /returns:nps
///   POST /returns:index
///   POST /returns:compute  (used by Angular frontend — requires Name for logging)
///
/// Combines all data needed in one stateless call:
/// user profile + transactions + all period rules.
/// The API logs one row to the public logbook on every call.
/// </summary>
public class ReturnsRequest
{
    /// <summary>
    /// User display name — required for /returns:compute (logbook entry).
    /// Optional for /returns:nps and /returns:index.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Current age in years. Must be less than 60.</summary>
    [Required]
    [Range(1, 59, ErrorMessage = "Age must be between 1 and 59.")]
    [JsonPropertyName("age")]
    public int Age { get; set; }

    /// <summary>
    /// Monthly salary in INR (pre-tax).
    /// Annual income = Wage * 12 (used for NPS deduction limit and tax calculation).
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Wage must be greater than 0.")]
    [JsonPropertyName("wage")]
    public double Wage { get; set; }

    /// <summary>
    /// Annual inflation rate as a decimal (e.g. 0.055 for 5.5%).
    /// Defaults to 0.055 if not provided or 0.
    /// </summary>
    [JsonPropertyName("inflation")]
    public double Inflation { get; set; } = 0.055;

    /// <summary>Fixed-amount override periods.</summary>
    [JsonPropertyName("q")]
    public List<QPeriod> Q { get; set; } = new();

    /// <summary>Additive extra-amount periods.</summary>
    [JsonPropertyName("p")]
    public List<PPeriod> P { get; set; } = new();

    /// <summary>Evaluation grouping date ranges.</summary>
    [JsonPropertyName("k")]
    public List<KPeriod> K { get; set; } = new();

    /// <summary>
    /// Enriched transactions (output of /transactions:parse or :validator).
    /// Must have valid Date, Amount, Ceiling, Remanent.
    /// </summary>
    [Required]
    [JsonPropertyName("transactions")]
    public List<Transaction> Transactions { get; set; } = new();
}