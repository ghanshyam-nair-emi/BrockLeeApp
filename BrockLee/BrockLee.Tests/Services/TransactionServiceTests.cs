// Test Type     : Unit
// Validation    : Expense parsing (ceiling + remanent) and transaction validation rules
// Command       : dotnet test --filter "FullyQualifiedName~TransactionServiceTests"

using BrockLee.DTOs;
using BrockLee.Models;
using BrockLee.Services;
using BrockLee.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace BrockLee.Tests.Services;

public sealed class TransactionServiceTests
{
    private readonly TransactionService _svc = new();

    // ── Parse: ceiling + remanent ─────────────────────────────────────────────

    [Theory]
    [InlineData(250, 300, 50)]    // challenge example
    [InlineData(375, 400, 25)]    // challenge example
    [InlineData(620, 700, 80)]    // challenge example
    [InlineData(480, 500, 20)]    // challenge example
    [InlineData(1519, 1600, 81)]    // spec example
    [InlineData(200, 200, 0)]    // already a multiple → remanent = 0
    [InlineData(100, 100, 0)]    // already a multiple
    [InlineData(101, 200, 99)]    // one above a multiple
    [InlineData(1, 100, 99)]    // minimum non-zero amount
    [InlineData(0, 0, 0)]    // zero amount
    public void Parse_Ceiling_And_Remanent_AreCorrect(
        double amount, double expectedCeiling, double expectedRemanent)
    {
        var result = _svc.Parse(new List<Expense>
        {
            new() { Date = DateTime.UtcNow, Amount = amount }
        });

        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Ceiling.Should()
              .BeApproximately(expectedCeiling, 0.001,
              $"ceiling of {amount} should be {expectedCeiling}");
        result.Transactions[0].Remanent.Should()
              .BeApproximately(expectedRemanent, 0.001,
              $"remanent of {amount} should be {expectedRemanent}");
    }

    [Fact]
    public void Parse_ChallengeExample_TotalsAreCorrect()
    {
        // From challenge spec: expenses 250, 375, 620, 480 → total remanent = 175
        var expenses = new List<Expense>
        {
            new() { Date = new DateTime(2023, 10, 12, 20, 15, 0), Amount = 250 },
            new() { Date = new DateTime(2023,  2, 28, 15, 49, 0), Amount = 375 },
            new() { Date = new DateTime(2023,  7,  1, 21, 59, 0), Amount = 620 },
            new() { Date = new DateTime(2023, 12, 17,  8,  9, 0), Amount = 480 }
        };

        var result = _svc.Parse(expenses);

        result.Transactions.Should().HaveCount(4);
        result.TotalRemanent.Should().BeApproximately(175, 0.001);
        result.TotalCeiling.Should().BeApproximately(2000, 0.001); // 300+400+700+500
        result.TotalExpense.Should().BeApproximately(1725, 0.001); // 250+375+620+480
    }

    [Fact]
    public void Parse_EmptyExpenses_ReturnsEmptyWithZeroTotals()
    {
        var result = _svc.Parse(new List<Expense>());

        result.Transactions.Should().BeEmpty();
        result.TotalRemanent.Should().Be(0);
        result.TotalCeiling.Should().Be(0);
        result.TotalExpense.Should().Be(0);
    }

    [Fact]
    public void Parse_PreservesOriginalDate()
    {
        var date = new DateTime(2023, 6, 15, 14, 30, 0);
        var result = _svc.Parse(new List<Expense>
        {
            new() { Date = date, Amount = 350 }
        });

        result.Transactions[0].Date.Should().Be(date);
    }

    // ── Validate: duplicate timestamps ────────────────────────────────────────

    [Fact]
    public void Validate_DuplicateTimestamps_AreSeparated()
    {
        var ts = new DateTime(2023, 1, 1, 10, 0, 0);
        var req = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions = new List<Transaction>
            {
                new() { Date = ts, Amount = 200, Ceiling = 200, Remanent = 0 },
                new() { Date = ts, Amount = 300, Ceiling = 300, Remanent = 0 }   // duplicate
            }
        };

