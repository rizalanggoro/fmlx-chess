using System.Collections.Concurrent;
using Api.Constants;
using Api.Enums;
using Api.Events;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;

namespace Api.Services;

public class GameManager : IGameManager
{
    private readonly IPlayer _blackPlayer;

    private readonly Dictionary<PieceColor, List<IChessPiece>> _captured = new()
    {
        [PieceColor.White] = [],
        [PieceColor.Black] = []
    };

    private readonly TimeSpan _initialTime;
    private readonly List<MoveHistoryEntry> _moveHistory = [];
    private readonly List<string> _positionHistory = [];
    private readonly IPlayer _whitePlayer;

    private readonly ConcurrentDictionary<PieceColor, Timer> _countdownTimers = new();

    private IPlayer? _drawOfferer;
    private int _halfMoveClock;

    public GameManager(IBoard board, IPlayer whitePlayer, IPlayer blackPlayer, TimeSpan initialTime)
    {
        GameBoard = board;
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

    // Dinonaktifkan: promosi memakai PromotionType dari langkah legal (Queen/Rook/Bishop/Knight). Lihat notes.md.
    // public PromotionSelectionHandler PromotionSelector { get; set; }

    public Dictionary<PieceColor, IReadOnlyList<IChessPiece>> CapturedPiecesTracker => new()
    {
        [PieceColor.White] = _captured[PieceColor.White],
        [PieceColor.Black] = _captured[PieceColor.Black]
    };

    public IBoard GameBoard { get; }
    public IPlayer CurrentPlayer { get; private set; }
    public GameState State { get; private set; }
    public GameResult? Result { get; private set; }
    public IReadOnlyList<MoveHistoryEntry> MoveHistory => _moveHistory;

    /// <summary>
    ///     Remaining time per colour, held in a PlayerTimer model. The countdown
    ///     logic does not live in PlayerTimer: GameManager starts, stops, and
    ///     ticks the clocks and ends the game on timeout. Ticks fire on
    ///     thread-pool threads via System.Threading.Timer.
    /// </summary>
    public Dictionary<PieceColor, IPlayerTimer> PlayerTimeTracker { get; } = new();

    public CastlingRights CastlingRights { get; private set; }
    public Position? EnPassantTarget { get; private set; }

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
        _positionHistory.Add(GeneratePositionKey(GameBoard, CurrentPlayer.Color));
        StartCountdown(CurrentPlayer.Color);

        SnapshotChangedEventHandler?.Invoke(
            this,
            new SnapshotChangedEventArgs(GameBoard, _moveHistory)
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
            new SnapshotChangedEventArgs(GameBoard, _moveHistory)
        );
    }

