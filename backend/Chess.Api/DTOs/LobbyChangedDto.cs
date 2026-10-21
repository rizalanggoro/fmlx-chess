namespace Api.DTOs;

public record LobbyChangedDto(IReadOnlyList<RoomStatusDto> Rooms);
