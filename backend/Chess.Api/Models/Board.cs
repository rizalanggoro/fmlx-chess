using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Models;

public class Board : IBoard
{
    public Board()
    {
        Tiles = new IChessTile[8, 8];

        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
            Tiles[c, r] = new ChessTile(
                new Position(c, r)
            );
    }

    public Board(IBoard source)
    {
        Tiles = new IChessTile[8, 8];

        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var sourceTile = source.Tiles[c, r];
            var tile = new ChessTile(sourceTile.TilePosition);
            if (sourceTile.Piece is not null)
                tile.Piece = new ChessPiece(
                    sourceTile.Piece.Color,
                    sourceTile.Piece.Type
                );

            Tiles[c, r] = tile;
        }
    }

    public IChessTile[,] Tiles { get; }
}