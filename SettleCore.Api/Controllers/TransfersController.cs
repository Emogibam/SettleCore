using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SettleCore.Core.Contracts;
using SettleCore.Core.Entities;
using SettleCore.Core.Saga;
using SettleCore.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SettleCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly SettleCoreDbContext _dbContext;

    public TransfersController(
        IPublishEndpoint publishEndpoint,
        SettleCoreDbContext dbContext)
    {
        _publishEndpoint = publishEndpoint;
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> InitiateTransfer([FromBody] TransferRequest request)
    {
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].ToString();
        var transferId = Guid.NewGuid();

        // 1. Persist initial operational transfer record
        var record = new TransferRecord
        {
            Id = Guid.NewGuid(),
            TransferId = transferId,
            SenderAccount = request.SenderAccount,
            DestinationAccount = request.RecipientAccount,
            Amount = request.Amount,
            Status = "PENDING_SETTLEMENT",
            CreatedAtUtc = DateTime.UtcNow
        };
        await _dbContext.TransferRecords.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        // 2. Publish event to RabbitMQ (Non-blocking!)
        await _publishEndpoint.Publish<ITransferRequestedEvent>(new
        {
            TransferId = transferId,
            SenderAccount = request.SenderAccount,
            RecipientAccount = request.RecipientAccount,
            Amount = request.Amount,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Accepted(new
        {
            TransferId = transferId,
            Status = "PENDING_SETTLEMENT",
            Message = "Transfer queued successfully for settlement processing."
        });
    }

    [HttpGet("{transferId:guid}")]
    public async Task<IActionResult> GetTransferById([FromRoute] Guid transferId)
    {
        var transfer = await _dbContext.TransferRecords
            .FirstOrDefaultAsync(t => t.TransferId == transferId);
        var sagaState = await _dbContext.Set<TransferState>()
            .FirstOrDefaultAsync(s => s.CorrelationId == transferId);

        if (transfer == null && sagaState == null)
            return NotFound(new { Message = $"Transfer {transferId} not found." });

        return Ok(new
        {
            TransferId = transferId,
            TransferRecordStatus = transfer?.Status ?? "NOT_FOUND",
            SagaState = sagaState?.CurrentState ?? "NOT_FOUND",
            Amount = transfer?.Amount ?? sagaState?.Amount ?? 0,
            SenderAccount = transfer?.SenderAccount ?? sagaState?.SenderAccount,
            DestinationAccount = transfer?.DestinationAccount ?? sagaState?.DestinationAccount,
            FailureReason = transfer?.FailureReason ?? sagaState?.FailureReason,
            SwitchReference = sagaState?.SwitchReference,
            CreatedAtUtc = transfer?.CreatedAtUtc ?? sagaState?.CreatedAtUtc,
            ReversedAtUtc = transfer?.ReversedAtUtc
        });
    }

    [HttpGet("accounts/{accountNumber}")]
    public async Task<IActionResult> GetAccount([FromRoute] string accountNumber)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
        var ledger = await _dbContext.LedgerEntries
            .Where(l => l.AccountNumber == accountNumber)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(10)
            .ToListAsync();

        if (account == null)
            return NotFound(new { Message = $"Account '{accountNumber}' not found." });

        return Ok(new
        {
            account.AccountNumber,
            account.Balance,
            account.UpdatedAtUtc,
            RecentLedgerEntries = ledger
        });
    }
}

public record TransferRequest(string SenderAccount, string RecipientAccount, decimal Amount);