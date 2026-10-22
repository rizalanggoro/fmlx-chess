using System.Collections.Concurrent;
using Api.Interfaces.Services;
using Api.Models;

namespace Api.Services;

public class RoomRegistry : IRoomRegistry
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public IEnumerable<GameRoom> All => _rooms.Values;

    public GameRoom? Find(string id)
    {
        return _rooms.GetValueOrDefault(id);
    }

    public void Add(GameRoom room)
    {
        _rooms[room.Id] = room;
    }
}