using System.Collections.Concurrent;
using Api.DTOs;
using Api.Enums;
using Api.Events;
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
        room.Manager.PieceMovedEventHandler += (_, _) => NotifyBoard(room.Id);
        room.Manager.PieceCapturedEventHandler += (_, e) => NotifyPieceCaptured(room.Id, e);
        room.Manager.TurnChangedEventHandler += (_, _) => NotifyRoom(room.Id);
        room.Manager.CheckDeclaredEventHandler += (_, _) => NotifyRoom(room.Id);
        room.Manager.GameEndedEventHandler += (_, e) => NotifyGameEnded(room.Id, e);
        room.Manager.DrawOfferedEventHandler += (_, e) => NotifyDrawOffered(room.Id, e);
        room.Manager.TimerTickedEventHandler += (_, e) => NotifyTimerTick(room.Id, e);
    }

    public void NotifyBoard(string roomId)
    {
        if (Find(roomId) is not { } room)
            return;

        var tiles = new List<TileDto>(64);
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var p = room.Manager.GameBoard.Tiles[c, r].Piece;
            tiles.Add(new TileDto(c, r, p?.Color, p?.Type));
        }

        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "BoardChanged",
            new BoardChangedDto(roomId, tiles));
    }

    private void NotifyPieceCaptured(string roomId, PieceCapturedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "PieceCaptured",
            new PieceCapturedDto(roomId, e.CapturedPiece.Color, e.CapturedPiece.Type));
    }

    private void NotifyTimerTick(string roomId, TimerTickEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "TimerTick",
            new TimerTickDto(roomId, e.Color, (int)e.TimeRemaining.TotalSeconds));
    }

    private void NotifyGameEnded(string roomId, GameEndedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "GameEnded",
            new GameEndedDto(roomId, e.Result));
    }

    private void NotifyDrawOffered(string roomId, DrawOfferedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "DrawOffered",
            new DrawOfferedDto(roomId, e.Offerer.Color, e.Offerer.Name));
    }

    private async Task SendSafeAsync(string group, string method = "GameChanged", object? payload = null)
    {
        try
        {
            if (payload is null)
                await hub.Clients.Group(group).SendAsync(method);
            else
                await hub.Clients.Group(group).SendAsync(method, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast {Method} to {Group}", method, group);
        }
    }
}