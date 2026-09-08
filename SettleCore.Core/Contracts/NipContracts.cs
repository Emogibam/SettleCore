using System;

namespace SettleCore.Core.Contracts;

/// <summary>
/// Status query response returned by the Mock NIP Switch.
/// </summary>
public record NipStatusResponse(
    Guid TransferId,
    string Status,
    string SwitchReference,
    DateTime ReconciledAtUtc
);

/// <summary>
/// Published when Hangfire successfully reconciles a pending transaction with the switch.
/// </summary>
public interface INipReconciliationSuccess
{
    Guid TransferId { get; }
    string SwitchReference { get; }
    DateTime ReconciledAtUtc { get; }
}

public record NipReconciliationSuccess(
    Guid TransferId,
    string SwitchReference,
    DateTime ReconciledAtUtc
) : INipReconciliationSuccess;

/// <summary>
/// Triggered when an outbound transfer to the NIP Switch times out.
/// </summary>
public interface INipSwitchTimeoutEvent
{
    Guid TransferId { get; }
    string Reason { get; }
    DateTime TimedOutAtUtc { get; }
}

public record NipSwitchTimeoutEvent(
    Guid TransferId,
    string Reason = "NIP Switch Gateway Timeout",
    DateTime TimedOutAtUtc = default
) : INipSwitchTimeoutEvent
{
    public DateTime TimedOutAtUtc { get; init; } = TimedOutAtUtc == default ? DateTime.UtcNow : TimedOutAtUtc;
}

/// <summary>
/// Published when Hangfire reconciliation confirms a definitive failure (or exhausted retries) from the switch.
/// </summary>
public interface INipReconciliationFailedEvent
{
    Guid TransferId { get; }
    string Reason { get; }
    DateTime FailedAtUtc { get; }
}

public record NipReconciliationFailed(
    Guid TransferId,
    string Reason,
    DateTime FailedAtUtc
) : INipReconciliationFailedEvent;

/// <summary>
/// Saga compensation command instructing the ledger to refund the sender account.
/// </summary>
public interface IReverseSenderDebitCommand
{
    Guid TransferId { get; }
    string SenderAccountId { get; }
    decimal Amount { get; }
    string FailureReason { get; }
    DateTime TimestampUtc { get; }
}

public record ReverseSenderDebitCommand(
    Guid TransferId,
    string SenderAccountId,
    decimal Amount,
    string FailureReason,
    DateTime TimestampUtc
) : IReverseSenderDebitCommand;
