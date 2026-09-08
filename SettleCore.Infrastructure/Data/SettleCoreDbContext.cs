using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using SettleCore.Core.Entities;
using SettleCore.Infrastructure.Data.Maps;
using System.Collections.Generic;

namespace SettleCore.Infrastructure.Data;

public class SettleCoreDbContext : SagaDbContext
{
    public SettleCoreDbContext(DbContextOptions<SettleCoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<TransferRecord> TransferRecords => Set<TransferRecord>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new TransferStateMap();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.Property(e => e.AccountNumber).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Balance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<TransferRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransferId);
            entity.Property(e => e.SenderAccount).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DestinationAccount).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(512);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransferId);
            entity.HasIndex(e => e.AccountNumber);
            entity.Property(e => e.AccountNumber).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.EntryType).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(256).IsRequired();
        });
    }
}
