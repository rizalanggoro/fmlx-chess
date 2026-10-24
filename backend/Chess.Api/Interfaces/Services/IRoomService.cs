using Api.DTOs;
using Api.Enums;
using Api.Models;

namespace Api.Interfaces.Services;

public interface IRoomService
{
    Room Create(RoomMode mode);
    Room? FindRoom(string roomId);
    bool Claim(Room room, PieceColor color, string? name = null);
    IReadOnlyList<RoomStatusDto> AvailableRooms();
    void NotifyLobby();
}