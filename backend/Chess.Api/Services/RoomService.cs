using Api.DTOs;
using Api.DTOs.Events;
using Api.Enums;
using Api.Events;
using Api.Hubs;
using Api.Interfaces.Services;
using Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public class RoomService(
    IHubContext<GameHub> hub,
    IRoomRegistry registry,
    ILogger<RoomService> logger
) : IRoomService
{
    public GameRoom Create(RoomMode mode)
    {
        string roomId;
        do
        {
            roomId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        } while (registry.Find(roomId) is not null);

        var manager = new GameManager(
            new Board(),
            new Player("White", PieceColor.White),
            new Player("Black", PieceColor.Black),
            TimeSpan.FromMinutes(10)
        );
        var room = new GameRoom(roomId, mode, manager);

        manager.SnapshotChangedEventHandler += (_, e) => NotifySnapshot(room, e);
        manager.PieceCapturedEventHandler += (_, e) => NotifyPieceCaptured(room.Id, e);
        manager.CheckDeclaredEventHandler += (_, e) => NotifyCheck(room.Id, e);
        manager.GameEndedEventHandler += (_, e) => NotifyGameEnded(room.Id, e);
        manager.DrawOfferedEventHandler += (_, e) => NotifyDrawOffered(room.Id, e);
        manager.DrawDeclinedEventHandler += (_, e) => NotifyDrawDeclined(room.Id, e);
        manager.TimerTickedEventHandler += (_, e) => NotifyTimerTick(room.Id, e);

        registry.Add(room);

        return room;
    }

    public GameRoom? FindRoom(string roomId)
    {
        return registry.Find(roomId);
    }

    public bool Claim(GameRoom room, PieceColor color, string? name = null)
    {
        if (color == PieceColor.White)
        {
            if (room.WhiteJoined)
                return false;

            room.WhiteJoined = true;
            room.WhiteName = name;

            return true;
        }

        if (room.BlackJoined)
            return false;

        room.BlackJoined = true;
        room.BlackName = name;

        return true;
    }

    public IReadOnlyList<RoomStatusDto> AvailableRooms()
    {
        return registry.All
            .Where(room => room.Mode == RoomMode.Online && room.WhiteJoined != room.BlackJoined)
            .OrderBy(room => room.Id)
            .Select(RoomStatusDto.FromGameRoom)
            .ToList();
    }

    public void NotifyLobby()
    {
        _ = SendSafeAsync(GameGroups.Lobby, "LobbyChanged", new LobbyChangedDto(AvailableRooms()));
    }

    private void NotifySnapshot(GameRoom room, SnapshotChangedEventArgs e)
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
            "SnapshotChanged",
            new SnapshotChangedDto(
                room.Id,
                RoomStatusDto.FromGameRoom(room),
                GameStateDto.FromGameRoom(room),
                board,
                e.History)
        );
    }

    private void NotifyCheck(string roomId, CheckEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "CheckDeclared",
            new CheckDeclaredDto(roomId, e.CheckedColor)
        );
    }

    private void NotifyPieceCaptured(string roomId, PieceCapturedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "PieceCaptured",
            new PieceCapturedDto(roomId, e.CapturedPiece.Color, e.CapturedPiece.Type)
        );
    }

    private void NotifyTimerTick(string roomId, TimerTickEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "TimerTick",
            new TimerTickDto(roomId, e.Color, (int)e.TimeRemaining.TotalSeconds)
        );
    }

    private void NotifyGameEnded(string roomId, GameEndedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "GameEnded",
            new GameEndedDto(roomId, e.Result)
        );
    }

    private void NotifyDrawOffered(string roomId, DrawOfferedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "DrawOffered",
            new DrawOfferedDto(roomId, e.Offerer.Color, e.Offerer.Name)
        );
    }

    private void NotifyDrawDeclined(string roomId, DrawDeclinedEventArgs e)
    {
        _ = SendSafeAsync(
            GameGroups.Room(roomId),
            "DrawDeclined",
            new DrawDeclinedDto(roomId, e.Offerer.Color)
        );
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