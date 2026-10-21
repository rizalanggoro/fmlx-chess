using Api.Enums;

namespace Api.DTOs.Events;

public record PieceCapturedDto(
    string RoomId,
    PieceColor Color,
    PieceType Type
);
