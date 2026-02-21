// Test Type     : Unit
// Validation    : Q period (fixed override), P period (additive),
//                 K grouping, tie-breaking rules, overlapping periods
// Command       : dotnet test --filter "FullyQualifiedName~FilterServiceTests"

using BrockLee.DTOs;
using BrockLee.Models;
using BrockLee.Services;
using BrockLee.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace BrockLee.Tests.Services;

public sealed class FilterServiceTests
{
    private readonly FilterService _svc = new();

    // ── Challenge example transactions ────────────────────────────────────────

    private static List<Transaction> ChallengeTransactions() =>
    [
        new() { Date = new DateTime(2023, 10, 12, 20, 15, 0), Amount = 250, Ceiling = 300, Remanent = 50  },
        new() { Date = new DateTime(2023,  2, 28, 15, 49, 0), Amount = 375, Ceiling = 400, Remanent = 25  },
        new() { Date = new DateTime(2023,  7,  1, 21, 59, 0), Amount = 620, Ceiling = 700, Remanent = 80  },
        new() { Date = new DateTime(2023, 12, 17,  8,  9, 0), Amount = 480, Ceiling = 500, Remanent = 20  }
    ];

    // ── Q Period tests ────────────────────────────────────────────────────────

    [Fact]
    public void Q_Period_ReplacesRemanentWithFixed()
    {
        // July fixed = 0 → 2023-07-01 expense remanent becomes 0
        var req = new FilterRequest
        {
            Transactions = ChallengeTransactions(),
            Q = [new QPeriod
            {
                Fixed = 0,
                Start = new DateTime(2023, 7,  1,  0,  0, 0),
                End   = new DateTime(2023, 7, 31, 23, 59, 59)
            }],
            P = [],
            K = []
        };

        var result = _svc.ApplyPeriods(req);

        var julyTx = result.Valid.Single(t => t.Date.Month == 7);
        julyTx.Remanent.Should().Be(0,
            "Q period with fixed=0 should override remanent to 0");
    }

    [Fact]
    public void Q_Period_DoesNotAffectTransactionsOutsideRange()
    {
        var req = new FilterRequest
        {
            Transactions = ChallengeTransactions(),
            Q = [new QPeriod
            {
                Fixed = 0,
                Start = new DateTime(2023, 7,  1,  0,  0, 0),
                End   = new DateTime(2023, 7, 31, 23, 59, 59)
            }],
            P = [],
            K = []
        };

        var result = _svc.ApplyPeriods(req);

        // Feb expense (outside July range) should keep original remanent = 25
        result.Valid.Single(t => t.Date.Month == 2)
              .Remanent.Should().BeApproximately(25, 0.001);

        // Oct expense should keep original remanent = 50
        result.Valid.Single(t => t.Date.Month == 10)
              .Remanent.Should().BeApproximately(50, 0.001);
    }

    [Fact]
    public void Q_Period_InclusiveBoundary_StartDate()
    {
        // Transaction ON start date must be affected
        var req = new FilterRequest
        {
            Transactions = [new()
            {
                Date     = new DateTime(2023, 7, 1, 0, 0, 0),  // exactly on start
                Amount   = 500,
                Ceiling  = 500,
                Remanent = 0
            }],
            Q = [new QPeriod
            {
                Fixed = 99,
                Start = new DateTime(2023, 7, 1, 0, 0, 0),
                End   = new DateTime(2023, 7, 31, 23, 59, 59)
            }],
            P = [],
            K = []
        };

        var result = _svc.ApplyPeriods(req);
        result.Valid[0].Remanent.Should().Be(99);
    }

    [Fact]
    public void Q_Period_InclusiveBoundary_EndDate()
    {
        // Transaction ON end date must be affected
        var req = new FilterRequest
        {
            Transactions = [new()
            {
                Date     = new DateTime(2023, 7, 31, 23, 59, 59),  // exactly on end
                Amount   = 500,
                Ceiling  = 500,
                Remanent = 0
            }],
            Q = [new QPeriod
            {
                Fixed = 77,
                Start = new DateTime(2023, 7, 1, 0, 0, 0),
                End   = new DateTime(2023, 7, 31, 23, 59, 59)
            }],
            P = [],
            K = []
        };

        var result = _svc.ApplyPeriods(req);
        result.Valid[0].Remanent.Should().Be(77);
    }

