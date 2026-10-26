using Api.Enums;
using Api.Interfaces.GameEngine;
using Api.Interfaces.Models;
using Api.Models;

namespace Api.GameEngine;

public class StandardBoardInitializer : IBoardInitializer
{
    private readonly IChessPiece[] _whiteBackRank;
    private readonly IChessPiece[] _whitePawns;
    private readonly IChessPiece[] _blackPawns;
    private readonly IChessPiece[] _blackBackRank;

    public StandardBoardInitializer()
    {
        var backRankPieces = new[]
        {
            PieceType.Rook,
            PieceType.Knight,
            PieceType.Bishop,
            PieceType.Queen,
            PieceType.King,
            PieceType.Bishop,
            PieceType.Knight,
            PieceType.Rook
        };

        _whiteBackRank = new IChessPiece[8];
        _whitePawns = new IChessPiece[8];
        _blackPawns = new IChessPiece[8];
        _blackBackRank = new IChessPiece[8];

        for (var c = 0; c < 8; c++)
        {
            _whiteBackRank[c] = new ChessPiece(PieceColor.White, backRankPieces[c]);
            _whitePawns[c] = new ChessPiece(PieceColor.White, PieceType.Pawn);
            _blackPawns[c] = new ChessPiece(PieceColor.Black, PieceType.Pawn);
            _blackBackRank[c] = new ChessPiece(PieceColor.Black, backRankPieces[c]);
        }
    }

    public void Initialize(IBoard board)
    {
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
            board.Tiles[r, c].Piece = null;

        for (var c = 0; c < 8; c++)
        {
            board.Tiles[0, c].Piece = _whiteBackRank[c];
            board.Tiles[1, c].Piece = _whitePawns[c];
            board.Tiles[6, c].Piece = _blackPawns[c];
            board.Tiles[7, c].Piece = _blackBackRank[c];
        }
    }
}
