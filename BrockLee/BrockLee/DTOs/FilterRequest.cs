using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BrockLee.Models;

namespace BrockLee.DTOs;

/// <summary>
/// Input for POST /transactions:filter
/// Applies Q (fixed override), P (additive extra), K (grouping) period rules.
/// </summary>
public class FilterRequest
{
    /// <summary>
    /// Fixed-amount override periods.
    /// Empty list = no Q rules applied.
    /// </summary>
    [JsonPropertyName("q")]
    public List<QPeriod> Q { get; set; } = new();

    /// <summary>
    /// Additive extra-amount periods.
    /// Empty list = no P rules applied.
    /// </summary>
    [JsonPropertyName("p")]
    public List<PPeriod> P { get; set; } = new();

    /// <summary>
    /// Evaluation grouping date ranges.
    /// Empty list = no K grouping output.
    /// </summary>
    [JsonPropertyName("k")]
    public List<KPeriod> K { get; set; } = new();

    [Required]
    [JsonPropertyName("transactions")]
    public List<Transaction> Transactions { get; set; } = new();
}

/// <summary>
/// Output for POST /transactions:filter
/// </summary>
public class FilterResponse
{
    /// <summary>
    /// Transactions after Q and P period rules applied.
    /// Remanent values are updated — these are ready for K grouping.
    /// </summary>
    [JsonPropertyName("valid")]
    public List<Transaction> Valid { get; set; } = new();

    /// <summary>
    /// Transactions that could not be processed (reserved for future hooks).
    /// Currently always empty — filter is non-destructive.
    /// </summary>
    [JsonPropertyName("invalid")]
    public List<InvalidTransaction> Invalid { get; set; } = new();
}