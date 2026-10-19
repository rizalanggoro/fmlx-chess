using Api.Enums;

namespace Api.Models;

public class ChessPiece(
    PieceColor color,
    PieceType type) : IChessPiece
{
    public PieceColor Color { get; } = color;
    public PieceType Type { get; } = type;
}
