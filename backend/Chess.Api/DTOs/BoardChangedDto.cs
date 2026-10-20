namespace Api.DTOs;

public record BoardChangedDto(
    string RoomId,
    IReadOnlyList<TileDto> Tiles
);