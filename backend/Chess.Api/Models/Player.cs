using Api.Enums;

namespace Api.Models;

public class Player(string name, PieceColor color) : IPlayer
{
    public string Name { get; } = name;
    public PieceColor Color { get; } = color;
}
