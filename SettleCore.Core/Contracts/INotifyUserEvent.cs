using System;

namespace SettleCore.Core.Contracts;

/// <summary>
/// Event contract for broadcasting real-time transaction status updates to clients over SignalR.
/// </summary>
public interface INotifyUserEvent
{
    Guid TransferId { get; }
    string Status { get; }
    string Message { get; }
    DateTime TimestampUtc { get; }
}

public record NotifyUserEvent(
    Guid TransferId,
    string Status,
    string Message,
    DateTime TimestampUtc
) : INotifyUserEvent;
