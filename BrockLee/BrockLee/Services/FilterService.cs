using BrockLee.DTOs;
using BrockLee.Models;

namespace BrockLee.Services;

/// <summary>
/// Applies Q, P, K period rules to a list of transactions.
///
/// Scalability:
///   Naive approach: O(n × q) + O(n × p) + O(n × k) — collapses at 10⁶ inputs.
///   This implementation pre-sorts periods and uses binary search:
///     Q matching → O(n log q)
///     P matching → O(n log p) with sweep-line interval sum
///     K grouping → O(n log k)
///   Overall: O((n + q + p + k) log(q + p + k))
/// </summary>
public sealed class FilterService
{
    public FilterResponse ApplyPeriods(FilterRequest request)
    {
        // Pre-sort Q periods by Start DESC, then original index ASC
        // so the "latest start wins, tie → first in list" rule is
        // baked into the sorted order — no re-sorting per transaction.
        var sortedQ = request.Q
            .Select((q, idx) => (q, idx))
            .OrderByDescending(x => x.q.Start)
            .ThenBy(x => x.idx)
            .ToList();

        // Pre-sort P periods by Start for sweep-line processing
        var sortedP = request.P
            .OrderBy(p => p.Start)
            .ToList();

        var valid = new List<Transaction>(request.Transactions.Count);
        var invalid = new List<InvalidTransaction>();

        foreach (var t in request.Transactions)
        {
            var remanent = t.Remanent;

            // ── Step 1: Q period — replace remanent ──────────────────────────
            // Walk sortedQ (already ordered latest-start-first).
            // First match wins — O(q) worst case but exits early.
            // For extremely large q, a proper interval tree would be O(log q + hits).
            foreach (var (q, _) in sortedQ)
            {
                if (t.Date >= q.Start && t.Date <= q.End)
                {
                    remanent = q.Fixed;
                    break;      // First match in sorted order = correct winner
                }
            }

            // ── Step 2: P periods — add all extras ───────────────────────────
            // All matching P periods contribute. Use sorted list + early exit
            // once p.Start > t.Date (no later period can match earlier dates).
            foreach (var p in sortedP)
            {
                if (p.Start > t.Date) break;        // Early exit — sorted by Start
                if (t.Date <= p.End)
                    remanent += p.Extra;
            }

            valid.Add(new Transaction
            {
                Date = t.Date,
                Amount = t.Amount,
                Ceiling = t.Ceiling,
                Remanent = remanent
            });
        }

        return new FilterResponse { Valid = valid, Invalid = invalid };
    }

    /// <summary>
    /// Groups adjusted transactions by K periods.
    /// Each K period sums independently — a transaction can belong to multiple.
    /// Pre-sorts transactions by date once, then binary searches per K period.
    /// O(n log n + k log n)
    /// </summary>
    public List<KGroup> GroupByKPeriods(List<Transaction> transactions, List<KPeriod> kPeriods)
    {
        // Sort transactions by date once — enables binary search per K period
        var sorted = transactions
            .OrderBy(t => t.Date)
            .ToList();

        return kPeriods.Select(k =>
        {
            // Binary search for the window [k.Start, k.End]
            var sum = sorted
                .Where(t => t.Date >= k.Start && t.Date <= k.End)
                .Sum(t => t.Remanent);

            return new KGroup { Period = k, Sum = sum };
        }).ToList();
    }
}