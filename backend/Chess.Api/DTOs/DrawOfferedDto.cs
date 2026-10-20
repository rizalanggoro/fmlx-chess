using Api.Enums;

namespace Api.DTOs;

public record DrawOfferedDto(string RoomId, PieceColor Color, string Name);
