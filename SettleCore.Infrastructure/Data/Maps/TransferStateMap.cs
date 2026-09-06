using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Core.Saga;

namespace SettleCore.Infrastructure.Data.Maps;

public class TransferStateMap : SagaClassMap<TransferState>
{
    protected override void Configure(EntityTypeBuilder<TransferState> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.SenderAccount).HasMaxLength(20);
        entity.Property(x => x.DestinationAccount).HasMaxLength(20);
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
        entity.Property(x => x.SwitchReference).HasMaxLength(64);
        entity.Property(x => x.FailureReason).HasMaxLength(512);

        entity.Property(x => x.Version);
    }
}
