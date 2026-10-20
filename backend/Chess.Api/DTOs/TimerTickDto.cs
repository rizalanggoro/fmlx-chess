using Api.Enums;

namespace Api.DTOs;

public record TimerTickDto(
    string RoomId,
    PieceColor Color,
    int RemainingSeconds
);