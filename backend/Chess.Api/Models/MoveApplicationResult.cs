using Api.ValueObjects;

namespace Api.Models;

public record MoveApplicationResult(
    IChessPiece MovingPiece,
    IChessPiece? CapturedPiece,
    Position? CapturedSquare);
