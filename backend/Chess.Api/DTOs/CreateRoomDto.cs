using System.Text.Json.Serialization;
using Api.Enums;

namespace Api.DTOs;

public record CreateRoomDto(
    [property: JsonRequired] RoomMode Mode,
    PieceColor? Color = null,
    string? PlayerName = null
);
