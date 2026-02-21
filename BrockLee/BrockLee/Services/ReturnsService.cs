using BrockLee.DTOs;
using BrockLee.Infrastructure;
using BrockLee.Models;
using BrockLee.Models.Entities;

namespace BrockLee.Services;

/// <summary>
/// Option B — Python is the PRIMARY compute engine.
/// All projection math is delegated to the Python sidecar.
/// This service handles: pipeline orchestration, K-grouping, logging.
/// </summary>
public sealed class ReturnsService
{
    private const double DefaultInflation = 0.055;
    private const int    RetirementAge    = 60;
    private const int    MinYears         = 5;

    private readonly FilterService       _filter;
    private readonly LogService          _log;
    private readonly CacheService        _cache;
    private readonly PythonBridgeService _python;
    private readonly ILogger<ReturnsService> _logger;

    public ReturnsService(
        FilterService       filter,
        LogService          log,
        CacheService        cache,
        PythonBridgeService python,
        ILogger<ReturnsService> logger)
    {
        _filter = filter;
        _log    = log;
        _cache  = cache;
        _python = python;
        _logger = logger;
    }

    public async Task<ReturnsResponse> CalculateAsync(
        ReturnsRequest request, bool isNps, double responseTimeMs)
    {
        var cacheKey = CacheService.HashKey(
            isNps ? "returns:nps" : "returns:index", request);

        return await _cache.GetOrCreateAsync(cacheKey, _cache.ComputeTtl, async () =>
        {
            // ── Step 1: Apply Q/P period rules (C# — pure logic) ─────────────
            var filterReq = new FilterRequest
            {
                Q = request.Q, P = request.P, K = request.K,
                Transactions = request.Transactions
            };
            var filtered = _filter.ApplyPeriods(filterReq);
            var groups   = _filter.GroupByKPeriods(filtered.Valid, request.K);

            int    years        = request.Age < RetirementAge
                                    ? RetirementAge - request.Age : MinYears;
            double annualIncome = request.Wage * 12;
            double inflation    = request.Inflation > 0
                                    ? request.Inflation : DefaultInflation;

            // ── Step 2: Project via Python (primary engine) ───────────────────
            var savingsByDates = new List<SavingsByDate>();

            foreach (var g in groups)
            {
                // Python computes both NPS and Index in one call
                var fullProjection = await _python.GetFullProjectionAsync(
                    principal    : g.Sum,
                    age          : request.Age,
                    annualIncome : annualIncome,
                    inflation    : inflation);

                var chosen = isNps ? fullProjection.Nps : fullProjection.Index;

                savingsByDates.Add(new SavingsByDate
                {
                    Start      = g.Period.Start,
                    End        = g.Period.End,
                    Amount     = Math.Round(g.Sum,           2),
                    Profits    = Math.Round(chosen.Profits,    2),
                    TaxBenefit = Math.Round(chosen.TaxBenefit, 2)
                });
            }

            var response = new ReturnsResponse
            {
                TransactionsTotalAmount  = Math.Round(filtered.Valid.Sum(t => t.Amount),  2),
                TransactionsTotalCeiling = Math.Round(filtered.Valid.Sum(t => t.Ceiling), 2),
                SavingsByDates           = savingsByDates
            };

            // ── Step 3: Log headline K-period (fire-and-forget) ───────────────
            var headline = groups
                .OrderByDescending(g => (g.Period.End - g.Period.Start).TotalDays)
                .FirstOrDefault();

            if (headline is not null)
            {
                var headlineProj = await _python.GetFullProjectionAsync(
                    headline.Sum, request.Age, annualIncome, inflation);

                var chosen = isNps ? headlineProj.Nps : headlineProj.Index;

                await _log.LogAsync(new UserLogEntity
                {
                    Name               = request.Name,
                    Age                = request.Age,
                    Wage               = (decimal)request.Wage,
                    AnnualIncome       = (decimal)annualIncome,
                    ExpenseCount       = request.Transactions.Count,
                    TotalExpenseAmount = (decimal)response.TransactionsTotalAmount,
                    TotalRemanent      = (decimal)headline.Sum,
                    NpsRealValue       = isNps
                        ? (decimal)Math.Round(chosen.Profits + headline.Sum, 2) : 0,
                    IndexRealValue     = !isNps
                        ? (decimal)Math.Round(chosen.Profits + headline.Sum, 2) : 0,
                    TaxBenefit         = (decimal)Math.Round(chosen.TaxBenefit, 2),
                    YearsToRetirement  = years,
                    ResponseTimeMs     = responseTimeMs
                });
            }

            return response;
        });
    }

    // Tax helper — still needed for validation elsewhere
    public static double CalculateTax(double income)
    {
        if (income <= 0) return 0;
        double tax = 0;
        if (income > 1_500_000) tax += (income - 1_500_000) * 0.30;
        if (income > 1_200_000) tax += (Math.Min(income, 1_500_000) - 1_200_000) * 0.20;
        if (income > 1_000_000) tax += (Math.Min(income, 1_200_000) - 1_000_000) * 0.15;
        if (income >   700_000) tax += (Math.Min(income, 1_000_000) -   700_000) * 0.10;
        return tax;
    }
}