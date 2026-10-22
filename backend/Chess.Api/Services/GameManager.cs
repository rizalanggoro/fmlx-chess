using System.Text;
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
        PlayerTimeTracker[CurrentPlayer.Color].StartTimer();

        SnapshotChangedEventHandler?.Invoke(this, new SnapshotChangedEventArgs(GameBoard, _moveHistory));
    }

    public void ResetGame()
    {
        State = GameState.NotStarted;
        Result = null;
        _drawOfferer = null;
        CurrentPlayer = _whitePlayer;

        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();
        SetupInitialBoardPosition();
        Reset();
        ResetTimers();

        _moveHistory.Clear();
        _captured[PieceColor.White].Clear();
        _captured[PieceColor.Black].Clear();

        SnapshotChangedEventHandler?.Invoke(this, new SnapshotChangedEventArgs(GameBoard, _moveHistory));
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
        UpdateCastlingRights(GameBoard, effective, piece, app.CapturedPiece);
        UpdateEnPassantTarget(GameBoard, effective, piece);
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
        var piece = GameBoard.Tiles[from.Column, from.Row].Piece;
        if (piece is null || piece.Color != CurrentPlayer.Color)
            return Array.Empty<Move>();
        var pseudo = GenerateMovesForPiece(GameBoard, from);
        var legal = new List<Move>();
        foreach (var m in pseudo)
            if (!WouldLeaveKingInCheck(GameBoard, m))
                legal.Add(m);
        return legal;
    }

    public IReadOnlyList<MoveHistoryEntry> GetMoveHistory(PieceColor color)
    {
        return _moveHistory.Where(e => e.PlayedBy == color).ToList();
    }

    public void OfferDraw(IPlayer offerer)
    {
        if (State != GameState.InProgress)
            return;
        _drawOfferer = offerer;
        DrawOfferedEventHandler?.Invoke(this, new DrawOfferedEventArgs(offerer));
    }

    public void RespondToDrawOffer(IPlayer responder, bool accepted)
    {
        if (_drawOfferer is null || responder.Color == _drawOfferer.Color)
            return;

        var offerer = _drawOfferer;
        _drawOfferer = null;
        if (accepted)
            EndGame(new GameResult(GameEndReason.DrawByAgreement, null));
        else
            DrawDeclinedEventHandler?.Invoke(this, new DrawDeclinedEventArgs(offerer));
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

    private void EndGame(GameResult result)
    {
        if (State == GameState.Completed)
            return;
        State = GameState.Completed;
        Result = result;
        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();

        GameEndedEventHandler?.Invoke(this, new GameEndedEventArgs(result));
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
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
            GameBoard.Tiles[c, r].Piece = null;

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
            GameBoard.Tiles[c, 0].Piece = new ChessPiece(PieceColor.White, backRankPieces[c]);
            GameBoard.Tiles[c, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            GameBoard.Tiles[c, 6].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
            GameBoard.Tiles[c, 7].Piece = new ChessPiece(PieceColor.Black, backRankPieces[c]);
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
        return GameBoard.Tiles[pos.Column, pos.Row];
    }

    public IChessPiece? GetPieceAt(Position pos)
    {
        return GetTileAt(pos).Piece;
    }

    /// <summary>
    ///     Disabled: no callers, pieces are read directly from the board.
    /// </summary>
    // public IReadOnlyList<IChessPiece> GetPieces(PieceColor color)
    // {
    //     var list = new List<IChessPiece>();
    //     for (var c = 0; c < 8; c++)
    //     for (var r = 0; r < 8; r++)
    //     {
    //         var p = GameBoard.Tiles[c, r].Piece;
    //         if (p is not null && p.Color == color)
    //             list.Add(p);
    //     }
    //
    //     return list;
    // }

    /// <summary>
    ///     Generates the pseudo-legal moves for the piece on the given square:
    ///     pawn moves, knight jumps, sliding moves for bishop/rook/queen, or king
    ///     steps plus castling. Returns an empty list when the square is empty.
    ///     King safety is checked afterwards by <see cref="GetLegalMoves" />.
    /// </summary>
    private List<Move> GenerateMovesForPiece(IBoard board, Position from)
    {
        var piece = board.Tiles[from.Column, from.Row].Piece;

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
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null)
            return moves;
        var dir = piece.Color == PieceColor.White ? 1 : -1;
        var startRow = piece.Color == PieceColor.White ? 1 : 6;
        var one = new Position(from.Column, from.Row + dir);
        if (IsInBounds(one) && board.Tiles[one.Column, one.Row].Piece is null)
        {
            AddPawnMove(moves, from, one, piece.Color);
            var two = new Position(from.Column, from.Row + 2 * dir);
            if (from.Row == startRow && IsInBounds(two) && board.Tiles[two.Column, two.Row].Piece is null)
                moves.Add(new Move(from, two));
        }

        foreach (var dc in SquareOffsets.PawnCaptureColumns)
        {
            var to = new Position(from.Column + dc, from.Row + dir);
            if (!IsInBounds(to))
                continue;
            var target = board.Tiles[to.Column, to.Row].Piece;
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
        var piece = board.Tiles[from.Column, from.Row].Piece;

        if (piece is null)
            return moves;

        foreach (var offset in SquareOffsets.Knight)
        {
            var to = from + offset;
            if (!IsInBounds(to))
                continue;

            var targetPiece = board.Tiles[to.Column, to.Row].Piece;
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
        var piece = board.Tiles[from.Column, from.Row].Piece;
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
                var targetPiece = board.Tiles[to.Column, to.Row].Piece;
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
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null)
            return moves;

        foreach (var offset in SquareOffsets.King)
        {
            var to = from + offset;
            if (!IsInBounds(to))
                continue;

            var targetPiece = board.Tiles[to.Column, to.Row].Piece;
            if (targetPiece is null || targetPiece.Color != piece.Color)
                moves.Add(new Move(from, to));
        }

        return moves;
    }

    private List<Move> GetCastlingMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null || piece.Type != PieceType.King)
            return moves;
        if (IsKingInCheck(board, piece.Color))
            return moves;
        var enemy = piece.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        if (piece.Color == PieceColor.White && from.Equals(new Position(4, 0)))
        {
            if (CastlingRights.WhiteKingSide
                && board.Tiles[5, 0].Piece is null
                && board.Tiles[6, 0].Piece is null
                && board.Tiles[7, 0].Piece is not null
                && board.Tiles[7, 0].Piece!.Type == PieceType.Rook
                && board.Tiles[7, 0].Piece!.Color == PieceColor.White
                && !IsSquareAttacked(board, new Position(5, 0), enemy)
                && !IsSquareAttacked(board, new Position(6, 0), enemy))
                moves.Add(new Move(from, new Position(6, 0)));
            if (CastlingRights.WhiteQueenSide
                && board.Tiles[1, 0].Piece is null
                && board.Tiles[2, 0].Piece is null
                && board.Tiles[3, 0].Piece is null
                && board.Tiles[0, 0].Piece is not null
                && board.Tiles[0, 0].Piece!.Type == PieceType.Rook
                && board.Tiles[0, 0].Piece!.Color == PieceColor.White
                && !IsSquareAttacked(board, new Position(3, 0), enemy)
                && !IsSquareAttacked(board, new Position(2, 0), enemy))
                moves.Add(new Move(from, new Position(2, 0)));
        }

        if (piece.Color == PieceColor.Black && from.Equals(new Position(4, 7)))
        {
            if (CastlingRights.BlackKingSide
                && board.Tiles[5, 7].Piece is null
                && board.Tiles[6, 7].Piece is null
                && board.Tiles[7, 7].Piece is not null
                && board.Tiles[7, 7].Piece!.Type == PieceType.Rook
                && board.Tiles[7, 7].Piece!.Color == PieceColor.Black
                && !IsSquareAttacked(board, new Position(5, 7), enemy)
                && !IsSquareAttacked(board, new Position(6, 7), enemy))
                moves.Add(new Move(from, new Position(6, 7)));
            if (CastlingRights.BlackQueenSide
                && board.Tiles[1, 7].Piece is null
                && board.Tiles[2, 7].Piece is null
                && board.Tiles[3, 7].Piece is null
                && board.Tiles[0, 7].Piece is not null
                && board.Tiles[0, 7].Piece!.Type == PieceType.Rook
                && board.Tiles[0, 7].Piece!.Color == PieceColor.Black
                && !IsSquareAttacked(board, new Position(3, 7), enemy)
                && !IsSquareAttacked(board, new Position(2, 7), enemy))
                moves.Add(new Move(from, new Position(2, 7)));
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

    private bool IsSquareAttacked(IBoard board, Position pos, PieceColor byColor)
    {
        var pawnRow = byColor == PieceColor.White ? pos.Row - 1 : pos.Row + 1;
        foreach (var dc in SquareOffsets.PawnCaptureColumns)
        {
            var p = new Position(pos.Column + dc, pawnRow);
            if (!IsInBounds(p))
                continue;

            var piece = board.Tiles[p.Column, p.Row].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.Pawn)
                return true;
        }

        foreach (var offset in SquareOffsets.Knight)
        {
            var p = pos + offset;
            if (!IsInBounds(p))
                continue;
            var piece = board.Tiles[p.Column, p.Row].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.Knight)
                return true;
        }

        foreach (var offset in SquareOffsets.King)
        {
            var p = pos + offset;
            if (!IsInBounds(p))
                continue;
            var piece = board.Tiles[p.Column, p.Row].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.King)
                return true;
        }

        foreach (var offset in SquareOffsets.Diagonal)
        {
            var p = pos + offset;
            while (IsInBounds(p))
            {
                var piece = board.Tiles[p.Column, p.Row].Piece;
                if (piece is not null)
                {
                    if (piece.Color == byColor && (piece.Type == PieceType.Bishop || piece.Type == PieceType.Queen))
                        return true;
                    break;
                }

                p += offset;
            }
        }

        foreach (var offset in SquareOffsets.Straight)
        {
            var p = pos + offset;
            while (IsInBounds(p))
            {
                var piece = board.Tiles[p.Column, p.Row].Piece;
                if (piece is not null)
                {
                    if (piece.Color == byColor && (piece.Type == PieceType.Rook || piece.Type == PieceType.Queen))
                        return true;
                    break;
                }

                p += offset;
            }
        }

        return false;
    }

    private bool IsKingInCheck(IBoard board, PieceColor color)
    {
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var piece = board.Tiles[c, r].Piece;
            if (piece is not null && piece.Color == color && piece.Type == PieceType.King)
            {
                var enemy = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
                return IsSquareAttacked(board, new Position(c, r), enemy);
            }
        }

        return false;
    }

    private bool WouldLeaveKingInCheck(IBoard board, Move move)
    {
        var mover = board.Tiles[move.From.Column, move.From.Row].Piece;
        if (mover is null)
            return true;
        var clone = new Board(board);
        ApplyMove(clone, move);
        return IsKingInCheck(clone, mover.Color);
    }

    private bool HasAnyLegalMove(IBoard board, PieceColor color)
    {
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var piece = board.Tiles[c, r].Piece;
            if (piece is null || piece.Color != color)
                continue;
            var from = new Position(c, r);
            var pseudo = GenerateMovesForPiece(board, from);
            foreach (var m in pseudo)
                if (!WouldLeaveKingInCheck(board, m))
                    return true;
        }

        return false;
    }

    private MoveApplicationResult ApplyMove(IBoard board, Move move)
    {
        var fromTile = board.Tiles[move.From.Column, move.From.Row];
        var toTile = board.Tiles[move.To.Column, move.To.Row];
        var moving = fromTile.Piece!;
        var captured = toTile.Piece;
        Position? capturedSquare = captured is not null ? move.To : null;
        if (moving.Type == PieceType.Pawn && EnPassantTarget.HasValue && move.To.Equals(EnPassantTarget.Value) &&
            captured is null)
        {
            var ep = new Position(move.To.Column, move.From.Row);
            captured = board.Tiles[ep.Column, ep.Row].Piece;
            capturedSquare = ep;
            board.Tiles[ep.Column, ep.Row].Piece = null;
        }

        fromTile.Piece = null;
        if (moving.Type == PieceType.Pawn && IsOnPromotionRank(move.To, moving.Color))
        {
            var promo = move.PromotionType ?? PieceType.Queen;
            toTile.Piece = new ChessPiece(moving.Color, promo);
        }
        else
        {
            toTile.Piece = moving;
        }

        if (moving.Type == PieceType.King && Math.Abs(move.To.Column - move.From.Column) == 2)
        {
            if (move.To.Column == 6)
            {
                var rook = board.Tiles[7, move.From.Row].Piece;
                board.Tiles[7, move.From.Row].Piece = null;
                board.Tiles[5, move.From.Row].Piece = rook;
            }
            else if (move.To.Column == 2)
            {
                var rook = board.Tiles[0, move.From.Row].Piece;
                board.Tiles[0, move.From.Row].Piece = null;
                board.Tiles[3, move.From.Row].Piece = rook;
            }
        }

        return new MoveApplicationResult(moving, captured, capturedSquare);
    }

    private void UpdateCastlingRights(IBoard board, Move move, IChessPiece movingPiece, IChessPiece? capturedPiece)
    {
        var cr = CastlingRights;
        if (movingPiece.Type == PieceType.King)
        {
            if (movingPiece.Color == PieceColor.White)
                cr = new CastlingRights(false, false, cr.BlackKingSide, cr.BlackQueenSide);
            else
                cr = new CastlingRights(cr.WhiteKingSide, cr.WhiteQueenSide, false, false);
        }

        if (movingPiece.Type == PieceType.Rook)
        {
            if (move.From.Equals(new Position(0, 0)))
                cr = new CastlingRights(cr.WhiteKingSide, false, cr.BlackKingSide, cr.BlackQueenSide);
            if (move.From.Equals(new Position(7, 0)))
                cr = new CastlingRights(false, cr.WhiteQueenSide, cr.BlackKingSide, cr.BlackQueenSide);
            if (move.From.Equals(new Position(0, 7)))
                cr = new CastlingRights(cr.WhiteKingSide, cr.WhiteQueenSide, cr.BlackKingSide, false);
            if (move.From.Equals(new Position(7, 7)))
                cr = new CastlingRights(cr.WhiteKingSide, cr.WhiteQueenSide, false, cr.BlackQueenSide);
        }

        if (capturedPiece is not null && capturedPiece.Type == PieceType.Rook)
        {
            if (move.To.Equals(new Position(0, 0)))
                cr = new CastlingRights(cr.WhiteKingSide, false, cr.BlackKingSide, cr.BlackQueenSide);
            if (move.To.Equals(new Position(7, 0)))
                cr = new CastlingRights(false, cr.WhiteQueenSide, cr.BlackKingSide, cr.BlackQueenSide);
            if (move.To.Equals(new Position(0, 7)))
                cr = new CastlingRights(cr.WhiteKingSide, cr.WhiteQueenSide, cr.BlackKingSide, false);
            if (move.To.Equals(new Position(7, 7)))
                cr = new CastlingRights(cr.WhiteKingSide, cr.WhiteQueenSide, false, cr.BlackQueenSide);
        }

        CastlingRights = cr;
    }

    private void UpdateEnPassantTarget(IBoard board, Move move, IChessPiece movingPiece)
    {
        if (movingPiece.Type == PieceType.Pawn && Math.Abs(move.To.Row - move.From.Row) == 2)
            EnPassantTarget = new Position(move.From.Column, (move.From.Row + move.To.Row) / 2);
        else
            EnPassantTarget = null;
    }

    public string GeneratePositionKey(IBoard board, PieceColor sideToMove)
    {
        var sb = new StringBuilder(128);
        for (var r = 7; r >= 0; r--)
        for (var c = 0; c < 8; c++)
        {
            var p = board.Tiles[c, r].Piece;
            if (p is null)
            {
                sb.Append('-');
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
            sb.Append(ch);
        }

        sb.Append(sideToMove == PieceColor.White ? 'w' : 'b');
        sb.Append(CastlingRights.WhiteKingSide ? 'K' : '-');
        sb.Append(CastlingRights.WhiteQueenSide ? 'Q' : '-');
        sb.Append(CastlingRights.BlackKingSide ? 'k' : '-');
        sb.Append(CastlingRights.BlackQueenSide ? 'q' : '-');
        if (EnPassantTarget.HasValue)
        {
            sb.Append((char)('a' + EnPassantTarget.Value.Column));
            sb.Append((char)('1' + EnPassantTarget.Value.Row));
        }
        else
        {
            sb.Append('-');
        }

        return sb.ToString();
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
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var p = GameBoard.Tiles[c, r].Piece;
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

    private void ChangeTurn()
    {
        var next = CurrentPlayer.Color == PieceColor.White ? _blackPlayer : _whitePlayer;
        PlayerTimeTracker[CurrentPlayer.Color].StopTimer();
        CurrentPlayer = next;
        PlayerTimeTracker[CurrentPlayer.Color].StartTimer();
        TurnChangedEventHandler?.Invoke(this, new TurnChangedEventArgs(next));
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

    public bool IsFiftyMoveRuleClaimAvailable()
    {
        return _halfMoveClock >= 100;
    }

    public bool IsSeventyFiveMoveRuleAutomatic()
    {
        return _halfMoveClock >= 150;
    }

    public void Reset()
    {
        _positionHistory.Clear();
        _halfMoveClock = 0;
    }

    private void ResetTimers()
    {
        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();

        var whiteTimer = new PlayerTimer(_initialTime);
        var blackTimer = new PlayerTimer(_initialTime);

        whiteTimer.TimerTick += (s, e) => TimerTickedEventHandler?.Invoke(
            this,
            new TimerTickEventArgs(
                PieceColor.White,
                e.TimeRemaining
            )
        );
        blackTimer.TimerTick += (s, e) => TimerTickedEventHandler?.Invoke(
            this,
            new TimerTickEventArgs(
                PieceColor.Black,
                e.TimeRemaining
            )
        );

        whiteTimer.TimeExpired += (s, e) => OnTimeExpired(PieceColor.White);
        blackTimer.TimeExpired += (s, e) => OnTimeExpired(PieceColor.Black);

        PlayerTimeTracker[PieceColor.White] = whiteTimer;
        PlayerTimeTracker[PieceColor.Black] = blackTimer;
    }

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