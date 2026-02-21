namespace BrockLee.Models;

public class SavingsByDate
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double Amount { get; set; }
    public double Profits { get; set; }
    public double TaxBenefit { get; set; }
}

public class KGroup
{
    public KPeriod Period { get; set; } = null!;
    public double Sum { get; set; }
}

public class ReturnsResponse
{
    public double TransactionsTotalAmount { get; set; }
    public double TransactionsTotalCeiling { get; set; }
    public List<SavingsByDate> SavingsByDates { get; set; } = new();
}