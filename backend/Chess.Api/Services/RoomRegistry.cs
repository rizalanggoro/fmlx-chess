using System.Collections.Concurrent;
using Api.Enums;
using Api.Hubs;
using Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public class RoomRegistry(IHubContext<GameHub> hub, ILogger<RoomRegistry> logger)
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public IEnumerable<GameRoom> All => _rooms.Values;

    public GameRoom Create(RoomMode mode)
    {
        string id;
        do
        {
            id = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        } while (_rooms.ContainsKey(id));

        var manager = new GameManager(new Board(), new Player("White", PieceColor.White),
            new Player("Black", PieceColor.Black), TimeSpan.FromMinutes(10));
        var room = new GameRoom(id, mode, manager);
        Hook(room);
        _rooms[id] = room;
        return room;
    }

    public GameRoom? Find(string id)
    {
        return _rooms.TryGetValue(id, out var room) ? room : null;
    }

    public void NotifyRoom(string roomId)
    {
        _ = SendSafeAsync(GameGroups.Room(roomId));
    }

    public void NotifyLobby()
    {
        _ = SendSafeAsync(GameGroups.Lobby);
    }

    private void Hook(GameRoom room)
    {
        room.Manager.PieceMoved += (_, _) => NotifyRoom(room.Id);
        room.Manager.PieceCaptured += (_, _) => NotifyRoom(room.Id);
        room.Manager.TurnChanged += (_, _) => NotifyRoom(room.Id);
        room.Manager.CheckDeclared += (_, _) => NotifyRoom(room.Id);
        room.Manager.GameEnded += (_, _) => NotifyRoom(room.Id);
        room.Manager.DrawOffered += (_, _) => NotifyRoom(room.Id);
    }

    private async Task SendSafeAsync(string group)
    {
        try
        {
            await hub.Clients.Group(group).SendAsync("GameChanged");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast game change");
        }
    }
}