        var result = _svc.Validate(req);

        result.Duplicates.Should().HaveCount(1);
        result.Valid.Should().HaveCount(1);
        result.Duplicates[0].Message.Should().Contain("uplicate");
    }

    [Fact]
    public void Validate_UniqueTimestamps_AllPass()
    {
        var req = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions = TransactionBuilder.BuildMany(
                (new DateTime(2023, 1, 1), 250),
                (new DateTime(2023, 2, 1), 375),
                (new DateTime(2023, 3, 1), 480)
            )
        };

        var result = _svc.Validate(req);

        result.Valid.Should().HaveCount(3);
        result.Invalid.Should().BeEmpty();
        result.Duplicates.Should().BeEmpty();
    }

    // ── Validate: amount range ────────────────────────────────────────────────

    [Fact]
    public void Validate_NegativeAmount_IsInvalid()
    {
        var req = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions = new List<Transaction>
            {
                new() { Date = DateTime.UtcNow, Amount = -10, Ceiling = 0, Remanent = 10 }
            }
        };

        var result = _svc.Validate(req);

        result.Invalid.Should().HaveCount(1);
        result.Invalid[0].Message.Should().Contain("range");
    }

    [Fact]
    public void Validate_AmountAtLimit_IsInvalid()
    {
        // x < 500,000 — exactly 500,000 should be invalid
        var req = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions = new List<Transaction>
            {
                new() { Date = DateTime.UtcNow, Amount = 500_000,
                        Ceiling = 500_000, Remanent = 0 }
            }
        };

        var result = _svc.Validate(req);
        result.Invalid.Should().HaveCount(1);
    }

    // ── Validate: ceiling/remanent integrity ──────────────────────────────────

    [Fact]
    public void Validate_IncorrectCeiling_IsInvalid()
    {
        var req = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions = new List<Transaction>
            {
                // Amount 250 → ceiling should be 300, not 400
                new() { Date = DateTime.UtcNow, Amount = 250, Ceiling = 400, Remanent = 150 }
            }
        };

        var result = _svc.Validate(req);

        result.Invalid.Should().HaveCount(1);
        result.Invalid[0].Message.Should().Contain("Ceiling");
    }

    [Fact]
    public void Validate_IncorrectRemanent_IsInvalid()
    {
        var req = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions = new List<Transaction>
            {
                // Ceiling 300 - Amount 250 = 50, not 99
                new() { Date = DateTime.UtcNow, Amount = 250, Ceiling = 300, Remanent = 99 }
            }
        };

        var result = _svc.Validate(req);

        result.Invalid.Should().HaveCount(1);
        result.Invalid[0].Message.Should().Contain("Remanent");
    }

    // ── Validate: wage guard ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ZeroWage_MarksAllTransactionsInvalid()
    {
        var req = new ValidatorRequest
        {
            Wage = 0,
            Transactions = TransactionBuilder.BuildMany(
                (new DateTime(2023, 1, 1), 250),
                (new DateTime(2023, 2, 1), 375)
            )
        };

        var result = _svc.Validate(req);

        result.Invalid.Should().HaveCount(2);
        result.Valid.Should().BeEmpty();
        result.Invalid.All(i => i.Message.Contains("wage")).Should().BeTrue();
    }

    [Fact]
    public void Validate_NegativeWage_MarksAllTransactionsInvalid()
    {
        var req = new ValidatorRequest
        {
            Wage = -1000,
            Transactions = TransactionBuilder.BuildMany(
                (new DateTime(2023, 1, 1), 250)
            )
        };

        var result = _svc.Validate(req);

        result.Invalid.Should().HaveCount(1);
        result.Valid.Should().BeEmpty();
    }

    // ── ComputeCeiling static method ──────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(100.01, 200)]
    [InlineData(199.99, 200)]
    [InlineData(200, 200)]
    [InlineData(499999, 499_900 + 100)] // 500,000
    public void ComputeCeiling_EdgeCases(double input, double expected)
    {
        TransactionService.ComputeCeiling(input)
            .Should().BeApproximately(expected, 0.001);
    }
}