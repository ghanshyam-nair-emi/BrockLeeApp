// Test Type     : Helper / Builder Pattern
// Validation    : Fluent builder for constructing test Transaction objects
// Command       : Used internally by test classes

using BrockLee.Models;

namespace BrockLee.Tests.Helpers;

/// <summary>
/// Fluent builder for Transaction test fixtures.
/// Avoids repetitive object initialisation across test files.
/// </summary>
public sealed class TransactionBuilder
{
    private DateTime _date = new(2023, 1, 1, 10, 0, 0);
    private double _amount = 250;
    private double _ceiling = 300;
    private double _remanent = 50;

    public TransactionBuilder WithDate(DateTime date) { _date = date; return this; }
    public TransactionBuilder WithAmount(double amount) { _amount = amount; return this; }
    public TransactionBuilder WithCeiling(double ceiling) { _ceiling = ceiling; return this; }
    public TransactionBuilder WithRemanent(double remanent) { _remanent = remanent; return this; }

    /// <summary>Auto-computes ceiling and remanent from amount using challenge rules.</summary>
    public TransactionBuilder WithComputedCeiling(double amount)
    {
        _amount = amount;
        _ceiling = amount % 100 == 0 ? amount : Math.Ceiling(amount / 100.0) * 100;
        _remanent = _ceiling - amount;
        return this;
    }

    public Transaction Build() => new()
    {
        Date = _date,
        Amount = _amount,
        Ceiling = _ceiling,
        Remanent = _remanent
    };

    /// <summary>Builds a list of transactions from (date, amount) pairs.</summary>
    public static List<Transaction> BuildMany(params (DateTime date, double amount)[] items)
        => items.Select(i =>
        {
            var ceiling = i.amount % 100 == 0
                ? i.amount
                : Math.Ceiling(i.amount / 100.0) * 100;
            return new Transaction
            {
                Date = i.date,
                Amount = i.amount,
                Ceiling = ceiling,
                Remanent = ceiling - i.amount
            };
        }).ToList();
}