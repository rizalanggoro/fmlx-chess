using Api.ValueObjects;

namespace Api.Models;

public interface IChessTile
{
    IChessPiece? Piece { get; set; }
    Position TilePosition { get; }
}
