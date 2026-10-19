using Api.Enums;

namespace Api.Models;

public record GameResult(
    GameEndReason Reason,
    PieceColor? Winner);
