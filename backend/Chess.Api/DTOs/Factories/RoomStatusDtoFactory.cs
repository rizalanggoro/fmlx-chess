using Api.Models;

namespace Api.DTOs.Factories;

public static class RoomStatusDtoFactory
{
    public static RoomStatusDto FromRoom(Room room)
    {
        return new RoomStatusDto(
            room.Id, room.Mode,
            room.WhiteJoined,
            room.BlackJoined,
            room.WhiteName,
            room.BlackName
        );
    }
}
