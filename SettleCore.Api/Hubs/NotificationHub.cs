using Microsoft.AspNetCore.SignalR;
using SettleCore.Infrastructure.Hubs;
using System.Threading.Tasks;

namespace SettleCore.Api.Hubs;

/// <summary>
/// API SignalR Hub for real-time transaction status notifications.
/// Inherits from SettleCore.Infrastructure.Hubs.NotificationHub.
/// </summary>
public class NotificationHub : SettleCore.Infrastructure.Hubs.NotificationHub
{
}
