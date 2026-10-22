using System.Text.Json.Serialization;
using Api.Enums;

namespace Api.DTOs.Requests;

public record CreateRoomRequest(
    [property: JsonRequired] RoomMode Mode,
    PieceColor? Color = null,
    string? PlayerName = null
);