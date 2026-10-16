namespace Api.Models;

public enum PieceColor
{
    Black,
    White
}

public enum PieceType
{
    King,
    Queen,
    Rook,
    Knight,
    Bishop,
    Pawn
}

public enum GameState
{
    NotStarted,
    InProgress,
    Completed
}

public enum GameEndReason
{
    Checkmate,
    DrawByStalemate,
    DrawByAgreement,
    DrawBy50MoveRule,
    DrawBy75MoveRule,
    DrawByDeadPosition,
    DrawByThreefoldRepetition,
    DrawByFivefoldRepetition,
    PlayerResigned,
    TimeExpired
}
