using Api.Enums;

namespace Api.DTOs.Events;

public record DrawOfferedDto(string RoomId, PieceColor Color, string Name);
