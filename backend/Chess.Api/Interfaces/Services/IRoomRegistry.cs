using Api.DTOs;
using Api.Enums;
using Api.Models;

namespace Api.Interfaces.Services;

public interface IRoomRegistry
{
    IEnumerable<GameRoom> All { get; }

    GameRoom Create(RoomMode mode);
    GameRoom? Find(string id);
    IReadOnlyList<RoomStatusDto> OpenRooms();
    void NotifyLobby();
}
