// Test Type     : Integration
// Validation    : Controller endpoints — request/response shapes,
//                 HTTP status codes, business rule enforcement
// Command       : dotnet test --filter "FullyQualifiedName~ControllerIntegrationTests"

using BrockLee.Controllers;
using BrockLee.DTOs;
using BrockLee.Middleware;
using BrockLee.Models;
using BrockLee.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BrockLee.Tests.Integration;

public sealed class ControllerIntegrationTests
{
    private readonly TransactionController _txController;
    private readonly PerformanceController _perfController;

    public ControllerIntegrationTests()
    {
        var txSvc = new TransactionService();
        var filterSvc = new FilterService();
        var tracker = new PerformanceTracker();

        _txController = new TransactionController(txSvc, filterSvc);
        _perfController = new PerformanceController(tracker);
    }

    // ── POST /transactions:parse ──────────────────────────────────────────────

    [Fact]
    public void Parse_ValidExpenses_Returns200WithTransactions()
    {
        var request = new ParseRequest
        {
            Expenses =
            [
                new() { Date = new DateTime(2023, 10, 12), Amount = 250 },
                new() { Date = new DateTime(2023,  2, 28), Amount = 375 }
            ]
        };

        var result = _txController.Parse(request) as OkObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var response = result.Value as ParseResponse;
        response.Should().NotBeNull();
        response!.Transactions.Should().HaveCount(2);
        response.TotalRemanent.Should().BeApproximately(75, 0.001); // 50 + 25
    }

    [Fact]
    public void Parse_EmptyExpenses_Returns400()
    {
        var request = new ParseRequest { Expenses = [] };

        var result = _txController.Parse(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Parse_NullExpenses_Returns400()
    {
        var request = new ParseRequest { Expenses = null! };

        var result = _txController.Parse(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── POST /transactions:validator ──────────────────────────────────────────

    [Fact]
    public void Validate_ZeroWage_Returns400()
    {
        var request = new ValidatorRequest { Wage = 0, Transactions = [] };

        var result = _txController.Validate(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Validate_ValidRequest_Returns200()
    {
        var request = new ValidatorRequest
        {
            Wage = 50_000,
            Transactions =
            [
                new() { Date = new DateTime(2023, 1, 1), Amount = 250, Ceiling = 300, Remanent = 50 }
            ]
        };

        var result = _txController.Validate(request) as OkObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
    }

    // ── POST /transactions:filter ─────────────────────────────────────────────

    [Fact]
    public void Filter_EmptyTransactions_Returns400()
    {
        var request = new FilterRequest { Transactions = [], Q = [], P = [], K = [] };

        var result = _txController.Filter(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Filter_ValidRequest_Returns200WithKGroups()
    {
        var request = new FilterRequest
        {
            Transactions =
            [
                new() { Date = new DateTime(2023, 6, 15), Amount = 250, Ceiling = 300, Remanent = 50 }
            ],
            Q = [],
            P = [],
            K = [new() { Start = new DateTime(2023, 1, 1), End = new DateTime(2023, 12, 31) }]
        };

        var result = _txController.Filter(request) as OkObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
    }

    // ── GET /performance ──────────────────────────────────────────────────────

    [Fact]
    public void Performance_Returns200WithExpectedShape()
    {
        var result = _perfController.GetPerformance() as OkObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        // Validate response shape has the three required fields
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("time", out _).Should().BeTrue("time field required");
        root.TryGetProperty("memory", out _).Should().BeTrue("memory field required");
        root.TryGetProperty("threads", out _).Should().BeTrue("threads field required");
    }
}