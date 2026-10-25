using System.Collections.Concurrent;
using Api.Constants;
using Api.DTOs;
using Api.Enums;
using Api.Events;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;

namespace Api.GameEngine;

public class ChessChessGameEngine : IChessGameEngine
{
    #region Fields

    private readonly IPlayer _blackPlayer, _whitePlayer;
    private IPlayer? _drawOfferer;

    private readonly Dictionary<PieceColor, List<IChessPiece>> _captured = new()
    {
        [PieceColor.White] = [],
        [PieceColor.Black] = []
    };

    private readonly ConcurrentDictionary<PieceColor, Timer> _countdownTimers = new();

    private readonly List<MoveHistoryEntry> _moveHistory = [];
    private readonly List<string> _positionHistory = [];
    private readonly TimeSpan _initialTime;

    private int _halfMoveClock;

    #endregion

    #region Constructor

    /// <summary>
    ///     Wires the engine to the room's board and players, stores the clock
    ///     length, and leaves the game in <see cref="GameState.NotStarted" />:
    ///     white to move, full castling rights, and both clocks set to
    ///     <paramref name="initialTime" />. Nothing is broadcast until
    ///     <see cref="StartGame" /> is called.
    /// </summary>
    public ChessChessGameEngine(IBoard board, IPlayer whitePlayer, IPlayer blackPlayer, TimeSpan initialTime)
    {
        Board = board;
        _whitePlayer = whitePlayer;
        _blackPlayer = blackPlayer;
        _initialTime = initialTime;
        CurrentPlayer = whitePlayer;
        State = GameState.NotStarted;
        // Dinonaktifkan: promosi selalu default Queen dari langkah legal. Lihat notes.md.
        // PromotionSelector = _ => PieceType.Queen;
        CastlingRights = new CastlingRights(true, true, true, true);

        ResetTimers();
    }

    #endregion

    #region Properties

    public Dictionary<PieceColor, IReadOnlyList<IChessPiece>> CapturedPiecesTracker => new()
    {
        [PieceColor.White] = _captured[PieceColor.White],
        [PieceColor.Black] = _captured[PieceColor.Black]
    };

    public IBoard Board { get; }
    public IPlayer CurrentPlayer { get; private set; }
    public GameState State { get; private set; }
    public GameResult? Result { get; private set; }

    public PieceColor? CheckedColor
    {
        get
        {
            if (IsKingInCheck(Board, PieceColor.White))
                return PieceColor.White;
            if (IsKingInCheck(Board, PieceColor.Black))
                return PieceColor.Black;

            return null;
        }
    }

    public IReadOnlyList<MoveHistoryEntry> MoveHistory => _moveHistory;
    public Dictionary<PieceColor, IPlayerTimer> PlayerTimeTracker { get; } = new();
    public CastlingRights CastlingRights { get; private set; }
    public Position? EnPassantTarget { get; private set; }

    /// <summary>
    ///     Tells whether the side to move may claim a draw by threefold
    ///     repetition right now. Exposed so the API can offer the claim.
    /// </summary>
    public bool CanClaimThreefoldRepetition =>
        State == GameState.InProgress && IsThreefoldRepetitionClaimAvailable();

    /// <summary>
    ///     Tells whether the side to move may claim a draw by the fifty-move
    ///     rule right now. Exposed so the API can offer the claim.
    /// </summary>
    public bool CanClaimFiftyMoveRule =>
        State == GameState.InProgress && IsFiftyMoveRuleClaimAvailable();

    #endregion

    #region Events

    // Dinonaktifkan: state room disiarkan lewat SnapshotChangedEventHandler. Lihat notes.md.
    // public event EventHandler<PieceMovedEventArgs>? PieceMovedEventHandler;
    // Dinonaktifkan: klik kotak ditangani frontend. Lihat notes.md.
    // public event EventHandler<SquareClickedEventArgs>? SquareClickedEventHandler;
    public event EventHandler<SnapshotChangedEventArgs>? SnapshotChangedEventHandler;
    public event EventHandler<PieceCapturedEventArgs>? PieceCapturedEventHandler;
    public event EventHandler<TurnChangedEventArgs>? TurnChangedEventHandler;
    public event EventHandler<CheckEventArgs>? CheckDeclaredEventHandler;
    public event EventHandler<GameEndedEventArgs>? GameEndedEventHandler;
    public event EventHandler<DrawOfferedEventArgs>? DrawOfferedEventHandler;
    public event EventHandler<DrawDeclinedEventArgs>? DrawDeclinedEventHandler;
    public event EventHandler<TimerTickEventArgs>? TimerTickedEventHandler;

    #endregion

    #region Game Lifecycle

    /// <summary>
    ///     Starts a fresh game from the initial position: white to move, clock
    ///     started for white, history and captured pieces cleared, then the new
    ///     state is broadcast through <see cref="SnapshotChangedEventHandler" />.
    /// </summary>
    public void StartGame()
    {
        Result = null;
        _drawOfferer = null;
        State = GameState.InProgress;
        CurrentPlayer = _whitePlayer;

        SetupInitialBoardPosition();
        Reset();
        ResetTimers();

        _moveHistory.Clear();
        _captured[PieceColor.White].Clear();
        _captured[PieceColor.Black].Clear();
        _positionHistory.Add(GeneratePositionKey(Board, CurrentPlayer.Color));
        StartCountdown(CurrentPlayer.Color);

        SnapshotChangedEventHandler?.Invoke(
            this,
            new SnapshotChangedEventArgs(Board, _moveHistory)
        );
    }

    /// <summary>
    ///     Puts the game back to <see cref="GameState.NotStarted" /> with a fresh
    ///     board and stopped timers, without starting a new game — the room waits
    ///     until <see cref="StartGame" /> is called again.
    /// </summary>
    public void ResetGame()
    {
        State = GameState.NotStarted;
        Result = null;
        _drawOfferer = null;
        CurrentPlayer = _whitePlayer;

        StopAllCountdowns();
        SetupInitialBoardPosition();
        Reset();
        ResetTimers();

        _moveHistory.Clear();
        _captured[PieceColor.White].Clear();
        _captured[PieceColor.Black].Clear();

        SnapshotChangedEventHandler?.Invoke(
            this,
            new SnapshotChangedEventArgs(Board, _moveHistory)
        );
    }

