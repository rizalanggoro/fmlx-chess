namespace Api.Models;

public class ChessPiece : IChessPiece
{
    public PieceColor Color { get; }
    public PieceType Type { get; }

    public ChessPiece(PieceColor color, PieceType type)
    {
        Color = color;
        Type = type;
    }
}