    [Fact]
    public void Q_Period_MultipleMatches_LatestStartWins()
    {
        // Two Q periods match — the one with the latest Start should win
        var tx = new Transaction
        {
            Date = new DateTime(2023, 6, 15),
            Amount = 500,
            Ceiling = 600,
            Remanent = 100
        };

        var req = new FilterRequest
        {
            Transactions = [tx],
            Q =
            [
                new QPeriod { Fixed = 10, Start = new DateTime(2023, 6,  1), End = new DateTime(2023, 6, 30) },
                new QPeriod { Fixed = 99, Start = new DateTime(2023, 6, 10), End = new DateTime(2023, 6, 30) }
                // Second has later start → should win
            ],
            P = [],
            K = []
        };

        var result = _svc.ApplyPeriods(req);
        result.Valid[0].Remanent.Should().Be(99,
            "the Q period with the latest start date should win");
    }

    [Fact]
    public void Q_Period_TieOnStartDate_FirstInListWins()
    {
        // Same Start date → first in original list wins (fixed=10, not fixed=99)
        var tx = new Transaction
        {
            Date = new DateTime(2023, 6, 15),
            Amount = 500,
            Ceiling = 600,
            Remanent = 100
        };

        var req = new FilterRequest
        {
            Transactions = [tx],
            Q =
            [
                new QPeriod { Fixed = 10, Start = new DateTime(2023, 6, 1), End = new DateTime(2023, 6, 30) },
                new QPeriod { Fixed = 99, Start = new DateTime(2023, 6, 1), End = new DateTime(2023, 6, 30) }
            ],
            P = [],
            K = []
        };

        var result = _svc.ApplyPeriods(req);
        result.Valid[0].Remanent.Should().Be(10,
            "tie on start date → first in list wins");
    }

    // ── P Period tests ────────────────────────────────────────────────────────

    [Fact]
    public void P_Period_AddsExtraToRemanent()
    {
        // Oct–Dec adds 25 → Oct expense: 50 + 25 = 75, Dec expense: 20 + 25 = 45
        var req = new FilterRequest
        {
            Transactions = ChallengeTransactions(),
            Q = [],
            P = [new PPeriod
            {
                Extra = 25,
                Start = new DateTime(2023, 10,  1,  8,  0, 0),
                End   = new DateTime(2023, 12, 31, 19, 59, 0)
            }],
            K = []
        };

        var result = _svc.ApplyPeriods(req);

        result.Valid.Single(t => t.Date.Month == 10).Remanent
              .Should().BeApproximately(75, 0.001, "50 + 25 extra");

        result.Valid.Single(t => t.Date.Month == 12).Remanent
              .Should().BeApproximately(45, 0.001, "20 + 25 extra");

        // Feb is outside P range — unchanged
        result.Valid.Single(t => t.Date.Month == 2).Remanent
              .Should().BeApproximately(25, 0.001, "Feb not in P range");
    }

    [Fact]
    public void P_Period_MultiplePeriods_AllExtrasAreSummed()
    {
        // Two P periods both matching → extras are summed (not last-wins)
        var tx = new Transaction
        {
            Date = new DateTime(2023, 6, 15),
            Amount = 250,
            Ceiling = 300,
            Remanent = 50
        };

        var req = new FilterRequest
        {
            Transactions = [tx],
            Q = [],
            P =
            [
                new PPeriod { Extra = 10, Start = new DateTime(2023, 6, 1), End = new DateTime(2023, 6, 30) },
                new PPeriod { Extra = 20, Start = new DateTime(2023, 6, 1), End = new DateTime(2023, 6, 30) }
            ],
            K = []
        };

        var result = _svc.ApplyPeriods(req);
        result.Valid[0].Remanent.Should().BeApproximately(80, 0.001,
            "50 base + 10 + 20 = 80 — all matching P extras summed");
    }

    [Fact]
    public void P_Period_AppliedAfterQ_BothRulesHonoured()
    {
        // Transaction in both Q (fixed=0) and P (extra=25)
        // Step 1: Q replaces remanent → 0
        // Step 2: P adds extra → 0 + 25 = 25
        var tx = new Transaction
        {
            Date = new DateTime(2023, 7, 15),
            Amount = 620,
            Ceiling = 700,
            Remanent = 80
        };

        var req = new FilterRequest
        {
            Transactions = [tx],
            Q = [new QPeriod
            {
                Fixed = 0,
                Start = new DateTime(2023, 7,  1,  0,  0, 0),
                End   = new DateTime(2023, 7, 31, 23, 59, 59)
            }],
            P = [new PPeriod
            {
                Extra = 25,
                Start = new DateTime(2023, 7,  1,  0,  0, 0),
                End   = new DateTime(2023, 7, 31, 23, 59, 59)
            }],
            K = []
        };

        var result = _svc.ApplyPeriods(req);
        result.Valid[0].Remanent.Should().BeApproximately(25, 0.001,
            "Q sets to 0, then P adds 25 → 25");
    }

