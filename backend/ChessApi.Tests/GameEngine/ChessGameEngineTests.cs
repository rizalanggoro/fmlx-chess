using Api.Enums;
using Api.Events;
using Api.GameEngine;
using Api.Interfaces.GameEngine;
using Api.Interfaces.Models;
using Api.Models;
using Api.ValueObjects;

namespace ChessApi.Tests.GameEngine;

[TestFixture]
public class ChessGameEngineTests
{
    private Player _whitePlayer = null!;
    private Player _blackPlayer = null!;
    private StandardBoardInitializer _standardInitializer = null!;

    [SetUp]
    public void SetUp()
    {
        _whitePlayer = new Player("White", PieceColor.White);
        _blackPlayer = new Player("Black", PieceColor.Black);
        _standardInitializer = new StandardBoardInitializer();
    }

    private ChessGameEngine CreateEngine(IBoardInitializer? initializer = null)
    {
        return new ChessGameEngine(
            new Board(),
            _whitePlayer,
            _blackPlayer,
            TimeSpan.FromMinutes(10),
            initializer ?? _standardInitializer
        );
    }

    private class CustomBoardInitializer(Action<IBoard> configure) : IBoardInitializer
    {
        public void Initialize(IBoard board)
        {
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
                board.Tiles[r, c].Piece = null;

            configure(board);
        }
    }

    #region Lifecycle & Board Setup

