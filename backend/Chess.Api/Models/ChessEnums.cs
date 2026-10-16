namespace Api.Models;

public enum PieceColor
{
    BLACK,
    WHITE
}

public enum PieceType
{
    KING,
    QUEEN,
    ROOK,
    KNIGHT,
    BISHOP,
    PAWN
}

public enum GameState
{
    NOT_STARTED,
    IN_PROGRESS,
    COMPLETED
}

public enum GameEndReason
{
    CHECKMATE,
    DRAW_BY_STALEMATE,
    DRAW_BY_AGREEMENT,
    DRAW_BY_50_MOVE_RULE,
    DRAW_BY_75_MOVE_RULE,
    DRAW_BY_DEAD_POSITION,
    DRAW_BY_THREEFOLD_REPETITION,
    DRAW_BY_FIVEFOLD_REPETITION,
    PLAYER_RESIGNED,
    TIME_EXPIRED
}
