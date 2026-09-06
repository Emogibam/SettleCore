using System;
using System.Threading.Tasks;

namespace SettleCore.Infrastructure.Services;

/// <summary>
/// Background reconciliation service invoked by Hangfire to poll external switch status.
/// </summary>
public interface INipReconciliationService
{
    /// <summary>
    /// Polls the payment switch for the status of a timed-out transfer.
    /// </summary>
    /// <param name="transferId">The unique identifier of the transfer transaction.</param>
    Task PollTransactionStatusAsync(Guid transferId);
}
