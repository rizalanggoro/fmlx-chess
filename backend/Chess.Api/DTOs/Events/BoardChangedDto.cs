using Api.DTOs;

namespace Api.DTOs.Events;

public record BoardChangedDto(
    string RoomId,
    IReadOnlyList<TileDto> Tiles
);