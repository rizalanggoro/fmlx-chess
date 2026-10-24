using Api.Models;

namespace Api.Interfaces.Services;

public interface IRoomRegistry
{
    IEnumerable<Room> All { get; }

    Room? Find(string id);
    void Add(Room room);
}