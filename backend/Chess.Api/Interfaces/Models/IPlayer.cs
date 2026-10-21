using Api.Enums;

namespace Api.Interfaces.Models;

public interface IPlayer
{
    string Name { get; }
    PieceColor Color { get; }
}
