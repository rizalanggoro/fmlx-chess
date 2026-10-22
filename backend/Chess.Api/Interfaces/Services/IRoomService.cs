using Api.DTOs;
using Api.Enums;
using Api.Models;

namespace Api.Interfaces.Services;

public interface IRoomService
{
    GameRoom Create(RoomMode mode);
    bool Claim(GameRoom room, PieceColor color, string? name = null);
    IReadOnlyList<RoomStatusDto> AvailableRooms();
    void NotifyLobby();
}
