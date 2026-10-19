using Api.Enums;

namespace Api.Models;

public record MoveHistoryEntry(
    Move Move,
    PieceColor PlayedBy);
