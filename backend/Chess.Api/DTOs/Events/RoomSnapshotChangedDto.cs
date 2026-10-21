using Api.ValueObjects;

namespace Api.DTOs.Events;

public record RoomSnapshotChangedDto(
    string RoomId,
    RoomStatusDto Room,
    GameStateDto State,
    IReadOnlyList<TileDto> Board,
    IReadOnlyList<MoveHistoryEntry> History
);