    #endregion

    #region Move Handling

    /// <summary>
    ///     Validates <paramref name="move" /> against the legal moves of the
    ///     current player — a promotion move must match the requested
    ///     <see cref="Move.PromotionType" /> — then plays it: updates castling
    ///     rights, the en passant target, captured pieces, and the move history,
    ///     raises the capture and check events, and either ends the game on an
    ///     automatic result or hands the turn over before broadcasting the new
    ///     snapshot. Returns <c>false</c> when the game is not in progress or the
    ///     move is not legal.
    /// </summary>
    public bool TryMakeMove(Move move)
    {
        if (State != GameState.InProgress)
            return false;

        if (!IsInBounds(move.From) || !IsInBounds(move.To))
            return false;

        var piece = GetPieceAt(move.From);
        if (piece is null || piece.Color != CurrentPlayer.Color)
            return false;

        var legalMoves = GetLegalMoves(move.From);
        Move? chosenMove = null;
        foreach (var legalMove in legalMoves)
        {
            if (legalMove.From != move.From || legalMove.To != move.To)
                continue;

            if (move.PromotionType is not null)
            {
                if (move.PromotionType != legalMove.PromotionType)
                    continue;

                chosenMove = legalMove;
                break;
            }

            if (legalMove.PromotionType is null)
            {
                chosenMove = legalMove;
                break;
            }

            chosenMove ??= legalMove;
        }

        if (chosenMove is null)
            return false;

        var effective = chosenMove;
        var isPawn = piece.Type == PieceType.Pawn;
        var moverColor = piece.Color;
        var opponent = moverColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        if (
            effective.PromotionType is null && isPawn &&
            IsOnPromotionRank(effective.To, piece.Color)
        )
            effective = effective with { PromotionType = PieceType.Queen };

        var targetPiece = GetPieceAt(effective.To);
        var isEnPassant = isPawn && EnPassantTarget.HasValue &&
                          effective.To == EnPassantTarget && targetPiece is null;
        var isCapture = targetPiece is not null || isEnPassant;

        var applyMoveResult = ApplyMove(Board, effective);
        if (applyMoveResult is null)
            return false;

        UpdateCastlingRights(effective, piece, applyMoveResult.CapturedPiece);
        UpdateEnPassantTarget(effective, piece);

        if (applyMoveResult.CapturedPiece is not null)
            _captured[piece.Color].Add(applyMoveResult.CapturedPiece);


        _moveHistory.Add(new MoveHistoryEntry(effective, moverColor));
        _drawOfferer = null;

        RecordCompletedMove(Board, opponent, isPawn, isCapture);

        if (applyMoveResult.CapturedPiece is not null && applyMoveResult.CapturedSquare.HasValue)
            PieceCapturedEventHandler?.Invoke(
                this,
                new PieceCapturedEventArgs(
                    applyMoveResult.CapturedPiece,
                    applyMoveResult.CapturedSquare.Value
                )
            );

        if (IsKingInCheck(Board, opponent))
            CheckDeclaredEventHandler?.Invoke(
                this,
                new CheckEventArgs(opponent)
            );

        if (EvaluateAutomaticResult() is { } automaticResult)
        {
            EndGame(automaticResult);
            SnapshotChangedEventHandler?.Invoke(
                this,
                new SnapshotChangedEventArgs(Board, _moveHistory)
            );

            return true;
        }

        ChangeTurn();
        SnapshotChangedEventHandler?.Invoke(
            this,
            new SnapshotChangedEventArgs(Board, _moveHistory)
        );

        return true;
    }

    /// <summary>
    ///     Tells whether the given position lies on the 8x8 board, i.e. both
    ///     column and row are between 0 and 7.
    /// </summary>
    public bool IsInBounds(Position pos)
    {
        return pos.Column is >= 0 and < 8 && pos.Row is >= 0 and < 8;
    }

    /// <summary>
    ///     Returns every legal move available to the side to move, across all
    ///     its pieces. The frontend prefetches this once per position so a
    ///     piece selection can be resolved without another request.
    /// </summary>
    public IReadOnlyList<Move> GetAllLegalMoves()
    {
        var legalMoves = new List<Move>();
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var pos = new Position(r, c);
            var piece = GetPieceAt(pos);
            if (piece is null || piece.Color != CurrentPlayer.Color)
                continue;

            legalMoves.AddRange(GetLegalMoves(pos));
        }