    [Test]
    public void StartGame_WhenNotStarted_SetsStateToInProgressAndWhiteAsCurrentPlayer()
    {
        var engine = CreateEngine();

        engine.StartGame();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.EqualTo(GameState.InProgress));
            Assert.That(engine.CurrentPlayer.Color, Is.EqualTo(PieceColor.White));
            Assert.That(engine.Result, Is.Null);
            Assert.That(engine.Board.Tiles[0, 4].Piece?.Type, Is.EqualTo(PieceType.King));
        });
    }

    [Test]
    public void ResetGame_WhenInProgress_ResetsStateToNotStartedAndClearsHistory()
    {
        var engine = CreateEngine();
        engine.StartGame();
        engine.TryMakeMove(new Move(new Position(1, 4), new Position(3, 4)));

        engine.ResetGame();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.EqualTo(GameState.NotStarted));
            Assert.That(engine.MoveHistory, Is.Empty);
            Assert.That(engine.Result, Is.Null);
        });
    }

    [Test]
    public async Task StartGame_WhenClockRunsOut_EndsGameWithTimeExpired()
    {
        var tcs = new TaskCompletionSource<GameEndedEventArgs>();
        var engine = new ChessGameEngine(
            new Board(),
            _whitePlayer,
            _blackPlayer,
            TimeSpan.FromSeconds(1),
            _standardInitializer
        );
        engine.GameEndedEventHandler += (_, e) => tcs.TrySetResult(e);

        engine.StartGame();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.That(completed, Is.SameAs(tcs.Task), "Clock timeout event did not fire within expected duration.");

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.TimeExpired));
            Assert.That(engine.Result?.Winner, Is.EqualTo(PieceColor.Black));
        });
    }

    [Test]
    public void IsInBounds_InsideBoardCoordinates_ReturnsTrue()
    {
        var engine = CreateEngine();

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsInBounds(new Position(0, 0)), Is.True);
            Assert.That(engine.IsInBounds(new Position(7, 7)), Is.True);
            Assert.That(engine.IsInBounds(new Position(3, 4)), Is.True);
        });
    }

    [Test]
    public void IsInBounds_OutsideBoardCoordinates_ReturnsFalse()
    {
        var engine = CreateEngine();

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsInBounds(new Position(-1, 0)), Is.False);
            Assert.That(engine.IsInBounds(new Position(0, 8)), Is.False);
            Assert.That(engine.IsInBounds(new Position(8, 8)), Is.False);
        });
    }

    [Test]
    public void GetBoard_WhenCalled_Returns64Tiles()
    {
        var engine = CreateEngine();
        engine.StartGame();

        var tiles = engine.GetBoard();

        Assert.Multiple(() =>
        {
            Assert.That(tiles, Has.Count.EqualTo(64));
            Assert.That(tiles.Any(t => t.Row == 0 && t.Column == 4 && t.Type == PieceType.King), Is.True);
        });
    }

    [Test]
    public void GetAllLegalMoves_AtGameStart_ReturnsTwentyOpeningMoves()
    {
        var engine = CreateEngine();
        engine.StartGame();

        var legalMoves = engine.GetAllLegalMoves();

        // 16 pawn moves (8 pawns x 2 options) + 4 knight moves (2 knights x 2 options) = 20
        Assert.That(legalMoves, Has.Count.EqualTo(20));
    }

    #endregion

    #region Movement Rules

    [Test]
    public void TryMakeMove_GameNotStarted_ReturnsFalse()
    {
        var engine = CreateEngine();
        // Not calling engine.StartGame()

        var result = engine.TryMakeMove(new Move(new Position(1, 4), new Position(3, 4)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_FromSquareEmpty_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(4, 4), new Position(5, 4)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_MovingOpponentPiece_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.StartGame(); // White's turn

        // Black pawn at (6, 4)
        var result = engine.TryMakeMove(new Move(new Position(6, 4), new Position(4, 4)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_PawnSingleStepForward_SucceedsAndSwitchesTurn()
    {
        var engine = CreateEngine();
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(1, 4), new Position(2, 4)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.CurrentPlayer.Color, Is.EqualTo(PieceColor.Black));
            Assert.That(engine.Board.Tiles[2, 4].Piece?.Type, Is.EqualTo(PieceType.Pawn));
            Assert.That(engine.Board.Tiles[1, 4].Piece, Is.Null);
            Assert.That(engine.MoveHistory, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TryMakeMove_PawnDoubleStepForward_SucceedsAndSetsEnPassantTarget()
    {
        var engine = CreateEngine();
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(1, 4), new Position(3, 4)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.EnPassantTarget, Is.EqualTo(new Position(2, 4)));
        });
    }

    [Test]
    public void TryMakeMove_PawnBlockedByPieceInFront_ReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[1, 4].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            board.Tiles[2, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(1, 4), new Position(2, 4)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_KnightJumpingOverPawns_Succeeds()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // White Knight from (0, 6) jumping over pawns to (2, 5)
        var result = engine.TryMakeMove(new Move(new Position(0, 6), new Position(2, 5)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[2, 5].Piece?.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(engine.Board.Tiles[0, 6].Piece, Is.Null);
        });
    }

    [Test]
    public void TryMakeMove_BishopBlockedByPawn_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // White Bishop at (0, 2) blocked by pawn at (1, 3)
        var result = engine.TryMakeMove(new Move(new Position(0, 2), new Position(2, 4)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_RookBlockedByPawn_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // White Rook at (0, 0) blocked by pawn at (1, 0)
        var result = engine.TryMakeMove(new Move(new Position(0, 0), new Position(3, 0)));

        Assert.That(result, Is.False);
    }

    #endregion

    #region Captures & En Passant

    [Test]
    public void TryMakeMove_DiagonalPawnCapture_CapturesEnemyPieceAndRaisesPieceCapturedEvent()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[3, 3].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            board.Tiles[4, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.Knight);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        PieceCapturedEventArgs? capturedEvent = null;
        engine.PieceCapturedEventHandler += (_, e) => capturedEvent = e;

        var result = engine.TryMakeMove(new Move(new Position(3, 3), new Position(4, 4)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[4, 4].Piece?.Color, Is.EqualTo(PieceColor.White));
            Assert.That(engine.CapturedPiecesTracker[PieceColor.White], Has.Count.EqualTo(1));
            Assert.That(capturedEvent, Is.Not.Null);
            Assert.That(capturedEvent!.CapturedPiece.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(capturedEvent.TilePosition, Is.EqualTo(new Position(4, 4)));
        });
    }

    [Test]
    public void TryMakeMove_EnPassantCapture_CapturesPawnAndRemovesItFromBoard()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // 1. e2-e4 (1,4)->(3,4) | a7-a6 (6,0)->(5,0)
        engine.TryMakeMove(new Move(new Position(1, 4), new Position(3, 4)));
        engine.TryMakeMove(new Move(new Position(6, 0), new Position(5, 0)));

        // 2. e4-e5 (3,4)->(4,4) | d7-d5 (6,3)->(4,3) -> sets EnPassantTarget to (5,3)
        engine.TryMakeMove(new Move(new Position(3, 4), new Position(4, 4)));
        engine.TryMakeMove(new Move(new Position(6, 3), new Position(4, 3)));

        Assert.That(engine.EnPassantTarget, Is.EqualTo(new Position(5, 3)));

        // 3. White captures en passant: (4,4) -> (5,3)
        var capturedResult = engine.TryMakeMove(new Move(new Position(4, 4), new Position(5, 3)));

        Assert.Multiple(() =>
        {
            Assert.That(capturedResult, Is.True);
            Assert.That(engine.Board.Tiles[5, 3].Piece?.Color, Is.EqualTo(PieceColor.White));
            Assert.That(engine.Board.Tiles[4, 3].Piece, Is.Null, "Black pawn at (4,3) should have been removed");
            Assert.That(engine.CapturedPiecesTracker[PieceColor.White], Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TryMakeMove_EnPassantNotTakenImmediately_ExpiresOnSubsequentTurn()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // 1. e2-e4 | a7-a6
        engine.TryMakeMove(new Move(new Position(1, 4), new Position(3, 4)));
        engine.TryMakeMove(new Move(new Position(6, 0), new Position(5, 0)));

        // 2. e4-e5 | d7-d5 (EnPassantTarget active)
        engine.TryMakeMove(new Move(new Position(3, 4), new Position(4, 4)));
        engine.TryMakeMove(new Move(new Position(6, 3), new Position(4, 3)));

        // 3. White ignores en passant and plays h2-h3
        engine.TryMakeMove(new Move(new Position(1, 7), new Position(2, 7)));

        Assert.That(engine.EnPassantTarget, Is.Null, "En passant target should clear after one turn");
    }

    #endregion

    #region Castling

    [Test]
    public void TryMakeMove_KingsideCastlingWhenLegal_MovesKingAndRookAndUpdatesCastlingRights()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 6)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[0, 6].Piece?.Type, Is.EqualTo(PieceType.King));
            Assert.That(engine.Board.Tiles[0, 5].Piece?.Type, Is.EqualTo(PieceType.Rook));
            Assert.That(engine.Board.Tiles[0, 4].Piece, Is.Null);
            Assert.That(engine.Board.Tiles[0, 7].Piece, Is.Null);
            Assert.That(engine.CastlingRights.WhiteKingSide, Is.False);
            Assert.That(engine.CastlingRights.WhiteQueenSide, Is.False);
        });
    }

    [Test]
    public void TryMakeMove_QueensideCastlingWhenLegal_MovesKingAndRookAndUpdatesCastlingRights()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 2)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[0, 2].Piece?.Type, Is.EqualTo(PieceType.King));
            Assert.That(engine.Board.Tiles[0, 3].Piece?.Type, Is.EqualTo(PieceType.Rook));
            Assert.That(engine.Board.Tiles[0, 4].Piece, Is.Null);
            Assert.That(engine.Board.Tiles[0, 0].Piece, Is.Null);
            Assert.That(engine.Board.Tiles[0, 1].Piece, Is.Null);
            Assert.That(engine.CastlingRights.WhiteKingSide, Is.False);
            Assert.That(engine.CastlingRights.WhiteQueenSide, Is.False);
        });
    }

    [Test]
    public void TryMakeMove_KingMoves_LosesBothCastlingRightsPermanently()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // 1. King moves away from initial square
        engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 5)));
        engine.TryMakeMove(new Move(new Position(7, 4), new Position(7, 5)));

        // 2. King moves back to initial square
        engine.TryMakeMove(new Move(new Position(0, 5), new Position(0, 4)));
        engine.TryMakeMove(new Move(new Position(7, 5), new Position(7, 4)));

        // 3. Attempt to castle should now fail
        var castlingAttempt = engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 6)));

        Assert.Multiple(() =>
        {
            Assert.That(castlingAttempt, Is.False);
            Assert.That(engine.CastlingRights.WhiteKingSide, Is.False);
            Assert.That(engine.CastlingRights.WhiteQueenSide, Is.False);
        });
    }

    [Test]
    public void TryMakeMove_KingsideRookMoves_LosesKingsideCastlingRightWhileRetainingQueenside()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[0, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // Kingside rook moves
        var moveResult = engine.TryMakeMove(new Move(new Position(0, 7), new Position(0, 6)));

        Assert.Multiple(() =>
        {
            Assert.That(moveResult, Is.True);
            Assert.That(engine.CastlingRights.WhiteKingSide, Is.False);
            Assert.That(engine.CastlingRights.WhiteQueenSide, Is.True);
        });
    }

    [Test]
    public void TryMakeMove_CastlingWhenPathBlocked_ReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 5].Piece = new ChessPiece(PieceColor.White, PieceType.Bishop); // Blocking path
            board.Tiles[0, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 6)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_CastlingWhenKingInCheck_ReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.Rook); // Attacking e1 directly
            board.Tiles[7, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 6)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_CastlingWhenPassingThroughCheck_ReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
            board.Tiles[7, 5].Piece = new ChessPiece(PieceColor.Black, PieceType.Rook); // Attacking f1 (0, 5)
            board.Tiles[7, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(0, 4), new Position(0, 6)));

        Assert.That(result, Is.False);
    }

    #endregion

    #region Promotion

    [Test]
    public void TryMakeMove_PawnReachesPromotionRank_PromotesToSpecifiedPieceType()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(6, 0), new Position(7, 0), PieceType.Knight));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[7, 0].Piece?.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(engine.Board.Tiles[6, 0].Piece, Is.Null);
        });
    }

    [Test]
    public void TryMakeMove_PawnReachesPromotionRankWithoutType_DefaultsToQueen()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(6, 0), new Position(7, 0)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[7, 0].Piece?.Type, Is.EqualTo(PieceType.Queen));
        });
    }

    [Test]
    public void TryMakeMove_PawnReachesPromotionRank_PromotesToRook()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(6, 0), new Position(7, 0), PieceType.Rook));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[7, 0].Piece?.Type, Is.EqualTo(PieceType.Rook));
        });
    }

    [Test]
    public void TryMakeMove_PawnReachesPromotionRank_PromotesToBishop()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        var result = engine.TryMakeMove(new Move(new Position(6, 0), new Position(7, 0), PieceType.Bishop));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.Board.Tiles[7, 0].Piece?.Type, Is.EqualTo(PieceType.Bishop));
        });
    }

    [Test]
    public void TryMakeMove_PawnReachesPromotionRank_InvalidPromotionType_ReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // King is not a valid promotion piece
        var result = engine.TryMakeMove(new Move(new Position(6, 0), new Position(7, 0), PieceType.King));

        Assert.That(result, Is.False);
    }

    #endregion

    #region Checks & Pins

    [Test]
    public void TryMakeMove_MoveExposingOwnKingToCheck_ReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[1, 4].Piece = new ChessPiece(PieceColor.White, PieceType.Bishop);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.Rook);
            board.Tiles[7, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // Bishop is pinned to king along file 4; moving it to (2, 5) breaks the pin
        var result = engine.TryMakeMove(new Move(new Position(1, 4), new Position(2, 5)));

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryMakeMove_MoveDeliveringCheck_RaisesCheckDeclaredEventAndSetsCheckedColor()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[3, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Rook);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        CheckEventArgs? checkEvent = null;
        engine.CheckDeclaredEventHandler += (_, e) => checkEvent = e;

        // White Rook moves from (3,0) to (7,0) attacking Black King on row 7
        var result = engine.TryMakeMove(new Move(new Position(3, 0), new Position(7, 0)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.CheckedColor, Is.EqualTo(PieceColor.Black));
            Assert.That(checkEvent, Is.Not.Null);
            Assert.That(checkEvent!.CheckedColor, Is.EqualTo(PieceColor.Black));
        });
    }

    [Test]
    public void TryMakeMove_WhenKingInCheck_NonEvasionMoveReturnsFalse()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.Rook); // Check along rank 0
            board.Tiles[1, 0].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            board.Tiles[7, 4].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // White Pawn on (1, 0) tries to step forward to (2, 0) without resolving check on King
        var illegalMove = engine.TryMakeMove(new Move(new Position(1, 0), new Position(2, 0)));

        // White King escapes check to (1, 4)
        var legalEvasion = engine.TryMakeMove(new Move(new Position(0, 4), new Position(1, 4)));

        Assert.Multiple(() =>
        {
            Assert.That(illegalMove, Is.False);
            Assert.That(legalEvasion, Is.True);
            Assert.That(engine.CheckedColor, Is.Null);
        });
    }

    #endregion

    #region Checkmate & Stalemate

    [Test]
    public void TryMakeMove_FoolsMateSequence_ResultsInCheckmateAndEndsGame()
    {
        var engine = CreateEngine();
        engine.StartGame();

        GameEndedEventArgs? endEvent = null;
        engine.GameEndedEventHandler += (_, e) => endEvent = e;

        // 1. f2-f3 (1,5)->(2,5) | e7-e5 (6,4)->(4,4)
        engine.TryMakeMove(new Move(new Position(1, 5), new Position(2, 5)));
        engine.TryMakeMove(new Move(new Position(6, 4), new Position(4, 4)));

        // 2. g2-g4 (1,6)->(3,6) | Qd8-h4# (7,3)->(3,7)
        engine.TryMakeMove(new Move(new Position(1, 6), new Position(3, 6)));
        var mateResult = engine.TryMakeMove(new Move(new Position(7, 3), new Position(3, 7)));

        Assert.Multiple(() =>
        {
            Assert.That(mateResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result, Is.Not.Null);
            Assert.That(engine.Result!.Reason, Is.EqualTo(GameEndReason.Checkmate));
            Assert.That(engine.Result.Winner, Is.EqualTo(PieceColor.Black));
            Assert.That(endEvent, Is.Not.Null);
        });
    }

    [Test]
    public void TryMakeMove_MoveDeliveringStalemate_EndsGameWithDrawByStalemate()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[2, 1].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[1, 3].Piece = new ChessPiece(PieceColor.White, PieceType.Queen);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // White Queen moves from (1,3) to (1,2), trapping Black King at (0,0) without check
        var result = engine.TryMakeMove(new Move(new Position(1, 3), new Position(1, 2)));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result, Is.Not.Null);
            Assert.That(engine.Result!.Reason, Is.EqualTo(GameEndReason.DrawByStalemate));
            Assert.That(engine.Result.Winner, Is.Null);
        });
    }

    #endregion

    #region Resign & Draw Claims

    [Test]
    public void Resign_WhitePlayerResigns_BlackWinsAndFiresGameEndedEvent()
    {
        var engine = CreateEngine();
        engine.StartGame();

        GameEndedEventArgs? endEvent = null;
        engine.GameEndedEventHandler += (_, e) => endEvent = e;

        engine.Resign(_whitePlayer);

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result, Is.Not.Null);
            Assert.That(engine.Result!.Reason, Is.EqualTo(GameEndReason.PlayerResigned));
            Assert.That(engine.Result.Winner, Is.EqualTo(PieceColor.Black));
            Assert.That(endEvent, Is.Not.Null);
        });
    }

    [Test]
    public void OfferDraw_WhenGameInProgress_FiresDrawOfferedEvent()
    {
        var engine = CreateEngine();
        engine.StartGame();

        DrawOfferedEventArgs? drawOfferEvent = null;
        engine.DrawOfferedEventHandler += (_, e) => drawOfferEvent = e;

        engine.OfferDraw(_whitePlayer);

        Assert.Multiple(() =>
        {
            Assert.That(drawOfferEvent, Is.Not.Null);
            Assert.That(drawOfferEvent!.Offerer.Color, Is.EqualTo(PieceColor.White));
        });
    }

    [Test]
    public void RespondToDrawOffer_PlayerAccepts_EndsGameWithDrawByAgreement()
    {
        var engine = CreateEngine();
        engine.StartGame();
        engine.OfferDraw(_whitePlayer);

        engine.RespondToDrawOffer(_blackPlayer, accepted: true);

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result, Is.Not.Null);
            Assert.That(engine.Result!.Reason, Is.EqualTo(GameEndReason.DrawByAgreement));
            Assert.That(engine.Result.Winner, Is.Null);
        });
    }

    [Test]
    public void RespondToDrawOffer_PlayerDeclines_FiresDrawDeclinedEvent()
    {
        var engine = CreateEngine();
        engine.StartGame();
        engine.OfferDraw(_whitePlayer);

        DrawDeclinedEventArgs? declinedEvent = null;
        engine.DrawDeclinedEventHandler += (_, e) => declinedEvent = e;

        engine.RespondToDrawOffer(_blackPlayer, accepted: false);

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.EqualTo(GameState.InProgress));
            Assert.That(declinedEvent, Is.Not.Null);
            Assert.That(declinedEvent!.Offerer.Color, Is.EqualTo(PieceColor.White));
        });
    }

    [Test]
    public void ClaimThreefoldRepetition_SamePositionOccursThreeTimes_AllowsDrawClaim()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // White Knight back and forth, Black Knight back and forth
        // 1. Nf3 Nf6
        engine.TryMakeMove(new Move(new Position(0, 6), new Position(2, 5)));
        engine.TryMakeMove(new Move(new Position(7, 6), new Position(5, 5)));

        // 2. Ng1 Ng8 (Position repeated 2nd time)
        engine.TryMakeMove(new Move(new Position(2, 5), new Position(0, 6)));
        engine.TryMakeMove(new Move(new Position(5, 5), new Position(7, 6)));

        // 3. Nf3 Nf6
        engine.TryMakeMove(new Move(new Position(0, 6), new Position(2, 5)));
        engine.TryMakeMove(new Move(new Position(7, 6), new Position(5, 5)));

        // 4. Ng1 Ng8 (Position repeated 3rd time)
        engine.TryMakeMove(new Move(new Position(2, 5), new Position(0, 6)));
        engine.TryMakeMove(new Move(new Position(5, 5), new Position(7, 6)));

        Assert.That(engine.CanClaimThreefoldRepetition, Is.True);

        var claimResult = engine.ClaimDrawByThreefoldRepetition(_whitePlayer);

        Assert.Multiple(() =>
        {
            Assert.That(claimResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawByThreefoldRepetition));
        });
    }

    [Test]
    public void ClaimFiftyMoveRule_WhenConditionNotMet_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.StartGame();

        var claimResult = engine.ClaimDrawByFiftyMoveRule(_whitePlayer);

        Assert.Multiple(() =>
        {
            Assert.That(claimResult, Is.False);
            Assert.That(engine.CanClaimFiftyMoveRule, Is.False);
        });
    }

    [Test]
    public void TryMakeMove_InsufficientMaterialKingVsKing_EndsGameWithDrawByDeadPosition()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[1, 1].Piece = new ChessPiece(PieceColor.Black, PieceType.Rook);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // White King captures Black's last piece (Rook) at (1, 1), leaving bare Kings
        var moveResult = engine.TryMakeMove(new Move(new Position(0, 0), new Position(1, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(moveResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawByDeadPosition));
            Assert.That(engine.Result?.Winner, Is.Null);
        });
    }

    [Test]
    public void TryMakeMove_InsufficientMaterialKingAndBishopVsKing_EndsGameWithDrawByDeadPosition()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Bishop);
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[1, 1].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // White King captures Black's Pawn at (1, 1), leaving K+B vs K
        var moveResult = engine.TryMakeMove(new Move(new Position(0, 0), new Position(1, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(moveResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawByDeadPosition));
        });
    }

    [Test]
    public void TryMakeMove_InsufficientMaterialKingAndKnightVsKing_EndsGameWithDrawByDeadPosition()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Knight);
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[1, 1].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // White King captures Black's Pawn at (1, 1), leaving K+N vs K
        var moveResult = engine.TryMakeMove(new Move(new Position(0, 0), new Position(1, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(moveResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawByDeadPosition));
        });
    }

    [Test]
    public void TryMakeMove_InsufficientMaterialBishopsOnSameColor_EndsGameWithDrawByDeadPosition()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 0].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            // (0, 1) -> (0+1)%2 = 1 (light square)
            board.Tiles[0, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Bishop);
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            // (2, 3) -> (2+3)%2 = 1 (same color square)
            board.Tiles[2, 3].Piece = new ChessPiece(PieceColor.Black, PieceType.Bishop);
            board.Tiles[1, 1].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        // White King captures Black's Pawn at (1, 1), leaving K+B vs K+B on same colored squares
        var moveResult = engine.TryMakeMove(new Move(new Position(0, 0), new Position(1, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(moveResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawByDeadPosition));
        });
    }

    private static void PlayNonRepeatingMoves(ChessGameEngine engine, int totalHalfMoves)
    {
        var whiteKnightMoves = new[]
        {
            new Move(new Position(0, 1), new Position(2, 2)),
            new Move(new Position(2, 2), new Position(4, 3)),
            new Move(new Position(4, 3), new Position(6, 2)),
            new Move(new Position(6, 2), new Position(4, 1)),
            new Move(new Position(4, 1), new Position(2, 0)),
            new Move(new Position(2, 0), new Position(0, 1)),
        };

        var blackKingMoves = new[]
        {
            new Move(new Position(7, 7), new Position(7, 6)),
            new Move(new Position(7, 6), new Position(6, 5)),
            new Move(new Position(6, 5), new Position(5, 6)),
            new Move(new Position(5, 6), new Position(6, 7)),
            new Move(new Position(6, 7), new Position(7, 7)),
        };

        var wIdx = 0;
        var bIdx = 0;
        for (var i = 0; i < totalHalfMoves; i++)
        {
            var move = i % 2 == 0
                ? whiteKnightMoves[wIdx++ % whiteKnightMoves.Length]
                : blackKingMoves[bIdx++ % blackKingMoves.Length];

            var ok = engine.TryMakeMove(move);
            Assert.That(ok, Is.True, $"Move {i} failed: {move.From} -> {move.To}");
        }
    }

    [Test]
    public void ClaimFiftyMoveRule_WhenHalfMoveClockReachesOneHundred_EndsGameWithDrawBy50MoveRule()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Knight);
            board.Tiles[1, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn); // Prevents dead position draw
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn); // Prevents dead position draw
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        PlayNonRepeatingMoves(engine, 100);

        Assert.That(engine.CanClaimFiftyMoveRule, Is.True);

        var claimResult = engine.ClaimDrawByFiftyMoveRule(_whitePlayer);

        Assert.Multiple(() =>
        {
            Assert.That(claimResult, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawBy50MoveRule));
        });
    }

    [Test]
    public void TryMakeMove_WhenHalfMoveClockReachesOneHundredFifty_EndsGameAutomaticallyWithDrawBy75MoveRule()
    {
        var initializer = new CustomBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Knight);
            board.Tiles[1, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn); // Prevents dead position draw
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn); // Prevents dead position draw
        });
        var engine = CreateEngine(initializer);
        engine.StartGame();

        PlayNonRepeatingMoves(engine, 149);
        Assert.That(engine.State, Is.EqualTo(GameState.InProgress));

        // Move 150 (Black King moves back to (7, 7), which is index 4 in blackKingMoves)
        var move150 = new Move(new Position(6, 7), new Position(7, 7));
        var result = engine.TryMakeMove(move150);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawBy75MoveRule));
        });
    }

    [Test]
    public void TryMakeMove_PositionOccursFiveTimes_EndsGameAutomaticallyWithFivefoldRepetition()
    {
        var engine = CreateEngine();
        engine.StartGame();

        // Initial position = 1st occurrence
        // 4 cycles of Nf3 Nf6 Ng1 Ng8 -> 4 more occurrences = 5 times total
        for (var i = 0; i < 3; i++)
        {
            engine.TryMakeMove(new Move(new Position(0, 6), new Position(2, 5)));
            engine.TryMakeMove(new Move(new Position(7, 6), new Position(5, 5)));
            engine.TryMakeMove(new Move(new Position(2, 5), new Position(0, 6)));
            engine.TryMakeMove(new Move(new Position(5, 5), new Position(7, 6)));
        }

        // Cycle 4
        engine.TryMakeMove(new Move(new Position(0, 6), new Position(2, 5)));
        engine.TryMakeMove(new Move(new Position(7, 6), new Position(5, 5)));
        engine.TryMakeMove(new Move(new Position(2, 5), new Position(0, 6)));
        var fifthOccurrenceMove = engine.TryMakeMove(new Move(new Position(5, 5), new Position(7, 6)));

        Assert.Multiple(() =>
        {
            Assert.That(fifthOccurrenceMove, Is.True);
            Assert.That(engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(engine.Result?.Reason, Is.EqualTo(GameEndReason.DrawByFivefoldRepetition));
        });
    }

    #endregion
}
