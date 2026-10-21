using Api.ValueObjects;

namespace Api.DTOs.Events;

public record GameEndedDto(
    string RoomId,
    GameResult Result
);