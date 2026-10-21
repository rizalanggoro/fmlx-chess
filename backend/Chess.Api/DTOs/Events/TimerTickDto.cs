using Api.Enums;

namespace Api.DTOs.Events;

public record TimerTickDto(
    string RoomId,
    PieceColor Color,
    int RemainingSeconds
);