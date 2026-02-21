using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BrockLee.Models;

/// <summary>
/// Raw expense input from the user.
/// Date must be unique per submission (tᵢ ≠ tⱼ enforced at service level).
/// Amount must be ≥ 0 and < 500,000.
/// </summary>
public class Expense
{
    [Required]
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [Required]
    [Range(0, 499_999.99, ErrorMessage = "Amount must be between 0 and 500,000 (exclusive).")]
    [JsonPropertyName("amount")]
    public double Amount { get; set; }
}