    public bool TryMakeMove(Move move)
    {
        if (State != GameState.InProgress)
            return false;
        if (!IsInBounds(move.From) || !IsInBounds(move.To))
            return false;
        var piece = GetPieceAt(move.From);
        if (piece is null || piece.Color != CurrentPlayer.Color)
            return false;
        var legal = GetLegalMoves(move.From);
        Move? chosen = null;
        foreach (var m in legal)
        {
            if (!m.From.Equals(move.From) || !m.To.Equals(move.To))
                continue;
            if (move.PromotionType is not null)
            {
                if (m.PromotionType == move.PromotionType)
                {
                    chosen = m;
                    break;
                }
            }
            else
            {
                if (m.PromotionType is null)
                {
                    chosen = m;
                    break;
                }

                chosen ??= m;
            }
        }

        if (chosen is null)
            return false;
        var effective = chosen;
        if (effective.PromotionType is null && piece.Type == PieceType.Pawn &&
            IsOnPromotionRank(effective.To, piece.Color))
            effective = effective with { PromotionType = PieceType.Queen };
        var wasPawn = piece.Type == PieceType.Pawn;
        var targetPiece = GetPieceAt(effective.To);
        var isEnPassant = wasPawn && EnPassantTarget.HasValue && effective.To.Equals(EnPassantTarget.Value) &&
                          targetPiece is null;
        var wasCapture = targetPiece is not null || isEnPassant;
        var app = ApplyMove(GameBoard, effective);
        if (app is null)
            return false;
        UpdateCastlingRights(GameBoard, effective, piece, app.CapturedPiece);
        UpdateEnPassantTarget(effective, piece);
        if (app.CapturedPiece is not null)
            _captured[piece.Color].Add(app.CapturedPiece);
        var moverColor = piece.Color;
        _moveHistory.Add(new MoveHistoryEntry(effective, moverColor));
        var opponent = moverColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
        RecordCompletedMove(GameBoard, opponent, wasPawn, wasCapture);
        _drawOfferer = null;
        // Dinonaktifkan: state room disiarkan lewat SnapshotChangedEventHandler. Lihat notes.md.
        // PieceMovedEventHandler?.Invoke(this, new PieceMovedEventArgs(effective));
        if (app.CapturedPiece is not null && app.CapturedSquare.HasValue)
            PieceCapturedEventHandler?.Invoke(this,
                new PieceCapturedEventArgs(app.CapturedPiece, app.CapturedSquare.Value));
        if (IsKingInCheck(GameBoard, opponent))
            CheckDeclaredEventHandler?.Invoke(this, new CheckEventArgs(opponent));
        var auto = EvaluateAutomaticResult();
        if (auto is not null)
        {
            EndGame(auto);
            SnapshotChangedEventHandler?.Invoke(this, new SnapshotChangedEventArgs(GameBoard, _moveHistory));
            return true;
        }

        ChangeTurn();
        SnapshotChangedEventHandler?.Invoke(this, new SnapshotChangedEventArgs(GameBoard, _moveHistory));
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

    public IReadOnlyList<Move> GetLegalMoves(Position from)
    {
        if (!IsInBounds(from))
            return Array.Empty<Move>();
        var piece = GameBoard.Tiles[from.Row, from.Column].Piece;
        if (piece is null || piece.Color != CurrentPlayer.Color)
            return Array.Empty<Move>();
        var pseudo = GenerateMovesForPiece(GameBoard, from);
        var legal = new List<Move>();
        foreach (var m in pseudo)
            if (!WouldLeaveKingInCheck(GameBoard, m))
                legal.Add(m);
        return legal;
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

        var offerer = _drawOfferer;
        _drawOfferer = null;

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
                new DrawDeclinedEventArgs(offerer)
            );
    }

    public bool ClaimDrawByThreefoldRepetition(IPlayer player)
    {
        if (State != GameState.InProgress)
            return false;
        if (!IsThreefoldRepetitionClaimAvailable())
            return false;
        EndGame(new GameResult(GameEndReason.DrawByThreefoldRepetition, null));
        return true;
    }

    public bool ClaimDrawByFiftyMoveRule(IPlayer player)
    {
        if (State != GameState.InProgress)
            return false;
        if (!IsFiftyMoveRuleClaimAvailable())
            return false;
        EndGame(new GameResult(GameEndReason.DrawBy50MoveRule, null));
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
            GameBoard.Tiles[r, c].Piece = null;

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
            GameBoard.Tiles[0, c].Piece = new ChessPiece(PieceColor.White, backRankPieces[c]);
            GameBoard.Tiles[1, c].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            GameBoard.Tiles[6, c].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
            GameBoard.Tiles[7, c].Piece = new ChessPiece(PieceColor.Black, backRankPieces[c]);
        }

