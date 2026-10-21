using Api.DTOs;

namespace Api.DTOs.Events;

public record LobbyChangedDto(IReadOnlyList<RoomStatusDto> Rooms);
