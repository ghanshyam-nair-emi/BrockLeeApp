using BrockLee.DTOs;
using BrockLee.Models;

namespace BrockLee.Services;

/// <summary>
/// Handles transaction parsing and validation.
///
/// Parse:    Expense → Transaction (adds ceiling + remanent)
/// Validate: Transaction list → valid / invalid / duplicate buckets
/// </summary>
public class TransactionService
{
    // ── Parse ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 of the pipeline.
    /// For each expense:
    ///   ceiling  = amount if amount % 100 == 0, else ⌈amount / 100⌉ × 100
    ///   remanent = ceiling - amount
    /// </summary>
    public ParseResponse Parse(List<Expense> expenses)
    {
        var transactions = expenses
            .Select(e =>
            {
                var ceiling = ComputeCeiling(e.Amount);
                var remanent = ceiling - e.Amount;

                return new Transaction
                {
                    Date = e.Date,
                    Amount = e.Amount,
                    Ceiling = ceiling,
                    Remanent = remanent
                };
            })
            .ToList();

        return new ParseResponse
        {
            Transactions = transactions,
            TotalRemanent = Math.Round(transactions.Sum(t => t.Remanent), 2),
            TotalCeiling = Math.Round(transactions.Sum(t => t.Ceiling), 2),
            TotalExpense = Math.Round(transactions.Sum(t => t.Amount), 2)
        };
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates a list of enriched transactions.
    ///
    /// Rules checked (in order):
    ///   1. Wage must be > 0  (returns all as invalid if not)
    ///   2. Amount ∈ [0, 500,000)
    ///   3. Ceiling matches recomputed expected ceiling
    ///   4. Remanent == ceiling - amount
    ///   5. Duplicate timestamps (tᵢ == tⱼ) → separate duplicates bucket
    /// </summary>
    public ValidatorResponse Validate(ValidatorRequest request)
    {
        var valid = new List<Transaction>();
        var invalid = new List<InvalidTransaction>();
        var duplicates = new List<InvalidTransaction>();

        // Guard: invalid wage poisons every transaction
        if (request.Wage <= 0)
        {
            foreach (var t in request.Transactions)
                invalid.Add(Invalidate(t, "Invalid wage: wage must be greater than 0."));
            return new ValidatorResponse { Valid = valid, Invalid = invalid, Duplicates = duplicates };
        }

        var seenDates = new HashSet<DateTime>();

        foreach (var t in request.Transactions)
        {
            // ── Duplicate check (before other rules) ─────────────────────────
            if (!seenDates.Add(t.Date))
            {
                duplicates.Add(Invalidate(t,
                    $"Duplicate timestamp: {t.Date:yyyy-MM-dd HH:mm:ss}"));
                continue;
            }

            var errors = new List<string>();

            // ── Amount range ──────────────────────────────────────────────────
            if (t.Amount < 0 || t.Amount >= 500_000)
                errors.Add($"Amount {t.Amount} is out of range [0, 500,000).");

            // ── Ceiling integrity ─────────────────────────────────────────────
            var expectedCeiling = ComputeCeiling(t.Amount);
            if (Math.Abs(t.Ceiling - expectedCeiling) > 0.001)
                errors.Add(
                    $"Ceiling {t.Ceiling} does not match expected {expectedCeiling} " +
                    $"for amount {t.Amount}.");

            // ── Remanent integrity ────────────────────────────────────────────
            var expectedRemanent = t.Ceiling - t.Amount;
            if (Math.Abs(t.Remanent - expectedRemanent) > 0.001)
                errors.Add(
                    $"Remanent {t.Remanent} does not equal ceiling ({t.Ceiling}) " +
                    $"minus amount ({t.Amount}) = {expectedRemanent}.");

            if (errors.Count > 0)
                invalid.Add(Invalidate(t, string.Join(" | ", errors)));
            else
                valid.Add(t);
        }

        return new ValidatorResponse
        {
            Valid = valid,
            Invalid = invalid,
            Duplicates = duplicates
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Next multiple of 100 ≥ amount.
    /// If amount is already a multiple of 100, returns amount unchanged.
    /// e.g. 250 → 300 | 400 → 400 | 1519 → 1600
    /// </summary>
    public static double ComputeCeiling(double amount)
        => amount % 100 == 0
            ? amount
            : Math.Ceiling(amount / 100.0) * 100;

    private static InvalidTransaction Invalidate(Transaction t, string message)
        => new()
        {
            Date = t.Date,
            Amount = t.Amount,
            Ceiling = t.Ceiling,
            Remanent = t.Remanent,
            Message = message
        };
}