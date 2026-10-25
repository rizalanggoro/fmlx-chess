using Api.DTOs;
using Api.Enums;
using Api.Interfaces.Models;
using Api.Models;
using Api.ValueObjects;

namespace Api.Interfaces.Services;

public interface IRoomService
{
    Room CreateRoom(RoomMode mode);
    Room? GetRoom(string roomId);
    bool ClaimRoom(Room room, PieceColor color, string? name = null);
    IReadOnlyList<RoomStatusDto> AvailableRooms();
    void NotifyLobby();

    void StartGame(Room room);
    void StartGameIfNotStarted(Room room);
    void ResetGame(Room room);

    IReadOnlyList<TileDto> GetBoard(Room room);
    IReadOnlyList<MoveHistoryEntry> GetHistory(Room room, PieceColor? color);

    GameActionResult MakeMove(Room room, Move move, PieceColor? color);
    GameActionResult Resign(Room room, IPlayer player);
    GameActionResult OfferDraw(Room room, IPlayer player);
    GameActionResult RespondToDrawOffer(Room room, IPlayer player, bool accepted);
    GameActionResult ClaimThreefoldRepetition(Room room, IPlayer player);
    GameActionResult ClaimFiftyMoveRule(Room room, IPlayer player);
}
