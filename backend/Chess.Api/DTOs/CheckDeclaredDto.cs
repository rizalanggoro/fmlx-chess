using Api.Enums;

namespace Api.DTOs;

public record CheckDeclaredDto(
    string RoomId,
    PieceColor Color
);
