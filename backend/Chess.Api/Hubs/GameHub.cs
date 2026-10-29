using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

public class GameHub(ILogger<GameHub> logger) : Hub
{
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected to SignalR: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
            logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        else
            logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

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
