using Api.Models;

namespace Api.ValueObjects;

public record MoveApplicationResult(
    IChessPiece MovingPiece,
    IChessPiece? CapturedPiece,
    Position? CapturedSquare
);