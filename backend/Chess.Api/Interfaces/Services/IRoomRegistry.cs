using Api.Models;

namespace Api.Interfaces.Services;

public interface IRoomRegistry
{
    IEnumerable<Room> All { get; }

    Room? Get(string id);
    void Create(Room room);
}