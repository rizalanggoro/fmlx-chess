using Api.Enums;

namespace Api.DTOs.Events;

public record DrawDeclinedDto(
    string RoomId,
    PieceColor Offerer
);