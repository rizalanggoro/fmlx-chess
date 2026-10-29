using Api.Enums;
using Api.GameEngine;
using Api.Models;

namespace ChessApi.Tests.GameEngine;

[TestFixture]
public class StandardBoardInitializerTests
{
    private StandardBoardInitializer _initializer = null!;
    private Board _board = null!;

    [SetUp]
    public void SetUp()
    {
        _initializer = new StandardBoardInitializer();
        _board = new Board();
    }

    [Test]
    public void Initialize_EmptyBoard_SetsWhiteBackRankPiecesCorrectly()
    {
        _initializer.Initialize(_board);

        var expectedPieces = new[]
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

        Assert.Multiple(() =>
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[0, c].Piece;
                Assert.That(piece, Is.Not.Null, $"White back-rank tile [0, {c}] should not be null");
                Assert.That(piece!.Color, Is.EqualTo(PieceColor.White));
                Assert.That(piece.Type, Is.EqualTo(expectedPieces[c]), $"Mismatch at column {c}");
            }
        });
    }

    [Test]
    public void Initialize_EmptyBoard_SetsWhitePawnsOnRowOne()
    {
        _initializer.Initialize(_board);

        Assert.Multiple(() =>
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[1, c].Piece;
                Assert.That(piece, Is.Not.Null, $"White pawn tile [1, {c}] should not be null");
                Assert.That(piece!.Color, Is.EqualTo(PieceColor.White));
                Assert.That(piece.Type, Is.EqualTo(PieceType.Pawn));
            }
        });
    }

    [Test]
    public void Initialize_EmptyBoard_LeavesMiddleRanksEmpty()
    {
        _initializer.Initialize(_board);

        Assert.Multiple(() =>
        {
            for (var r = 2; r <= 5; r++)
            for (var c = 0; c < 8; c++)
            {
                Assert.That(_board.Tiles[r, c].Piece, Is.Null, $"Middle tile [{r}, {c}] should be empty");
            }
        });
    }

    [Test]
    public void Initialize_EmptyBoard_SetsBlackPawnsOnRowSix()
    {
        _initializer.Initialize(_board);

        Assert.Multiple(() =>
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[6, c].Piece;
                Assert.That(piece, Is.Not.Null, $"Black pawn tile [6, {c}] should not be null");
                Assert.That(piece!.Color, Is.EqualTo(PieceColor.Black));
                Assert.That(piece.Type, Is.EqualTo(PieceType.Pawn));
            }
        });
    }

    [Test]
    public void Initialize_EmptyBoard_SetsBlackBackRankPiecesCorrectly()
    {
        _initializer.Initialize(_board);

        var expectedPieces = new[]
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

        Assert.Multiple(() =>
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = _board.Tiles[7, c].Piece;
                Assert.That(piece, Is.Not.Null, $"Black back-rank tile [7, {c}] should not be null");
                Assert.That(piece!.Color, Is.EqualTo(PieceColor.Black));
                Assert.That(piece.Type, Is.EqualTo(expectedPieces[c]), $"Mismatch at column {c}");
            }
        });
    }

    [Test]
    public void Initialize_BoardWithPreExistingPieces_OverwritesAndResetsToInitialLayout()
    {
        // Put pieces randomly on the board
        _board.Tiles[3, 3].Piece = new ChessPiece(PieceColor.White, PieceType.Queen);
        _board.Tiles[4, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.Rook);

        _initializer.Initialize(_board);

        Assert.Multiple(() =>
        {
            Assert.That(_board.Tiles[3, 3].Piece, Is.Null);
            Assert.That(_board.Tiles[4, 4].Piece, Is.Null);
            Assert.That(_board.Tiles[0, 3].Piece?.Type, Is.EqualTo(PieceType.Queen));
            Assert.That(_board.Tiles[7, 0].Piece?.Type, Is.EqualTo(PieceType.Rook));
        });
    }
}

