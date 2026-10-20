using Api.Enums;

namespace Api.DTOs;

public record PieceCapturedDto(
    string RoomId,
    PieceColor Color,
    PieceType Type
);
