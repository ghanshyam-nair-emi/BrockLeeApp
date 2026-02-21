using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BrockLee.Models;

/// <summary>
/// Q Period — Fixed amount override.
/// When a transaction falls within [Start, End] (inclusive),
/// its remanent is REPLACED by Fixed.
/// If multiple Q periods match, the one with the latest Start wins.
/// Tie on Start → first in the original list wins.
/// fixed &lt; 500,000
/// </summary>
public class QPeriod
{
    [Range(0, 499_999.99)]
    [JsonPropertyName("fixed")]
    public double Fixed { get; set; }

    [Required]
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [Required]
    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}

/// <summary>
/// P Period — Additive extra amount.
/// When a transaction falls within [Start, End] (inclusive),
/// its remanent has Extra ADDED to it.
/// ALL matching P periods are applied (summed), never replaced.
/// extra &lt; 500,000
/// </summary>
public class PPeriod
{
    [Range(0, 499_999.99)]
    [JsonPropertyName("extra")]
    public double Extra { get; set; }

    [Required]
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [Required]
    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}

/// <summary>
/// K Period — Evaluation grouping range.
/// At the end of the year, all transactions within [Start, End] are summed.
/// A transaction can belong to multiple K periods (each summed independently).
/// </summary>
public class KPeriod
{
    [Required]
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [Required]
    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}