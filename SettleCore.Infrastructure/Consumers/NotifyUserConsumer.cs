using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SettleCore.Core.Contracts;
using SettleCore.Infrastructure.Hubs;
using System;
using System.Threading.Tasks;

namespace SettleCore.Infrastructure.Consumers;

/// <summary>
/// Consumes INotifyUserEvent and broadcasts real-time transaction updates to client devices over SignalR WebSockets.
/// </summary>
public class NotifyUserConsumer : IConsumer<INotifyUserEvent>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotifyUserConsumer> _logger;

    public NotifyUserConsumer(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotifyUserConsumer> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<INotifyUserEvent> context)
    {
        var notification = context.Message;
        var groupName = notification.TransferId.ToString();

        _logger.LogInformation(
            "[SignalR Push Engine] Broadcasting ReceiveTransferStatus to group '{GroupName}'. TransferId: {TransferId}, Status: {Status}, Message: {Message}",
            groupName, notification.TransferId, notification.Status, notification.Message);

        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveTransferStatus", new
        {
            TransferId = notification.TransferId,
            Status = notification.Status,
            Message = notification.Message,
            TimestampUtc = notification.TimestampUtc
        }, context.CancellationToken);

        _logger.LogInformation(
            "[SignalR Push Engine] Real-time push notification successfully broadcast for TransferId: {TransferId} at {TimestampUtc}",
            notification.TransferId, notification.TimestampUtc);
    }
}
