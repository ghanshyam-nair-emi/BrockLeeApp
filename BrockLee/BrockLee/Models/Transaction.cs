using System.Text.Json.Serialization;

namespace BrockLee.Models;

/// <summary>
/// An expense enriched with ceiling and remanent.
/// ceiling  = next multiple of 100 above amount (or same if already a multiple).
/// remanent = ceiling - amount  → the amount queued for investment.
/// </summary>
public class Transaction
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("ceiling")]
    public double Ceiling { get; set; }

    [JsonPropertyName("remanent")]
    public double Remanent { get; set; }
}

/// <summary>
/// A transaction that failed validation, extended with a human-readable reason.
/// </summary>
public class InvalidTransaction : Transaction
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}