using Api.DTOs;
using Api.Enums;
using Api.ValueObjects;

namespace Api.Interfaces.Services;

public interface IRoomService
{
    Task<ServiceResult<RoomSessionDto>> CreateRoom(CreateRoomDto request);
    Task<ServiceResult<RoomSessionDto>> JoinRoom(string roomId, JoinRoomDto dto);
    ServiceResult<RoomStatusDto> GetStatus(string roomId);
    IReadOnlyList<RoomStatusDto> AvailableRooms();

    ServiceResult<GameStateDto> GetState(string roomId);

    ServiceResult<GameStateDto> StartGame(string roomId);
    ServiceResult<GameStateDto> ResetGame(string roomId);

    ServiceResult<IReadOnlyList<TileDto>> GetBoard(string roomId);
    ServiceResult<LegalMovesDto> GetLegalMoves(string roomId);
    ServiceResult<IReadOnlyList<MoveHistoryEntry>> GetHistory(string roomId, PieceColor? color);

    ServiceResult<GameStateDto> MakeMove(string roomId, MakeMoveDto dto);
    ServiceResult<GameStateDto> Resign(string roomId, PlayerActionDto request);
    ServiceResult<GameStateDto> OfferDraw(string roomId, PlayerActionDto request);
    ServiceResult<GameStateDto> RespondToDrawOffer(string roomId, PlayerActionDto request);
    ServiceResult<GameStateDto> ClaimThreefoldRepetition(string roomId, PlayerActionDto request);
    ServiceResult<GameStateDto> ClaimFiftyMoveRule(string roomId, PlayerActionDto request);
}
