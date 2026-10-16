namespace Api;

public class GameManager
{
    private IPlayer? drawOfferer;
    private readonly IPlayer _whitePlayer;
    private readonly IPlayer _blackPlayer;
    private readonly TimeSpan _initialTime;
    private readonly List<MoveHistoryEntry> _moveHistory = new();
    private readonly Dictionary<PieceColor, List<IChessPiece>> _captured = new()
    {
        [PieceColor.WHITE] = new(),
        [PieceColor.BLACK] = new()
    };
    private int halfmoveClock;
    private List<string> positionHistory = new();

    public IBoard GameBoard { get; private set; }
    public IPlayer CurrentPlayer { get; private set; }
    public GameState State { get; private set; }
    public GameResult? Result { get; private set; }
    public IReadOnlyList<MoveHistoryEntry> MoveHistory => _moveHistory;
    public PromotionSelectionHandler PromotionSelector { get; set; }
    public Dictionary<PieceColor, IPlayerTimer> PlayerTimeTracker { get; } = new();
    public Dictionary<PieceColor, IReadOnlyList<IChessPiece>> CapturedPiecesTracker => new()
    {
        [PieceColor.WHITE] = _captured[PieceColor.WHITE],
        [PieceColor.BLACK] = _captured[PieceColor.BLACK]
    };
    public CastlingRights CastlingRights { get; private set; }
    public Position? EnPassantTarget { get; private set; }

    public event EventHandler<PieceMovedEventArgs> PieceMoved = delegate { };
    public event EventHandler<PieceCapturedEventArgs> PieceCaptured = delegate { };
    public event EventHandler<TurnChangedEventArgs> TurnChanged = delegate { };
    public event EventHandler<CheckEventArgs> CheckDeclared = delegate { };
    public event EventHandler<GameEndedEventArgs> GameEnded = delegate { };
    public event EventHandler<DrawOfferedEventArgs> DrawOffered = delegate { };
    public event EventHandler<TimerTickEventArgs> TimerTick = delegate { };

    public GameManager(IBoard board, IPlayer whitePlayer, IPlayer blackPlayer, TimeSpan initialTime)
    {
        GameBoard = board;
        _whitePlayer = whitePlayer;
        _blackPlayer = blackPlayer;
        _initialTime = initialTime;
        CurrentPlayer = whitePlayer;
        State = GameState.NOT_STARTED;
        PromotionSelector = _ => PieceType.QUEEN;
        CastlingRights = new CastlingRights(true, true, true, true);
        ResetTimers();
    }

    public void StartGame()
    {
        SetupInitialBoardPosition();
        Result = null;
        _moveHistory.Clear();
        _captured[PieceColor.WHITE].Clear();
        _captured[PieceColor.BLACK].Clear();
        Reset();
        drawOfferer = null;
        State = GameState.IN_PROGRESS;
        CurrentPlayer = _whitePlayer;
        ResetTimers();
        positionHistory.Add(GeneratePositionKey(GameBoard, CurrentPlayer.Color));
        PlayerTimeTracker[CurrentPlayer.Color].StartTimer();
    }

    public void ResetGame()
    {
        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();
        SetupInitialBoardPosition();
        State = GameState.NOT_STARTED;
        Result = null;
        _moveHistory.Clear();
        _captured[PieceColor.WHITE].Clear();
        _captured[PieceColor.BLACK].Clear();
        Reset();
        drawOfferer = null;
        CurrentPlayer = _whitePlayer;
        CastlingRights = new CastlingRights(true, true, true, true);
        EnPassantTarget = null;
        ResetTimers();
    }

    private void EndGame(GameResult result)
    {
        if (State == GameState.COMPLETED)
            return;
        State = GameState.COMPLETED;
        Result = result;
        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();
        GameEnded.Invoke(this, new GameEndedEventArgs(result));
    }

