using Api.Enums;
using Api.Models;

namespace Api.DTOs;

public record RoomStatusDto(
    string Id,
    RoomMode Mode,
    bool WhiteJoined,
    bool BlackJoined,
    string? WhiteName = null,
    string? BlackName = null
)
{
    public static RoomStatusDto FromGameRoom(Room room)
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