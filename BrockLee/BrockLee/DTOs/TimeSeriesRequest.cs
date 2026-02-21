using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BrockLee.DTOs;

/// <summary>
/// Request for time-series year-by-year projection.
/// Maps to Python ProjectionRequest.
/// </summary>
public class TimeSeriesRequest
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

    [JsonPropertyName("inflation")]
    public double Inflation { get; set; } = 0.055;
}

/// <summary>
/// Single year entry in the time-series projection.
/// </summary>
public class TimeSeriesEntry
{
    [JsonPropertyName("yearOffset")]
    public int YearOffset { get; set; }

    [JsonPropertyName("projectionYear")]
    public int ProjectionYear { get; set; }

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("balance")]
    public double Balance { get; set; }

    [JsonPropertyName("nominalValue")]
    public double NominalValue { get; set; }

    [JsonPropertyName("realValue")]
    public double RealValue { get; set; }

    [JsonPropertyName("roi")]
    public double Roi { get; set; }

    [JsonPropertyName("growthRate")]
    public double GrowthRate { get; set; }

    [JsonPropertyName("isRetirement")]
    public bool IsRetirement { get; set; }
}

/// <summary>
/// Milestone entry tracking when specific balance thresholds are reached.
/// </summary>
public class MilestoneEntry
{
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    [JsonPropertyName("projectionYear")]
    public int ProjectionYear { get; set; }

    [JsonPropertyName("yearOffset")]
    public int YearOffset { get; set; }

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("realValue")]
    public double RealValue { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Response containing time-series progression and milestones.
/// </summary>
public class TimeSeriesResponse
{
    [JsonPropertyName("principal")]
    public double Principal { get; set; }

    [JsonPropertyName("startAge")]
    public int StartAge { get; set; }

    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonPropertyName("timeline")]
    public List<TimeSeriesEntry> Timeline { get; set; } = new();

    [JsonPropertyName("milestones")]
    public List<MilestoneEntry> Milestones { get; set; } = new();
}