        return legalMoves;
    }

    /// <summary>
    ///     Returns the legal moves of the piece on the given square, or an empty
    ///     list when the square is off-board or holds no piece of the side to
    ///     move. Batch callers use <see cref="GetAllLegalMoves" />.
    ///     Declared private although the class diagram shows it public, because
    ///     only <see cref="TryMakeMove" /> and <see cref="GetAllLegalMoves" />
    ///     call it.
    /// </summary>
    private IReadOnlyList<Move> GetLegalMoves(Position from)
    {
        if (!IsInBounds(from))
            return [];

        var pos = new Position(from.Row, from.Column);
        var piece = GetPieceAt(pos);
        if (piece is null || piece.Color != CurrentPlayer.Color)
            return [];

        var pseudoMoves = GenerateMovesForPiece(from);
        var legalMoves = new List<Move>();
        foreach (var pseudoMove in pseudoMoves)
            if (!WouldLeaveKingInCheck(Board, pseudoMove))
                legalMoves.Add(pseudoMove);

        return legalMoves;
    }

    /// <summary>
    ///     Returns the moves played by the given colour, in play order.
    /// </summary>
    public IReadOnlyList<MoveHistoryEntry> GetMoveHistory(PieceColor color)
    {
        return _moveHistory
            .Where(e => e.PlayedBy == color)
            .ToList();
    }

    #endregion

    #region Draw & Resign

    /// <summary>
    ///     Records the given player as the one offering a draw and notifies the
    ///     room; ignored when the game is not in progress.
    /// </summary>
    public void OfferDraw(IPlayer offerer)
    {
        if (State != GameState.InProgress)
            return;

        _drawOfferer = offerer;
        DrawOfferedEventHandler?.Invoke(
            this,
            new DrawOfferedEventArgs(offerer)
        );
    }

    /// <summary>
    ///     Settles the pending draw offer; ignored when no offer exists or the
    ///     responder is the offerer. Accepting ends the game as a draw by
    ///     agreement, declining notifies the room and play continues.
    /// </summary>
    public void RespondToDrawOffer(IPlayer responder, bool accepted)
    {
        if (_drawOfferer is null || responder.Color == _drawOfferer.Color)
            return;

        if (accepted)
            EndGame(
                new GameResult(
                    GameEndReason.DrawByAgreement,
                    null
                )
            );
        else
            DrawDeclinedEventHandler?.Invoke(
                this,
                new DrawDeclinedEventArgs(_drawOfferer)
            );

        _drawOfferer = null;
    }

    /// <summary>
    ///     Ends the game as a draw by threefold repetition when the current
    ///     position has occurred three times and the claiming side asks for it.
    ///     Returns <c>false</c>, leaving the game untouched, when the game is
    ///     not in progress or the claim is not available.
    /// </summary>
    public bool ClaimDrawByThreefoldRepetition(IPlayer player)
    {
        if (State != GameState.InProgress)
            return false;

        if (!IsThreefoldRepetitionClaimAvailable())
            return false;

        EndGame(
            new GameResult(
                GameEndReason.DrawByThreefoldRepetition,
                null
            )
        );

        return true;
    }

    /// <summary>
    ///     Ends the game as a draw by the fifty-move rule when fifty moves by
    ///     each side have passed without a pawn move or a capture, and the
    ///     claiming side asks for it. Returns <c>false</c>, leaving the game
    ///     untouched, when the game is not in progress or the claim is not
    ///     available.
    /// </summary>
    public bool ClaimDrawByFiftyMoveRule(IPlayer player)
    {
        if (State != GameState.InProgress)
            return false;

        if (!IsFiftyMoveRuleClaimAvailable())
            return false;

        EndGame(
            new GameResult(
                GameEndReason.DrawBy50MoveRule,
                null
            )
        );

        return true;
    }

    /// <summary>
    ///     Resigns on behalf of the given player, awarding the win to the
    ///     opponent; ignored when the game is not in progress.
    /// </summary>
    public void Resign(IPlayer player)
    {
        if (State != GameState.InProgress)
            return;

        var winner = player.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;

        EndGame(
            new GameResult(
                GameEndReason.PlayerResigned,
                winner
            )
        );
    }

    #endregion

    #region Game End & Board Setup

    /// <summary>
    ///     Marks the game as completed with the given result, stops the clocks,
    ///     and notifies the room; ignored when the game has already ended.
    /// </summary>
    private void EndGame(GameResult result)
    {
        if (State == GameState.Completed)
            return;

        State = GameState.Completed;
        Result = result;

        StopAllCountdowns();

        GameEndedEventHandler?.Invoke(
            this,
            new GameEndedEventArgs(result)
        );
    }

    /// <summary>
    ///     Clears the board and places the standard starting position: back-rank
    ///     pieces on rows 0 and 7, pawns on rows 1 and 6, then resets castling
    ///     rights and the en passant target.
    ///     Declared private although the class diagram shows it public, because it
    ///     is only called by <see cref="StartGame" /> and <see cref="ResetGame" />.
    /// </summary>
    private void SetupInitialBoardPosition()
    {
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
            Board.Tiles[r, c].Piece = null;

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

        for (var c = 0; c < 8; c++)
        {
            Board.Tiles[0, c].Piece = new ChessPiece(PieceColor.White, backRankPieces[c]);
            Board.Tiles[1, c].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            Board.Tiles[6, c].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
            Board.Tiles[7, c].Piece = new ChessPiece(PieceColor.Black, backRankPieces[c]);
        }

        CastlingRights = new CastlingRights(
            true,
            true,
            true,
            true
        );
        EnPassantTarget = null;
    }

    #endregion

    #region Board Access

    /// <summary>
    ///     Returns the board as 64 <see cref="TileDto" /> rows/columns, the shape
    ///     the HTTP board endpoint and the snapshot payload both send to clients.
    /// </summary>
    public IReadOnlyList<TileDto> GetBoard()
    {
        var tiles = new List<TileDto>(64);
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var pos = new Position(r, c);
            var piece = GetPieceAt(pos);
            tiles.Add(
                new TileDto(
                    r,
                    c,
                    piece?.Color,
                    piece?.Type
                )
            );
        }

        return tiles;
    }


    /// <summary>
    ///     Returns the tile at the given square, or <c>null</c> when the
    ///     position lies off the board.
    /// </summary>
    private IChessTile? GetTileAt(Position pos)
    {
        return !IsInBounds(pos) ? null : Board.Tiles[pos.Row, pos.Column];
    }

    /// <summary>
    ///     Returns the piece standing on the given square, or <c>null</c> when
    ///     the square is empty or lies off the board.
    /// </summary>
    private IChessPiece? GetPieceAt(Position pos)
    {
        return GetTileAt(pos)?.Piece ?? null;
    }

    #endregion

    #region Move Generation

    /// <summary>
    ///     Generates the pseudo-legal moves for the piece on the given square:
    ///     pawn moves, knight jumps, sliding moves for bishop/rook/queen, or king
    ///     steps plus castling. Returns an empty list when the square is empty.
    ///     King safety is checked afterwards by <see cref="GetLegalMoves" />.
    /// </summary>
    private List<Move> GenerateMovesForPiece(Position from)
    {
        var piece = GetPieceAt(from);

        if (piece is null)
            return [];

        switch (piece.Type)
        {
            case PieceType.Pawn:
                return GeneratePawnMoves(from);
            case PieceType.Knight:
                return GenerateKnightMoves(from);
            case PieceType.Bishop or PieceType.Rook or PieceType.Queen:
                return GenerateSlidingMoves(from);
            case PieceType.King:
            {
                var moves = GenerateKingMoves(from);
                moves.AddRange(GetCastlingMoves(from));

                return moves;
            }

            default:
                return [];
        }
    }

    /// <summary>
    ///     Generates the pseudo-legal pawn moves from the given square: one step
    ///     forward when empty, two steps from the starting rank, diagonal
    ///     captures of enemy pieces, and en passant. Promotion targets are
    ///     expanded into the four promotion choices by
    ///     <see cref="AddPawnMove" />. King safety is checked afterwards by
    ///     <see cref="GetLegalMoves" />.
    /// </summary>
    private List<Move> GeneratePawnMoves(Position fromPos)
    {
        var moves = new List<Move>();
        var piece = GetPieceAt(fromPos);
        if (piece is null)
            return moves;

        var dir = piece.Color == PieceColor.White ? 1 : -1;
        var startRow = piece.Color == PieceColor.White ? 1 : 6;
        var one = fromPos with { Row = fromPos.Row + dir };
        if (IsInBounds(one) && GetPieceAt(one) is null)
        {
            AddPawnMove(moves, fromPos, one, piece.Color);

            var two = fromPos with { Row = fromPos.Row + 2 * dir };
            if (fromPos.Row == startRow && IsInBounds(two) && GetPieceAt(two) is null)
                moves.Add(new Move(fromPos, two));
        }

        foreach (var dc in SquareOffsets.PawnCaptureColumns)
        {
            var toPos = new Position(
                fromPos.Row + dir,
                fromPos.Column + dc
            );
            if (!IsInBounds(toPos))
                continue;

            var targetPiece = GetPieceAt(toPos);
            if (targetPiece is not null && targetPiece.Color != piece.Color)
                AddPawnMove(moves, fromPos, toPos, piece.Color);
            else if (targetPiece is null && EnPassantTarget.HasValue && toPos == EnPassantTarget.Value)
                moves.Add(new Move(fromPos, toPos));
        }

        return moves;
    }

    /// <summary>
    ///     Appends the move to <paramref name="moves" />: a plain move normally,
    ///     or the four promotion choices — queen, rook, bishop, knight — when
    ///     the target lies on the promotion rank.
    /// </summary>
    private void AddPawnMove(List<Move> moves, Position fromPos, Position toPos, PieceColor pieceColor)
    {
        if (IsOnPromotionRank(toPos, pieceColor))
        {
            moves.Add(new Move(fromPos, toPos, PieceType.Queen));
            moves.Add(new Move(fromPos, toPos, PieceType.Rook));
            moves.Add(new Move(fromPos, toPos, PieceType.Bishop));
            moves.Add(new Move(fromPos, toPos, PieceType.Knight));
        }
        else
        {
            moves.Add(new Move(fromPos, toPos));
        }
    }

    /// <summary>
    ///     Generates the pseudo-legal knight jumps from the given square: up to
    ///     eight L-shaped targets, keeping only squares that are empty or hold
    ///     an enemy piece.
    /// </summary>
    private List<Move> GenerateKnightMoves(Position from)
    {
        var moves = new List<Move>();
        var piece = GetPieceAt(from);

        if (piece is null)
            return moves;

        foreach (var offset in SquareOffsets.Knight)
        {
            var to = from + offset;
            if (!IsInBounds(to))
                continue;

            var targetPiece = GetPieceAt(to);
            if (targetPiece is null || targetPiece.Color != piece.Color)
                moves.Add(new Move(from, to));
        }

        return moves;
    }

    /// <summary>
    ///     Generates the pseudo-legal sliding moves from the given square: four
    ///     diagonal directions for a bishop, four straight ones for a rook, and
    ///     all eight for a queen. Each ray stops after capturing an enemy piece
    ///     or before a friendly one.
    /// </summary>
    private List<Move> GenerateSlidingMoves(Position from)
    {
        var moves = new List<Move>();
        var piece = GetPieceAt(from);
        if (piece is null)
            return moves;

        var directions = piece.Type switch
        {
            PieceType.Bishop => SquareOffsets.Diagonal,
            PieceType.Rook => SquareOffsets.Straight,
            _ => SquareOffsets.Queen
        };

        foreach (var offset in directions)
        {
            var to = from + offset;
            while (IsInBounds(to))
            {
                var targetPiece = GetPieceAt(to);
                if (targetPiece is null)
                {
                    moves.Add(new Move(from, to));
                }
                else
                {
                    if (targetPiece.Color != piece.Color)
                        moves.Add(new Move(from, to));
                    break;
                }

                to += offset;
            }
        }

        return moves;
    }

    /// <summary>
    ///     Generates the pseudo-legal king steps from the given square: the eight
    ///     neighbouring squares that are empty or hold an enemy piece. Castling
    ///     is handled separately by <see cref="GetCastlingMoves" />.
    /// </summary>
    private List<Move> GenerateKingMoves(Position from)
    {
        var moves = new List<Move>();
        var piece = GetPieceAt(from);
        if (piece is null)
            return moves;

        foreach (var offset in SquareOffsets.King)
        {
            var to = from + offset;
            if (!IsInBounds(to))
                continue;

            var targetPiece = GetPieceAt(to);
            if (targetPiece is null || targetPiece.Color != piece.Color)
                moves.Add(new Move(from, to));
        }

        return moves;
    }

    /// <summary>
    ///     Generates the castling moves available to the king on its starting
    ///     square: the right must still be held, the squares between king and
    ///     rook must be empty, the rook must sit on its home square, and neither
    ///     the king's square nor the squares it crosses may be attacked. Returns
    ///     an empty list for any other piece or square; the destination square
    ///     is validated afterwards by <see cref="GetLegalMoves" />.
    /// </summary>
    private List<Move> GetCastlingMoves(Position from)
    {
        var moves = new List<Move>();
        var piece = GetPieceAt(from);
        if (piece is null || piece.Type != PieceType.King)
            return moves;

        if (IsKingInCheck(Board, piece.Color))
            return moves;

        var enemyColor = piece.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        if (piece.Color == PieceColor.White && from == new Position(0, 4))
        {
            if (
                CastlingRights.WhiteKingSide &&
                GetPieceAt(new Position(0, 5)) is null &&
                GetPieceAt(new Position(0, 6)) is null &&
                GetPieceAt(new Position(0, 7)) is { Type: PieceType.Rook, Color: PieceColor.White } &&
                !IsSquareAttacked(Board, new Position(0, 5), enemyColor) &&
                !IsSquareAttacked(Board, new Position(0, 6), enemyColor)
            ) moves.Add(new Move(from, new Position(0, 6)));

            if (
                CastlingRights.WhiteQueenSide &&
                GetPieceAt(new Position(0, 1)) is null &&
                GetPieceAt(new Position(0, 2)) is null &&
                GetPieceAt(new Position(0, 3)) is null &&
                GetPieceAt(new Position(0, 0)) is { Type: PieceType.Rook, Color: PieceColor.White } &&
                !IsSquareAttacked(Board, new Position(0, 3), enemyColor) &&
                !IsSquareAttacked(Board, new Position(0, 2), enemyColor)
            ) moves.Add(new Move(from, new Position(0, 2)));
        }

        if (piece.Color == PieceColor.Black && from == new Position(7, 4))
        {
            if (
                CastlingRights.BlackKingSide &&
                GetPieceAt(new Position(7, 5)) is null &&
                GetPieceAt(new Position(7, 6)) is null &&
                GetPieceAt(new Position(7, 7)) is { Type: PieceType.Rook, Color: PieceColor.Black } &&
                !IsSquareAttacked(Board, new Position(7, 5), enemyColor) &&
                !IsSquareAttacked(Board, new Position(7, 6), enemyColor)
            ) moves.Add(new Move(from, new Position(7, 6)));

            if (
                CastlingRights.BlackQueenSide &&
                GetPieceAt(new Position(7, 1)) is null &&
                GetPieceAt(new Position(7, 2)) is null &&
                GetPieceAt(new Position(7, 3)) is null &&
                GetPieceAt(new Position(7, 0)) is { Type: PieceType.Rook, Color: PieceColor.Black } &&
                !IsSquareAttacked(Board, new Position(7, 3), enemyColor) &&
                !IsSquareAttacked(Board, new Position(7, 2), enemyColor)
            ) moves.Add(new Move(from, new Position(7, 2)));
        }

        return moves;
    }

    /// <summary>
    ///     Tells whether the given square is the far rank for that colour, i.e.
    ///     row 7 for white and row 0 for black — the rank where a pawn promotes.
    /// </summary>
    private bool IsOnPromotionRank(Position position, PieceColor color)
    {
        return color == PieceColor.White ? position.Row == 7 : position.Row == 0;
    }

    #endregion

    #region Check Detection

    /// <summary>
    ///     Tells whether any piece of <paramref name="byColor" /> attacks the given
    ///     square: pawn capture squares, knight and king offsets, then bishop/queen
    ///     rays along the diagonals and rook/queen rays along the files and ranks.
    ///     A ray stops at the first piece, so a blocker makes the square safe. This
    ///     is a pure board query — it ignores whose turn it is and does not check
    ///     move legality. Used for king safety, castling, and checkmate detection.
    /// </summary>
    private bool IsSquareAttacked(IBoard board, Position pos, PieceColor byColor)
    {
        var pawnRow = byColor == PieceColor.White ? pos.Row - 1 : pos.Row + 1;
        foreach (var dc in SquareOffsets.PawnCaptureColumns)
        {
            var newPos = new Position(pawnRow, pos.Column + dc);
            if (!IsInBounds(newPos))
                continue;

            var piece = board.Tiles[newPos.Row, newPos.Column].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.Pawn)
                return true;
        }

        foreach (var offset in SquareOffsets.Knight)
        {
            var newPos = pos + offset;
            if (!IsInBounds(newPos))
                continue;

            var piece = board.Tiles[newPos.Row, newPos.Column].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.Knight)
                return true;
        }

        foreach (var offset in SquareOffsets.King)
        {
            var newPos = pos + offset;
            if (!IsInBounds(newPos))
                continue;

            var piece = board.Tiles[newPos.Row, newPos.Column].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.King)
                return true;
        }

        foreach (var offset in SquareOffsets.Diagonal)
        {
            var newPos = pos + offset;
            while (IsInBounds(newPos))
            {
                var piece = board.Tiles[newPos.Row, newPos.Column].Piece;
                if (piece is not null)
                {
                    if (piece.Color == byColor && piece.Type is PieceType.Bishop or PieceType.Queen)
                        return true;

                    break;
                }

                newPos += offset;
            }
        }

        foreach (var offset in SquareOffsets.Straight)
        {
            var newPos = pos + offset;
            while (IsInBounds(newPos))
            {
                var piece = board.Tiles[newPos.Row, newPos.Column].Piece;
                if (piece is not null)
                {
                    if (piece.Color == byColor && piece.Type is PieceType.Rook or PieceType.Queen)
                        return true;

                    break;
                }

                newPos += offset;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether the king of the given colour is attacked: finds that
    ///     king on <paramref name="board" /> and asks <see cref="IsSquareAttacked" />
    ///     whether the opposite colour attacks its square. The board may be a
    ///     clone, so callers can test a hypothetical move before playing it.
    ///     Returns <c>false</c> when no king of that colour is on the board.
    /// </summary>
    private bool IsKingInCheck(IBoard board, PieceColor color)
    {
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var piece = board.Tiles[r, c].Piece;
            if (piece is null || piece.Color != color || piece.Type != PieceType.King)
                continue;

            var enemyColor = color == PieceColor.White ? PieceColor.Black : PieceColor.White;

            return IsSquareAttacked(
                board,
                new Position(r, c),
                enemyColor
            );
        }

        return false;
    }

    /// <summary>
    ///     Tells whether playing the given move would leave the mover's own king
    ///     in check. The move is applied to a clone of the board first, so the real
    ///     board is untouched. A move with no piece on its origin square cannot be
    ///     played, so it is treated as unsafe.
    /// </summary>
    private bool WouldLeaveKingInCheck(IBoard board, Move move)
    {
        var cloneBoard = new Board(board);
        var applied = ApplyMove(cloneBoard, move);
        if (applied is null)
            return true;

        return IsKingInCheck(cloneBoard, applied.MovingPiece.Color);
    }

    /// <summary>
    ///     Tells whether the given colour still has at least one legal move,
    ///     stopping at the first one found: pseudo-legal moves are generated for
    ///     each occupied square and filtered by <see cref="WouldLeaveKingInCheck" />.
    ///     Combined with <see cref="IsKingInCheck" /> it separates checkmate
    ///     (in check, no moves) from stalemate (not in check, no moves).
    /// </summary>
    private bool HasAnyLegalMove(IBoard board, PieceColor color)
    {
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var piece = board.Tiles[r, c].Piece;
            if (piece is null || piece.Color != color)
                continue;

            var from = new Position(r, c);
            var moves = GenerateMovesForPiece(from);

            foreach (var move in moves)
                if (!WouldLeaveKingInCheck(board, move))
                    return true;
        }

        return false;
    }

    #endregion

    #region Move Application

    /// <summary>
    ///     Applies the move to the given board and returns what changed: the
    ///     moving piece, the captured piece if any, and the square it came from.
    ///     Returns <c>null</c> and leaves the board untouched when the origin
    ///     square holds no piece.
    /// </summary>
    private MoveApplicationResult? ApplyMove(IBoard board, Move move)
    {
        var fromTile = board.Tiles[move.From.Row, move.From.Column];
        var movingPiece = fromTile.Piece;
        if (movingPiece is null)
            return null;

        var toTile = board.Tiles[move.To.Row, move.To.Column];
        var capturedPiece = toTile.Piece;

        Position? capturedPos = capturedPiece is not null ? move.To : null;

        if (
            movingPiece.Type == PieceType.Pawn &&
            EnPassantTarget.HasValue &&
            move.To == EnPassantTarget &&
            capturedPiece is null
        )
        {
            var capturedPawnPos = new Position(move.From.Row, move.To.Column);
            capturedPiece = board.Tiles[capturedPawnPos.Row, capturedPawnPos.Column].Piece;
            capturedPos = capturedPawnPos;
            board.Tiles[capturedPawnPos.Row, capturedPawnPos.Column].Piece = null;
        }

        fromTile.Piece = null;
        if (movingPiece.Type == PieceType.Pawn && IsOnPromotionRank(move.To, movingPiece.Color))
            toTile.Piece = new ChessPiece(
                movingPiece.Color,
                move.PromotionType ?? PieceType.Queen
            );
        else
            toTile.Piece = movingPiece;

        if (movingPiece.Type != PieceType.King || Math.Abs(move.To.Column - move.From.Column) != 2)
            return new MoveApplicationResult(
                movingPiece,
                capturedPiece,
                capturedPos
            );

        switch (move.To.Column)
        {
            case 6:
            {
                var rook = board.Tiles[move.From.Row, 7].Piece;
                board.Tiles[move.From.Row, 7].Piece = null;
                board.Tiles[move.From.Row, 5].Piece = rook;
                break;
            }
            case 2:
            {
                var rook = board.Tiles[move.From.Row, 0].Piece;
                board.Tiles[move.From.Row, 0].Piece = null;
                board.Tiles[move.From.Row, 3].Piece = rook;
                break;
            }
        }

        return new MoveApplicationResult(
            movingPiece,
            capturedPiece,
            capturedPos
        );
    }

    /// <summary>
    ///     Revokes the castling rights that the just-played move invalidated:
    ///     both rights of a side whose king moved, the matching side when a rook
    ///     leaves its home square, and the matching side when a rook is captured
    ///     on its home square. Everything else is left untouched.
    /// </summary>
    private void UpdateCastlingRights(Move move, IChessPiece movingPiece, IChessPiece? capturedPiece)
    {
        var currentCastlingRights = CastlingRights;
        if (movingPiece.Type == PieceType.King)
        {
            if (movingPiece.Color == PieceColor.White)
                currentCastlingRights = currentCastlingRights with
                {
                    WhiteKingSide = false,
                    WhiteQueenSide = false
                };
            else
                currentCastlingRights = currentCastlingRights with
                {
                    BlackKingSide = false,
                    BlackQueenSide = false
                };
        }

        if (movingPiece.Type == PieceType.Rook)
        {
            if (move.From == new Position(0, 0))
                currentCastlingRights = currentCastlingRights with
                {
                    WhiteQueenSide = false
                };

            if (move.From == new Position(0, 7))
                currentCastlingRights = currentCastlingRights with
                {
                    WhiteKingSide = false
                };

            if (move.From == new Position(7, 0))
                currentCastlingRights = currentCastlingRights with
                {
                    BlackQueenSide = false
                };

            if (move.From == new Position(7, 7))
                currentCastlingRights = currentCastlingRights with
                {
                    BlackKingSide = false
                };
        }

        if (capturedPiece is not null && capturedPiece.Type == PieceType.Rook)
        {
            if (move.To == new Position(0, 0))
                currentCastlingRights = currentCastlingRights with
                {
                    WhiteQueenSide = false
                };

            if (move.To == new Position(0, 7))
                currentCastlingRights = currentCastlingRights with
                {
                    WhiteKingSide = false
                };

            if (move.To == new Position(7, 0))
                currentCastlingRights = currentCastlingRights with
                {
                    BlackQueenSide = false
                };

            if (move.To == new Position(7, 7))
                currentCastlingRights = currentCastlingRights with
                {
                    BlackKingSide = false
                };
        }

        CastlingRights = currentCastlingRights;
    }

    /// <summary>
    ///     Sets EnPassantTarget to the square a pawn skipped when it just moved
    ///     two squares forward, or clears it for any other move because the right
    ///     expires after one move. The board parameter was removed: the skipped
    ///     square follows from the move alone (same column, midpoint row), so
    ///     reading the board was unnecessary.
    /// </summary>
    private void UpdateEnPassantTarget(Move move, IChessPiece movingPiece)
    {
        if (movingPiece.Type == PieceType.Pawn && Math.Abs(move.To.Row - move.From.Row) == 2)
            EnPassantTarget = move.From with
            {
                Row = (move.From.Row + move.To.Row) / 2
            };
        else
            EnPassantTarget = null;
    }

    #endregion

    #region Position, Result & Turn

    /// <summary>
    ///     Builds a compact key for the current position (piece placement, side
    ///     to move, castling rights, en passant target) used to detect repeated
    ///     positions.
    ///     Private because only GameManager itself calls it: it records the key
    ///     and counts repeats in _positionHistory.
    /// </summary>
    private string GeneratePositionKey(IBoard board, PieceColor sideToMove)
    {
        var key = string.Empty;
        for (var r = 7; r >= 0; r--)
        for (var c = 0; c < 8; c++)
        {
            var p = board.Tiles[r, c].Piece;
            if (p is null)
            {
                key += '-';
                continue;
            }

            var ch = p.Type switch
            {
                PieceType.King => 'k',
                PieceType.Queen => 'q',
                PieceType.Rook => 'r',
                PieceType.Bishop => 'b',
                PieceType.Knight => 'n',
                _ => 'p'
            };

            if (p.Color == PieceColor.White)
                ch = char.ToUpperInvariant(ch);

            key += ch;
        }

        key += sideToMove == PieceColor.White ? 'w' : 'b';
        key += CastlingRights.WhiteKingSide ? 'K' : '-';
        key += CastlingRights.WhiteQueenSide ? 'Q' : '-';
        key += CastlingRights.BlackKingSide ? 'k' : '-';
        key += CastlingRights.BlackQueenSide ? 'q' : '-';

        if (EnPassantTarget.HasValue)
        {
            var enPassantCol = (char)('a' + EnPassantTarget.Value.Column);
            var enPassantRow = (char)('1' + EnPassantTarget.Value.Row);
            key += $"{enPassantCol}{enPassantRow}";

            return key;
        }

        key += "-";

        return key;
    }

    /// <summary>
    ///     Evaluates the position left by the just-played move and returns the
    ///     result that applies automatically, or <c>null</c> when play goes on.
    ///     Checks, in order: fivefold repetition, the seventy-five-move rule,
    ///     checkmate or stalemate of the opponent, and a dead position.
    ///     <see cref="CurrentPlayer" /> is still the mover here because the turn
    ///     is changed afterwards.
    /// </summary>
    private GameResult? EvaluateAutomaticResult()
    {
        if (IsFivefoldRepetitionAutomatic())
            return new GameResult(
                GameEndReason.DrawByFivefoldRepetition,
                null
            );

        if (IsSeventyFiveMoveRuleAutomatic())
            return new GameResult(
                GameEndReason.DrawBy75MoveRule,
                null
            );

        var mover = CurrentPlayer.Color;
        var opponentColor = mover == PieceColor.White ? PieceColor.Black : PieceColor.White;
        var inCheck = IsKingInCheck(Board, opponentColor);
        var hasMoves = HasAnyLegalMove(Board, opponentColor);

        if (inCheck && !hasMoves)
            return new GameResult(GameEndReason.Checkmate, mover);

        if (!inCheck && !hasMoves)
            return new GameResult(GameEndReason.DrawByStalemate, null);

        if (IsDeadPosition())
            return new GameResult(GameEndReason.DrawByDeadPosition, null);

        return null;
    }

    /// <summary>
    ///     Tells whether checkmate is impossible for both sides given the
    ///     material on the board: bare kings, a lone bishop or knight, or only
    ///     bishops standing on squares of a single colour. Any pawn, rook, or
    ///     queen, or any other combination of minor pieces, returns
    ///     <c>false</c>.
    /// </summary>
    private bool IsDeadPosition()
    {
        // K vs K                  → dead
        // K+B vs K                → dead
        // K+N vs K                → dead
        // K+B vs K+B (same color) → dead

        // K+R vs K
        // K+Q vs K
        // K+P vs K
        // K+B vs K+B (diff color)
        // K+N vs K+N

        var knights = 0;
        var bishops = 0;
        var bishopColor = -1;
        var mixedBishopColor = false;

        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var piece = GetPieceAt(new Position(r, c));
            if (piece is null || piece.Type == PieceType.King)
                continue;

            if (piece.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen)
                return false;

            if (piece.Type == PieceType.Knight)
                knights++;

            if (piece.Type == PieceType.Bishop)
            {
                bishops++;
                var square = (c + r) % 2;
                if (bishopColor == -1)
                    bishopColor = square;
                else if (bishopColor != square)
                    mixedBishopColor = true;
            }
        }

        if (knights + bishops <= 1)
            return true;

        if (knights == 0 && !mixedBishopColor)
            return true;

        return false;
    }

    /// <summary>
    ///     Hands the turn to the other colour: stops the previous clock, switches
    ///     CurrentPlayer, starts the opponent's clock, and raises the turn-changed
    ///     event.
    /// </summary>
    private void ChangeTurn()
    {
        StopCountdown(CurrentPlayer.Color);

        var nextPlayer = CurrentPlayer.Color == PieceColor.White ? _blackPlayer : _whitePlayer;
        CurrentPlayer = nextPlayer;

        StartCountdown(CurrentPlayer.Color);

        TurnChangedEventHandler?.Invoke(
            this,
            new TurnChangedEventArgs(nextPlayer)
        );
    }

    #endregion

    #region Draw Rule Bookkeeping

    /// <summary>
    ///     Updates the draw bookkeeping after a move: resets the half-move clock
    ///     on a pawn move or capture and otherwise advances it, then records the
    ///     position key for the side now to move.
    /// </summary>
    private void RecordCompletedMove(IBoard board, PieceColor sideToMove, bool wasPawnMove, bool wasCapture)
    {
        _halfMoveClock = wasPawnMove || wasCapture ? 0 : _halfMoveClock + 1;
        _positionHistory.Add(GeneratePositionKey(board, sideToMove));
    }

    /// <summary>
    ///     Tells whether the side to move may claim a draw because the current
    ///     position has occurred at least three times.
    /// </summary>
    private bool IsThreefoldRepetitionClaimAvailable()
    {
        var key = GeneratePositionKey(Board, CurrentPlayer.Color);
        return _positionHistory.Count(k => k == key) >= 3;
    }

    /// <summary>
    ///     Tells whether the current position has occurred at least five times,
    ///     in which case the game is drawn automatically without a claim.
    /// </summary>
    private bool IsFivefoldRepetitionAutomatic()
    {
        var key = GeneratePositionKey(Board, CurrentPlayer.Color);
        return _positionHistory.Count(k => k == key) >= 5;
    }

    /// <summary>
    ///     Tells whether the fifty-move rule can be claimed now, i.e. at least
    ///     fifty moves by each side have passed without a pawn move or capture.
    ///     The class diagram lists this as public, but it is private because
    ///     only GameManager itself consults it.
    /// </summary>
    private bool IsFiftyMoveRuleClaimAvailable()
    {
        return _halfMoveClock >= 100;
    }

    /// <summary>
    ///     Tells whether the seventy-five-move rule applies automatically, i.e.
    ///     seventy-five moves by each side have passed without a pawn move or
    ///     capture. The class diagram lists this as public, but it is private
    ///     because only GameManager itself consults it.
    /// </summary>
    private bool IsSeventyFiveMoveRuleAutomatic()
    {
        return _halfMoveClock >= 150;
    }

    #endregion

    #region Reset & Clocks

    /// <summary>
    ///     Clears the draw-tracking bookkeeping: the position history used to
    ///     detect repetitions and the half-move clock used by the fifty-move and
    ///     seventy-five-move rules. This is not a full game reset — that is
    ///     <see cref="ResetGame" />, which also restores the board, the clocks,
    ///     and the histories. Private because only StartGame and ResetGame call
    ///     it; the class diagram lists it as public, but the diagram is
    ///     read-only, so the discrepancy is documented here instead.
    /// </summary>
    private void Reset()
    {
        _positionHistory.Clear();
        _halfMoveClock = 0;
    }

    /// <summary>
    ///     Resets both clocks to the initial time and stops any running timer.
    ///     PlayerTimer is only a model; restarting the countdown is GameManager's
    ///     job, so no timer logic lives in the model.
    /// </summary>
    private void ResetTimers()
    {
        StopAllCountdowns();

        PlayerTimeTracker[PieceColor.White] = new PlayerTimer(_initialTime);
        PlayerTimeTracker[PieceColor.Black] = new PlayerTimer(_initialTime);
    }

    /// <summary>
    ///     Starts the one-second countdown for the given colour, replacing any
    ///     timer already running for it. Each tick consumes one second of that
    ///     player's clock, broadcasts the remaining time, and ends the game once
    ///     the clock reaches zero. Ticks run on thread-pool threads.
    /// </summary>
    private void StartCountdown(PieceColor color)
    {
        StopCountdown(color);

        _countdownTimers[color] = new Timer(
            _ =>
            {
                var playerTimer = PlayerTimeTracker[color];
                playerTimer.RemainingTime -= TimeSpan.FromSeconds(1);

                if (playerTimer.RemainingTime <= TimeSpan.Zero)
                {
                    playerTimer.RemainingTime = TimeSpan.Zero;

                    StopCountdown(color);
                    OnTimeExpired(color);

                    return;
                }

                TimerTickedEventHandler?.Invoke(
                    this,
                    new TimerTickEventArgs(color, playerTimer.RemainingTime)
                );
            },
            null,
            1000,
            1000
        );
    }

    /// <summary>
    ///     Stops and disposes the countdown timer of the given colour, if one is
    ///     running. Safe to call when no timer exists.
    /// </summary>
    private void StopCountdown(PieceColor color)
    {
        if (_countdownTimers.TryRemove(color, out var timer))
            timer.Dispose();
    }

    /// <summary>
    ///     Stops every running countdown timer, used when the game is reset or
    ///     ends so no clock keeps ticking afterwards.
    /// </summary>
    private void StopAllCountdowns()
    {
        foreach (var color in _countdownTimers.Keys)
            StopCountdown(color);
    }

    /// <summary>
    ///     Handles a clock running out, called from the countdown tick in
    ///     <see cref="StartCountdown" /> once the expiring side's remaining time
    ///     reaches zero: the opponent wins with
    ///     <see cref="GameEndReason.TimeExpired" />. Does nothing when the game
    ///     is already over, so a late tick cannot overwrite an existing result.
    /// </summary>
    private void OnTimeExpired(PieceColor color)
    {
        if (State != GameState.InProgress)
            return;

        var winner = color == PieceColor.White ? PieceColor.Black : PieceColor.White;

        EndGame(
            new GameResult(
                GameEndReason.TimeExpired,
                winner
            )
        );
    }

    #endregion
}