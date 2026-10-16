namespace Api.Models;

public class ChessTile : IChessTile
{
    public IChessPiece? Piece { get; set; }
    public Position TilePosition { get; }

    public ChessTile(Position tilePosition)
    {
        TilePosition = tilePosition;
    }
}
