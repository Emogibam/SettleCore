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
