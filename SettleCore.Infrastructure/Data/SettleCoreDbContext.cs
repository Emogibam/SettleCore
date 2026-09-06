using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using SettleCore.Infrastructure.Data.Maps;
using System.Collections.Generic;

namespace SettleCore.Infrastructure.Data;

public class SettleCoreDbContext : SagaDbContext
{
    public SettleCoreDbContext(DbContextOptions<SettleCoreDbContext> options)
        : base(options)
    {
    }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new TransferStateMap();
        }
    }
}
