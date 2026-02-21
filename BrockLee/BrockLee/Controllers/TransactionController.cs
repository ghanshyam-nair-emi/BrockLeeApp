using BrockLee.DTOs;
using BrockLee.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrockLee.Controllers;

[ApiController]
[Route("blackrock/challenge/v1")]
public class TransactionController : ControllerBase
{
    private readonly TransactionService _txService;
    private readonly FilterService _filterService;

    public TransactionController(TransactionService txService, FilterService filterService)
    {
        _txService = txService;
        _filterService = filterService;
    }

    /// <summary>
    /// POST /blackrock/challenge/v1/transactions:parse
    /// Enriches raw expenses with ceiling and remanent.
    /// </summary>
    [HttpPost("transactions:parse")]
    public IActionResult Parse([FromBody] ParseRequest request)
    {
        if (request.Expenses is null || request.Expenses.Count == 0)
            return BadRequest(new { message = "Expenses list is empty or null." });

        var result = _txService.Parse(request.Expenses);
        return Ok(result);
    }

    /// <summary>
    /// POST /blackrock/challenge/v1/transactions:validator
    /// Validates transactions against wage constraints and data integrity.
    /// </summary>
    [HttpPost("transactions:validator")]
    public IActionResult Validate([FromBody] ValidatorRequest request)
    {
        if (request.Wage <= 0)
            return BadRequest(new { message = "Wage must be greater than 0." });

        var result = _txService.Validate(request);
        return Ok(result);
    }

    /// <summary>
    /// POST /blackrock/challenge/v1/transactions:filter
    /// Applies Q, P, K period rules to transactions.
    /// </summary>
    [HttpPost("transactions:filter")]
    public IActionResult Filter([FromBody] FilterRequest request)
    {
        if (request.Transactions is null || request.Transactions.Count == 0)
            return BadRequest(new { message = "Transactions list is empty or null." });

        var filtered = _filterService.ApplyPeriods(request);

        // Also return K-period groupings for transparency
        var groups = _filterService.GroupByKPeriods(filtered.Valid, request.K);

        return Ok(new
        {
            valid = filtered.Valid,
            invalid = filtered.Invalid,
            kGroups = groups.Select(g => new
            {
                start = g.Period.Start,
                end = g.Period.End,
                amount = Math.Round(g.Sum, 2)
            })
        });
    }
}