    // ── K Period tests ────────────────────────────────────────────────────────

    [Fact]
    public void K_Period_MarToNov_SumsCorrectly()
    {
        // Challenge expected: 75 for Mar–Nov
        var adjusted = new List<Transaction>
        {
            new() { Date = new DateTime(2023, 10, 12), Remanent = 75  },  // in range
            new() { Date = new DateTime(2023,  2, 28), Remanent = 25  },  // before Mar → excluded
            new() { Date = new DateTime(2023,  7,  1), Remanent =  0  },  // in range (Q zeroed)
            new() { Date = new DateTime(2023, 12, 17), Remanent = 45  }   // after Nov → excluded
        };

        var groups = _svc.GroupByKPeriods(adjusted, [new KPeriod
        {
            Start = new DateTime(2023,  3,  1,  0,  0, 0),
            End   = new DateTime(2023, 11, 30, 23, 59, 59)
        }]);

        groups.Should().HaveCount(1);
        groups[0].Sum.Should().BeApproximately(75, 0.001,
            "only Oct(75) and Jul(0) are in range → 75");
    }

    [Fact]
    public void K_Period_FullYear_SumsCorrectly()
    {
        // Challenge expected: 145 for full year
        var adjusted = new List<Transaction>
        {
            new() { Date = new DateTime(2023, 10, 12), Remanent = 75  },
            new() { Date = new DateTime(2023,  2, 28), Remanent = 25  },
            new() { Date = new DateTime(2023,  7,  1), Remanent =  0  },
            new() { Date = new DateTime(2023, 12, 17), Remanent = 45  }
        };

        var groups = _svc.GroupByKPeriods(adjusted, [new KPeriod
        {
            Start = new DateTime(2023,  1,  1,  0,  0, 0),
            End   = new DateTime(2023, 12, 31, 23, 59, 59)
        }]);

        groups[0].Sum.Should().BeApproximately(145, 0.001,
            "25 + 0 + 75 + 45 = 145");
    }

    [Fact]
    public void K_Period_TransactionBelongsToMultipleGroups()
    {
        // Same transaction counted in two overlapping K periods independently
        var adjusted = new List<Transaction>
        {
            new() { Date = new DateTime(2023, 6, 15), Remanent = 50 }
        };

        var groups = _svc.GroupByKPeriods(adjusted,
        [
            new KPeriod { Start = new DateTime(2023, 1, 1), End = new DateTime(2023, 12, 31) },
            new KPeriod { Start = new DateTime(2023, 6, 1), End = new DateTime(2023,  6, 30) }
        ]);

        groups.Should().HaveCount(2);
        groups[0].Sum.Should().BeApproximately(50, 0.001, "full year includes the transaction");
        groups[1].Sum.Should().BeApproximately(50, 0.001, "June period also includes it");
    }

    [Fact]
    public void K_Period_EmptyTransactions_ReturnsZeroSum()
    {
        var groups = _svc.GroupByKPeriods(new List<Transaction>(), [new KPeriod
        {
            Start = new DateTime(2023, 1, 1),
            End   = new DateTime(2023, 12, 31)
        }]);

        groups[0].Sum.Should().Be(0);
    }

    [Fact]
    public void K_Period_NoPeriodsProvided_ReturnsEmptyList()
    {
        var transactions = TransactionBuilder.BuildMany(
            (new DateTime(2023, 1, 1), 250)
        );

        var groups = _svc.GroupByKPeriods(transactions, new List<KPeriod>());
        groups.Should().BeEmpty();
    }

    [Fact]
    public void K_Period_InclusiveBoundaries()
    {
        // Transactions on exactly start and end dates must be included
        var adjusted = new List<Transaction>
        {
            new() { Date = new DateTime(2023,  3,  1,  0,  0, 0), Remanent = 10 }, // = start
            new() { Date = new DateTime(2023, 11, 30, 23, 59, 59), Remanent = 20 }, // = end
            new() { Date = new DateTime(2023,  2, 28), Remanent = 99 }              // before start → excluded
        };

        var groups = _svc.GroupByKPeriods(adjusted, [new KPeriod
        {
            Start = new DateTime(2023,  3,  1,  0,  0, 0),
            End   = new DateTime(2023, 11, 30, 23, 59, 59)
        }]);

        groups[0].Sum.Should().BeApproximately(30, 0.001,
            "only start-boundary(10) and end-boundary(20) included → 30");
    }
}