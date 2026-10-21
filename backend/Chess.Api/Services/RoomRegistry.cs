using System.Collections.Concurrent;
using Api.DTOs;
using Api.DTOs.Events;
using Api.Enums;
using Api.Events;
using Api.Hubs;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public class RoomRegistry(IHubContext<GameHub> hub, ILogger<RoomRegistry> logger) : IRoomRegistry
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public IEnumerable<GameRoom> All => _rooms.Values;

    public GameRoom Create(RoomMode mode)
    {
        string roomId;
        do
        {
            roomId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        } while (_rooms.ContainsKey(roomId));

        var manager = new GameManager(
            new Board(),
            new Player("White", PieceColor.White),
            new Player("Black", PieceColor.Black),
            TimeSpan.FromMinutes(10)
        );
        var room = new GameRoom(roomId, mode, manager);

        Hook(room);
        _rooms[roomId] = room;

        return room;
    }

    public GameRoom? Find(string id)
    {
        return _rooms.GetValueOrDefault(id);
    }

    public void NotifyLobby()
    {
        _ = SendSafeAsync(GameGroups.Lobby, "LobbyChanged", new LobbyChangedDto(OpenRooms()));
    }

    // ponytail: all open rooms in one array; add paging when a lobby actually gets crowded
    public IReadOnlyList<RoomStatusDto> OpenRooms() =>
        _rooms.Values
            .Where(room => room.Mode == RoomMode.Online && room.WhiteJoined != room.BlackJoined)
            .OrderBy(room => room.Id)
            .Select(RoomStatusDto.From)
            .ToList();

    private void NotifySnapshot(GameRoom room, RoomSnapshotChangedEventArgs e)
    {
        var board = new List<TileDto>(64);
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var p = e.Board.Tiles[c, r].Piece;
            board.Add(new TileDto(c, r, p?.Color, p?.Type));
        }

        _ = SendSafeAsync(
            GameGroups.Room(room.Id),
            "RoomSnapshotChanged",
            new RoomSnapshotChangedDto(
                room.Id,
                RoomStatusDto.From(room),
                GameStateDto.From(room),
                board,
                e.History));
    }

    private void Hook(GameRoom room)
    {
        room.Manager.SnapshotChangedEventHandler += (_, e) => NotifySnapshot(room, e);
        room.Manager.PieceCapturedEventHandler += (_, e) => NotifyPieceCaptured(room.Id, e);
        room.Manager.CheckDeclaredEventHandler += (_, e) => NotifyCheck(room.Id, e);
        room.Manager.GameEndedEventHandler += (_, e) => NotifyGameEnded(room.Id, e);
        room.Manager.DrawOfferedEventHandler += (_, e) => NotifyDrawOffered(room.Id, e);
        room.Manager.DrawDeclinedEventHandler += (_, e) => NotifyDrawDeclined(room.Id, e);
        room.Manager.TimerTickedEventHandler += (_, e) => NotifyTimerTick(room.Id, e);
    }

    private void NotifyCheck(string roomId, CheckEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "CheckDeclared",
            new CheckDeclaredDto(roomId, e.CheckedColor));
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

    private void NotifyDrawDeclined(string roomId, DrawDeclinedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "DrawDeclined",
            new DrawDeclinedDto(roomId, e.Offerer.Color));
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