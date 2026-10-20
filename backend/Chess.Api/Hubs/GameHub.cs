using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

public class GameHub : Hub
{
    public Task JoinGroup(string group) =>
        Groups.AddToGroupAsync(Context.ConnectionId, group);

    public Task LeaveGroup(string group) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
}
