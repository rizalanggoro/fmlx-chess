using Api.ValueObjects;

namespace Api.Interfaces.Models;

public interface IChessTile
{
    IChessPiece? Piece { get; set; }
    Position TilePosition { get; }
}
