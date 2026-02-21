namespace BrockLee.Models.Entities;

/// <summary>
/// Public logbook entry — one row per API submission.
/// Stateless API: we only persist this summary for the dashboard.
/// </summary>
public class UserLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }

    /// <summary>Monthly wage in INR</summary>
    public decimal Wage { get; set; }

    /// <summary>Annual income = Wage * 12</summary>
    public decimal AnnualIncome { get; set; }

    /// <summary>Total number of expenses submitted</summary>
    public int ExpenseCount { get; set; }

    /// <summary>Total raw expense amount</summary>
    public decimal TotalExpenseAmount { get; set; }

    /// <summary>Total remanent (savings) across all expenses after Q/P rules</summary>
    public decimal TotalRemanent { get; set; }

    /// <summary>NPS projected real value (inflation-adjusted)</summary>
    public decimal NpsRealValue { get; set; }

    /// <summary>Index Fund projected real value (inflation-adjusted)</summary>
    public decimal IndexRealValue { get; set; }

    /// <summary>NPS tax benefit in INR</summary>
    public decimal TaxBenefit { get; set; }

    /// <summary>Years to retirement (60 - age)</summary>
    public int YearsToRetirement { get; set; }

    /// <summary>UTC timestamp of submission</summary>
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    /// <summary>API response time in ms for this request</summary>
    public double ResponseTimeMs { get; set; }
}