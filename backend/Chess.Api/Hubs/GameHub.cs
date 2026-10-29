using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

public class GameHub(ILogger<GameHub> logger) : Hub
{
    public Task JoinGroup(string group)
    {
        logger.LogDebug("Client {ConnectionId} joined SignalR group {Group}", Context.ConnectionId, group);
        return Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public Task LeaveGroup(string group)
    {
        logger.LogDebug("Client {ConnectionId} left SignalR group {Group}", Context.ConnectionId, group);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }
}
