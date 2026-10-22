using Api.Models;

namespace Api.Interfaces.Services;

public interface IRoomRegistry
{
    IEnumerable<GameRoom> All { get; }

    GameRoom? Find(string id);
    void Add(GameRoom room);
}
