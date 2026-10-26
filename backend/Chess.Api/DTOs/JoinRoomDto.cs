using Api.Enums;

namespace Api.DTOs;

public record JoinRoomDto(
    PieceColor? Color = null,
    string? Name = null
);
