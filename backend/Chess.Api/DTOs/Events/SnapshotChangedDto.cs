using Api.ValueObjects;

namespace Api.DTOs.Events;

public record SnapshotChangedDto(
    string RoomId,
    RoomStatusDto Room,
    GameStateDto State,
    IReadOnlyList<TileDto> Board,
    IReadOnlyList<MoveHistoryEntry> History
);