using System.Collections.Concurrent;
using Api.Interfaces.Services;
using Api.Models;

namespace Api.Services;

public class RoomRegistry : IRoomRegistry
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public IEnumerable<Room> All => _rooms.Values;

    public Room? Get(string id)
    {
        return _rooms.GetValueOrDefault(id);
    }

    public void Create(Room room)
    {
        _rooms[room.Id] = room;
    }
}