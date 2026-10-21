using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Models;

public class ChessTile(Position tilePosition) : IChessTile
{
    public IChessPiece? Piece { get; set; }
    public Position TilePosition { get; } = tilePosition;
}