        CastlingRights = new CastlingRights(
            true,
            true,
            true,
            true
        );
        EnPassantTarget = null;
    }

    public IChessTile GetTileAt(Position pos)
    {
        if (!IsInBounds(pos))
            throw new ArgumentOutOfRangeException(nameof(pos));
        return GameBoard.Tiles[pos.Row, pos.Column];
    }

    public IChessPiece? GetPieceAt(Position pos)
    {
        return GetTileAt(pos).Piece;
    }

    /// <summary>
    ///     Returns every piece of the given colour still on the board. Nothing
    ///     calls it: clients receive the board as 64 tiles in
    ///     <c>SnapshotChangedDto</c>, so a per-colour piece list is never needed.
    ///     Kept public only because the class diagram lists it; the diagram is
    ///     read-only, so the discrepancy is documented here and in notes.md.
    /// </summary>
    [Obsolete(
        "Not used: the board is broadcast as 64 tiles via SnapshotChangedDto."
    )]
    public IReadOnlyList<IChessPiece> GetPieces(PieceColor color)
    {
        var list = new List<IChessPiece>();
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var p = GameBoard.Tiles[r, c].Piece;
            if (p is not null && p.Color == color)
                list.Add(p);
        }

        return list;
    }

    /// <summary>
    ///     Generates the pseudo-legal moves for the piece on the given square:
    ///     pawn moves, knight jumps, sliding moves for bishop/rook/queen, or king
    ///     steps plus castling. Returns an empty list when the square is empty.
    ///     King safety is checked afterwards by <see cref="GetLegalMoves" />.
    /// </summary>
    private List<Move> GenerateMovesForPiece(IBoard board, Position from)
    {
        var piece = board.Tiles[from.Row, from.Column].Piece;

        if (piece is null)
            return [];

        switch (piece.Type)
        {
            case PieceType.Pawn:
                return GeneratePawnMoves(board, from);
            case PieceType.Knight:
                return GenerateKnightMoves(board, from);
            case PieceType.Bishop or PieceType.Rook or PieceType.Queen:
                return GenerateSlidingMoves(board, from);
            case PieceType.King:
            {
                var moves = GenerateKingMoves(board, from);
                moves.AddRange(GetCastlingMoves(board, from));

                return moves;
            }

            default:
                return [];
        }
    }

    private List<Move> GeneratePawnMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Row, from.Column].Piece;
        if (piece is null)
            return moves;
        var dir = piece.Color == PieceColor.White ? 1 : -1;
        var startRow = piece.Color == PieceColor.White ? 1 : 6;
        var one = new Position(from.Row + dir, from.Column);
        if (IsInBounds(one) && board.Tiles[one.Row, one.Column].Piece is null)
        {
            AddPawnMove(moves, from, one, piece.Color);
            var two = new Position(from.Row + 2 * dir, from.Column);
            if (from.Row == startRow && IsInBounds(two) && board.Tiles[two.Row, two.Column].Piece is null)
                moves.Add(new Move(from, two));
        }

        foreach (var dc in SquareOffsets.PawnCaptureColumns)
        {
            var to = new Position(from.Row + dir, from.Column + dc);
            if (!IsInBounds(to))
                continue;
            var target = board.Tiles[to.Row, to.Column].Piece;
            if (target is not null && target.Color != piece.Color)
                AddPawnMove(moves, from, to, piece.Color);
            else if (target is null && EnPassantTarget.HasValue && to.Equals(EnPassantTarget.Value))
                moves.Add(new Move(from, to));
        }

        return moves;
    }

    private void AddPawnMove(List<Move> moves, Position from, Position to, PieceColor color)
    {
        if (IsOnPromotionRank(to, color))
        {
            moves.Add(new Move(from, to, PieceType.Queen));
            moves.Add(new Move(from, to, PieceType.Rook));
            moves.Add(new Move(from, to, PieceType.Bishop));
            moves.Add(new Move(from, to, PieceType.Knight));
        }
        else
        {
            moves.Add(new Move(from, to));
        }
    }

    /// <summary>
    ///     Generates the pseudo-legal knight jumps from the given square: up to
    ///     eight L-shaped targets, keeping only squares that are empty or hold
    ///     an enemy piece.
    /// </summary>
    private List<Move> GenerateKnightMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Row, from.Column].Piece;

        if (piece is null)
            return moves;

        foreach (var offset in SquareOffsets.Knight)
        {
            var to = from + offset;
            if (!IsInBounds(to))
                continue;

            var targetPiece = board.Tiles[to.Row, to.Column].Piece;
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
    private List<Move> GenerateSlidingMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Row, from.Column].Piece;
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
                var targetPiece = board.Tiles[to.Row, to.Column].Piece;
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
    private List<Move> GenerateKingMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Row, from.Column].Piece;
        if (piece is null)
            return moves;

        foreach (var offset in SquareOffsets.King)
        {
            var to = from + offset;
            if (!IsInBounds(to))
                continue;

            var targetPiece = board.Tiles[to.Row, to.Column].Piece;
            if (targetPiece is null || targetPiece.Color != piece.Color)
                moves.Add(new Move(from, to));
        }

        return moves;
    }

    private List<Move> GetCastlingMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Row, from.Column].Piece;
        if (piece is null || piece.Type != PieceType.King)
            return moves;
        if (IsKingInCheck(board, piece.Color))
            return moves;
        var enemy = piece.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        if (piece.Color == PieceColor.White && from.Equals(new Position(0, 4)))
        {
            if (CastlingRights.WhiteKingSide
                && board.Tiles[0, 5].Piece is null
                && board.Tiles[0, 6].Piece is null
                && board.Tiles[0, 7].Piece is not null
                && board.Tiles[0, 7].Piece!.Type == PieceType.Rook
                && board.Tiles[0, 7].Piece!.Color == PieceColor.White
                && !IsSquareAttacked(board, new Position(0, 5), enemy)
                && !IsSquareAttacked(board, new Position(0, 6), enemy))
                moves.Add(new Move(from, new Position(0, 6)));
            if (CastlingRights.WhiteQueenSide
                && board.Tiles[0, 1].Piece is null
                && board.Tiles[0, 2].Piece is null
                && board.Tiles[0, 3].Piece is null
                && board.Tiles[0, 0].Piece is not null
                && board.Tiles[0, 0].Piece!.Type == PieceType.Rook
                && board.Tiles[0, 0].Piece!.Color == PieceColor.White
                && !IsSquareAttacked(board, new Position(0, 3), enemy)
                && !IsSquareAttacked(board, new Position(0, 2), enemy))
                moves.Add(new Move(from, new Position(0, 2)));
        }

        if (piece.Color == PieceColor.Black && from.Equals(new Position(7, 4)))
        {
            if (CastlingRights.BlackKingSide
                && board.Tiles[7, 5].Piece is null
                && board.Tiles[7, 6].Piece is null
                && board.Tiles[7, 7].Piece is not null
                && board.Tiles[7, 7].Piece!.Type == PieceType.Rook
                && board.Tiles[7, 7].Piece!.Color == PieceColor.Black
                && !IsSquareAttacked(board, new Position(7, 5), enemy)
                && !IsSquareAttacked(board, new Position(7, 6), enemy))
                moves.Add(new Move(from, new Position(7, 6)));
            if (CastlingRights.BlackQueenSide
                && board.Tiles[7, 1].Piece is null
                && board.Tiles[7, 2].Piece is null
                && board.Tiles[7, 3].Piece is null
                && board.Tiles[7, 0].Piece is not null
                && board.Tiles[7, 0].Piece!.Type == PieceType.Rook
                && board.Tiles[7, 0].Piece!.Color == PieceColor.Black
                && !IsSquareAttacked(board, new Position(7, 3), enemy)
                && !IsSquareAttacked(board, new Position(7, 2), enemy))
                moves.Add(new Move(from, new Position(7, 2)));
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
            var moves = GenerateMovesForPiece(board, from);

            foreach (var move in moves)
                if (!WouldLeaveKingInCheck(board, move))
                    return true;
        }

        return false;
    }

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

    private void UpdateCastlingRights(IBoard board, Move move, IChessPiece movingPiece, IChessPiece? capturedPiece)
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

    private GameResult? EvaluateAutomaticResult()
    {
        if (IsFivefoldRepetitionAutomatic())
            return new GameResult(GameEndReason.DrawByFivefoldRepetition, null);
        if (IsSeventyFiveMoveRuleAutomatic())
            return new GameResult(GameEndReason.DrawBy75MoveRule, null);
        var mover = CurrentPlayer.Color;
        var opponent = mover == PieceColor.White ? PieceColor.Black : PieceColor.White;
        var inCheck = IsKingInCheck(GameBoard, opponent);
        var hasMoves = HasAnyLegalMove(GameBoard, opponent);
        if (inCheck && !hasMoves)
            return new GameResult(GameEndReason.Checkmate, mover);
        if (!inCheck && !hasMoves)
            return new GameResult(GameEndReason.DrawByStalemate, null);
        if (IsDeadPosition())
            return new GameResult(GameEndReason.DrawByDeadPosition, null);
        return null;
    }

    private bool IsDeadPosition()
    {
        var knights = 0;
        var bishops = 0;
        var bishopColor = -1;
        var mixedBishopColor = false;
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var p = GameBoard.Tiles[r, c].Piece;
            if (p is null || p.Type == PieceType.King)
                continue;
            if (p.Type == PieceType.Pawn || p.Type == PieceType.Rook || p.Type == PieceType.Queen)
                return false;
            if (p.Type == PieceType.Knight)
                knights++;
            if (p.Type == PieceType.Bishop)
            {
                bishops++;
                var sq = (c + r) % 2;
                if (bishopColor == -1)
                    bishopColor = sq;
                else if (bishopColor != sq)
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

    public void RecordCompletedMove(IBoard board, PieceColor sideToMove, bool wasPawnMove, bool wasCapture)
    {
        _halfMoveClock = wasPawnMove || wasCapture ? 0 : _halfMoveClock + 1;
        _positionHistory.Add(GeneratePositionKey(board, sideToMove));
    }

    public bool IsThreefoldRepetitionClaimAvailable()
    {
        var key = GeneratePositionKey(GameBoard, CurrentPlayer.Color);
        return _positionHistory.Count(k => k == key) >= 3;
    }

    public bool IsFivefoldRepetitionAutomatic()
    {
        var key = GeneratePositionKey(GameBoard, CurrentPlayer.Color);
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
}