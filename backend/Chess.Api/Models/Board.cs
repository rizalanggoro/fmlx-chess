namespace Api.Models;

public class Board : IBoard
{
    public IChessTile[,] Tiles { get; }

    public Board()
    {
        Tiles = new IChessTile[8, 8];
        for (var c = 0; c < 8; c++)
            for (var r = 0; r < 8; r++)
                Tiles[c, r] = new ChessTile(new Position(c, r));
    }

    public Board(IBoard source)
    {
        Tiles = new IChessTile[8, 8];
        for (var c = 0; c < 8; c++)
            for (var r = 0; r < 8; r++)
            {
                var s = source.Tiles[c, r];
                var t = new ChessTile(s.TilePosition);
                if (s.Piece is not null)
                    t.Piece = new ChessPiece(s.Piece.Color, s.Piece.Type);
                Tiles[c, r] = t;
            }
    }
}
