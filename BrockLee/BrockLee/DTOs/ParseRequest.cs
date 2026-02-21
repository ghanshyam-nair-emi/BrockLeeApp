using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BrockLee.Models;

namespace BrockLee.DTOs;

/// <summary>
/// Input for POST /transactions:parse
/// Receives raw expenses, returns enriched transactions.
/// </summary>
public class ParseRequest
{
    [Required]
    [JsonPropertyName("expenses")]
    public List<Expense> Expenses { get; set; } = new();
}

/// <summary>
/// Output for POST /transactions:parse
/// </summary>
public class ParseResponse
{
    [JsonPropertyName("transactions")]
    public List<Transaction> Transactions { get; set; } = new();

    /// <summary>Sum of all remanents across all transactions.</summary>
    [JsonPropertyName("totalRemanent")]
    public double TotalRemanent { get; set; }

    /// <summary>Sum of all ceiling values.</summary>
    [JsonPropertyName("totalCeiling")]
    public double TotalCeiling { get; set; }

    /// <summary>Sum of all original expense amounts.</summary>
    [JsonPropertyName("totalExpense")]
    public double TotalExpense { get; set; }
}