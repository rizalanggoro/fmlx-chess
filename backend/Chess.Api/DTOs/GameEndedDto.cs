using Api.ValueObjects;

namespace Api.DTOs;

public record GameEndedDto(string RoomId, GameResult Result);
