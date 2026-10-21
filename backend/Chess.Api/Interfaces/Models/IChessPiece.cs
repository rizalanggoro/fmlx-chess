using Api.Enums;

namespace Api.Interfaces.Models;

public interface IChessPiece
{
    PieceColor Color { get; }
    PieceType Type { get; }
}