    public bool TryMakeMove(Move move)
    {
        if (State != GameState.IN_PROGRESS)
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
        if (effective.PromotionType is null && piece.Type == PieceType.PAWN && IsOnPromotionRank(effective.To, piece.Color))
            effective = effective with { PromotionType = PromotionSelector(piece.Color) };
        bool wasPawn = piece.Type == PieceType.PAWN;
        var targetPiece = GetPieceAt(effective.To);
        bool isEnPassant = wasPawn && EnPassantTarget.HasValue && effective.To.Equals(EnPassantTarget.Value) && targetPiece is null;
        bool wasCapture = targetPiece is not null || isEnPassant;
        var app = ApplyMove(GameBoard, effective);
        UpdateCastlingRights(GameBoard, effective, piece, app.CapturedPiece);
        UpdateEnPassantTarget(GameBoard, effective, piece);
        if (app.CapturedPiece is not null)
            _captured[piece.Color].Add(app.CapturedPiece);
        var moverColor = piece.Color;
        _moveHistory.Add(new MoveHistoryEntry(effective, moverColor));
        var opponent = moverColor == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
        RecordCompletedMove(GameBoard, opponent, wasPawn, wasCapture);
        drawOfferer = null;
        PieceMoved.Invoke(this, new PieceMovedEventArgs(effective));
        if (app.CapturedPiece is not null && app.CapturedSquare.HasValue)
            PieceCaptured.Invoke(this, new PieceCapturedEventArgs(app.CapturedPiece, app.CapturedSquare.Value));
        if (IsKingInCheck(GameBoard, opponent))
            CheckDeclared.Invoke(this, new CheckEventArgs(opponent));
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
        var back = new[] { PieceType.ROOK, PieceType.KNIGHT, PieceType.BISHOP, PieceType.QUEEN, PieceType.KING, PieceType.BISHOP, PieceType.KNIGHT, PieceType.ROOK };
        for (var c = 0; c < 8; c++)
        {
            GameBoard.Tiles[c, 0].Piece = new ChessPiece(PieceColor.WHITE, back[c]);
            GameBoard.Tiles[c, 1].Piece = new ChessPiece(PieceColor.WHITE, PieceType.PAWN);
            GameBoard.Tiles[c, 6].Piece = new ChessPiece(PieceColor.BLACK, PieceType.PAWN);
            GameBoard.Tiles[c, 7].Piece = new ChessPiece(PieceColor.BLACK, back[c]);
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
        if (piece.Type == PieceType.PAWN)
            return GeneratePawnMoves(board, from);
        if (piece.Type == PieceType.KNIGHT)
            return GenerateKnightMoves(board, from);
        if (piece.Type == PieceType.BISHOP || piece.Type == PieceType.ROOK || piece.Type == PieceType.QUEEN)
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
        int dir = piece.Color == PieceColor.WHITE ? 1 : -1;
        int startRow = piece.Color == PieceColor.WHITE ? 1 : 6;
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
            moves.Add(new Move(from, to, PieceType.QUEEN));
            moves.Add(new Move(from, to, PieceType.ROOK));
            moves.Add(new Move(from, to, PieceType.BISHOP));
            moves.Add(new Move(from, to, PieceType.KNIGHT));
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
        if (piece.Type == PieceType.BISHOP || piece.Type == PieceType.QUEEN)
        {
            dirs.Add((1, 1));
            dirs.Add((1, -1));
            dirs.Add((-1, 1));
            dirs.Add((-1, -1));
        }
        if (piece.Type == PieceType.ROOK || piece.Type == PieceType.QUEEN)
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
        if (piece is null || piece.Type != PieceType.KING)
            return moves;
        if (IsKingInCheck(board, piece.Color))
            return moves;
        var enemy = piece.Color == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
        if (piece.Color == PieceColor.WHITE && from.Equals(new Position(4, 0)))
        {
            if (CastlingRights.WhiteKingSide
                && board.Tiles[5, 0].Piece is null
                && board.Tiles[6, 0].Piece is null
                && board.Tiles[7, 0].Piece is not null
                && board.Tiles[7, 0].Piece!.Type == PieceType.ROOK
                && board.Tiles[7, 0].Piece!.Color == PieceColor.WHITE
                && !IsSquareAttacked(board, new Position(5, 0), enemy)
                && !IsSquareAttacked(board, new Position(6, 0), enemy))
                moves.Add(new Move(from, new Position(6, 0)));
            if (CastlingRights.WhiteQueenSide
                && board.Tiles[1, 0].Piece is null
                && board.Tiles[2, 0].Piece is null
                && board.Tiles[3, 0].Piece is null
                && board.Tiles[0, 0].Piece is not null
                && board.Tiles[0, 0].Piece!.Type == PieceType.ROOK
                && board.Tiles[0, 0].Piece!.Color == PieceColor.WHITE
                && !IsSquareAttacked(board, new Position(3, 0), enemy)
                && !IsSquareAttacked(board, new Position(2, 0), enemy))
                moves.Add(new Move(from, new Position(2, 0)));
        }
        if (piece.Color == PieceColor.BLACK && from.Equals(new Position(4, 7)))
        {
            if (CastlingRights.BlackKingSide
                && board.Tiles[5, 7].Piece is null
                && board.Tiles[6, 7].Piece is null
                && board.Tiles[7, 7].Piece is not null
                && board.Tiles[7, 7].Piece!.Type == PieceType.ROOK
                && board.Tiles[7, 7].Piece!.Color == PieceColor.BLACK
                && !IsSquareAttacked(board, new Position(5, 7), enemy)
                && !IsSquareAttacked(board, new Position(6, 7), enemy))
                moves.Add(new Move(from, new Position(6, 7)));
            if (CastlingRights.BlackQueenSide
                && board.Tiles[1, 7].Piece is null
                && board.Tiles[2, 7].Piece is null
                && board.Tiles[3, 7].Piece is null
                && board.Tiles[0, 7].Piece is not null
                && board.Tiles[0, 7].Piece!.Type == PieceType.ROOK
                && board.Tiles[0, 7].Piece!.Color == PieceColor.BLACK
                && !IsSquareAttacked(board, new Position(3, 7), enemy)
                && !IsSquareAttacked(board, new Position(2, 7), enemy))
                moves.Add(new Move(from, new Position(2, 7)));
        }
        return moves;
    }

    private bool IsOnPromotionRank(Position position, PieceColor color)
    {
        return color == PieceColor.WHITE ? position.Row == 7 : position.Row == 0;
    }

    private bool IsSquareAttacked(IBoard board, Position pos, PieceColor byColor)
    {
        int pawnRow = byColor == PieceColor.WHITE ? pos.Row - 1 : pos.Row + 1;
        foreach (var dc in new[] { -1, 1 })
        {
            var p = new Position(pos.Column + dc, pawnRow);
            if (p.Column >= 0 && p.Column < 8 && p.Row >= 0 && p.Row < 8)
            {
                var piece = board.Tiles[p.Column, p.Row].Piece;
                if (piece is not null && piece.Color == byColor && piece.Type == PieceType.PAWN)
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
            if (piece is not null && piece.Color == byColor && piece.Type == PieceType.KNIGHT)
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
                if (piece is not null && piece.Color == byColor && piece.Type == PieceType.KING)
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
                    if (piece.Color == byColor && (piece.Type == PieceType.BISHOP || piece.Type == PieceType.QUEEN))
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
                    if (piece.Color == byColor && (piece.Type == PieceType.ROOK || piece.Type == PieceType.QUEEN))
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
                if (piece is not null && piece.Color == color && piece.Type == PieceType.KING)
                {
                    var enemy = color == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
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
        IChessPiece? captured = toTile.Piece;
        Position? capturedSquare = captured is not null ? move.To : null;
        if (moving.Type == PieceType.PAWN && EnPassantTarget.HasValue && move.To.Equals(EnPassantTarget.Value) && captured is null)
        {
            var ep = new Position(move.To.Column, move.From.Row);
            captured = board.Tiles[ep.Column, ep.Row].Piece;
            capturedSquare = ep;
            board.Tiles[ep.Column, ep.Row].Piece = null;
        }
        fromTile.Piece = null;
        if (moving.Type == PieceType.PAWN && IsOnPromotionRank(move.To, moving.Color))
        {
            var promo = move.PromotionType ?? PromotionSelector(moving.Color);
            toTile.Piece = new ChessPiece(moving.Color, promo);
        }
        else
        {
            toTile.Piece = moving;
        }
        if (moving.Type == PieceType.KING && Math.Abs(move.To.Column - move.From.Column) == 2)
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
        if (movingPiece.Type == PieceType.KING)
        {
            if (movingPiece.Color == PieceColor.WHITE)
                cr = new CastlingRights(false, false, cr.BlackKingSide, cr.BlackQueenSide);
            else
                cr = new CastlingRights(cr.WhiteKingSide, cr.WhiteQueenSide, false, false);
        }
        if (movingPiece.Type == PieceType.ROOK)
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
        if (capturedPiece is not null && capturedPiece.Type == PieceType.ROOK)
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
        if (movingPiece.Type == PieceType.PAWN && Math.Abs(move.To.Row - move.From.Row) == 2)
            EnPassantTarget = new Position(move.From.Column, (move.From.Row + move.To.Row) / 2);
        else
            EnPassantTarget = null;
    }

    public string GeneratePositionKey(IBoard board, PieceColor sideToMove)
    {
        var sb = new System.Text.StringBuilder(128);
        for (var r = 7; r >= 0; r--)
            for (var c = 0; c < 8; c++)
            {
                var p = board.Tiles[c, r].Piece;
                if (p is null)
                {
                    sb.Append('-');
                    continue;
                }
                char ch = p.Type switch
                {
                    PieceType.KING => 'k',
                    PieceType.QUEEN => 'q',
                    PieceType.ROOK => 'r',
                    PieceType.BISHOP => 'b',
                    PieceType.KNIGHT => 'n',
                    _ => 'p'
                };
                if (p.Color == PieceColor.WHITE)
                    ch = char.ToUpperInvariant(ch);
                sb.Append(ch);
            }
        sb.Append(sideToMove == PieceColor.WHITE ? 'w' : 'b');
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
            return new GameResult(GameEndReason.DRAW_BY_FIVEFOLD_REPETITION, null);
        if (IsSeventyFiveMoveRuleAutomatic())
            return new GameResult(GameEndReason.DRAW_BY_75_MOVE_RULE, null);
        var mover = CurrentPlayer.Color;
        var opponent = mover == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
        bool inCheck = IsKingInCheck(GameBoard, opponent);
        bool hasMoves = HasAnyLegalMove(GameBoard, opponent);
        if (inCheck && !hasMoves)
            return new GameResult(GameEndReason.CHECKMATE, mover);
        if (!inCheck && !hasMoves)
            return new GameResult(GameEndReason.DRAW_BY_STALEMATE, null);
        if (IsDeadPosition())
            return new GameResult(GameEndReason.DRAW_BY_DEAD_POSITION, null);
        return null;
    }

    private bool IsDeadPosition()
    {
        int knights = 0;
        int bishops = 0;
        int bishopColor = -1;
        bool mixedBishopColor = false;
        for (var c = 0; c < 8; c++)
            for (var r = 0; r < 8; r++)
            {
                var p = GameBoard.Tiles[c, r].Piece;
                if (p is null || p.Type == PieceType.KING)
                    continue;
                if (p.Type == PieceType.PAWN || p.Type == PieceType.ROOK || p.Type == PieceType.QUEEN)
                    return false;
                if (p.Type == PieceType.KNIGHT)
                    knights++;
                if (p.Type == PieceType.BISHOP)
                {
                    bishops++;
                    int sq = (c + r) % 2;
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
        if (State != GameState.IN_PROGRESS)
            return;
        drawOfferer = offerer;
        DrawOffered.Invoke(this, new DrawOfferedEventArgs(offerer));
    }

    public void RespondToDrawOffer(IPlayer responder, bool accepted)
    {
        if (drawOfferer is null || responder.Color == drawOfferer.Color)
            return;
        if (accepted)
            EndGame(new GameResult(GameEndReason.DRAW_BY_AGREEMENT, null));
        drawOfferer = null;
    }

    public bool ClaimDrawByThreefoldRepetition(IPlayer player)
    {
        if (State != GameState.IN_PROGRESS)
            return false;
        if (!IsThreefoldRepetitionClaimAvailable())
            return false;
        EndGame(new GameResult(GameEndReason.DRAW_BY_THREEFOLD_REPETITION, null));
        return true;
    }

    public bool ClaimDrawByFiftyMoveRule(IPlayer player)
    {
        if (State != GameState.IN_PROGRESS)
            return false;
        if (!IsFiftyMoveRuleClaimAvailable())
            return false;
        EndGame(new GameResult(GameEndReason.DRAW_BY_50_MOVE_RULE, null));
        return true;
    }

    public void Resign(IPlayer player)
    {
        if (State != GameState.IN_PROGRESS)
            return;
        var winner = player.Color == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
        EndGame(new GameResult(GameEndReason.PLAYER_RESIGNED, winner));
    }

    private void ChangeTurn()
    {
        var next = CurrentPlayer.Color == PieceColor.WHITE ? _blackPlayer : _whitePlayer;
        PlayerTimeTracker[CurrentPlayer.Color].StopTimer();
        CurrentPlayer = next;
        PlayerTimeTracker[CurrentPlayer.Color].StartTimer();
        TurnChanged.Invoke(this, new TurnChangedEventArgs(next));
    }

    public void RecordCompletedMove(IBoard board, PieceColor sideToMove, bool wasPawnMove, bool wasCapture)
    {
        halfmoveClock = wasPawnMove || wasCapture ? 0 : halfmoveClock + 1;
        positionHistory.Add(GeneratePositionKey(board, sideToMove));
    }

    public bool IsThreefoldRepetitionClaimAvailable()
    {
        var key = GeneratePositionKey(GameBoard, CurrentPlayer.Color);
        return positionHistory.Count(k => k == key) >= 3;
    }

    public bool IsFivefoldRepetitionAutomatic()
    {
        var key = GeneratePositionKey(GameBoard, CurrentPlayer.Color);
        return positionHistory.Count(k => k == key) >= 5;
    }

    public bool IsFiftyMoveRuleClaimAvailable()
    {
        return halfmoveClock >= 100;
    }

    public bool IsSeventyFiveMoveRuleAutomatic()
    {
        return halfmoveClock >= 150;
    }

    public void Reset()
    {
        positionHistory.Clear();
        halfmoveClock = 0;
    }

    private void ResetTimers()
    {
        foreach (var t in PlayerTimeTracker.Values)
            t.StopTimer();
        var wt = new PlayerTimer(_initialTime);
        var bt = new PlayerTimer(_initialTime);
        wt.TimerTick += (s, e) => TimerTick.Invoke(this, new TimerTickEventArgs(PieceColor.WHITE, e.TimeRemaining));
        bt.TimerTick += (s, e) => TimerTick.Invoke(this, new TimerTickEventArgs(PieceColor.BLACK, e.TimeRemaining));
        wt.TimeExpired += (s, e) => OnTimeExpired(PieceColor.WHITE);
        bt.TimeExpired += (s, e) => OnTimeExpired(PieceColor.BLACK);
        PlayerTimeTracker[PieceColor.WHITE] = wt;
        PlayerTimeTracker[PieceColor.BLACK] = bt;
    }

    private void OnTimeExpired(PieceColor color)
    {
        if (State != GameState.IN_PROGRESS)
            return;
        var winner = color == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
        EndGame(new GameResult(GameEndReason.TIME_EXPIRED, winner));
    }

    private static PieceColor Opposite(PieceColor color)
    {
        return color == PieceColor.WHITE ? PieceColor.BLACK : PieceColor.WHITE;
    }
}
