using Api.Enums;

namespace Api.DTOs.Events;

public record CheckDeclaredDto(
    string RoomId,
    PieceColor Color
);
