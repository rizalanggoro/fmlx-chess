using Api.Enums;
using Api.Models;

namespace Api.Services;

public interface IRoomRegistry
{
    IEnumerable<GameRoom> All { get; }

    GameRoom Create(RoomMode mode);
    GameRoom? Find(string id);
    void NotifyRoom(string roomId);
    void NotifyLobby();
    void NotifyBoard(string roomId);
}
