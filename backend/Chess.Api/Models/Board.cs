using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Models;

public class Board : IBoard
{
    public Board()
    {
        Tiles = new IChessTile[8, 8];

        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
            Tiles[r, c] = new ChessTile(
                new Position(r, c)
            );
    }

    public Board(IBoard source)
    {
        Tiles = new IChessTile[8, 8];

        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var sourceTile = source.Tiles[r, c];
            var tile = new ChessTile(sourceTile.TilePosition);
            if (sourceTile.Piece is not null)
                tile.Piece = new ChessPiece(
                    sourceTile.Piece.Color,
                    sourceTile.Piece.Type
                );

            Tiles[r, c] = tile;
        }
    }

    public IChessTile[,] Tiles { get; }
}