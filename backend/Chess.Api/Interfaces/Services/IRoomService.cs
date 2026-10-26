using Api.DTOs;
using Api.Enums;
using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Interfaces.Services;

public interface IRoomService
{
    Task<ServiceResult<RoomSessionDto>> CreateRoom(RoomMode mode, PieceColor? color, string? name);
    Task<ServiceResult<RoomSessionDto>> JoinRoom(string roomId, PieceColor? color, string? name);
    ServiceResult<RoomStatusDto> GetStatus(string roomId);
    IReadOnlyList<RoomStatusDto> AvailableRooms();

    ServiceResult<GameStateDto> GetState(string roomId);

    ServiceResult<GameStateDto> StartGame(string roomId);
    ServiceResult<GameStateDto> ResetGame(string roomId);

    ServiceResult<IReadOnlyList<TileDto>> GetBoard(string roomId);
    ServiceResult<LegalMovesDto> GetLegalMoves(string roomId);
    ServiceResult<IReadOnlyList<MoveHistoryEntry>> GetHistory(string roomId, PieceColor? color);

    ServiceResult<GameStateDto> MakeMove(string roomId, Move move, PieceColor? color);
    ServiceResult<GameStateDto> Resign(string roomId, IPlayer player);
    ServiceResult<GameStateDto> OfferDraw(string roomId, IPlayer player);
    ServiceResult<GameStateDto> RespondToDrawOffer(string roomId, IPlayer player, bool accepted);
    ServiceResult<GameStateDto> ClaimThreefoldRepetition(string roomId, IPlayer player);
    ServiceResult<GameStateDto> ClaimFiftyMoveRule(string roomId, IPlayer player);
}