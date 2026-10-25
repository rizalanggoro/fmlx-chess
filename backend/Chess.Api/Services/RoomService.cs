using Api.DTOs;
using Api.DTOs.Events;
using Api.DTOs.Factories;
using Api.Enums;
using Api.Events;
using Api.GameEngine;
using Api.Hubs;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public class RoomService(
    IHubContext<GameHub> hub,
    IRoomRegistry registry,
    ILogger<RoomService> logger
) : IRoomService
{
    public Room CreateRoom(RoomMode mode)
    {
        string roomId;
        do
        {
            roomId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        } while (registry.Find(roomId) is not null);

        var manager = new ChessChessGameEngine(
            new Board(),
            new Player("White", PieceColor.White),
            new Player("Black", PieceColor.Black),
            TimeSpan.FromMinutes(10)
        );
        var room = new Room(roomId, mode, manager);

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

    public Room? GetRoom(string roomId)
    {
        return registry.Find(roomId);
    }

    public bool ClaimRoom(Room room, PieceColor color, string? name = null)
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

    public void StartGame(Room room)
    {
        room.Engine.StartGame();
    }

    public void StartGameIfNotStarted(Room room)
    {
        if (room.Engine.State == GameState.NotStarted)
            room.Engine.StartGame();
    }

    public void ResetGame(Room room)
    {
        room.Engine.ResetGame();
    }

    public IReadOnlyList<TileDto> GetBoard(Room room)
    {
        return room.Engine.GetBoard();
    }

    public IReadOnlyList<MoveHistoryEntry> GetHistory(Room room, PieceColor? color)
    {
        return color.HasValue ? room.Engine.GetMoveHistory(color.Value) : room.Engine.MoveHistory;
    }

    public GameActionResult MakeMove(Room room, Move move, PieceColor? color)
    {
        if (room.Mode == RoomMode.Online)
        {
            if (color is null || !room.IsJoined(color.Value))
                return GameActionResult.NotAPlayer;
            if (color.Value != room.Engine.CurrentPlayer.Color)
                return GameActionResult.NotYourTurn;
        }

        return room.Engine.TryMakeMove(move) ? GameActionResult.Ok : GameActionResult.Invalid;
    }

    public GameActionResult Resign(Room room, IPlayer player)
    {
        if (!IsPlayer(room, player))
            return GameActionResult.NotAPlayer;

        room.Engine.Resign(player);
        return GameActionResult.Ok;
    }

    public GameActionResult OfferDraw(Room room, IPlayer player)
    {
        if (!IsPlayer(room, player))
            return GameActionResult.NotAPlayer;

        room.Engine.OfferDraw(player);
        return GameActionResult.Ok;
    }

    public GameActionResult RespondToDrawOffer(Room room, IPlayer player, bool accepted)
    {
        if (!IsPlayer(room, player))
            return GameActionResult.NotAPlayer;

        room.Engine.RespondToDrawOffer(player, accepted);
        return GameActionResult.Ok;
    }

    public GameActionResult ClaimThreefoldRepetition(Room room, IPlayer player)
    {
        if (!IsPlayer(room, player))
            return GameActionResult.NotAPlayer;

        return room.Engine.ClaimDrawByThreefoldRepetition(player)
            ? GameActionResult.Ok
            : GameActionResult.Invalid;
    }

    public GameActionResult ClaimFiftyMoveRule(Room room, IPlayer player)
    {
        if (!IsPlayer(room, player))
            return GameActionResult.NotAPlayer;

        return room.Engine.ClaimDrawByFiftyMoveRule(player)
            ? GameActionResult.Ok
            : GameActionResult.Invalid;
    }

    private static bool IsPlayer(Room room, IPlayer player)
    {
        return room.Mode != RoomMode.Online || room.IsJoined(player.Color);
    }

    public IReadOnlyList<RoomStatusDto> AvailableRooms()
    {
        return registry.All
            .Where(room => room.Mode == RoomMode.Online && room.WhiteJoined != room.BlackJoined)
            .OrderBy(room => room.Id)
            .Select(RoomStatusDtoFactory.FromRoom)
            .ToList();
    }

    public void NotifyLobby()
    {
        _ = SendSafeAsync(GameGroups.Lobby, "LobbyChanged", new LobbyChangedDto(AvailableRooms()));
    }

    private void NotifySnapshot(Room room, SnapshotChangedEventArgs e)
    {
        var board = new List<TileDto>(64);
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var p = e.Board.Tiles[r, c].Piece;
            board.Add(new TileDto(r, c, p?.Color, p?.Type));
        }

        _ = SendSafeAsync(
            GameGroups.Room(room.Id),
            "SnapshotChanged",
            new SnapshotChangedDto(
                room.Id,
                RoomStatusDtoFactory.FromRoom(room),
                GameStateDtoFactory.FromRoom(room),
                board,
                e.History,
                LegalMovesDtoFactory.FromRoom(room)
            )
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

