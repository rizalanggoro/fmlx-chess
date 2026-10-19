using Api.Enums;

namespace Api.Models;

public interface IPlayer
{
    string Name { get; }
    PieceColor Color { get; }
}
