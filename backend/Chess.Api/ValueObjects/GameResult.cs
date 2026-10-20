using Api.Enums;

namespace Api.ValueObjects;

public record GameResult(
    GameEndReason Reason,
    PieceColor? Winner
);