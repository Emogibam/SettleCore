using MassTransit;
using Microsoft.Extensions.Logging;
using SettleCore.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SettleCore.Infrastructure.Consumers;
public class TransferRequestedConsumer : IConsumer<ITransferRequestedEvent>
{
    private readonly ILogger<TransferRequestedConsumer> _logger;

    public TransferRequestedConsumer(ILogger<TransferRequestedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ITransferRequestedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(" [x] Received Transfer Message: TransferId={TransferId}, Amount={Amount:C}, Sender={Sender}",
            msg.TransferId, msg.Amount, msg.SenderAccount);

        // We will transition this to the Saga State Machine in Day 4
        await Task.CompletedTask;
    }
}
