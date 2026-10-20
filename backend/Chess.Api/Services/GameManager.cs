using System.Text;
using Api.Enums;
using Api.Events;
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
        PromotionSelector = _ => PieceType.Queen;
        CastlingRights = new CastlingRights(true, true, true, true);
        ResetTimers();
    }

    public IBoard GameBoard { get; }
    public IPlayer CurrentPlayer { get; private set; }
    public GameState State { get; private set; }
    public GameResult? Result { get; private set; }
    public IReadOnlyList<MoveHistoryEntry> MoveHistory => _moveHistory;
    public PromotionSelectionHandler PromotionSelector { get; set; }
    public Dictionary<PieceColor, IPlayerTimer> PlayerTimeTracker { get; } = new();

    public Dictionary<PieceColor, IReadOnlyList<IChessPiece>> CapturedPiecesTracker => new()
    {
        [PieceColor.White] = _captured[PieceColor.White],
        [PieceColor.Black] = _captured[PieceColor.Black]
    };

    public CastlingRights CastlingRights { get; private set; }
    public Position? EnPassantTarget { get; private set; }

    public event EventHandler<PieceMovedEventArgs>? PieceMovedEventHandler;
    public event EventHandler<PieceCapturedEventArgs>? PieceCapturedEventHandler;
    public event EventHandler<TurnChangedEventArgs>? TurnChangedEventHandler;
    public event EventHandler<CheckEventArgs>? CheckDeclaredEventHandler;
    public event EventHandler<GameEndedEventArgs>? GameEndedEventHandler;
    public event EventHandler<DrawOfferedEventArgs>? DrawOfferedEventHandler;
    public event EventHandler<TimerTickEventArgs>? TimerTickedEventHandler;

    public void StartGame()
    {
        SetupInitialBoardPosition();
        Result = null;
        _moveHistory.Clear();
        _captured[PieceColor.White].Clear();
        _captured[PieceColor.Black].Clear();
        Reset();
        _drawOfferer = null;
        State = GameState.InProgress;
        CurrentPlayer = _whitePlayer;
        ResetTimers();
        _positionHistory.Add(GeneratePositionKey(GameBoard, CurrentPlayer.Color));
        PlayerTimeTracker[CurrentPlayer.Color].StartTimer();
    }

    public void ResetGame()
    {
        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();
        SetupInitialBoardPosition();
        State = GameState.NotStarted;
        Result = null;
        _moveHistory.Clear();
        _captured[PieceColor.White].Clear();
        _captured[PieceColor.Black].Clear();
        Reset();
        _drawOfferer = null;
        CurrentPlayer = _whitePlayer;
        CastlingRights = new CastlingRights(true, true, true, true);
        EnPassantTarget = null;
        ResetTimers();
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
            effective = effective with { PromotionType = PromotionSelector(piece.Color) };
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
        PieceMovedEventHandler?.Invoke(this, new PieceMovedEventArgs(effective));
        if (app.CapturedPiece is not null && app.CapturedSquare.HasValue)
            PieceCapturedEventHandler?.Invoke(this,
                new PieceCapturedEventArgs(app.CapturedPiece, app.CapturedSquare.Value));
        if (IsKingInCheck(GameBoard, opponent))
            CheckDeclaredEventHandler?.Invoke(this, new CheckEventArgs(opponent));
        var auto = EvaluateAutomaticResult();
        if (auto is not null)
        {
            EndGame(auto);
            return true;
        }

        ChangeTurn();
        return true;
    }

    public void SetupInitialBoardPosition()
    {
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
            GameBoard.Tiles[c, r].Piece = null;
        var back = new[]
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen, PieceType.King, PieceType.Bishop,
            PieceType.Knight, PieceType.Rook
        };
        for (var c = 0; c < 8; c++)
        {
            GameBoard.Tiles[c, 0].Piece = new ChessPiece(PieceColor.White, back[c]);
            GameBoard.Tiles[c, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            GameBoard.Tiles[c, 6].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
            GameBoard.Tiles[c, 7].Piece = new ChessPiece(PieceColor.Black, back[c]);
        }

        CastlingRights = new CastlingRights(true, true, true, true);
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

    public bool IsInBounds(Position pos)
    {
        return pos.Column >= 0 && pos.Column < 8 && pos.Row >= 0 && pos.Row < 8;
    }

    public IReadOnlyList<IChessPiece> GetPieces(PieceColor color)
    {
        var list = new List<IChessPiece>();
        for (var c = 0; c < 8; c++)
        for (var r = 0; r < 8; r++)
        {
            var p = GameBoard.Tiles[c, r].Piece;
            if (p is not null && p.Color == color)
                list.Add(p);
        }

        return list;
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

    private List<Move> GenerateMovesForPiece(IBoard board, Position from)
    {
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null)
            return new List<Move>();
        if (piece.Type == PieceType.Pawn)
            return GeneratePawnMoves(board, from);
        if (piece.Type == PieceType.Knight)
            return GenerateKnightMoves(board, from);
        if (piece.Type == PieceType.Bishop || piece.Type == PieceType.Rook || piece.Type == PieceType.Queen)
            return GenerateSlidingMoves(board, from);
        var moves = GenerateKingMoves(board, from);
        moves.AddRange(GetCastlingMoves(board, from));
        return moves;
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

        foreach (var dc in new[] { -1, 1 })
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

    private List<Move> GenerateKnightMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null)
            return moves;
        var offsets = new[] { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };
        foreach (var (dc, dr) in offsets)
        {
            var to = new Position(from.Column + dc, from.Row + dr);
            if (!IsInBounds(to))
                continue;
            var target = board.Tiles[to.Column, to.Row].Piece;
            if (target is null || target.Color != piece.Color)
                moves.Add(new Move(from, to));
        }

        return moves;
    }

    private List<Move> GenerateSlidingMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null)
            return moves;
        var dirs = new List<(int, int)>();
        if (piece.Type == PieceType.Bishop || piece.Type == PieceType.Queen)
        {
            dirs.Add((1, 1));
            dirs.Add((1, -1));
            dirs.Add((-1, 1));
            dirs.Add((-1, -1));
        }

        if (piece.Type == PieceType.Rook || piece.Type == PieceType.Queen)
        {
            dirs.Add((1, 0));
            dirs.Add((-1, 0));
            dirs.Add((0, 1));
            dirs.Add((0, -1));
        }

        foreach (var (dc, dr) in dirs)
        {
            var c = from.Column + dc;
            var r = from.Row + dr;
            while (c >= 0 && c < 8 && r >= 0 && r < 8)
            {
                var target = board.Tiles[c, r].Piece;
                if (target is null)
                {
                    moves.Add(new Move(from, new Position(c, r)));
                }
                else
                {
                    if (target.Color != piece.Color)
                        moves.Add(new Move(from, new Position(c, r)));
                    break;
                }

                c += dc;
                r += dr;
            }
        }

        return moves;
    }

    private List<Move> GenerateKingMoves(IBoard board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.Tiles[from.Column, from.Row].Piece;
        if (piece is null)
            return moves;
        for (var dc = -1; dc <= 1; dc++)
        for (var dr = -1; dr <= 1; dr++)
        {
            if (dc == 0 && dr == 0)
                continue;
            var to = new Position(from.Column + dc, from.Row + dr);
            if (!IsInBounds(to))
                continue;
            var target = board.Tiles[to.Column, to.Row].Piece;
            if (target is null || target.Color != piece.Color)
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

    private bool IsOnPromotionRank(Position position, PieceColor color)
    {
        return color == PieceColor.White ? position.Row == 7 : position.Row == 0;
    }

    private bool IsSquareAttacked(IBoard board, Position pos, PieceColor byColor)
    {
        var pawnRow = byColor == PieceColor.White ? pos.Row - 1 : pos.Row + 1;
        foreach (var dc in new[] { -1, 1 })
        {
            var p = new Position(pos.Column + dc, pawnRow);
            if (p.Column >= 0 && p.Column < 8 && p.Row >= 0 && p.Row < 8)
            {
                var piece = board.Tiles[p.Column, p.Row].Piece;
                if (piece is not null && piece.Color == byColor && piece.Type == PieceType.Pawn)
                    return true;
            }
        }

        var knightOffsets = new[] { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };
        foreach (var (dc, dr) in knightOffsets)
        {
            var p = new Position(pos.Column + dc, pos.Row + dr);
            if (!IsInBounds(p))
                continue;
            var piece = board.Tiles[p.Column, p.Row].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.Knight)
                return true;
        }

        for (var dc = -1; dc <= 1; dc++)
        for (var dr = -1; dr <= 1; dr++)
        {
            if (dc == 0 && dr == 0)
                continue;
            var p = new Position(pos.Column + dc, pos.Row + dr);
            if (!IsInBounds(p))
                continue;
            var piece = board.Tiles[p.Column, p.Row].Piece;
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.King)
                return true;
        }

        var diag = new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) };
        foreach (var (dc, dr) in diag)
        {
            var c = pos.Column + dc;
            var r = pos.Row + dr;
            while (c >= 0 && c < 8 && r >= 0 && r < 8)
            {
                var piece = board.Tiles[c, r].Piece;
                if (piece is not null)
                {
                    if (piece.Color == byColor && (piece.Type == PieceType.Bishop || piece.Type == PieceType.Queen))
                        return true;
                    break;
                }

                c += dc;
                r += dr;
            }
        }

        var straight = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var (dc, dr) in straight)
        {
            var c = pos.Column + dc;
            var r = pos.Row + dr;
            while (c >= 0 && c < 8 && r >= 0 && r < 8)
            {
                var piece = board.Tiles[c, r].Piece;
                if (piece is not null)
                {
                    if (piece.Color == byColor && (piece.Type == PieceType.Rook || piece.Type == PieceType.Queen))
                        return true;
                    break;
                }

                c += dc;
                r += dr;
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
            var promo = move.PromotionType ?? PromotionSelector(moving.Color);
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
        if (accepted)
            EndGame(new GameResult(GameEndReason.DrawByAgreement, null));
        _drawOfferer = null;
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

    private static PieceColor Opposite(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}