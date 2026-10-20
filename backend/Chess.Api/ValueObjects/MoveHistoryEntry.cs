using Api.Enums;

namespace Api.ValueObjects;

public record MoveHistoryEntry(
    Move Move,
    PieceColor PlayedBy
);