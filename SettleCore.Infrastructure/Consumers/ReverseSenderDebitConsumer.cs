using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SettleCore.Core.Contracts;
using SettleCore.Core.Entities;
using SettleCore.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace SettleCore.Infrastructure.Consumers;

/// <summary>
/// Handles saga compensation commands by executing transactional ledger reversals to refund debited sender accounts.
/// </summary>
public class ReverseSenderDebitConsumer : IConsumer<IReverseSenderDebitCommand>
{
    private readonly SettleCoreDbContext _dbContext;
    private readonly ILogger<ReverseSenderDebitConsumer> _logger;

    public ReverseSenderDebitConsumer(
        SettleCoreDbContext dbContext,
        ILogger<ReverseSenderDebitConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<IReverseSenderDebitCommand> context)
    {
        var command = context.Message;

        _logger.LogWarning(
            "[Compensation Engine] Received ReverseSenderDebitCommand. TransferId: {TransferId}, SenderAccountId: {SenderAccountId}, Amount: {Amount:C}, FailureReason: {FailureReason}",
            command.TransferId, command.SenderAccountId, command.Amount, command.FailureReason);

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(context.CancellationToken);

            try
            {
                // 1. Credit the customer balance back to the database
                var account = await _dbContext.Accounts
                    .FirstOrDefaultAsync(a => a.AccountNumber == command.SenderAccountId, context.CancellationToken);

                if (account == null)
                {
                    _logger.LogInformation(
                        "[Compensation Engine] Account {SenderAccountId} not found. Creating account with refunded balance {Amount:C}.",
                        command.SenderAccountId, command.Amount);

                    account = new Account
                    {
                        Id = Guid.NewGuid(),
                        AccountNumber = command.SenderAccountId,
                        Balance = command.Amount,
                        UpdatedAtUtc = DateTime.UtcNow
                    };
                    await _dbContext.Accounts.AddAsync(account, context.CancellationToken);
                }
                else
                {
                    var previousBalance = account.Balance;
                    account.Balance += command.Amount;
                    account.UpdatedAtUtc = DateTime.UtcNow;

                    _logger.LogInformation(
                        "[Compensation Engine] Refunded {Amount:C} to Account {SenderAccountId}. Previous Balance: {PreviousBalance:C}, New Balance: {NewBalance:C}",
                        command.Amount, command.SenderAccountId, previousBalance, account.Balance);
                }

                // 2. Append immutable ledger audit entry
                var ledgerEntry = new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TransferId = command.TransferId,
                    AccountNumber = command.SenderAccountId,
                    Amount = command.Amount,
                    EntryType = "CREDIT",
                    Description = $"Saga Reversal Refund: {command.FailureReason}",
                    CreatedAtUtc = DateTime.UtcNow
                };
                await _dbContext.LedgerEntries.AddAsync(ledgerEntry, context.CancellationToken);

                // 3. Update transfer record status to REVERSED
                var transferRecord = await _dbContext.TransferRecords
                    .FirstOrDefaultAsync(t => t.TransferId == command.TransferId, context.CancellationToken);

                if (transferRecord != null)
                {
                    transferRecord.Status = "REVERSED";
                    transferRecord.FailureReason = command.FailureReason;
                    transferRecord.ReversedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    transferRecord = new TransferRecord
                    {
                        Id = Guid.NewGuid(),
                        TransferId = command.TransferId,
                        SenderAccount = command.SenderAccountId,
                        DestinationAccount = string.Empty,
                        Amount = command.Amount,
                        Status = "REVERSED",
                        FailureReason = command.FailureReason,
                        CreatedAtUtc = command.TimestampUtc,
                        ReversedAtUtc = DateTime.UtcNow
                    };
                    await _dbContext.TransferRecords.AddAsync(transferRecord, context.CancellationToken);
                }

                // 4. Save and commit atomically
                await _dbContext.SaveChangesAsync(context.CancellationToken);
                await transaction.CommitAsync(context.CancellationToken);

                _logger.LogInformation(
                    "[Compensation Engine] Successfully executed ledger compensation for TransferId: {TransferId}, Account: {SenderAccountId}, Status: REVERSED, NewBalance: {Balance:C}",
                    command.TransferId, command.SenderAccountId, account.Balance);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(context.CancellationToken);
                _logger.LogError(
                    ex,
                    "[Compensation Engine] Ledger reversal compensation failed for TransferId: {TransferId}, SenderAccountId: {SenderAccountId}",
                    command.TransferId, command.SenderAccountId);
                throw;
            }
        });
    }
}
