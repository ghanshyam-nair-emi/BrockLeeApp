using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BrockLee.Models;

namespace BrockLee.DTOs;

/// <summary>
/// Input for POST /transactions:validator
/// Validates enriched transactions against wage and data integrity rules.
/// </summary>
public class ValidatorRequest
{
    /// <summary>Monthly wage in INR (pre-tax). Must be > 0.</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Wage must be greater than 0.")]
    [JsonPropertyName("wage")]
    public double Wage { get; set; }

    [Required]
    [JsonPropertyName("transactions")]
    public List<Transaction> Transactions { get; set; } = new();
}

/// <summary>
/// Output for POST /transactions:validator
/// </summary>
public class ValidatorResponse
{
    /// <summary>Transactions that passed all validation rules.</summary>
    [JsonPropertyName("valid")]
    public List<Transaction> Valid { get; set; } = new();

    /// <summary>Transactions that failed one or more validation rules.</summary>
    [JsonPropertyName("invalid")]
    public List<InvalidTransaction> Invalid { get; set; } = new();

    /// <summary>Transactions with duplicate timestamps (tᵢ = tⱼ violation).</summary>
    [JsonPropertyName("duplicates")]
    public List<InvalidTransaction> Duplicates { get; set; } = new();
}