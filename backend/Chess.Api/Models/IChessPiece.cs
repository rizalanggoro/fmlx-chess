namespace Api.Models;

public interface IChessPiece
{
    PieceColor Color { get; }
    PieceType Type { get; }
}
