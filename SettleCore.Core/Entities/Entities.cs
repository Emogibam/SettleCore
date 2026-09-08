using System;

namespace SettleCore.Core.Entities;

/// <summary>
/// Represents a customer's financial account and current balance.
/// </summary>
public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Immutable ledger audit entry maintaining double-entry credit/debit transaction history.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string EntryType { get; set; } = string.Empty; // "CREDIT" or "DEBIT"
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Operational record tracking transfer lifecycle status (PENDING, COMPLETED, REVERSED).
/// </summary>
public class TransferRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferId { get; set; }
    public string SenderAccount { get; set; } = string.Empty;
    public string DestinationAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, COMPLETED, REVERSED
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReversedAtUtc { get; set; }
}
