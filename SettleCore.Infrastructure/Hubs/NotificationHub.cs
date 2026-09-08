using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SettleCore.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub that manages real-time client WebSocket connections and transaction groups.
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// Places the caller's connection into an isolated group keyed by the transaction ID.
    /// </summary>
    /// <param name="transferId">The transaction/transfer identifier.</param>
    public async Task JoinTransferGroup(string transferId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, transferId);
    }
}
